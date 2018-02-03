#pragma once


#include "engine/lumix.h"
#include "engine/blob.h"
#include "engine/resource.h"
#include "engine/universe/component.h"
#include "engine/metaprogramming.h"


#define LUMIX_PROP_FULL(Scene, Getter, Setter) \
	&Scene::Getter, #Scene "::" #Getter, &Scene::Setter, #Scene "::" #Setter
#define LUMIX_PROP(Scene, Property) \
	&Scene::get##Property, #Scene "::get" #Property, &Scene::set##Property, #Scene "::set" #Property
#define LUMIX_ENUM_VALUE(value) EnumValue(#value, (int)value)
#define LUMIX_FUNC(Func)\
	&Func, #Func

namespace Lumix
{


template <typename T> class Array;
struct IAllocator;
class Path;
class PropertyDescriptorBase;
struct Int2;
struct Vec2;
struct Vec3;
struct Vec4;


namespace Reflection
{


struct IAttribute
{
	enum Type
	{
		MIN,
		CLAMP,
		RADIANS,
		COLOR,
		RESOURCE
	};

	virtual ~IAttribute() {}
	virtual int getType() const = 0;
};

struct ComponentBase;
struct IPropertyVisitor;
struct SceneBase;
struct EnumBase;

struct IAttributeVisitor
{
	virtual ~IAttributeVisitor() {}
	virtual void visit(const IAttribute& attr) = 0;
};

struct PropertyBase
{
	virtual ~PropertyBase() {}

	virtual void visit(IAttributeVisitor& visitor) const = 0;
	virtual void setValue(ComponentUID cmp, int index, InputBlob& stream) const = 0;
	virtual void getValue(ComponentUID cmp, int index, OutputBlob& stream) const = 0;

	const char* name;
	const char* getter_code = "";
	const char* setter_code = "";
};


LUMIX_ENGINE_API void init(IAllocator& allocator);
LUMIX_ENGINE_API void shutdown();

LUMIX_ENGINE_API int getScenesCount();
LUMIX_ENGINE_API const SceneBase& getScene(int index);

LUMIX_ENGINE_API int getEnumsCount();
LUMIX_ENGINE_API const EnumBase& getEnum(int index);

LUMIX_ENGINE_API const IAttribute* getAttribute(const PropertyBase& prop, IAttribute::Type type);
LUMIX_ENGINE_API void registerEnum(const EnumBase& e);
LUMIX_ENGINE_API void registerScene(const SceneBase& scene);
LUMIX_ENGINE_API const ComponentBase* getComponent(ComponentType cmp_type);
LUMIX_ENGINE_API const PropertyBase* getProperty(ComponentType cmp_type, const char* property);
LUMIX_ENGINE_API const PropertyBase* getProperty(ComponentType cmp_type, u32 property_name_hash);
LUMIX_ENGINE_API const PropertyBase* getProperty(ComponentType cmp_type, const char* property, const char* subproperty);


LUMIX_ENGINE_API ComponentType getComponentType(const char* id);
LUMIX_ENGINE_API u32 getComponentTypeHash(ComponentType type);
LUMIX_ENGINE_API ComponentType getComponentTypeFromHash(u32 hash);
LUMIX_ENGINE_API int getComponentTypesCount();
LUMIX_ENGINE_API const char* getComponentTypeID(int index);


namespace detail 
{


template <typename T> void writeToStream(OutputBlob& stream, T value)
{
	stream.write(value);
}
	
template <typename T> T readFromStream(InputBlob& stream)
{
	return stream.read<T>();
}

template <> LUMIX_ENGINE_API Path readFromStream<Path>(InputBlob& stream);
template <> LUMIX_ENGINE_API void writeToStream<Path>(OutputBlob& stream, Path);
template <> LUMIX_ENGINE_API void writeToStream<const Path&>(OutputBlob& stream, const Path& path);
template <> LUMIX_ENGINE_API const char* readFromStream<const char*>(InputBlob& stream);
template <> LUMIX_ENGINE_API void writeToStream<const char*>(OutputBlob& stream, const char* path);


template <typename Getter> struct GetterProxy;

template <typename R, typename C>
struct GetterProxy<R(C::*)(Entity, int)>
{
	using Getter = R(C::*)(Entity, int);
	static void invoke(OutputBlob& stream, C* inst, Getter getter, Entity entity, int index)
	{
		R value = (inst->*getter)(entity, index);
		writeToStream(stream, value);
	}
};

template <typename R, typename C>
struct GetterProxy<R(C::*)(Entity)>
{
	using Getter = R(C::*)(Entity);
	static void invoke(OutputBlob& stream, C* inst, Getter getter, Entity entity, int index)
	{
		R value = (inst->*getter)(entity);
		writeToStream(stream, value);
	}
};


template <typename Setter> struct SetterProxy;

template <typename C, typename A>
struct SetterProxy<void (C::*)(Entity, int, A)>
{
	using Setter = void (C::*)(Entity, int, A);
	static void invoke(InputBlob& stream, C* inst, Setter setter, Entity entity, int index)
	{
		using Value = RemoveCR<A>;
		auto value = readFromStream<Value>(stream);
		(inst->*setter)(entity, index, value);
	}
};

template <typename C, typename A>
struct SetterProxy<void (C::*)(Entity, A)>
{
	using Setter = void (C::*)(Entity, A);
	static void invoke(InputBlob& stream, C* inst, Setter setter, Entity entity, int index)
	{
		using Value = RemoveCR<A>;
		auto value = readFromStream<Value>(stream);
		(inst->*setter)(entity, value);
	}
};


} // namespace detail


struct ResourceAttribute : IAttribute
{
	ResourceAttribute(const char* file_type, ResourceType type) { this->file_type = file_type; this->type = type; }
	ResourceAttribute() {}

	int getType() const override { return RESOURCE; }

	const char* file_type;
	ResourceType type;
};


struct MinAttribute : IAttribute
{
	explicit MinAttribute(float min) { this->min = min; }
	MinAttribute() {}

	int getType() const override { return MIN; }

	float min;
};


struct ClampAttribute : IAttribute
{
	ClampAttribute(float min, float max) { this->min = min; this->max = max; }
	ClampAttribute() {}

	int getType() const override { return CLAMP; }

	float min;
	float max;
};


struct RadiansAttribute : IAttribute
{
	int getType() const override { return RADIANS; }
};


struct ColorAttribute : IAttribute
{
	int getType() const override { return COLOR; }
};


template <typename T> struct Property : PropertyBase {};


struct IBlobProperty : PropertyBase {};


struct ISampledFuncProperty : PropertyBase
{
	virtual float getMaxX() const = 0;
};


struct IEnumProperty : public PropertyBase
{
	void visit(IAttributeVisitor& visitor) const override {}
	virtual int getEnumCount(ComponentUID cmp) const = 0;
	virtual const char* getEnumName(ComponentUID cmp, int index) const = 0;
	virtual int getEnumValueIndex(ComponentUID cmp, int value) const = 0;
	virtual int getEnumValue(ComponentUID cmp, int index) const = 0;
};


struct IArrayProperty : PropertyBase
{
	virtual bool canAddRemove() const = 0;
	virtual void addItem(ComponentUID cmp, int index) const = 0;
	virtual void removeItem(ComponentUID cmp, int index) const = 0;
	virtual int getCount(ComponentUID cmp) const = 0;
	virtual void visit(IPropertyVisitor& visitor) const = 0;
};


struct IPropertyVisitor
{
	virtual ~IPropertyVisitor() {}

	virtual void begin(const ComponentBase&) {}
	virtual void visit(const Property<float>& prop) = 0;
	virtual void visit(const Property<int>& prop) = 0;
	virtual void visit(const Property<Entity>& prop) = 0;
	virtual void visit(const Property<Int2>& prop) = 0;
	virtual void visit(const Property<Vec2>& prop) = 0;
	virtual void visit(const Property<Vec3>& prop) = 0;
	virtual void visit(const Property<Vec4>& prop) = 0;
	virtual void visit(const Property<Path>& prop) = 0;
	virtual void visit(const Property<bool>& prop) = 0;
	virtual void visit(const Property<const char*>& prop) = 0;
	virtual void visit(const IArrayProperty& prop) = 0;
	virtual void visit(const IEnumProperty& prop) = 0;
	virtual void visit(const IBlobProperty& prop) = 0;
	virtual void visit(const ISampledFuncProperty& prop) = 0;
	virtual void end(const ComponentBase&) {}
};


struct ISimpleComponentVisitor : IPropertyVisitor
{
	virtual void visitProperty(const PropertyBase& prop) = 0;

	void visit(const Property<float>& prop) override { visitProperty(prop); }
	void visit(const Property<int>& prop) override { visitProperty(prop); }
	void visit(const Property<Entity>& prop) override { visitProperty(prop); }
	void visit(const Property<Int2>& prop) override { visitProperty(prop); }
	void visit(const Property<Vec2>& prop) override { visitProperty(prop); }
	void visit(const Property<Vec3>& prop) override { visitProperty(prop); }
	void visit(const Property<Vec4>& prop) override { visitProperty(prop); }
	void visit(const Property<Path>& prop) override { visitProperty(prop); }
	void visit(const Property<bool>& prop) override { visitProperty(prop); }
	void visit(const Property<const char*>& prop) override { visitProperty(prop); }
	void visit(const IArrayProperty& prop) override { visitProperty(prop); }
	void visit(const IEnumProperty& prop) override { visitProperty(prop); }
	void visit(const IBlobProperty& prop) override { visitProperty(prop); }
	void visit(const ISampledFuncProperty& prop) override { visitProperty(prop); }
};


struct IFunctionVisitor
{
	virtual ~IFunctionVisitor() {}
	virtual void visit(const struct FunctionBase& func) = 0;
};


struct ComponentBase
{
	virtual ~ComponentBase() {}

	virtual int getPropertyCount() const = 0;
	virtual int getFunctionCount() const = 0;
	virtual void visit(IPropertyVisitor&) const = 0;
	virtual void visit(IFunctionVisitor&) const = 0;

	const char* name;
	ComponentType component_type;
};


template <typename Getter, typename Setter, typename Descriptor>
struct EnumProperty : IEnumProperty
{
	void getValue(ComponentUID cmp, int index, OutputBlob& stream) const override
	{
		using C = typename ClassOf<Getter>::Type;
		C* inst = static_cast<C*>(cmp.scene);
		static_assert(4 == sizeof(typename ResultOf<Getter>::Type), "enum must have 4 bytes");
		detail::GetterProxy<Getter>::invoke(stream, inst, getter, cmp.entity, index);
	}

	void setValue(ComponentUID cmp, int index, InputBlob& stream) const override
	{
		using C = typename ClassOf<Getter>::Type;
		C* inst = static_cast<C*>(cmp.scene);

		static_assert(4 == sizeof(typename ResultOf<Getter>::Type), "enum must have 4 bytes");
		detail::SetterProxy<Setter>::invoke(stream, inst, setter, cmp.entity, index);
	}


	int getEnumCount(ComponentUID cmp) const override
	{
		return descriptor.values_count;
	}


	const char* getEnumName(ComponentUID cmp, int index) const override
	{
		return descriptor.values[index].name;
	}


	int getEnumValue(ComponentUID cmp, int index) const override
	{
		return descriptor.values[index].value;
	}


	int getEnumValueIndex(ComponentUID cmp, int value) const override
	{
		for (int i = 0; i < descriptor.values_count; ++i)
		{
			if (descriptor.values[i].value == value) return i;
		}
		return -1;
	}


	Getter getter;
	Setter setter;
	Descriptor descriptor;
};


template <typename Getter, typename Setter, typename Counter, typename Namer>
struct DynEnumProperty : IEnumProperty
{
	void getValue(ComponentUID cmp, int index, OutputBlob& stream) const override
	{
		using C = typename ClassOf<Getter>::Type;
		C* inst = static_cast<C*>(cmp.scene);
		static_assert(4 == sizeof(typename ResultOf<Getter>::Type), "enum must have 4 bytes");
		detail::GetterProxy<Getter>::invoke(stream, inst, getter, cmp.entity, index);
	}

	void setValue(ComponentUID cmp, int index, InputBlob& stream) const override
	{
		using C = typename ClassOf<Getter>::Type;
		C* inst = static_cast<C*>(cmp.scene);

		static_assert(4 == sizeof(typename ResultOf<Getter>::Type), "enum must have 4 bytes");
		detail::SetterProxy<Setter>::invoke(stream, inst, setter, cmp.entity, index);
	}

	int getEnumValueIndex(ComponentUID cmp, int value) const override { return value; }
	int getEnumValue(ComponentUID cmp, int index) const override { return index; }

	int getEnumCount(ComponentUID cmp) const override
	{
		using C = typename ClassOf<Getter>::Type;
		C* inst = static_cast<C*>(cmp.scene);
		return (inst->*counter)();
	}


	const char* getEnumName(ComponentUID cmp, int index) const override
	{
		using C = typename ClassOf<Getter>::Type;
		C* inst = static_cast<C*>(cmp.scene);
		return (inst->*namer)(index);
	}

	Getter getter;
	Setter setter;
	Counter counter;
	Namer namer;
};


template <typename Getter, typename Setter, typename Counter>
struct SampledFuncProperty : ISampledFuncProperty
{
	void getValue(ComponentUID cmp, int index, OutputBlob& stream) const override
	{
		ASSERT(index == -1);
		using C = typename ClassOf<Getter>::Type;
		C* inst = static_cast<C*>(cmp.scene);
		int count = (inst->*counter)(cmp.entity);
		stream.write(count);
		const Vec2* values = (inst->*getter)(cmp.entity);
		stream.write(values, sizeof(float) * 2 * count);
	}

	void setValue(ComponentUID cmp, int index, InputBlob& stream) const override
	{
		ASSERT(index == -1);
		using C = typename ClassOf<Getter>::Type;
		C* inst = static_cast<C*>(cmp.scene);
		int count;
		stream.read(count);
		auto* buf = (const Vec2*)stream.skip(sizeof(float) * 2 * count);
		(inst->*setter)(cmp.entity, buf, count);
	}

	float getMaxX() const override { return max_x; }

	void visit(IAttributeVisitor& visitor) const override {}

	Getter getter;
	Setter setter;
	Counter counter;
	float max_x;
};


template <typename Getter, typename Setter, typename... Attributes>
struct BlobProperty : IBlobProperty
{
	void getValue(ComponentUID cmp, int index, OutputBlob& stream) const override
	{
		using C = typename ClassOf<Getter>::Type;
		C* inst = static_cast<C*>(cmp.scene);
		(inst->*getter)(cmp.entity, stream);
	}

	void setValue(ComponentUID cmp, int index, InputBlob& stream) const override
	{
		using C = typename ClassOf<Getter>::Type;
		C* inst = static_cast<C*>(cmp.scene);
		(inst->*setter)(cmp.entity, stream);
	}

	void visit(IAttributeVisitor& visitor) const override {
		apply([&](auto& x) { visitor.visit(x); }, attributes);
	}

	Tuple<Attributes...> attributes;
	Getter getter;
	Setter setter;
};


template <typename T, typename Getter, typename Setter, typename... Attributes>
struct CommonProperty : Property<T>
{
	void visit(IAttributeVisitor& visitor) const override
	{
		apply([&](auto& x) { visitor.visit(x); }, attributes);
	}

	void getValue(ComponentUID cmp, int index, OutputBlob& stream) const override
	{
		using C = typename ClassOf<Getter>::Type;
		C* inst = static_cast<C*>(cmp.scene);
		detail::GetterProxy<Getter>::invoke(stream, inst, getter, cmp.entity, index);
	}

	void setValue(ComponentUID cmp, int index, InputBlob& stream) const override
	{
		using C = typename ClassOf<Getter>::Type;
		C* inst = static_cast<C*>(cmp.scene);
		detail::SetterProxy<Setter>::invoke(stream, inst, setter, cmp.entity, index);
	}


	Tuple<Attributes...> attributes;
	Getter getter;
	Setter setter;
};


template <typename Counter, typename Adder, typename Remover, typename... Props>
struct ArrayProperty : IArrayProperty
{
	ArrayProperty() {}


	bool canAddRemove() const override { return true; }


	void setValue(ComponentUID cmp, int index, InputBlob& stream) const override
	{
		ASSERT(index == -1);

		int count;
		stream.read(count);
		while (getCount(cmp) < count)
		{
			addItem(cmp, -1);
		}
		while (getCount(cmp) > count)
		{
			removeItem(cmp, getCount(cmp) - 1);
		}
		for (int i = 0; i < count; ++i)
		{
			struct : ISimpleComponentVisitor
			{
				void visitProperty(const PropertyBase& prop) override
				{
					prop.setValue(cmp, index, *stream);
				}

				InputBlob* stream;
				int index;
				ComponentUID cmp;

			} v;
			v.stream = &stream;
			v.index = i;
			v.cmp = cmp;
			visit(v);
		}
	}


	void getValue(ComponentUID cmp, int index, OutputBlob& stream) const override
	{
		ASSERT(index == -1);
		int count = getCount(cmp);
		stream.write(count);
		for (int i = 0; i < count; ++i)
		{
			struct : ISimpleComponentVisitor
			{
				void visitProperty(const PropertyBase& prop) override
				{
					prop.getValue(cmp, index, *stream);
				}

				OutputBlob* stream;
				int index;
				ComponentUID cmp;

			} v;
			v.stream = &stream;
			v.index = i;
			v.cmp = cmp;
			visit(v);
		}
	}


	void addItem(ComponentUID cmp, int index) const override
	{
		using C = typename ClassOf<Counter>::Type;
		C* inst = static_cast<C*>(cmp.scene);
		(inst->*adder)(cmp.entity, index);
	}


	void removeItem(ComponentUID cmp, int index) const override
	{
		using C = typename ClassOf<Counter>::Type;
		C* inst = static_cast<C*>(cmp.scene);
		(inst->*remover)(cmp.entity, index);
	}


	int getCount(ComponentUID cmp) const override
	{ 
		using C = typename ClassOf<Counter>::Type;
		C* inst = static_cast<C*>(cmp.scene);
		return (inst->*counter)(cmp.entity);
	}


	void visit(IPropertyVisitor& visitor) const override
	{
		apply([&](auto& x) { visitor.visit(x); }, properties);
	}


	void visit(IAttributeVisitor& visitor) const override {}


	Tuple<Props...> properties;
	Counter counter;
	Adder adder;
	Remover remover;
};


template <typename Counter, typename... Props>
struct ConstArrayProperty : IArrayProperty
{
	ConstArrayProperty() {}


	bool canAddRemove() const override { return false; }


	void setValue(ComponentUID cmp, int index, InputBlob& stream) const override
	{
		ASSERT(index == -1);

		int count;
		stream.read(count);
		if (getCount(cmp) != count) return;
		
		for (int i = 0; i < count; ++i)
		{
			struct : ISimpleComponentVisitor
			{
				void visitProperty(const PropertyBase& prop) override
				{
					prop.setValue(cmp, index, *stream);
				}

				InputBlob* stream;
				int index;
				ComponentUID cmp;

			} v;
			v.stream = &stream;
			v.index = i;
			v.cmp = cmp;
			visit(v);
		}
	}


	void getValue(ComponentUID cmp, int index, OutputBlob& stream) const override
	{
		ASSERT(index == -1);
		int count = getCount(cmp);
		stream.write(count);
		for (int i = 0; i < count; ++i)
		{
			struct : ISimpleComponentVisitor
			{
				void visitProperty(const PropertyBase& prop) override
				{
					prop.getValue(cmp, index, *stream);
				}

				OutputBlob* stream;
				int index;
				ComponentUID cmp;

			} v;
			v.stream = &stream;
			v.index = i;
			v.cmp = cmp;
			visit(v);
		}
	}


	void addItem(ComponentUID cmp, int index) const override { ASSERT(false); }
	void removeItem(ComponentUID cmp, int index) const override { ASSERT(false); }


	int getCount(ComponentUID cmp) const override
	{
		using C = typename ClassOf<Counter>::Type;
		C* inst = static_cast<C*>(cmp.scene);
		return (inst->*counter)(cmp.entity);
	}


	void visit(IPropertyVisitor& visitor) const override
	{
		apply([&](auto& x) { visitor.visit(x); }, properties);
	}


	void visit(IAttributeVisitor& visitor) const override {

	}


	Tuple<Props...> properties;
	Counter counter;
};


struct IComponentVisitor
{
	virtual ~IComponentVisitor() {}
	virtual void visit(const ComponentBase& cmp) = 0;
};

namespace internal
{
	static const unsigned int FRONT_SIZE = sizeof("Lumix::Reflection::internal::GetTypeNameHelper<") - 1u;
	static const unsigned int BACK_SIZE = sizeof(">::GetTypeName") - 1u;

	template <typename T>
	struct GetTypeNameHelper
	{
		static const char* GetTypeName()
		{
#ifdef _WIN32
			static const size_t size = sizeof(__FUNCTION__) - FRONT_SIZE - BACK_SIZE;
			static char typeName[size] = {};
			copyMemory(typeName, __FUNCTION__ + FRONT_SIZE, size - 1u);
#else
			static const size_t size = sizeof(__PRETTY_FUNCTION__) - FRONT_SIZE - BACK_SIZE;
			static char typeName[size] = {};
			copyMemory(typeName, __PRETTY_FUNCTION__ + FRONT_SIZE, size - 1u);
#endif

			return typeName;
		}
	};
}


template <typename T>
const char* getTypeName()
{
	return internal::GetTypeNameHelper<T>::GetTypeName();
}


struct EnumValue
{
	EnumValue() {}
	EnumValue(const char* name, int value)
		: name(name)
		, value(value)
	{
		const char* c = name + stringLength(name);
		while (*c != ':' && c > name)
		{
			--c;
		}
		if (*c == ':') ++c;
		this->name = c;
	}

	const char* name;
	int value;
};


struct EnumBase
{
	const char* name;
	EnumValue* values;
	int values_count;
};


template <int Count>
struct EnumDescriptor : EnumBase
{
	EnumDescriptor() {}
	EnumDescriptor(const EnumDescriptor& rhs)
	{
		*this = rhs;
	}

	void operator =(const EnumDescriptor& rhs)
	{
		copyMemory(this, &rhs, sizeof(*this));
		values = values_buffer;
	}

	void setValues(int) { }

	template <typename... T>
	void setValues(int i, EnumValue head, T... tail)
	{
		values_buffer[i] = head;
		values = values_buffer;
		values_count = lengthOf(values_buffer);
		setValues(i + 1, tail...);
	}

	EnumValue values_buffer[Count];
};


template <typename T, typename... Values>
auto enumDesciptor(Values... values)
{
	EnumDescriptor<sizeof...(Values)> e;
	e.name = Reflection::getTypeName<T>();
	e.setValues(0, values ...);
	return e;
}


struct SceneBase
{
	virtual ~SceneBase() {}

	virtual int getFunctionCount() const = 0;
	virtual void visit(IFunctionVisitor&) const = 0;
	virtual void visit(IComponentVisitor& visitor) const = 0;

	const char* name;
};


template <typename Components, typename Funcs>
struct Scene : SceneBase
{
	int getFunctionCount() const override { return TupleSize<Funcs>::result; }


	void visit(IFunctionVisitor& visitor) const override
	{
		apply([&](const auto& func) { visitor.visit(func); }, functions);
	}


	void visit(IComponentVisitor& visitor) const override
	{
		apply([&](const auto& cmp) { visitor.visit(cmp); }, components);
	}


	Components components;
	Funcs functions;
};


template <typename Funcs, typename Props>
struct Component : ComponentBase
{
	int getPropertyCount() const override { return TupleSize<Props>::result; }
	int getFunctionCount() const override { return TupleSize<Funcs>::result; }


	void visit(IPropertyVisitor& visitor) const override
	{
		visitor.begin(*this);
		apply([&](auto& x) { visitor.visit(x); }, properties);
		visitor.end(*this);
	}


	void visit(IFunctionVisitor& visitor) const override
	{
		apply([&](auto& x) { visitor.visit(x); }, functions);
	}


	Props properties;
	Funcs functions;
};


template <typename... Components, typename... Funcs>
auto scene(const char* name, Tuple<Funcs...> funcs, Components... components)
{
	Scene<Tuple<Components...>, Tuple<Funcs...>> scene;
	scene.name = name;
	scene.functions = funcs;
	scene.components = makeTuple(components...);
	return scene;
}


template <typename... Components>
auto scene(const char* name, Components... components)
{
	Scene<Tuple<Components...>, Tuple<>> scene;
	scene.name = name;
	scene.components = makeTuple(components...);
	return scene;
}


struct FunctionBase
{
	virtual ~FunctionBase() {}

	virtual int getArgCount() const = 0;
	virtual const char* getReturnType() const = 0;
	virtual const char* getArgType(int i) const = 0;

	const char* decl_code;
};


template <typename F> struct Function;


template <typename R, typename C, typename... Args>
struct Function<R (C::*)(Args...)> : FunctionBase
{
	using F = R(C::*)(Args...);
	F function;

	int getArgCount() const override { return ArgCount<F>::result; }
	const char* getReturnType() const override { return getTypeName<typename ResultOf<F>::Type>(); }
	
	const char* getArgType(int i) const override
	{
		const char* expand[] = {
			getTypeName<Args>()...
		};
		return expand[i];
	}
};


template <typename F>
auto function(F func, const char* decl_code)
{
	Function<F> ret;
	ret.function = func;
	ret.decl_code = decl_code;
	return ret;
}


template <typename... F>
auto functions(F... functions)
{
	Tuple<F...> f = makeTuple(functions...);
	return f;
}


template <typename... Props, typename... Funcs>
auto component(const char* name, Tuple<Funcs...> functions, Props... props)
{
	Component<Tuple<Funcs...>, Tuple<Props...>> cmp;
	cmp.name = name;
	cmp.functions = functions;
	cmp.properties = makeTuple(props...);
	cmp.component_type = getComponentType(name);
	return cmp;
}


template <typename... Props>
auto component(const char* name, Props... props)
{
	Component<Tuple<>, Tuple<Props...>> cmp;
	cmp.name = name;
	cmp.properties = makeTuple(props...);
	cmp.component_type = getComponentType(name);
	return cmp;
}


template <typename Getter, typename Setter, typename... Attributes>
auto blob_property(const char* name, Getter getter, const char* getter_code, Setter setter, const char* setter_code, Attributes... attributes)
{
	BlobProperty<Getter, Setter, Attributes...> p;
	p.attributes = makeTuple(attributes...);
	p.getter = getter;
	p.setter = setter;
	p.getter_code = getter_code;
	p.setter_code = setter_code;
	p.name = name;
	return p;
}


template <typename Getter, typename Setter, typename Counter>
auto sampled_func_property(const char* name, Getter getter, const char* getter_code, Setter setter, const char* setter_code, Counter counter, float max_x)
{
	using R = typename ResultOf<Getter>::Type;
	SampledFuncProperty<Getter, Setter, Counter> p;
	p.getter = getter;
	p.setter = setter;
	p.getter_code = getter_code;
	p.setter_code = setter_code;
	p.counter = counter;
	p.name = name;
	p.max_x = max_x;
	return p;
}


template <typename Getter, typename Setter, typename... Attributes>
auto property(const char* name, Getter getter, const char* getter_code, Setter setter, const char* setter_code, Attributes... attributes)
{
	using R = typename ResultOf<Getter>::Type;
	CommonProperty<R, Getter, Setter, Attributes...> p;
	p.attributes = makeTuple(attributes...);
	p.getter = getter;
	p.setter = setter;
	p.getter_code = getter_code;
	p.setter_code = setter_code;
	p.name = name;
	return p;
}


template <typename Getter, typename Setter, typename Descriptor, typename... Attributes>
auto enum_property(const char* name, Getter getter, const char* getter_code, Setter setter, const char* setter_code, Descriptor desc, Attributes... attributes)
{
	EnumProperty<Getter, Setter, Descriptor> p;
	p.getter = getter;
	p.setter = setter;
	p.getter_code = getter_code;
	p.setter_code = setter_code;
	p.descriptor = desc;
	p.name = name;
	return p;
}


template <typename Getter, typename Setter, typename Counter, typename Namer, typename... Attributes>
auto dyn_enum_property(const char* name, Getter getter, const char* getter_code, Setter setter, const char* setter_code, Counter counter, Namer namer, Attributes... attributes)
{
	DynEnumProperty<Getter, Setter, Counter, Namer> p;
	p.getter = getter;
	p.setter = setter;
	p.getter_code = getter_code;
	p.setter_code = setter_code;
	p.namer = namer;
	p.counter = counter;
	p.name = name;
	return p;
}


template <typename Counter, typename Adder, typename Remover, typename... Props>
auto array(const char* name, Counter counter, Adder adder, Remover remover, Props... properties)
{
	ArrayProperty<Counter, Adder, Remover, Props...> p;
	p.name = name;
	p.counter = counter;
	p.adder = adder;
	p.remover = remover;
	p.properties = makeTuple(properties...);
	return p;
}


template <typename Counter, typename... Props>
auto const_array(const char* name, Counter counter, Props... properties)
{
	ConstArrayProperty<Counter, Props...> p;
	p.name = name;
	p.counter = counter;
	p.properties = makeTuple(properties...);
	return p;
}


} // namespace Reflection


} // namespace Lumix
