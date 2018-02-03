#pragma once


#include "engine/log.h"
#include "engine/matrix.h"
#include "engine/metaprogramming.h"
#include <lua.hpp>
#include <lauxlib.h> // must be after lua.hpp


namespace Lumix
{
namespace LuaWrapper
{


template <typename T> inline T toType(lua_State* L, int index)
{
	return (T)lua_touserdata(L, index);
}
template <> inline int toType(lua_State* L, int index)
{
	return (int)lua_tointeger(L, index);
}
template <> inline u16 toType(lua_State* L, int index)
{
	return (u16)lua_tointeger(L, index);
}
template <> inline Entity toType(lua_State* L, int index)
{
	return {(int)lua_tointeger(L, index)};
}
template <> inline Vec3 toType(lua_State* L, int index)
{
	Vec3 v;
	lua_rawgeti(L, index, 1);
	v.x = (float)lua_tonumber(L, -1);
	lua_pop(L, 1);
	lua_rawgeti(L, index, 2);
	v.y = (float)lua_tonumber(L, -1);
	lua_pop(L, 1);
	lua_rawgeti(L, index, 3);
	v.z = (float)lua_tonumber(L, -1);
	lua_pop(L, 1);
	return v;
}
template <> inline Vec4 toType(lua_State* L, int index)
{
	Vec4 v;
	lua_rawgeti(L, index, 1);
	v.x = (float)lua_tonumber(L, -1);
	lua_pop(L, 1);
	lua_rawgeti(L, index, 2);
	v.y = (float)lua_tonumber(L, -1);
	lua_pop(L, 1);
	lua_rawgeti(L, index, 3);
	v.z = (float)lua_tonumber(L, -1);
	lua_pop(L, 1);
	lua_rawgeti(L, index, 4);
	v.w = (float)lua_tonumber(L, -1);
	lua_pop(L, 1);
	return v;
}
template <> inline Quat toType(lua_State* L, int index)
{
	Quat v;
	lua_rawgeti(L, index, 1);
	v.x = (float)lua_tonumber(L, -1);
	lua_pop(L, 1);
	lua_rawgeti(L, index, 2);
	v.y = (float)lua_tonumber(L, -1);
	lua_pop(L, 1);
	lua_rawgeti(L, index, 3);
	v.z = (float)lua_tonumber(L, -1);
	lua_pop(L, 1);
	lua_rawgeti(L, index, 4);
	v.w = (float)lua_tonumber(L, -1);
	lua_pop(L, 1);
	return v;
}
template <> inline Vec2 toType(lua_State* L, int index)
{
	Vec2 v;
	lua_rawgeti(L, index, 1);
	v.x = (float)lua_tonumber(L, -1);
	lua_pop(L, 1);
	lua_rawgeti(L, index, 2);
	v.y = (float)lua_tonumber(L, -1);
	lua_pop(L, 1);
	return v;
}
template <> inline Matrix toType(lua_State* L, int index)
{
	Matrix v;
	for (int i = 0; i < 16; ++i)
	{
		lua_rawgeti(L, index, i + 1);
		(&(v.m11))[i] = (float)lua_tonumber(L, -1);
		lua_pop(L, 1);
	}
	return v;
}
template <> inline Int2 toType(lua_State* L, int index)
{
	Int2 v;
	lua_rawgeti(L, index, 1);
	v.x = (int)lua_tointeger(L, -1);
	lua_pop(L, 1);
	lua_rawgeti(L, index, 2);
	v.y = (int)lua_tointeger(L, -1);
	lua_pop(L, 1);
	return v;
}
template <> inline i64 toType(lua_State* L, int index)
{
	return (i64)lua_tointeger(L, index);
}
template <> inline u32 toType(lua_State* L, int index)
{
	return (u32)lua_tointeger(L, index);
}
template <> inline u64 toType(lua_State* L, int index)
{
	return (u64)lua_tointeger(L, index);
}
template <> inline bool toType(lua_State* L, int index)
{
	return lua_toboolean(L, index) != 0;
}
template <> inline float toType(lua_State* L, int index)
{
	return (float)lua_tonumber(L, index);
}
template <> inline const char* toType(lua_State* L, int index)
{
	return lua_tostring(L, index);
}
template <> inline void* toType(lua_State* L, int index)
{
	return lua_touserdata(L, index);
}


template <typename T> inline const char* typeToString()
{
	return "userdata";
}
template <> inline const char* typeToString<int>()
{
	return "number|integer";
}
template <> inline const char* typeToString<u16>()
{
	return "number|u16";
}
template <> inline const char* typeToString<Entity>()
{
	return "entity";
}
template <> inline const char* typeToString<u32>()
{
	return "number|integer";
}
template <> inline const char* typeToString<const char*>()
{
	return "string";
}
template <> inline const char* typeToString<bool>()
{
	return "boolean";
}

template <> inline const char* typeToString<float>()
{
	return "number|float";
}


template <typename T> inline bool isType(lua_State* L, int index)
{
	return lua_islightuserdata(L, index) != 0;
}
template <> inline bool isType<int>(lua_State* L, int index)
{
	return lua_isinteger(L, index) != 0;
}
template <> inline bool isType<u16>(lua_State* L, int index)
{
	return lua_isinteger(L, index) != 0;
}
template <> inline bool isType<Entity>(lua_State* L, int index)
{
	return lua_isinteger(L, index) != 0;
}
template <> inline bool isType<Vec3>(lua_State* L, int index)
{
	return lua_istable(L, index) != 0 && lua_rawlen(L, index) == 3;
}
template <> inline bool isType<Vec4>(lua_State* L, int index)
{
	return lua_istable(L, index) != 0 && lua_rawlen(L, index) == 4;
}
template <> inline bool isType<Vec2>(lua_State* L, int index)
{
	return lua_istable(L, index) != 0 && lua_rawlen(L, index) == 2;
}
template <> inline bool isType<Matrix>(lua_State* L, int index)
{
	return lua_istable(L, index) != 0 && lua_rawlen(L, index) == 16;
}
template <> inline bool isType<Quat>(lua_State* L, int index)
{
	return lua_istable(L, index) != 0 && lua_rawlen(L, index) == 4;
}
template <> inline bool isType<u32>(lua_State* L, int index)
{
	return lua_isinteger(L, index) != 0;
}
template <> inline bool isType<u64>(lua_State* L, int index)
{
	return lua_isinteger(L, index) != 0;
}
template <> inline bool isType<i64>(lua_State* L, int index)
{
	return lua_isinteger(L, index) != 0;
}
template <> inline bool isType<bool>(lua_State* L, int index)
{
	return lua_isboolean(L, index) != 0;
}
template <> inline bool isType<float>(lua_State* L, int index)
{
	return lua_isnumber(L, index) != 0;
}
template <> inline bool isType<const char*>(lua_State* L, int index)
{
	return lua_isstring(L, index) != 0;
}
template <> inline bool isType<void*>(lua_State* L, int index)
{
	return lua_islightuserdata(L, index) != 0;
}


template <typename T> inline void push(lua_State* L, T value)
{
	lua_pushlightuserdata(L, value);
}
template <> inline void push(lua_State* L, float value)
{
	lua_pushnumber(L, value);
}
template <typename T> inline void push(lua_State* L, const T* value)
{
	lua_pushlightuserdata(L, (T*)value);
}
template <> inline void push(lua_State* L, Entity value)
{
	lua_pushinteger(L, value.index);
}
inline void push(lua_State* L, const Vec2& value)
{
	lua_createtable(L, 2, 0);

	lua_pushnumber(L, value.x);
	lua_rawseti(L, -2, 1);

	lua_pushnumber(L, value.y);
	lua_rawseti(L, -2, 2);
}
inline void push(lua_State* L, const Matrix& value)
{
	lua_createtable(L, 16, 0);

	for (int i = 0; i < 16; ++i)
	{
		lua_pushnumber(L, (&value.m11)[i]);
		lua_rawseti(L, -2, i + 1);
	}
}
inline void push(lua_State* L, const Int2& value)
{
	lua_createtable(L, 2, 0);

	lua_pushinteger(L, value.x);
	lua_rawseti(L, -2, 1);

	lua_pushinteger(L, value.y);
	lua_rawseti(L, -2, 2);
}
inline void push(lua_State* L, const Vec3& value)
{
	lua_createtable(L, 3, 0);

	lua_pushnumber(L, value.x);
	lua_rawseti(L, -2, 1);

	lua_pushnumber(L, value.y);
	lua_rawseti(L, -2, 2);

	lua_pushnumber(L, value.z);
	lua_rawseti(L, -2, 3);
}
inline void push(lua_State* L, const Vec4& value)
{
	lua_createtable(L, 4, 0);

	lua_pushnumber(L, value.x);
	lua_rawseti(L, -2, 1);

	lua_pushnumber(L, value.y);
	lua_rawseti(L, -2, 2);

	lua_pushnumber(L, value.z);
	lua_rawseti(L, -2, 3);

	lua_pushnumber(L, value.w);
	lua_rawseti(L, -2, 4);
}
inline void push(lua_State* L, const Quat& value)
{
	lua_createtable(L, 4, 0);

	lua_pushnumber(L, value.x);
	lua_rawseti(L, -2, 1);

	lua_pushnumber(L, value.y);
	lua_rawseti(L, -2, 2);

	lua_pushnumber(L, value.z);
	lua_rawseti(L, -2, 3);

	lua_pushnumber(L, value.w);
	lua_rawseti(L, -2, 4);
}
template <> inline void push(lua_State* L, bool value)
{
	lua_pushboolean(L, value);
}
template <> inline void push(lua_State* L, const char* value)
{
	lua_pushstring(L, value);
}
template <> inline void push(lua_State* L, char* value)
{
	lua_pushstring(L, value);
}
template <> inline void push(lua_State* L, int value)
{
	lua_pushinteger(L, value);
}
template <> inline void push(lua_State* L, u16 value)
{
	lua_pushinteger(L, value);
}
template <> inline void push(lua_State* L, unsigned int value)
{
	lua_pushinteger(L, value);
}
template <> inline void push(lua_State* L, u64 value)
{
	lua_pushinteger(L, value);
}
template <> inline void push(lua_State* L, void* value)
{
	lua_pushlightuserdata(L, value);
}


inline void createSystemVariable(lua_State* L, const char* system, const char* var_name, void* value)
{
	if (lua_getglobal(L, system) == LUA_TNIL)
	{
		lua_pop(L, 1);
		lua_newtable(L);
		lua_setglobal(L, system);
		lua_getglobal(L, system);
	}
	lua_pushlightuserdata(L, value);
	lua_setfield(L, -2, var_name);
	lua_pop(L, 1);
}


inline void createSystemVariable(lua_State* L, const char* system, const char* var_name, int value)
{
	if (lua_getglobal(L, system) == LUA_TNIL)
	{
		lua_pop(L, 1);
		lua_newtable(L);
		lua_setglobal(L, system);
		lua_getglobal(L, system);
	}
	lua_pushinteger(L, value);
	lua_setfield(L, -2, var_name);
	lua_pop(L, 1);
}


inline void createSystemFunction(lua_State* L, const char* system, const char* var_name, lua_CFunction fn)
{
	if (lua_getglobal(L, system) == LUA_TNIL)
	{
		lua_pop(L, 1);
		lua_newtable(L);
		lua_setglobal(L, system);
		lua_getglobal(L, system);
	}
	lua_pushcfunction(L, fn);
	lua_setfield(L, -2, var_name);
	lua_pop(L, 1);
}


inline void createSystemClosure(lua_State* L, const char* system, void* system_ptr, const char* var_name, lua_CFunction fn)
{
	if (lua_getglobal(L, system) == LUA_TNIL)
	{
		lua_pop(L, 1);
		lua_newtable(L);
		lua_setglobal(L, system);
		lua_getglobal(L, system);
	}
	lua_pushlightuserdata(L, system_ptr);
	lua_pushcclosure(L, fn, 1);
	lua_setfield(L, -2, var_name);
	lua_pop(L, 1);
}



inline const char* luaTypeToString(int type)
{
	switch (type)
	{
		case LUA_TNUMBER: return "number";
		case LUA_TBOOLEAN: return "boolean";
		case LUA_TFUNCTION: return "function";
		case LUA_TLIGHTUSERDATA: return "light userdata";
		case LUA_TNIL: return "nil";
		case LUA_TSTRING: return "string";
		case LUA_TTABLE: return "table";
		case LUA_TUSERDATA: return "userdata";
		default: return "Unknown";
	}
}


inline void argError(lua_State* L, int index, const char* expected_type)
{
	char buf[128];
	copyString(buf, "expected ");
	catString(buf, expected_type);
	catString(buf, ", got ");
	int type = lua_type(L, index);
	catString(buf, LuaWrapper::luaTypeToString(type));
	luaL_argerror(L, index, buf);
}


template <typename T> void argError(lua_State* L, int index)
{
	argError(L, index, typeToString<T>());
}


template <typename T> T checkArg(lua_State* L, int index)
{
	if (!isType<T>(L, index))
	{
		argError<T>(L, index);
	}
	return toType<T>(L, index);
}


inline void checkTableArg(lua_State* L, int index)
{
	if (!lua_istable(L, index))
	{
		argError(L, index, "table");
	}
}


template <typename T>
inline void getOptionalField(lua_State* L, int idx, const char* field_name, T* out)
{
	if (lua_getfield(L, idx, field_name) != LUA_TNIL && isType<T>(L, -1))
	{
		*out = toType<T>(L, -1);
	}
	lua_pop(L, 1);
}


namespace details
{


template <typename T, int index>
RemoveCVR<T> convert(lua_State* L)
{
	return checkArg<RemoveCVR<T>>(L, index);
}


template <typename T> struct Caller;


template <int... indices>
struct Caller<Indices<indices...>>
{
	template <typename R, typename... Args>
	static int callFunction(R (*f)(Args...), lua_State* L)
	{
		R v = f(convert<Args, indices>(L)...);
		push(L, v);
		return 1;
	}


	template <typename... Args>
	static int callFunction(void (*f)(Args...), lua_State* L)
	{
		f(convert<Args, indices>(L)...);
		return 0;
	}


	template <typename R, typename... Args>
	static int callFunction(R(*f)(lua_State*, Args...), lua_State* L)
	{
		R v = f(L, convert<Args, indices>(L)...);
		push(L, v);
		return 1;
	}


	template <typename... Args>
	static int callFunction(void(*f)(lua_State*, Args...), lua_State* L)
	{
		f(L, convert<Args, indices>(L)...);
		return 0;
	}


	template <typename C, typename... Args>
	static int callMethod(C* inst, void(C::*f)(lua_State*, Args...), lua_State* L)
	{
		(inst->*f)(L, convert<Args, indices>(L)...);
		return 0;
	}


	template <typename R, typename C, typename... Args>
	static int callMethod(C* inst, R(C::*f)(lua_State*, Args...), lua_State* L)
	{
		R v = (inst->*f)(L, convert<Args, indices>(L)...);
		push(L, v);
		return 1;
	}


	template <typename R, typename C, typename... Args>
	static int callMethod(C* inst, R(C::*f)(lua_State*, Args...) const, lua_State* L)
	{
		R v = (inst->*f)(L, convert<Args, indices>(L)...);
		push(L, v);
		return 1;
	}


	template <typename C, typename... Args>
	static int callMethod(C* inst, void(C::*f)(Args...), lua_State* L)
	{
		(inst->*f)(convert<Args, indices>(L)...);
		return 0;
	}


	template <typename R, typename C, typename... Args>
	static int callMethod(C* inst, R(C::*f)(Args...), lua_State* L)
	{
		R v = (inst->*f)(convert<Args, indices>(L)...);
		push(L, v);
		return 1;
	}


	template <typename R, typename C, typename... Args>
	static int callMethod(C* inst, R(C::*f)(Args...) const, lua_State* L)
	{
		R v = (inst->*f)(convert<Args, indices>(L)...);
		push(L, v);
		return 1;
	}
};


template <typename R, typename... Args> constexpr int arity(R (*f)(Args...))
{
	return sizeof...(Args);
}


template <typename R, typename... Args> constexpr int arity(R (*f)(lua_State*, Args...))
{
	return sizeof...(Args);
}


template <typename R, typename C, typename... Args> constexpr int arity(R (C::*f)(Args...))
{
	return sizeof...(Args);
}


template <typename R, typename C, typename... Args> constexpr int arity(R(C::*f)(Args...) const)
{
	return sizeof...(Args);
}


template <typename R, typename C, typename... Args> constexpr int arity(R (C::*f)(lua_State*, Args...))
{
	return sizeof...(Args);
}


template <typename R, typename C, typename... Args> constexpr int arity(R (C::*f)(lua_State*, Args...) const)
{
	return sizeof...(Args);
}


} // namespace details


template <typename T, T t> int wrap(lua_State* L)
{
	using indices = typename BuildIndices<0, details::arity(t)>::result;
	return details::Caller<indices>::callFunction(t, L);
}


template <typename C, typename T, T t> int wrapMethod(lua_State* L)
{
	using indices = typename BuildIndices<1, details::arity(t)>::result;
	auto* inst = checkArg<C*>(L, 1);
	return details::Caller<indices>::callMethod(inst, t, L);
}


template <typename C, typename T, T t> int wrapMethodClosure(lua_State* L)
{
	using indices = typename BuildIndices<0, details::arity(t)>::result;
	int index = lua_upvalueindex(1);
	if (!isType<T>(L, index))
	{
		g_log_error.log("Lua") << "Invalid Lua closure";
		ASSERT(false);
		return 0;
	}
	auto* inst = checkArg<C*>(L, index);
	return details::Caller<indices>::callMethod(inst, t, L);
}


} // namespace LuaWrapper
} // namespace Lumix
