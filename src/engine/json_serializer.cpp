#include "json_serializer.h"
#include "engine/fs/file_system.h"
#include "engine/log.h"
#include "engine/math_utils.h"
#include "engine/path.h"
#include <cstdlib>


namespace Lumix
{


class ErrorProxy
{
public:
	explicit ErrorProxy(JsonDeserializer& serializer)
		: m_log(g_log_error, "serializer", serializer.m_allocator)
	{
		serializer.m_is_error = true;
		const char* c = serializer.m_data;
		int line = 0;
		int column = 0;
		while (c < serializer.m_token)
		{
			if (*c == '\n')
			{
				++line;
				column = 0;
			}
			++column;
			++c;
		}

		m_log << serializer.m_path << "(line " << (line + 1) << ", column " << column << "): ";
	}
	LogProxy& log() { return m_log; }

private:
	LogProxy m_log;
};


JsonSerializer::JsonSerializer(FS::IFile& file, const Path& path)
	: m_file(file)
{
	m_is_first_in_block = true;
}


JsonDeserializer::JsonDeserializer(FS::IFile& file,
	const Path& path,
	IAllocator& allocator)
	: m_file(file)
	, m_allocator(allocator)
{
	m_is_error = false;
	copyString(m_path, path.c_str());
	m_is_first_in_block = true;
	m_data = nullptr;
	m_is_string_token = false;
	m_data_size = (int)file.size();
	if (file.getBuffer() != nullptr)
	{
		m_data = (const char*)file.getBuffer();
		m_own_data = false;
	}
	else
	{
		int size = (int)m_file.size();
		char* data = (char*)m_allocator.allocate(size);
		m_own_data = true;
		file.read(data, m_data_size);
		m_data = data;
	}
	m_token = m_data;
	m_token_size = 0;
	deserializeToken();
}


JsonDeserializer::~JsonDeserializer()
{
	if (m_own_data) m_allocator.deallocate((void*)m_data);
}


#pragma region serialization


void JsonSerializer::serialize(const char* label, Entity value)
{
	serialize(label, value.index);
}


void JsonSerializer::serialize(const char* label, unsigned int value)
{
	writeBlockComma();
	char tmp[20];
	writeString(label);
	toCString(value, tmp, 20);
	m_file.write(" : ", stringLength(" : "));
	m_file.write(tmp, stringLength(tmp));
	m_is_first_in_block = false;
}


void JsonSerializer::serialize(const char* label, u16 value)
{
	writeBlockComma();
	char tmp[20];
	writeString(label);
	toCString(value, tmp, 20);
	m_file.write(" : ", stringLength(" : "));
	m_file.write(tmp, stringLength(tmp));
	m_is_first_in_block = false;
}


void JsonSerializer::serialize(const char* label, float value)
{
	writeBlockComma();
	char tmp[20];
	writeString(label);
	toCString(value, tmp, 20, 8);
	m_file.write(" : ", stringLength(" : "));
	m_file.write(tmp, stringLength(tmp));
	m_is_first_in_block = false;
}


void JsonSerializer::serialize(const char* label, int value)
{
	writeBlockComma();
	char tmp[20];
	writeString(label);
	toCString(value, tmp, 20);
	m_file.write(" : ", stringLength(" : "));
	m_file.write(tmp, stringLength(tmp));
	m_is_first_in_block = false;
}


void JsonSerializer::serialize(const char* label, const Path& value)
{
	writeBlockComma();
	writeString(label);
	m_file.write(" : \"", 4);
	m_file.write(value.c_str(), value.length());
	m_file.write("\"", 1);
	m_is_first_in_block = false;
}


void JsonSerializer::serialize(const char* label, const char* value)
{
	writeBlockComma();
	writeString(label);
	m_file.write(" : \"", 4);
	if (value == nullptr)
	{
		m_file.write("", 1);
	}
	else
	{
		m_file.write(value, stringLength(value));
	}
	m_file.write("\"", 1);
	m_is_first_in_block = false;
}


void JsonSerializer::serialize(const char* label, bool value)
{
	writeBlockComma();
	writeString(label);
	m_file.write(value ? " : true" : " : false", value ? 7 : 8);
	m_is_first_in_block = false;
}


void JsonSerializer::beginObject()
{
	writeBlockComma();
	m_file.write("{", 1);
	m_is_first_in_block = true;
}


void JsonSerializer::beginObject(const char* label)
{
	writeBlockComma();
	writeString(label);
	m_file.write(" : {", 4);
	m_is_first_in_block = true;
}

void JsonSerializer::endObject()
{
	m_file.write("}", 1);
	m_is_first_in_block = false;
}


void JsonSerializer::beginArray(const char* label)
{
	writeBlockComma();
	writeString(label);
	m_file.write(" : [", 4);
	m_is_first_in_block = true;
}


void JsonSerializer::beginArray()
{
	writeBlockComma();
	m_file.write("[", 1);
	m_is_first_in_block = true;
}


void JsonSerializer::endArray()
{
	m_file.write("]", 1);
	m_is_first_in_block = false;
}

void JsonSerializer::serializeArrayItem(const char* value)
{
	writeBlockComma();
	writeString(value);
	m_is_first_in_block = false;
}


/*void JsonSerializer::serializeArrayItem(string& value)
{
	writeBlockComma();
	writeString(value.c_str() ? value.c_str() : "");
	m_is_first_in_block = false;
}*/


void JsonSerializer::serializeArrayItem(unsigned int value)
{
	writeBlockComma();
	char tmp[20];
	toCString(value, tmp, 20);
	m_file.write(tmp, stringLength(tmp));
	m_is_first_in_block = false;
}


void JsonSerializer::serializeArrayItem(Entity value)
{
	serializeArrayItem(value.index);
}


void JsonSerializer::serializeArrayItem(int value)
{
	writeBlockComma();
	char tmp[20];
	toCString(value, tmp, 20);
	m_file.write(tmp, stringLength(tmp));
	m_is_first_in_block = false;
}


void JsonSerializer::serializeArrayItem(i64 value)
{
	writeBlockComma();
	char tmp[30];
	toCString(value, tmp, 30);
	m_file.write(tmp, stringLength(tmp));
	m_is_first_in_block = false;
}


void JsonSerializer::serializeArrayItem(float value)
{
	writeBlockComma();
	char tmp[20];
	toCString(value, tmp, 20, 8);
	m_file.write(tmp, stringLength(tmp));
	m_is_first_in_block = false;
}


void JsonSerializer::serializeArrayItem(bool value)
{
	writeBlockComma();
	m_file.write(value ? "true" : "false", value ? 4 : 5);
	m_is_first_in_block = false;
}

#pragma endregion


#pragma region deserialization


bool JsonDeserializer::isNextBoolean() const
{
	if (m_is_string_token) return false;
	if (m_token_size == 4 && compareStringN(m_token, "true", 4) == 0) return true;
	if (m_token_size == 5 && compareStringN(m_token, "false", 5) == 0) return true;
	return false;
}


void JsonDeserializer::deserialize(const char* label, Entity& value, Entity default_value)
{
	deserialize(label, value.index, default_value.index);
}


void JsonDeserializer::deserialize(bool& value, bool default_value)
{
	value = !m_is_string_token ? m_token_size == 4 && (compareStringN(m_token, "true", 4) == 0)
							   : default_value;
	deserializeToken();
}


void JsonDeserializer::deserialize(float& value, float default_value)
{
	if (!m_is_string_token)
	{
		value = tokenToFloat();
	}
	else
	{
		value = default_value;
	}
	deserializeToken();
}


void JsonDeserializer::deserialize(i32& value, i32 default_value)
{
	if (m_is_string_token || !fromCString(m_token, m_token_size, &value))
	{
		value = default_value;
	}
	deserializeToken();
}


void JsonDeserializer::deserialize(const char* label, Path& value, const Path& default_value)
{
	deserializeLabel(label);
	if (!m_is_string_token)
	{
		value = default_value;
	}
	else
	{
		char tmp[MAX_PATH_LENGTH];
		int size = Math::minimum(lengthOf(tmp) - 1, m_token_size);
		copyMemory(tmp, m_token, size);
		tmp[size] = '\0';
		value = tmp;
		deserializeToken();
	}
}


void JsonDeserializer::deserialize(Path& value, const Path& default_value)
{
	if (!m_is_string_token)
	{
		value = default_value;
	}
	else
	{
		char tmp[MAX_PATH_LENGTH];
		int size = Math::minimum(lengthOf(tmp) - 1, m_token_size);
		copyMemory(tmp, m_token, size);
		tmp[size] = '\0';
		value = tmp;
		deserializeToken();
	}
}


void JsonDeserializer::deserialize(char* value, int max_length, const char* default_value)
{
	if (!m_is_string_token)
	{
		copyString(value, max_length, default_value);
	}
	else
	{
		int size = Math::minimum(max_length - 1, m_token_size);
		copyMemory(value, m_token, size);
		value[size] = '\0';
		deserializeToken();
	}
}


void JsonDeserializer::deserialize(const char* label, float& value, float default_value)
{
	deserializeLabel(label);
	if (!m_is_string_token)
	{
		value = tokenToFloat();
		deserializeToken();
	}
	else
	{
		value = default_value;
	}
}


void JsonDeserializer::deserialize(const char* label, u32& value, u32 default_value)
{
	deserializeLabel(label);
	if (m_is_string_token || !fromCString(m_token, m_token_size, &value))
	{
		value = default_value;
	}
	else
	{
		deserializeToken();
	}
}


void JsonDeserializer::deserialize(const char* label, u16& value, u16 default_value)
{
	deserializeLabel(label);
	if (m_is_string_token || !fromCString(m_token, m_token_size, &value))
	{
		value = default_value;
	}
	else
	{
		deserializeToken();
	}
}


bool JsonDeserializer::isObjectEnd()
{
	if (m_token == m_data + m_data_size)
	{
		ErrorProxy(*this).log() << "Unexpected end of file while looking for the end of an object.";
		return true;
	}

	return (!m_is_string_token && m_token_size == 1 && m_token[0] == '}');
}


void JsonDeserializer::deserialize(const char* label, i32& value, i32 default_value)
{
	deserializeLabel(label);
	if (m_is_string_token || !fromCString(m_token, m_token_size, &value))
	{
		value = default_value;
	}
	deserializeToken();
}


void JsonDeserializer::deserialize(const char* label,
	char* value,
	int max_length,
	const char* default_value)
{
	deserializeLabel(label);
	if (!m_is_string_token)
	{
		copyString(value, max_length, default_value);
	}
	else
	{
		int size = Math::minimum(max_length - 1, m_token_size);
		copyMemory(value, m_token, size);
		value[size] = '\0';
		deserializeToken();
	}
}


void JsonDeserializer::deserializeArrayBegin(const char* label)
{
	deserializeLabel(label);
	expectToken('[');
	m_is_first_in_block = true;
	deserializeToken();
}


void JsonDeserializer::expectToken(char expected_token)
{
	if (m_is_string_token || m_token_size != 1 || m_token[0] != expected_token)
	{
		char tmp[2];
		tmp[0] = expected_token;
		tmp[1] = 0;
		ErrorProxy(*this).log() << "Unexpected token \""
								<< string(m_token, m_token_size, m_allocator) << "\", expected "
								<< tmp << ".";
		deserializeToken();
	}
}


void JsonDeserializer::deserializeArrayBegin()
{
	expectToken('[');
	m_is_first_in_block = true;
	deserializeToken();
}


void JsonDeserializer::deserializeRawString(char* buffer, int max_length)
{
	int size = Math::minimum(max_length - 1, m_token_size);
	copyMemory(buffer, m_token, size);
	buffer[size] = '\0';
	deserializeToken();
}


void JsonDeserializer::nextArrayItem()
{
	if (!m_is_first_in_block)
	{
		expectToken(',');
		deserializeToken();
	}
}


bool JsonDeserializer::isArrayEnd()
{
	if (m_token == m_data + m_data_size)
	{
		ErrorProxy(*this).log() << "Unexpected end of file while looking for the end of an array.";
		return true;
	}

	return (!m_is_string_token && m_token_size == 1 && m_token[0] == ']');
}


void JsonDeserializer::deserializeArrayEnd()
{
	expectToken(']');
	m_is_first_in_block = false;
	deserializeToken();
}


void JsonDeserializer::deserializeArrayItem(char* value, int max_length, const char* default_value)
{
	deserializeArrayComma();
	if (m_is_string_token)
	{
		int size = Math::minimum(max_length - 1, m_token_size);
		copyMemory(value, m_token, size);
		value[size] = '\0';
		deserializeToken();
	}
	else
	{
		ErrorProxy(*this).log() << "Unexpected token \""
								<< string(m_token, m_token_size, m_allocator)
								<< "\", expected string.";
		deserializeToken();
		copyString(value, max_length, default_value);
	}
}


void JsonDeserializer::deserializeArrayItem(Entity& value, Entity default_value)
{
	deserializeArrayItem(value.index, default_value.index);
}


void JsonDeserializer::deserializeArrayItem(u32& value, u32 default_value)
{
	deserializeArrayComma();
	if (m_is_string_token || !fromCString(m_token, m_token_size, &value))
	{
		value = default_value;
	}
	deserializeToken();
}


void JsonDeserializer::deserializeArrayItem(i32& value, i32 default_value)
{
	deserializeArrayComma();
	if (m_is_string_token || !fromCString(m_token, m_token_size, &value))
	{
		value = default_value;
	}
	deserializeToken();
}


void JsonDeserializer::deserializeArrayItem(i64& value, i64 default_value)
{
	deserializeArrayComma();
	if (m_is_string_token || !fromCString(m_token, m_token_size, &value))
	{
		value = default_value;
	}
	deserializeToken();
}


void JsonDeserializer::deserializeArrayItem(float& value, float default_value)
{
	deserializeArrayComma();
	if (m_is_string_token)
	{
		value = default_value;
	}
	else
	{
		value = tokenToFloat();
	}
	deserializeToken();
}


void JsonDeserializer::deserializeArrayItem(bool& value, bool default_value)
{
	deserializeArrayComma();
	if (m_is_string_token)
	{
		value = default_value;
	}
	else
	{
		value = m_token_size == 4 && compareStringN("true", m_token, m_token_size) == 0;
	}
	deserializeToken();
}


void JsonDeserializer::deserialize(const char* label, bool& value, bool default_value)
{
	deserializeLabel(label);
	if (!m_is_string_token)
	{
		value = m_token_size == 4 && compareStringN("true", m_token, 4) == 0;
	}
	else
	{
		value = default_value;
	}
	deserializeToken();
}


static bool isDelimiter(char c)
{
	return c == '\t' || c == '\n' || c == ' ' || c == '\r';
}


void JsonDeserializer::deserializeArrayComma()
{
	if (m_is_first_in_block)
	{
		m_is_first_in_block = false;
	}
	else
	{

		expectToken(',');
		deserializeToken();
	}
}


static bool isSingleCharToken(char c)
{
	return c == ',' || c == '[' || c == ']' || c == '{' || c == '}' || c == ':';
}


void JsonDeserializer::deserializeToken()
{
	m_token += m_token_size;
	if (m_is_string_token)
	{
		++m_token;
	}

	while (m_token < m_data + m_data_size && isDelimiter(*m_token))
	{
		++m_token;
	}
	if (*m_token == '/' && m_token < m_data + m_data_size - 1 && m_token[1] == '/')
	{
		m_token_size = int((m_data + m_data_size) - m_token);
		m_is_string_token = false;
	}
	else if (*m_token == '"')
	{
		++m_token;
		m_is_string_token = true;
		const char* token_end = m_token;
		while (token_end < m_data + m_data_size && *token_end != '"')
		{
			++token_end;
		}
		if (token_end == m_data + m_data_size)
		{
			ErrorProxy(*this).log() << "Unexpected end of file while looking for \".";
			m_token_size = 0;
		}
		else
		{
			m_token_size = int(token_end - m_token);
		}
	}
	else if (isSingleCharToken(*m_token))
	{
		m_is_string_token = false;
		m_token_size = 1;
	}
	else
	{
		m_is_string_token = false;
		const char* token_end = m_token;
		while (token_end < m_data + m_data_size && !isDelimiter(*token_end) &&
			   !isSingleCharToken(*token_end))
		{
			++token_end;
		}
		m_token_size = int(token_end - m_token);
	}
}


void JsonDeserializer::deserializeObjectBegin()
{
	m_is_first_in_block = true;
	expectToken('{');
	deserializeToken();
}

void JsonDeserializer::deserializeObjectEnd()
{
	expectToken('}');
	m_is_first_in_block = false;
	deserializeToken();
}


void JsonDeserializer::deserializeLabel(char* label, int max_length)
{
	if (!m_is_first_in_block)
	{
		expectToken(',');
		deserializeToken();
	}
	else
	{
		m_is_first_in_block = false;
	}
	if (!m_is_string_token)
	{
		ErrorProxy(*this).log() << "Unexpected token \""
								<< string(m_token, m_token_size, m_allocator)
								<< "\", expected string.";
		deserializeToken();
	}
	copyNString(label, max_length, m_token, m_token_size);
	deserializeToken();
	expectToken(':');
	deserializeToken();
}


void JsonSerializer::writeString(const char* str)
{
	m_file.write("\"", 1);
	if (str)
	{
		m_file.write(str, stringLength(str));
	}
	m_file.write("\"", 1);
}


void JsonSerializer::writeBlockComma()
{
	if (!m_is_first_in_block)
	{
		m_file.write(",\n", 2);
	}
}


void JsonDeserializer::deserializeLabel(const char* label)
{
	if (!m_is_first_in_block)
	{
		expectToken(',');
		deserializeToken();
	}
	else
	{
		m_is_first_in_block = false;
	}
	if (!m_is_string_token)
	{
		ErrorProxy(*this).log() << "Unexpected token \""
								<< string(m_token, m_token_size, m_allocator)
								<< "\", expected string.";
		deserializeToken();
	}
	if (compareStringN(label, m_token, m_token_size) != 0)
	{
		ErrorProxy(*this).log() << "Unexpected label \""
								<< string(m_token, m_token_size, m_allocator) << "\", expected \""
								<< label << "\".";
		deserializeToken();
	}
	deserializeToken();
	if (m_is_string_token || m_token_size != 1 || m_token[0] != ':')
	{
		ErrorProxy(*this).log() << "Unexpected label \""
								<< string(m_token, m_token_size, m_allocator) << "\", expected \""
								<< label << "\".";
		deserializeToken();
	}
	deserializeToken();
}


#pragma endregion


float JsonDeserializer::tokenToFloat()
{
	char tmp[64];
	int size = Math::minimum((int)sizeof(tmp) - 1, m_token_size);
	copyMemory(tmp, m_token, size);
	tmp[size] = '\0';
	return (float)atof(tmp);
}


} // namespace Lumix
