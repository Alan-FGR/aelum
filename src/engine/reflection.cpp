#include "engine/reflection.h"
#include "engine/crc32.h"
#include "engine/default_allocator.h"
#include "engine/log.h"


namespace Lumix
{


namespace Reflection
{


namespace detail
{


template <> Path readFromStream<Path>(InputBlob& stream)
{
	const char* c_str = (const char*)stream.getData() + stream.getPosition();
	Path path(c_str);
	stream.skip(stringLength(c_str) + 1);
	return path;
}


template <> void writeToStream<const Path&>(OutputBlob& stream, const Path& path)
{
	const char* str = path.c_str();
	stream.write(str, stringLength(str) + 1);
}


template <> void writeToStream<Path>(OutputBlob& stream, Path path)
{
	const char* str = path.c_str();
	stream.write(str, stringLength(str) + 1);
}


template <> const char* readFromStream<const char*>(InputBlob& stream)
{
	const char* c_str = (const char*)stream.getData() + stream.getPosition();
	stream.skip(stringLength(c_str) + 1);
	return c_str;
}


template <> void writeToStream<const char*>(OutputBlob& stream, const char* value)
{
	stream.write(value, stringLength(value) + 1);
}


}

struct ComponentTypeData
{
	char id[50];
	u32 id_hash;
};


static IAllocator* g_allocator = nullptr;
static const SceneBase* g_scenes[64];
static int g_scenes_count = 0;
static Array<const EnumBase*>* g_enums = nullptr;


struct ComponentLink
{
	const ComponentBase* desc;
	ComponentLink* next;
};


static ComponentLink* g_first_component = nullptr;


const IAttribute* getAttribute(const PropertyBase& prop, IAttribute::Type type)
{
	struct AttrVisitor : IAttributeVisitor
	{
		void visit(const IAttribute& attr) override
		{
			if (attr.getType() == type) result = &attr;
		}
		const IAttribute* result = nullptr;
		IAttribute::Type type;
	} attr_visitor;
	attr_visitor.type = type;
	prop.visit(attr_visitor);
	return attr_visitor.result;
}


const ComponentBase* getComponent(ComponentType cmp_type)
{
	ComponentLink* link = g_first_component;
	while (link)
	{
		if (link->desc->component_type == cmp_type) return link->desc;
		link = link->next;
	}

	return nullptr;
}


const PropertyBase* getProperty(ComponentType cmp_type, u32 property_name_hash)
{
	auto* cmp = getComponent(cmp_type);
	if (!cmp) return nullptr;
	struct Visitor : ISimpleComponentVisitor
	{
		void visitProperty(const PropertyBase& prop) override {
			if (crc32(prop.name) == property_name_hash) result = &prop;
		}
		void visit(const IArrayProperty& prop) override {
			if (result) return;
			visitProperty(prop);
			prop.visit(*this);
		}

		u32 property_name_hash;
		const PropertyBase* result = nullptr;
	} visitor;
	visitor.property_name_hash = property_name_hash;
	cmp->visit(visitor);
	return visitor.result;
}



const PropertyBase* getProperty(ComponentType cmp_type, const char* property, const char* subproperty)
{
	auto* cmp = getComponent(cmp_type);
	if (!cmp) return nullptr;
	struct Visitor : ISimpleComponentVisitor
	{
		void visitProperty(const PropertyBase& prop) override {}

		void visit(const IArrayProperty& prop) override {
			if (equalStrings(prop.name, property))
			{
				struct Subvisitor : ISimpleComponentVisitor
				{
					void visitProperty(const PropertyBase& prop) override {
						if (equalStrings(prop.name, property)) result = &prop;
					}
					const char* property;
					const PropertyBase* result = nullptr;
				} subvisitor;
				subvisitor.property = subproperty;
				prop.visit(subvisitor);
				result = subvisitor.result;
			}
		}

		const char* subproperty;
		const char* property;
		const PropertyBase* result = nullptr;
	} visitor;
	visitor.subproperty = subproperty;
	visitor.property = property;
	cmp->visit(visitor);
	return visitor.result;
}



const PropertyBase* getProperty(ComponentType cmp_type, const char* property)
{
	auto* cmp = getComponent(cmp_type);
	if (!cmp) return nullptr;
	struct Visitor : ISimpleComponentVisitor
	{
		void visitProperty(const PropertyBase& prop) override {
			if (equalStrings(prop.name, property)) result = &prop;
		}

		const char* property;
		const PropertyBase* result = nullptr;
	} visitor;
	visitor.property = property;
	cmp->visit(visitor);
	return visitor.result;
}


void registerComponent(const ComponentBase& desc)
{
	ComponentLink* link = LUMIX_NEW(*g_allocator, ComponentLink);
	link->next = g_first_component;
	link->desc = &desc;
	g_first_component = link;
}


void registerEnum(const EnumBase& e)
{
	g_enums->push(&e);
}


void registerScene(const SceneBase& scene)
{
	struct : IComponentVisitor
	{
		void visit(const ComponentBase& cmp) override
		{
			registerComponent(cmp);
		}
	} visitor;
	scene.visit(visitor);

	ASSERT(g_scenes_count < lengthOf(g_scenes));
	g_scenes[g_scenes_count] = &scene;
	++g_scenes_count;
}


static Array<ComponentTypeData>& getComponentTypes()
{
	static DefaultAllocator allocator;
	static Array<ComponentTypeData> types(allocator);
	return types;
}


void init(IAllocator& allocator)
{
	g_allocator = &allocator;
	g_enums = LUMIX_NEW(allocator, Array<const EnumBase*>)(allocator);
}


static void destroy(ComponentLink* link)
{
	if (!link) return;
	destroy(link->next);
	LUMIX_DELETE(*g_allocator, link);
}


void shutdown()
{
	destroy(g_first_component);
	LUMIX_DELETE(*g_allocator, g_enums);
	g_allocator = nullptr;
}


ComponentType getComponentTypeFromHash(u32 hash)
{
	auto& types = getComponentTypes();
	for (int i = 0, c = types.size(); i < c; ++i)
	{
		if (types[i].id_hash == hash)
		{
			return {i};
		}
	}
	ASSERT(false);
	return {-1};
}


u32 getComponentTypeHash(ComponentType type)
{
	return getComponentTypes()[type.index].id_hash;
}


ComponentType getComponentType(const char* id)
{
	u32 id_hash = crc32(id);
	auto& types = getComponentTypes();
	for (int i = 0, c = types.size(); i < c; ++i)
	{
		if (types[i].id_hash == id_hash)
		{
			return {i};
		}
	}

	auto& cmp_types = getComponentTypes();
	if (types.size() == ComponentType::MAX_TYPES_COUNT)
	{
		g_log_error.log("Engine") << "Too many component types";
		return INVALID_COMPONENT_TYPE;
	}

	ComponentTypeData& type = cmp_types.emplace();
	copyString(type.id, id);
	type.id_hash = id_hash;
	return {getComponentTypes().size() - 1};
}


int getComponentTypesCount()
{
	return getComponentTypes().size();
}


const char* getComponentTypeID(int index)
{
	return getComponentTypes()[index].id;
}


int getScenesCount()
{
	return g_scenes_count;
}


const SceneBase& getScene(int index)
{
	ASSERT(index < lengthOf(g_scenes) && g_scenes[index]);
	return *g_scenes[index];
}


int getEnumsCount()
{
	return g_enums->size();
}


const EnumBase& getEnum(int index)
{
	return *(*g_enums)[index];
}


} // namespace Reflection


} // namespace Lumix
