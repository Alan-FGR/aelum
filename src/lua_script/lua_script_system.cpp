#include "lua_script_system.h"
#include "engine/array.h"
#include "engine/binary_array.h"
#include "engine/blob.h"
#include "engine/crc32.h"
#include "engine/debug/debug.h"
#include "engine/engine.h"
#include "engine/flag_set.h"
#include "engine/iallocator.h"
#include "engine/input_system.h"
#include "engine/iplugin.h"
#include "engine/log.h"
#include "engine/lua_wrapper.h"
#include "engine/profiler.h"
#include "engine/reflection.h"
#include "engine/serializer.h"
#include "engine/string.h"
#include "engine/universe/universe.h"
#include "gui/gui_scene.h"
#include "lua_script/lua_script_manager.h"


namespace Lumix
{


	static const ComponentType LUA_SCRIPT_TYPE = Reflection::getComponentType("lua_script");


	enum class LuaSceneVersion : int
	{
		PROPERTY_TYPE,
		FLAGS,

		LATEST
	};


	class LuaScriptSystemImpl LUMIX_FINAL : public IPlugin
	{
	public:
		explicit LuaScriptSystemImpl(Engine& engine);
		virtual ~LuaScriptSystemImpl();

		void createScenes(Universe& universe) override;
		void destroyScene(IScene* scene) override;
		const char* getName() const override { return "lua_script"; }
		LuaScriptManager& getScriptManager() { return m_script_manager; }

		Engine& m_engine;
		Debug::Allocator m_allocator;
		LuaScriptManager m_script_manager;
	};


	struct LuaScriptSceneImpl LUMIX_FINAL : public LuaScriptScene
	{
		struct TimerData
		{
			float time;
			lua_State* state;
			int func;
		};

		struct CallbackData
		{
			LuaScript* script;
			lua_State* state;
			int environment;
		};


		struct ScriptInstance
		{
			enum Flags : u32
			{
				ENABLED = 1 << 0
			};

			explicit ScriptInstance(IAllocator& allocator)
				: m_properties(allocator)
				, m_script(nullptr)
				, m_state(nullptr)
				, m_environment(-1)
				, m_thread_ref(-1)
			{
				m_flags.set(ENABLED);
			}

			LuaScript* m_script;
			lua_State* m_state;
			int m_environment;
			int m_thread_ref;
			Array<Property> m_properties;
			FlagSet<Flags, u32> m_flags;
		};


		struct ScriptComponent
		{
			ScriptComponent(LuaScriptSceneImpl& scene, IAllocator& allocator)
				: m_scripts(allocator)
				, m_scene(scene)
				, m_entity(INVALID_ENTITY)
			{
			}


			static int getProperty(ScriptInstance& inst, u32 hash)
			{
				for(int i = 0, c = inst.m_properties.size(); i < c; ++i)
				{
					if (inst.m_properties[i].name_hash == hash) return i;
				}
				return -1;
			}


			void detectProperties(ScriptInstance& inst)
			{
				static const u32 INDEX_HASH = crc32("__index");
				static const u32 THIS_HASH = crc32("this");
				lua_State* L = inst.m_state;
				bool is_env_valid = lua_rawgeti(L, LUA_REGISTRYINDEX, inst.m_environment) == LUA_TTABLE; // [env]
				ASSERT(is_env_valid);
				lua_pushnil(L); // [env, nil]
				auto& allocator = m_scene.m_system.m_allocator;
				BinaryArray valid_properties(m_scene.m_system.m_engine.getLIFOAllocator());
				valid_properties.resize(inst.m_properties.size());
				valid_properties.setAllZeros();

				while (lua_next(L, -2)) // [env, key, value] | [env]
				{
					if (lua_type(L, -1) != LUA_TFUNCTION)
					{
						const char* name = lua_tostring(L, -2);
						if(name[0] != '_')
						{
							u32 hash = crc32(name);
							if (m_scene.m_property_names.find(hash) < 0)
							{
								m_scene.m_property_names.emplace(hash, name, allocator);
							}
							if (hash != INDEX_HASH && hash != THIS_HASH)
							{
								int prop_index = getProperty(inst, hash);
								if (prop_index >= 0)
								{
									valid_properties[prop_index] = true;
									Property& existing_prop = inst.m_properties[prop_index];
									if (existing_prop.type == Property::ANY)
									{
										switch (lua_type(inst.m_state, -1))
										{
										case LUA_TSTRING: existing_prop.type = Property::STRING; break;
										case LUA_TBOOLEAN: existing_prop.type = Property::BOOLEAN; break;
										default: existing_prop.type = Property::FLOAT;
										}
									}
									m_scene.applyProperty(inst, existing_prop, existing_prop.stored_value.c_str());
								}
								else
								{
									auto& prop = inst.m_properties.emplace(allocator);
									valid_properties.push(true);
									switch (lua_type(inst.m_state, -1))
									{
									case LUA_TBOOLEAN: prop.type = Property::BOOLEAN; break;
									case LUA_TSTRING: prop.type = Property::STRING; break;
									default: prop.type = Property::FLOAT;
									}
									prop.name_hash = hash;
								}
							}
						}
					}
					lua_pop(L, 1); // [env, key]
				}
				// [env]
				for (int i = inst.m_properties.size() - 1; i >= 0; --i)
				{
					if (valid_properties[i]) continue;
					inst.m_properties.eraseFast(i);
				}
				lua_pop(L, 1);
			}


			void onScriptLoaded(Resource::State, Resource::State, Resource& resource)
			{
				lua_State* L = m_scene.m_system.m_engine.getState();
				for (auto& script : m_scripts)
				{
					if (!script.m_script) continue;
					if (!script.m_script->isReady()) continue;
					if (script.m_script != &resource) continue;

					bool is_reload = true;
					if (!script.m_state)
					{
						is_reload = false;
						script.m_environment = -1;

						script.m_state = lua_newthread(L); // [thread]
						script.m_thread_ref = luaL_ref(L, LUA_REGISTRYINDEX); // []
						lua_newtable(script.m_state); // [env]
						// reference environment
						lua_pushvalue(script.m_state, -1); // [env, env]
						script.m_environment = luaL_ref(script.m_state, LUA_REGISTRYINDEX); // [env]

						// environment's metatable & __index
						lua_pushvalue(script.m_state, -1); // [env, env]
						lua_setmetatable(script.m_state, -2); // [env]
						lua_pushglobaltable(script.m_state); // [evn, _G]
						lua_setfield(script.m_state, -2, "__index");  // [env]

						// set this
						lua_pushinteger(script.m_state, m_entity.index); // [env, index]
						lua_setfield(script.m_state, -2, "this"); // [env]
					}
					else
					{
						bool is_env_valid = lua_rawgeti(script.m_state, LUA_REGISTRYINDEX, script.m_environment) == LUA_TTABLE; // [env]
						ASSERT(is_env_valid);
					}

					bool errors = luaL_loadbuffer(script.m_state,
						script.m_script->getSourceCode(),
						stringLength(script.m_script->getSourceCode()),
						script.m_script->getPath().c_str()) != LUA_OK; // [env, func]

					if (errors)
					{
						g_log_error.log("Lua Script") << script.m_script->getPath() << ": "
							<< lua_tostring(script.m_state, -1);
						lua_pop(script.m_state, 1);
						continue;
					}

					lua_pushvalue(script.m_state, -2); // [env, func, env]
					lua_setupvalue(script.m_state, -2, 1); // function's environment [env, func]

					m_scene.m_current_script_instance = &script;
					errors = errors || lua_pcall(script.m_state, 0, 0, 0) != LUA_OK; // [env]
					if (errors)
					{
						g_log_error.log("Lua Script") << script.m_script->getPath() << ": "
							<< lua_tostring(script.m_state, -1);
						lua_pop(script.m_state, 1);
					}
					lua_pop(script.m_state, 1); // []

					detectProperties(script);

					if (m_scene.m_is_game_running) m_scene.startScript(script, is_reload);
				}
			}


			Array<ScriptInstance> m_scripts;
			LuaScriptSceneImpl& m_scene;
			Entity m_entity;
		};


		struct FunctionCall : IFunctionCall
		{
			void add(int parameter) override
			{
				lua_pushinteger(state, parameter);
				++parameter_count;
			}


			void add(bool parameter) override
			{
				lua_pushboolean(state, parameter);
				++parameter_count;
			}


			void add(float parameter) override
			{
				lua_pushnumber(state, parameter);
				++parameter_count;
			}


			void add(void* parameter) override
			{
				lua_pushlightuserdata(state, parameter);
				++parameter_count;
			}


			void addEnvironment(int env) override
			{
				bool is_valid = lua_rawgeti(state, LUA_REGISTRYINDEX, env) == LUA_TTABLE;
				ASSERT(is_valid);
				++parameter_count;
			}


			int parameter_count;
			lua_State* state;
			bool is_in_progress;
			ScriptComponent* cmp;
			int scr_index;
		};


	public:
		LuaScriptSceneImpl(LuaScriptSystemImpl& system, Universe& ctx)
			: m_system(system)
			, m_universe(ctx)
			, m_scripts(system.m_allocator)
			, m_updates(system.m_allocator)
			, m_input_handlers(system.m_allocator)
			, m_timers(system.m_allocator)
			, m_property_names(system.m_allocator)
			, m_is_game_running(false)
			, m_is_api_registered(false)
		{
			m_function_call.is_in_progress = false;
			
			registerAPI();
			ctx.registerComponentType(LUA_SCRIPT_TYPE
				, this
				, &LuaScriptSceneImpl::createLuaScriptComponent
				, &LuaScriptSceneImpl::destroyLuaScriptComponent
				, &LuaScriptSceneImpl::serializeLuaScript
				, &LuaScriptSceneImpl::deserializeLuaScript);
		}


		int getVersion() const override { return (int)LuaSceneVersion::LATEST; }


		IFunctionCall* beginFunctionCall(Entity entity, int scr_index, const char* function) override
		{
			ASSERT(!m_function_call.is_in_progress);

			auto* script_cmp = m_scripts[entity];
			auto& script = script_cmp->m_scripts[scr_index];
			if (!script.m_state) return nullptr;

			bool is_env_valid = lua_rawgeti(script.m_state, LUA_REGISTRYINDEX, script.m_environment) == LUA_TTABLE;
			ASSERT(is_env_valid);
			if (lua_getfield(script.m_state, -1, function) != LUA_TFUNCTION)
			{
				lua_pop(script.m_state, 2);
				return nullptr;
			}

			m_function_call.state = script.m_state;
			m_function_call.cmp = script_cmp;
			m_function_call.is_in_progress = true;
			m_function_call.parameter_count = 0;
			m_function_call.scr_index = scr_index;

			return &m_function_call;
		}


		void endFunctionCall() override
		{
			ASSERT(m_function_call.is_in_progress);

			m_function_call.is_in_progress = false;

			auto& script = m_function_call.cmp->m_scripts[m_function_call.scr_index];
			if (!script.m_state) return;

			if (lua_pcall(script.m_state, m_function_call.parameter_count, 0, 0) != LUA_OK)
			{
				g_log_warning.log("Lua Script") << lua_tostring(script.m_state, -1);
				lua_pop(script.m_state, 1);
			}
			lua_pop(script.m_state, 1);
		}


		int getPropertyCount(Entity entity, int scr_index) override
		{
			return m_scripts[entity]->m_scripts[scr_index].m_properties.size();
		}


		const char* getPropertyName(Entity entity, int scr_index, int prop_index) override
		{
			return getPropertyName(m_scripts[entity]->m_scripts[scr_index].m_properties[prop_index].name_hash);
		}


		ResourceType getPropertyResourceType(Entity entity, int scr_index, int prop_index) override
		{
			return m_scripts[entity]->m_scripts[scr_index].m_properties[prop_index].resource_type;
		}


		Property::Type getPropertyType(Entity entity, int scr_index, int prop_index) override
		{
			return m_scripts[entity]->m_scripts[scr_index].m_properties[prop_index].type;
		}


		void getScriptData(Entity entity, OutputBlob& blob) override
		{
			auto* scr = m_scripts[entity];
			blob.write(scr->m_scripts.size());
			for (int i = 0; i < scr->m_scripts.size(); ++i)
			{
				auto& inst = scr->m_scripts[i];
				blob.writeString(inst.m_script ? inst.m_script->getPath().c_str() : "");
				blob.write(inst.m_flags);
				blob.write(inst.m_properties.size());
				for (auto& prop : inst.m_properties)
				{
					blob.write(prop.name_hash);
					blob.write(prop.type);
					char tmp[1024];
					tmp[0] = '\0';
					const char* prop_name = getPropertyName(prop.name_hash);
					if(prop_name) getPropertyValue(entity, i, getPropertyName(prop.name_hash), tmp, lengthOf(tmp));
					blob.writeString(prop_name ? tmp : prop.stored_value.c_str());
				}
			}
		}


		void setScriptData(Entity entity, InputBlob& blob) override
		{
			auto* scr = m_scripts[entity];
			int count;
			blob.read(count);
			for (int i = 0; i < count; ++i)
			{
				int idx = addScript(entity);
				auto& inst = scr->m_scripts[idx];
				char tmp[MAX_PATH_LENGTH];
				blob.readString(tmp, lengthOf(tmp));
				blob.read(inst.m_flags);
				setScriptPath(entity, idx, Path(tmp));
				
				int prop_count;
				blob.read(prop_count);
				for (int j = 0; j < prop_count; ++j)
				{
					u32 hash;
					blob.read(hash);
					int prop_index = scr->getProperty(inst, hash);
					if (prop_index < 0)
					{
						scr->m_scripts[idx].m_properties.emplace(m_system.m_allocator);
						prop_index = scr->m_scripts[idx].m_properties.size() - 1;
					}
					auto& prop = scr->m_scripts[idx].m_properties[prop_index];
					prop.name_hash = hash;
					blob.read(prop.type);
					char tmp[1024];
					blob.readString(tmp, lengthOf(tmp));
					prop.stored_value = tmp;
					if (scr->m_scripts[idx].m_state) applyProperty(scr->m_scripts[idx], prop, tmp);
				}
			}
		}


		void clear() override
		{
			Path invalid_path;
			for (auto* script_cmp : m_scripts)
			{
				if (!script_cmp) continue;

				for (auto script : script_cmp->m_scripts)
				{
					setScriptPath(*script_cmp, script, invalid_path);
				}
				LUMIX_DELETE(m_system.m_allocator, script_cmp);
			}
			m_scripts.clear();
		}


		lua_State* getState(Entity entity, int scr_index) override
		{
			return m_scripts[entity]->m_scripts[scr_index].m_state;
		}


		Universe& getUniverse() override { return m_universe; }


		static int setPropertyType(lua_State* L)
		{
			const char* prop_name = LuaWrapper::checkArg<const char*>(L, 1);
			int type = LuaWrapper::checkArg<int>(L, 2);
			ResourceType resource_type;
			if (type == Property::Type::RESOURCE)
			{
				resource_type = ResourceType(LuaWrapper::checkArg<const char*>(L, 3));
			}
			int tmp = lua_getglobal(L, "g_scene_lua_script");
			ASSERT(tmp == LUA_TLIGHTUSERDATA);
			auto* scene = LuaWrapper::toType<LuaScriptSceneImpl*>(L, -1);
			u32 prop_name_hash = crc32(prop_name);
			for (auto& prop : scene->m_current_script_instance->m_properties)
			{
				if (prop.name_hash == prop_name_hash)
				{
					prop.type = (Property::Type)type;
					prop.resource_type = resource_type;
					lua_pop(L, -1);
					return 0;
				}
			}

			auto& prop = scene->m_current_script_instance->m_properties.emplace(scene->m_system.m_allocator);
			prop.name_hash = prop_name_hash;
			prop.type = (Property::Type)type;
			prop.resource_type = resource_type;
			if (scene->m_property_names.find(prop_name_hash) < 0)
			{
				scene->m_property_names.emplace(prop_name_hash, prop_name, scene->m_system.m_allocator);
			}
			return 0;
		}


		void registerPropertyAPI()
		{
			lua_State* L = m_system.m_engine.getState();
			auto f = &LuaWrapper::wrap<decltype(&setPropertyType), &setPropertyType>;
			LuaWrapper::createSystemFunction(L, "Editor", "setPropertyType", f);
			LuaWrapper::createSystemVariable(L, "Editor", "BOOLEAN_PROPERTY", Property::BOOLEAN);
			LuaWrapper::createSystemVariable(L, "Editor", "FLOAT_PROPERTY", Property::FLOAT);
			LuaWrapper::createSystemVariable(L, "Editor", "ENTITY_PROPERTY", Property::ENTITY);
			LuaWrapper::createSystemVariable(L, "Editor", "RESOURCE_PROPERTY", Property::RESOURCE);
		}


		static int getEnvironment(lua_State* L)
		{
			auto* scene = LuaWrapper::checkArg<LuaScriptScene*>(L, 1);
			Entity entity = LuaWrapper::checkArg<Entity>(L, 2);
			int scr_index = LuaWrapper::checkArg<int>(L, 3);

			if (!scene->getUniverse().hasComponent(entity, LUA_SCRIPT_TYPE))
			{
				lua_pushnil(L);
				return 1;
			}
			int count = scene->getScriptCount(entity);
			if (scr_index >= count)
			{
				lua_pushnil(L);
				return 1;
			}

			int env = scene->getEnvironment(entity, scr_index);
			if (env < 0)
			{
				lua_pushnil(L);
			}
			else
			{
				bool is_valid = lua_rawgeti(L, LUA_REGISTRYINDEX, env) == LUA_TTABLE;
				ASSERT(is_valid);
			}
			return 1;
		}


		struct GetPropertyVisitor LUMIX_FINAL : Reflection::IPropertyVisitor
		{
			template <typename T>
			LUMIX_FORCE_INLINE void get(const Reflection::Property<T>& prop)
			{
				T v;
				OutputBlob blob(&v, sizeof(v));
				prop.getValue(cmp, -1, blob);
				LuaWrapper::push(L, v);
			}

			void visit(const Reflection::Property<float>& prop) override { get(prop); }
			void visit(const Reflection::Property<int>& prop) override { get(prop); }
			void visit(const Reflection::Property<bool>& prop) override { get(prop); }
			void visit(const Reflection::Property<Int2>& prop) override { get(prop); }
			void visit(const Reflection::Property<Vec2>& prop) override { get(prop); }
			void visit(const Reflection::Property<Vec3>& prop) override { get(prop); }
			void visit(const Reflection::Property<Vec4>& prop) override { get(prop); }
			void visit(const Reflection::Property<Entity>& prop) override { get(prop); }

			void visit(const Reflection::Property<Path>& prop) override
			{
				char buf[1024];
				OutputBlob blob(buf, sizeof(buf));
				prop.getValue(cmp, -1, blob);
				LuaWrapper::push(L, buf);
			}

			void visit(const Reflection::Property<const char*>& prop) override
			{
				char buf[1024];
				OutputBlob blob(buf, sizeof(buf));
				prop.getValue(cmp, -1, blob);
				LuaWrapper::push(L, buf);
			}

			void visit(const Reflection::IArrayProperty& prop) override { ASSERT(false); }
			void visit(const Reflection::IEnumProperty& prop) override { ASSERT(false); }
			void visit(const Reflection::IBlobProperty& prop) override { ASSERT(false); }
			void visit(const Reflection::ISampledFuncProperty& prop) override { ASSERT(false); }

			ComponentUID cmp;
			lua_State* L;
		};


		template <typename T>
		static int LUA_getProperty(lua_State* L)
		{
			auto* prop = LuaWrapper::toType<T*>(L, lua_upvalueindex(1));
			GetPropertyVisitor visitor;
			visitor.L = L;
			visitor.cmp.type = { LuaWrapper::toType<int>(L, lua_upvalueindex(2)) };
			visitor.cmp.scene = LuaWrapper::checkArg<IScene*>(L, 1);
			visitor.cmp.entity = LuaWrapper::checkArg<Entity>(L, 2);
			visitor.visit(*prop);
			return 1;
		}


		struct SetPropertyVisitor LUMIX_FINAL : Reflection::IPropertyVisitor
		{
			template <typename T>
			LUMIX_FORCE_INLINE void set(const Reflection::Property<T>& prop)
			{
				auto v = LuaWrapper::checkArg<T>(L, 3);
				InputBlob blob(&v, sizeof(v));
				prop.setValue(cmp, -1, blob);
			}

			void visit(const Reflection::Property<float>& prop) override { set(prop); }
			void visit(const Reflection::Property<int>& prop) override { set(prop); }
			void visit(const Reflection::Property<bool>& prop) override { set(prop); }
			void visit(const Reflection::Property<Int2>& prop) override { set(prop); }
			void visit(const Reflection::Property<Vec2>& prop) override { set(prop); }
			void visit(const Reflection::Property<Vec3>& prop) override { set(prop); }
			void visit(const Reflection::Property<Vec4>& prop) override { set(prop); }
			void visit(const Reflection::Property<Entity>& prop) override { set(prop); }

			void visit(const Reflection::Property<Path>& prop) override
			{
				auto* v = LuaWrapper::checkArg<const char*>(L, 3);
				InputBlob blob(v, stringLength(v) + 1);
				prop.setValue(cmp, -1, blob);
			}
			
			void visit(const Reflection::Property<const char*>& prop) override
			{
				auto* v = LuaWrapper::checkArg<const char*>(L, 3);
				InputBlob blob(v, stringLength(v) + 1);
				prop.setValue(cmp, -1, blob);
			}

			void visit(const Reflection::IArrayProperty& prop) override { ASSERT(false); }
			void visit(const Reflection::IEnumProperty& prop) override { ASSERT(false); }
			void visit(const Reflection::IBlobProperty& prop) override { ASSERT(false); }
			void visit(const Reflection::ISampledFuncProperty& prop) override { ASSERT(false); }

			ComponentUID cmp;
			lua_State* L;
		};


		template <typename T>
		static int LUA_setProperty(lua_State* L)
		{
			auto* prop = LuaWrapper::toType<T*>(L, lua_upvalueindex(1));
			ComponentType type = { LuaWrapper::toType<int>(L, lua_upvalueindex(2)) };
			SetPropertyVisitor visitor;
			visitor.L = L;
			visitor.cmp.scene = LuaWrapper::checkArg<IScene*>(L, 1);
			visitor.cmp.type = type;
			visitor.cmp.entity = LuaWrapper::checkArg<Entity>(L, 2);
			visitor.visit(*prop);

			return 0;
		}

		
		static void convertPropertyToLuaName(const char* src, char* out, int max_size)
		{
			ASSERT(max_size > 0);
			bool to_upper = true;
			char* dest = out;
			while (*src && dest - out < max_size - 1)
			{
				if (isLetter(*src))
				{
					*dest = to_upper && !isUpperCase(*src) ? *src - 'a' + 'A' : *src;
					to_upper = false;
					++dest;
				}
				else if (isNumeric(*src))
				{
					*dest = *src;
					++dest;
				}
				else
				{
					to_upper = true;
				}
				++src;
			}
			*dest = 0;
		}


		struct LuaCreatePropertyVisitor : Reflection::IPropertyVisitor
		{
			template <typename T>
			void set(T& prop)
			{
				char tmp[50];
				char setter[50];
				char getter[50];
				convertPropertyToLuaName(prop.name, tmp, lengthOf(tmp));
				copyString(setter, "set");
				copyString(getter, "get");
				catString(setter, tmp);
				catString(getter, tmp);
				lua_pushlightuserdata(L, (void*)&prop);
				lua_pushinteger(L, cmp_type.index);
				lua_pushcclosure(L, &LUA_setProperty<T>, 2);
				lua_setfield(L, -2, setter);

				lua_pushlightuserdata(L, (void*)&prop);
				lua_pushinteger(L, cmp_type.index);
				lua_pushcclosure(L, &LUA_getProperty<T>, 2);
				lua_setfield(L, -2, getter);
			}

			void visit(const Reflection::Property<float>& prop) override { set(prop); }
			void visit(const Reflection::Property<int>& prop) override { set(prop); }
			void visit(const Reflection::Property<Entity>& prop) override { set(prop); }
			void visit(const Reflection::Property<Int2>& prop) override { set(prop); }
			void visit(const Reflection::Property<Vec2>& prop) override { set(prop); }
			void visit(const Reflection::Property<Vec3>& prop) override { set(prop); }
			void visit(const Reflection::Property<Vec4>& prop) override { set(prop); }
			void visit(const Reflection::Property<Path>& prop) override { set(prop); }
			void visit(const Reflection::Property<bool>& prop) override { set(prop); }
			void visit(const Reflection::Property<const char*>& prop) override { set(prop); }
			void visit(const Reflection::IArrayProperty& prop) override {}
			void visit(const Reflection::IEnumProperty& prop) override {}
			void visit(const Reflection::IBlobProperty& prop) override {}
			void visit(const Reflection::ISampledFuncProperty& prop) override {}

			ComponentType cmp_type;
			lua_State* L;
		};

		void registerProperties()
		{
			int cmps_count = Reflection::getComponentTypesCount();
			lua_State* L = m_system.m_engine.getState();
			for (int i = 0; i < cmps_count; ++i)
			{
				const char* cmp_name = Reflection::getComponentTypeID(i);
				lua_newtable(L);
				lua_pushvalue(L, -1);
				char tmp[50];
				convertPropertyToLuaName(cmp_name, tmp, lengthOf(tmp));
				lua_setglobal(L, tmp);

				ComponentType cmp_type = Reflection::getComponentType(cmp_name);
				const Reflection::ComponentBase* cmp_desc = Reflection::getComponent(cmp_type);
				
				LuaCreatePropertyVisitor visitor;
				visitor.cmp_type = cmp_type;
				visitor.L = L;

				cmp_desc->visit(visitor);

				lua_pop(L, 1);
			}
		}



		void cancelTimer(int timer_func)
		{
			for (int i = 0, c = m_timers.size(); i < c; ++i)
			{
				if (m_timers[i].func == timer_func)
				{
					m_timers.eraseFast(i);
					break;
				}
			}
		}


		static int setTimer(lua_State* L)
		{
			auto* scene = LuaWrapper::checkArg<LuaScriptSceneImpl*>(L, 1);
			float time = LuaWrapper::checkArg<float>(L, 2);
			if (!lua_isfunction(L, 3)) LuaWrapper::argError(L, 3, "function");
			TimerData& timer = scene->m_timers.emplace();
			timer.time = time;
			timer.state = L;
			lua_pushvalue(L, 3);
			timer.func = luaL_ref(L, LUA_REGISTRYINDEX);
			lua_pop(L, 1);
			LuaWrapper::push(L, timer.func);
			return 1;
		}


		void setScriptSource(Entity entity, int scr_index, const char* path)
		{
			setScriptPath(entity, scr_index, Path(path));
		}


		void registerAPI()
		{
			if (m_is_api_registered) return;

			m_is_api_registered = true;

			lua_State* engine_state = m_system.m_engine.getState();
			
			registerProperties();
			registerPropertyAPI();
			LuaWrapper::createSystemFunction(
				engine_state, "LuaScript", "getEnvironment", &LuaScriptSceneImpl::getEnvironment);
			
			#define REGISTER_FUNCTION(F) \
				do { \
					auto f = &LuaWrapper::wrapMethod<LuaScriptSceneImpl, \
						decltype(&LuaScriptSceneImpl::F), \
						&LuaScriptSceneImpl::F>; \
					LuaWrapper::createSystemFunction(engine_state, "LuaScript", #F, f); \
				} while(false)

			REGISTER_FUNCTION(addScript);
			REGISTER_FUNCTION(getScriptCount);
			REGISTER_FUNCTION(setScriptSource);
			REGISTER_FUNCTION(cancelTimer);

			#undef REGISTER_FUNCTION

			LuaWrapper::createSystemFunction(engine_state, "LuaScript", "setTimer", &LuaScriptSceneImpl::setTimer);
		}


		int getEnvironment(Entity entity, int scr_index) override
		{
			return m_scripts[entity]->m_scripts[scr_index].m_environment;
		}


		const char* getPropertyName(u32 name_hash) const
		{
			int idx = m_property_names.find(name_hash);
			if(idx >= 0) return m_property_names.at(idx).c_str();
			return nullptr;
		}


		void applyResourceProperty(ScriptInstance& script, const char* name, Property& prop, const char* value)
		{
			bool is_env_valid = lua_rawgeti(script.m_state, LUA_REGISTRYINDEX, script.m_environment) == LUA_TTABLE;
			ASSERT(is_env_valid);
			lua_getfield(script.m_state, -1, name);
			int res_idx = LuaWrapper::toType<int>(script.m_state, -1);
			m_system.m_engine.unloadLuaResource(res_idx);
			lua_pop(script.m_state, 1);

			int new_res = m_system.m_engine.addLuaResource(Path(value), prop.resource_type);
			lua_pushinteger(script.m_state, new_res);
			lua_setfield(script.m_state, -2, name);
			lua_pop(script.m_state, 1);
		}


		void applyProperty(ScriptInstance& script, Property& prop, const char* value)
		{
			if (!value) return;
			lua_State* state = script.m_state;
			if (!state) return;
			const char* name = getPropertyName(prop.name_hash);
			if (!name) return;

			if (prop.type == Property::RESOURCE)
			{
				applyResourceProperty(script, name, prop, value);
				return;
			}

			StaticString<1024> tmp(name, " = ");
			if (prop.type == Property::STRING) tmp << "\"" << value << "\"";
			else tmp << value;

			bool errors = luaL_loadbuffer(state, tmp, stringLength(tmp), nullptr) != LUA_OK;
			if (errors)
			{
				g_log_error.log("Lua Script") << script.m_script->getPath() << ": " << lua_tostring(state, -1);
				lua_pop(state, 1);
				return;
			}

			bool is_env_valid = lua_rawgeti(script.m_state, LUA_REGISTRYINDEX, script.m_environment) == LUA_TTABLE;
			ASSERT(is_env_valid);
			lua_setupvalue(script.m_state, -2, 1);

			errors = errors || lua_pcall(state, 0, 0, 0) != LUA_OK;

			if (errors)
			{
				g_log_error.log("Lua Script") << script.m_script->getPath() << ": " << lua_tostring(state, -1);
				lua_pop(state, 1);
			}
		}


		void setPropertyValue(Entity entity,
			int scr_index,
			const char* name,
			const char* value) override
		{
			auto* script_cmp = m_scripts[entity];
			if (!script_cmp) return;
			Property& prop = getScriptProperty(entity, scr_index, name);
			if (!script_cmp->m_scripts[scr_index].m_state)
			{
				prop.stored_value = value;
				return;
			}

			applyProperty(script_cmp->m_scripts[scr_index], prop, value);
		}


		const char* getPropertyName(Entity entity, int scr_index, int index) const
		{
			auto& script = m_scripts[entity]->m_scripts[scr_index];

			return getPropertyName(script.m_properties[index].name_hash);
		}


		int getPropertyCount(Entity entity, int scr_index) const
		{
			auto& script = m_scripts[entity]->m_scripts[scr_index];

			return script.m_properties.size();
		}


		static void* luaAllocator(void* ud, void* ptr, size_t osize, size_t nsize)
		{
			auto& allocator = *static_cast<IAllocator*>(ud);
			if (nsize == 0)
			{
				allocator.deallocate(ptr);
				return nullptr;
			}
			if (nsize > 0 && ptr == nullptr) return allocator.allocate(nsize);

			void* new_mem = allocator.allocate(nsize);
			copyMemory(new_mem, ptr, Math::minimum(osize, nsize));
			allocator.deallocate(ptr);
			return new_mem;
		}


		void disableScript(ScriptInstance& inst)
		{
			for (int i = 0; i < m_timers.size(); ++i)
			{
				if (m_timers[i].state == inst.m_state)
				{
					luaL_unref(m_timers[i].state, LUA_REGISTRYINDEX, m_timers[i].func);
					m_timers.eraseFast(i);
					--i;
				}
			}

			for (int i = 0; i < m_updates.size(); ++i)
			{
				if (m_updates[i].state == inst.m_state)
				{
					m_updates.eraseFast(i);
					break;
				}
			}

			for (int i = 0; i < m_input_handlers.size(); ++i)
			{
				if (m_input_handlers[i].state == inst.m_state)
				{
					m_input_handlers.eraseFast(i);
					break;
				}
			}
		}


		void destroyInstance(ScriptComponent& scr,  ScriptInstance& inst)
		{
			bool is_env_valid = lua_rawgeti(inst.m_state, LUA_REGISTRYINDEX, inst.m_environment) == LUA_TTABLE;
			ASSERT(is_env_valid);
			if (lua_getfield(inst.m_state, -1, "onDestroy") != LUA_TFUNCTION)
			{
				lua_pop(inst.m_state, 2);
			}
			else
			{
				if (lua_pcall(inst.m_state, 0, 0, 0) != LUA_OK)
				{
					g_log_error.log("Lua Script") << lua_tostring(inst.m_state, -1);
					lua_pop(inst.m_state, 1);
				}
				lua_pop(inst.m_state, 1);
			}

			disableScript(inst);

			luaL_unref(inst.m_state, LUA_REGISTRYINDEX, inst.m_thread_ref);
			luaL_unref(inst.m_state, LUA_REGISTRYINDEX, inst.m_environment);
			inst.m_state = nullptr;
		}


		void setScriptPath(ScriptComponent& cmp, ScriptInstance& inst, const Path& path)
		{
			registerAPI();

			if (inst.m_script)
			{
				if (inst.m_state) destroyInstance(cmp, inst);
				inst.m_properties.clear();
				auto& cb = inst.m_script->getObserverCb();
				cb.unbind<ScriptComponent, &ScriptComponent::onScriptLoaded>(&cmp);
				m_system.getScriptManager().unload(*inst.m_script);
			}
			inst.m_script = path.isValid() ? static_cast<LuaScript*>(m_system.getScriptManager().load(path)) : nullptr;
			if (inst.m_script)
			{
				inst.m_script->onLoaded<ScriptComponent, &ScriptComponent::onScriptLoaded>(&cmp);
			}
		}


		void startScript(ScriptInstance& instance, bool is_restart)
		{
			if (!instance.m_flags.isSet(ScriptInstance::ENABLED)) return;
			if (!instance.m_state) return;

			if (is_restart) disableScript(instance);

			if (lua_rawgeti(instance.m_state, LUA_REGISTRYINDEX, instance.m_environment) != LUA_TTABLE)
			{
				ASSERT(false);
				lua_pop(instance.m_state, 1);
				return;
			}
			if (lua_getfield(instance.m_state, -1, "update") == LUA_TFUNCTION)
			{
				auto& update_data = m_updates.emplace();
				update_data.script = instance.m_script;
				update_data.state = instance.m_state;
				update_data.environment = instance.m_environment;
			}
			lua_pop(instance.m_state, 1);
			if (lua_getfield(instance.m_state, -1, "onInputEvent") == LUA_TFUNCTION)
			{
				auto& callback = m_input_handlers.emplace();
				callback.script = instance.m_script;
				callback.state = instance.m_state;
				callback.environment = instance.m_environment;
			}
			lua_pop(instance.m_state, 1);

			if (!is_restart)
			{
				if (lua_getfield(instance.m_state, -1, "init") != LUA_TFUNCTION)
				{
					lua_pop(instance.m_state, 2);
					return;
				}

				if (lua_pcall(instance.m_state, 0, 0, 0) != LUA_OK)
				{
					g_log_error.log("Lua Script") << lua_tostring(instance.m_state, -1);
					lua_pop(instance.m_state, 1);
				}
			}
			lua_pop(instance.m_state, 1);
		}


		void onButtonClicked(Entity e) { onGUIEvent(e, "onButtonClicked"); }
		void onRectHovered(Entity e) { onGUIEvent(e, "onRectHovered"); }
		void onRectHoveredOut(Entity e) { onGUIEvent(e, "onRectHoveredOut"); }


		LUMIX_FORCE_INLINE void onGUIEvent(Entity e, const char* event)
		{
			if (!m_universe.hasComponent(e, LUA_SCRIPT_TYPE)) return;

			for (int i = 0, c = getScriptCount(e); i < c; ++i)
			{
				auto* call = beginFunctionCall(e, i, event);
				if (call) endFunctionCall();
			}
		}


		void startGame() override
		{
			m_is_game_running = true;
			m_gui_scene = (GUIScene*)m_universe.getScene(crc32("gui"));
			if (m_gui_scene)
			{
				m_gui_scene->buttonClicked().bind<LuaScriptSceneImpl, &LuaScriptSceneImpl::onButtonClicked>(this);
				m_gui_scene->rectHovered().bind<LuaScriptSceneImpl, &LuaScriptSceneImpl::onRectHovered>(this);
				m_gui_scene->rectHoveredOut().bind<LuaScriptSceneImpl, &LuaScriptSceneImpl::onRectHoveredOut>(this);
			}
		}


		void stopGame() override
		{
			if (m_gui_scene)
			{
				m_gui_scene->buttonClicked().unbind<LuaScriptSceneImpl, &LuaScriptSceneImpl::onButtonClicked>(this);
				m_gui_scene->rectHovered().unbind<LuaScriptSceneImpl, &LuaScriptSceneImpl::onRectHovered>(this);
				m_gui_scene->rectHoveredOut().unbind<LuaScriptSceneImpl, &LuaScriptSceneImpl::onRectHoveredOut>(this);
			}
			m_gui_scene = nullptr;
			m_scripts_init_called = false;
			m_is_game_running = false;
			m_updates.clear();
			m_input_handlers.clear();
			m_timers.clear();
		}


		void createLuaScriptComponent(Entity entity)
		{
			auto& allocator = m_system.m_allocator;
			ScriptComponent* script = LUMIX_NEW(allocator, ScriptComponent)(*this, allocator);
			script->m_entity = entity;
			m_scripts.insert(entity, script);
			m_universe.onComponentCreated(entity, LUA_SCRIPT_TYPE, this);
		}


		void destroyLuaScriptComponent(Entity entity)
		{
			auto* script = m_scripts[entity];
			for (auto& scr : script->m_scripts)
			{
				if (scr.m_state) destroyInstance(*script, scr);
				if (scr.m_script)
				{
					auto& cb = scr.m_script->getObserverCb();
					cb.unbind<ScriptComponent, &ScriptComponent::onScriptLoaded>(script);
					m_system.getScriptManager().unload(*scr.m_script);
				}
			}
			LUMIX_DELETE(m_system.m_allocator, script);
			m_scripts.erase(entity);
			m_universe.onComponentDestroyed(entity, LUA_SCRIPT_TYPE, this);
		}


		void getPropertyValue(Entity entity,
			int scr_index,
			const char* property_name,
			char* out,
			int max_size) override
		{
			ASSERT(max_size > 0);

			u32 hash = crc32(property_name);
			auto& inst = m_scripts[entity]->m_scripts[scr_index];
			for (auto& prop : inst.m_properties)
			{
				if (prop.name_hash == hash)
				{
					if (inst.m_script->isReady())
						getProperty(prop, property_name, inst, out, max_size);
					else
						copyString(out, max_size, prop.stored_value.c_str());
					return;
				}
			}
			*out = '\0';
		}


		void getProperty(Property& prop, const char* prop_name, ScriptInstance& scr, char* out, int max_size)
		{
			if(max_size <= 0) return;
			if (!scr.m_state)
			{
				copyString(out, max_size, prop.stored_value.c_str());
				return;
			}

			*out = '\0';
			lua_rawgeti(scr.m_state, LUA_REGISTRYINDEX, scr.m_environment);
			if (lua_getfield(scr.m_state, -1, prop_name) == LUA_TNIL)
			{
				copyString(out, max_size, prop.stored_value.c_str());
				lua_pop(scr.m_state, 2);
				return;
			}
			switch (prop.type)
			{
				case Property::BOOLEAN:
				{
					bool b = lua_toboolean(scr.m_state, -1) != 0;
					copyString(out, max_size, b ? "true" : "false");
				}
				break;
				case Property::FLOAT:
				{
					float val = (float)lua_tonumber(scr.m_state, -1);
					toCString(val, out, max_size, 8);
				}
				break;
				case Property::ENTITY:
				{
					Entity val = { (int)lua_tointeger(scr.m_state, -1) };
					toCString(val.index, out, max_size);
				}
				break;
				case Property::STRING:
				{
					copyString(out, max_size, lua_tostring(scr.m_state, -1));
				}
				break;
				case Property::RESOURCE:
				{
					int res_idx = LuaWrapper::toType<int>(scr.m_state, -1);
					Resource* res = m_system.m_engine.getLuaResource(res_idx);
					copyString(out, max_size, res ? res->getPath().c_str() : "");
				}
				break;
				default: ASSERT(false); break;
			}
			lua_pop(scr.m_state, 2);
		}


		void serializeLuaScript(ISerializer& serializer, Entity entity)
		{
			ScriptComponent* script = m_scripts[entity];
			serializer.write("count", script->m_scripts.size());
			for (ScriptInstance& inst : script->m_scripts)
			{
				serializer.write("source", inst.m_script ? inst.m_script->getPath().c_str() : "");
				serializer.write("flags", inst.m_flags.base);
				serializer.write("prop_count", inst.m_properties.size());
				for (Property& prop : inst.m_properties)
				{
					const char* name = getPropertyName(prop.name_hash);
					serializer.write("prop_name", name ? name : "");
					int idx = m_property_names.find(prop.name_hash);
					if (idx >= 0)
					{
						const char* name = m_property_names.at(idx).c_str();
						serializer.write("prop_type", (int)prop.type);
						if (prop.type == Property::ENTITY)
						{
							lua_rawgeti(inst.m_state, LUA_REGISTRYINDEX, inst.m_environment);
							if (lua_getfield(inst.m_state, -1, name) == LUA_TNIL)
							{
								serializer.write("prop_value", prop.stored_value.c_str());
							}
							else
							{
								Entity val = {(int)lua_tointeger(inst.m_state, -1)};
								EntityGUID guid = serializer.getGUID(val);
								char tmp[128];
								toCString(guid.value, tmp, lengthOf(tmp));
								serializer.write("prop_value", tmp);
							}
						}
						else
						{
							char tmp[1024];
							getProperty(prop, name, inst, tmp, lengthOf(tmp));
							serializer.write("prop_value", tmp);
						}
					}
					else
					{
						serializer.write("prop_type", (int)Property::ANY);
						serializer.write("prop_value", "");
					}
				}
			}
		}


		void deserializeLuaScript(IDeserializer& serializer, Entity entity, int scene_version)
		{
			auto& allocator = m_system.m_allocator;
			ScriptComponent* script = LUMIX_NEW(allocator, ScriptComponent)(*this, allocator);
			script->m_entity = entity;
			m_scripts.insert(entity, script);
			
			int count;
			serializer.read(&count);
			script->m_scripts.reserve(count);
			for (int i = 0; i < count; ++i)
			{
				ScriptInstance& inst = script->m_scripts.emplace(allocator);
				char tmp[MAX_PATH_LENGTH];
				serializer.read(tmp, lengthOf(tmp));
				setScriptPath(entity, i, Path(tmp));
				if(scene_version >(int)LuaSceneVersion::FLAGS)
					serializer.read(&inst.m_flags.base);

				int prop_count;
				serializer.read(&prop_count);
				for (int j = 0; j < prop_count; ++j)
				{
					char tmp[1024];
					serializer.read(tmp, lengthOf(tmp));
					u32 hash = crc32(tmp);
					int prop_idx = ScriptComponent::getProperty(inst, hash);
					Property* prop;
					if (prop_idx < 0)
					{
						prop = &inst.m_properties.emplace(allocator);
						prop->type = Property::ANY;
						prop->name_hash = hash;
						if (m_property_names.find(hash) < 0)
						{
							m_property_names.emplace(hash, tmp, allocator);
						}
					}
					else
					{
						prop = &inst.m_properties[prop_idx];
					}
					tmp[0] = 0;
					if (scene_version > (int)LuaSceneVersion::PROPERTY_TYPE) serializer.read((int*)&prop->type);
					serializer.read(tmp, lengthOf(tmp));
					
					if (prop->type == Property::ENTITY)
					{
						u64 guid;
						fromCString(tmp, lengthOf(tmp), &guid);
						Entity entity = serializer.getEntity({guid});
						toCString(entity.index, tmp, lengthOf(tmp));
					}
					prop->stored_value = tmp;
					applyProperty(inst, *prop, tmp);
				}
			}

			m_universe.onComponentCreated(entity, LUA_SCRIPT_TYPE, this);
		}


		void serialize(OutputBlob& serializer) override
		{
			serializer.write(m_scripts.size());
			for (auto iter = m_scripts.begin(), end = m_scripts.end(); iter != end; ++iter)
			{
				ScriptComponent* script_cmp = iter.value();
				serializer.write(script_cmp->m_entity);
				serializer.write(script_cmp->m_scripts.size());
				for (auto& scr : script_cmp->m_scripts)
				{
					serializer.writeString(scr.m_script ? scr.m_script->getPath().c_str() : "");
					serializer.write(scr.m_flags);
					serializer.write(scr.m_properties.size());
					for (Property& prop : scr.m_properties)
					{
						serializer.write(prop.name_hash);
						int idx = m_property_names.find(prop.name_hash);
						if (idx >= 0)
						{
							const char* name = m_property_names.at(idx).c_str();
							char tmp[1024];
							getProperty(prop, name, scr, tmp, lengthOf(tmp));
							serializer.writeString(tmp);
						}
						else
						{
							serializer.writeString("");
						}
					}
				}
			}
		}


		void deserialize(InputBlob& serializer) override
		{
			int len = serializer.read<int>();
			m_scripts.rehash(len);
			for (int i = 0; i < len; ++i)
			{
				auto& allocator = m_system.m_allocator;
				ScriptComponent* script = LUMIX_NEW(allocator, ScriptComponent)(*this, allocator);

				serializer.read(script->m_entity);
				m_scripts.insert(script->m_entity, script);
				int scr_count;
				serializer.read(scr_count);
				for (int j = 0; j < scr_count; ++j)
				{
					auto& scr = script->m_scripts.emplace(allocator);

					char tmp[MAX_PATH_LENGTH];
					serializer.readString(tmp, MAX_PATH_LENGTH);
					serializer.read(scr.m_flags);
					scr.m_state = nullptr;
					int prop_count;
					serializer.read(prop_count);
					scr.m_properties.reserve(prop_count);
					for (int j = 0; j < prop_count; ++j)
					{
						Property& prop = scr.m_properties.emplace(allocator);
						prop.type = Property::ANY;
						serializer.read(prop.name_hash);
						char tmp[1024];
						tmp[0] = 0;
						serializer.readString(tmp, sizeof(tmp));
						prop.stored_value = tmp;
					}
					setScriptPath(*script, scr, Path(tmp));
				}
				m_universe.onComponentCreated(script->m_entity, LUA_SCRIPT_TYPE, this);
			}
		}


		IPlugin& getPlugin() const override { return m_system; }


		void initScripts()
		{
			ASSERT(!m_scripts_init_called && m_is_game_running);
			// copy m_scripts to tmp, because scripts can create other scripts -> m_scripts is not const
			Array<ScriptComponent*> tmp(m_system.m_allocator);
			tmp.reserve(m_scripts.size());
			for (auto* scr : m_scripts) tmp.push(scr);

			for (auto* scr : tmp)
			{
				for (int j = 0; j < scr->m_scripts.size(); ++j)
				{
					auto& instance = scr->m_scripts[j];
					if (!instance.m_script) continue;
					if (!instance.m_script->isReady()) continue;
					if (!instance.m_flags.isSet(ScriptInstance::ENABLED)) continue;

					startScript(instance, false);
				}
			}
			m_scripts_init_called = true;
		}


		void updateTimers(float time_delta)
		{
			int timers_to_remove[1024];
			int timers_to_remove_count = 0;
			for (int i = 0, c = m_timers.size(); i < c; ++i)
			{
				auto& timer = m_timers[i];
				timer.time -= time_delta;
				if (timer.time < 0)
				{
					if (lua_rawgeti(timer.state, LUA_REGISTRYINDEX, timer.func) != LUA_TFUNCTION)
					{
						ASSERT(false);
					}

					if (lua_pcall(timer.state, 0, 0, 0) != LUA_OK)
					{
						g_log_error.log("Lua Script") << lua_tostring(timer.state, -1);
						lua_pop(timer.state, 1);
					}
					timers_to_remove[timers_to_remove_count] = i;
					++timers_to_remove_count;
					if (timers_to_remove_count >= lengthOf(timers_to_remove))
					{
						g_log_error.log("Lua Script") << "Too many lua timers in one frame, some are not executed";
						break;
					}
				}
			}
			for (int i = timers_to_remove_count - 1; i >= 0; --i)
			{
				auto& timer = m_timers[timers_to_remove[i]];
				luaL_unref(timer.state, LUA_REGISTRYINDEX, timer.func);
				m_timers.eraseFast(timers_to_remove[i]);
			}
		}


		void processInputEvent(const CallbackData& callback, const InputSystem::Event& event)
		{
			lua_State* L = callback.state;
			lua_newtable(L); // [lua_event]
			LuaWrapper::push(L, (u32)event.type); // [lua_event, event.type]
			lua_setfield(L, -2, "type"); // [lua_event]

			lua_newtable(L); // [lua_event, lua_device]
			LuaWrapper::push(L, (u32)event.device->type); // [lua_event, lua_device, device.type]
			lua_setfield(L, -2, "type"); // [lua_event, lua_device]
			LuaWrapper::push(L, event.device->index); // [lua_event, lua_device, device.index]
			lua_setfield(L, -2, "index"); // [lua_event, lua_device]

			lua_setfield(L, -2, "device"); // [lua_event]

			switch(event.type)
			{
				case InputSystem::Event::DEVICE_ADDED:
					break;
				case InputSystem::Event::DEVICE_REMOVED:
					break;
				case InputSystem::Event::BUTTON:
					LuaWrapper::push(L, (u32)event.data.button.state); // [lua_event, button.state]
					lua_setfield(L, -2, "state"); // [lua_event]
					LuaWrapper::push(L, event.data.button.scancode); // [lua_event, button.scancode]
					lua_setfield(L, -2, "scancode"); // [lua_event]
					LuaWrapper::push(L, event.data.button.key_id); // [lua_event, button.x_abs]
					lua_setfield(L, -2, "key_id"); // [lua_event]
					LuaWrapper::push(L, event.data.button.x_abs); // [lua_event, button.x_abs]
					lua_setfield(L, -2, "x_abs"); // [lua_event]
					LuaWrapper::push(L, event.data.button.y_abs); // [lua_event, button.y_abs]
					lua_setfield(L, -2, "y_abs"); // [lua_event]
					break;
				case InputSystem::Event::AXIS:
					LuaWrapper::push(L, event.data.axis.x); // [lua_event, axis.x]
					lua_setfield(L, -2, "x"); // [lua_event]
					LuaWrapper::push(L, event.data.axis.y); // [lua_event, axis.y]
					lua_setfield(L, -2, "y"); // [lua_event]
					LuaWrapper::push(L, event.data.axis.x_abs); // [lua_event, axis.x_abs]
					lua_setfield(L, -2, "x_abs"); // [lua_event]
					LuaWrapper::push(L, event.data.axis.y_abs); // [lua_event, axis.y_abs]
					lua_setfield(L, -2, "y_abs"); // [lua_event]
					break;
				case InputSystem::Event::TEXT_INPUT:
					LuaWrapper::push(L, event.data.text.text); // [lua_event, axis.x]
					lua_setfield(L, -2, "text"); // [lua_event]
					break;
				default:
					ASSERT(false);
					break;
			}


			if (lua_rawgeti(L, LUA_REGISTRYINDEX, callback.environment) != LUA_TTABLE) // [lua_event, environment]
			{
				ASSERT(false);
			}
			if (lua_getfield(L, -1, "onInputEvent") != LUA_TFUNCTION)  // [lua_event, environment, func]
			{
				lua_pop(L, 3); // []
				return;
			}

			lua_pushvalue(L, -3); // [lua_event, environment, func, lua_event]
			
			if (lua_pcall(L, 1, 0, 0) != LUA_OK)// [lua_event, environment]
			{
				g_log_error.log("Lua Script") << lua_tostring(L, -1);
				lua_pop(L, 1); // []
			}
			lua_pop(L, 2); // []
		}


		void processInputEvents()
		{
			if (m_input_handlers.empty()) return;
			InputSystem& input_system = m_system.m_engine.getInputSystem();
			const InputSystem::Event* events = input_system.getEvents();
			for (int i = 0, c = input_system.getEventsCount(); i < c; ++i)
			{
				for (const CallbackData& cb : m_input_handlers)
				{
					processInputEvent(cb, events[i]);
				}
			}
		}


		void update(float time_delta, bool paused) override
		{
			PROFILE_FUNCTION();

			if (!m_is_game_running) return;
			if (!m_scripts_init_called) initScripts();

			if (paused) return;

			processInputEvents();
			updateTimers(time_delta);

			for (int i = 0; i < m_updates.size(); ++i)
			{
				CallbackData update_item = m_updates[i];
				if (lua_rawgeti(update_item.state, LUA_REGISTRYINDEX, update_item.environment) != LUA_TTABLE)
				{
					ASSERT(false);
				}
				if (lua_getfield(update_item.state, -1, "update") != LUA_TFUNCTION)
				{
					lua_pop(update_item.state, 2);
					continue;
				}

				lua_pushnumber(update_item.state, time_delta);
				if (lua_pcall(update_item.state, 1, 0, 0) != LUA_OK)
				{
					g_log_error.log("Lua Script") << lua_tostring(update_item.state, -1);
					lua_pop(update_item.state, 1);
				}
				lua_pop(update_item.state, 1);
			}
		}


		Property& getScriptProperty(Entity entity, int scr_index, const char* name)
		{
			u32 name_hash = crc32(name);
			ScriptComponent* script_cmp = m_scripts[entity];
			for (auto& prop : script_cmp->m_scripts[scr_index].m_properties)
			{
				if (prop.name_hash == name_hash)
				{
					return prop;
				}
			}

			script_cmp->m_scripts[scr_index].m_properties.emplace(m_system.m_allocator);
			auto& prop = script_cmp->m_scripts[scr_index].m_properties.back();
			prop.name_hash = name_hash;
			return prop;
		}


		Path getScriptPath(Entity entity, int scr_index) override
		{
			auto& tmp = m_scripts[entity]->m_scripts[scr_index];
			return tmp.m_script ? tmp.m_script->getPath() : Path("");
		}


		void setScriptPath(Entity entity, int scr_index, const Path& path) override
		{
			auto* script_cmp = m_scripts[entity];
			if (script_cmp->m_scripts.size() <= scr_index) return;
			setScriptPath(*script_cmp, script_cmp->m_scripts[scr_index], path);
		}


		int getScriptCount(Entity entity) override
		{
			return m_scripts[entity]->m_scripts.size();
		}


		void insertScript(Entity entity, int idx) override
		{
			m_scripts[entity]->m_scripts.emplaceAt(idx, m_system.m_allocator);
		}


		int addScript(Entity entity) override
		{
			ScriptComponent* script_cmp = m_scripts[entity];
			script_cmp->m_scripts.emplace(m_system.m_allocator);
			return script_cmp->m_scripts.size() - 1;
		}


		void moveScript(Entity entity, int scr_index, bool up) override
		{
			auto* script_cmp = m_scripts[entity];
			if (!up && scr_index > script_cmp->m_scripts.size() - 2) return;
			if (up && scr_index == 0) return;
			int other = up ? scr_index - 1 : scr_index + 1;
			ScriptInstance tmp = script_cmp->m_scripts[scr_index];
			script_cmp->m_scripts[scr_index] = script_cmp->m_scripts[other];
			script_cmp->m_scripts[other] = tmp;
		}


		void enableScript(Entity entity, int scr_index, bool enable) override
		{
			ScriptInstance& inst = m_scripts[entity]->m_scripts[scr_index];
			if (inst.m_flags.isSet(ScriptInstance::ENABLED) == enable) return;

			inst.m_flags.set(ScriptInstance::ENABLED, enable);
			if(enable)
			{
				startScript(inst, false);
			}
			else
			{
				disableScript(inst);
			}
		}


		bool isScriptEnabled(Entity entity, int scr_index) const override
		{
			return m_scripts[entity]->m_scripts[scr_index].m_flags.isSet(ScriptInstance::ENABLED);
		}


		void removeScript(Entity entity, int scr_index) override
		{
			setScriptPath(entity, scr_index, Path());
			m_scripts[entity]->m_scripts.eraseFast(scr_index);
		}


		void serializeScript(Entity entity, int scr_index, OutputBlob& blob) override
		{
			auto& scr = m_scripts[entity]->m_scripts[scr_index];
			blob.writeString(scr.m_script ? scr.m_script->getPath().c_str() : "");
			blob.write(scr.m_flags);
			blob.write(scr.m_properties.size());
			for (auto prop : scr.m_properties)
			{
				blob.write(prop.name_hash);
				char tmp[1024];
				const char* property_name = getPropertyName(prop.name_hash);
				if (!property_name)
				{
					blob.writeString(prop.stored_value.c_str());
				}
				else
				{
					getProperty(prop, property_name, scr, tmp, lengthOf(tmp));
					blob.writeString(tmp);
				}
			}
		}


		void deserializeScript(Entity entity, int scr_index, InputBlob& blob) override
		{
			auto& scr = m_scripts[entity]->m_scripts[scr_index];
			int count;
			char path[MAX_PATH_LENGTH];
			blob.readString(path, lengthOf(path));
			blob.read(scr.m_flags);
			blob.read(count);
			scr.m_environment = -1;
			scr.m_properties.clear();
			char buf[256];
			for (int i = 0; i < count; ++i)
			{
				auto& prop = scr.m_properties.emplace(m_system.m_allocator);
				prop.type = Property::ANY;
				blob.read(prop.name_hash);
				blob.readString(buf, lengthOf(buf));
				prop.stored_value = buf;
			}
			setScriptPath(entity, scr_index, Path(path));
		}


		LuaScriptSystemImpl& m_system;
		HashMap<Entity, ScriptComponent*> m_scripts;
		AssociativeArray<u32, string> m_property_names;
		Array<CallbackData> m_input_handlers;
		Universe& m_universe;
		Array<CallbackData> m_updates;
		Array<TimerData> m_timers;
		FunctionCall m_function_call;
		ScriptInstance* m_current_script_instance;
		bool m_scripts_init_called = false;
		bool m_is_api_registered = false;
		bool m_is_game_running = false;
		GUIScene* m_gui_scene = nullptr;
	};


	LuaScriptSystemImpl::LuaScriptSystemImpl(Engine& engine)
		: m_engine(engine)
		, m_allocator(engine.getAllocator())
		, m_script_manager(m_allocator)
	{
		m_script_manager.create(LuaScript::TYPE, engine.getResourceManager());

		using namespace Reflection;
		static auto lua_scene = scene("lua_script",
			component("lua_script",
				blob_property("data", LUMIX_PROP(LuaScriptScene, ScriptData))
			)
		);
		registerScene(lua_scene);
	}


	LuaScriptSystemImpl::~LuaScriptSystemImpl()
	{
		m_script_manager.destroy();
	}


	void LuaScriptSystemImpl::createScenes(Universe& ctx)
	{
		auto* scene = LUMIX_NEW(m_allocator, LuaScriptSceneImpl)(*this, ctx);
		ctx.addScene(scene);
	}


	void LuaScriptSystemImpl::destroyScene(IScene* scene)
	{
		LUMIX_DELETE(m_allocator, scene);
	}


	LUMIX_PLUGIN_ENTRY(lua_script)
	{
		return LUMIX_NEW(engine.getAllocator(), LuaScriptSystemImpl)(engine);
	}
}
