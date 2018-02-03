#pragma once


#include "engine/lumix.h"
#include "engine/vec.h"
#include <bgfx/bgfx.h>


struct lua_State;


namespace Lumix
{


class JsonSerializer;


class FrameBuffer
{
	public:
		struct RenderBuffer
		{
			static const u32 DEFAULT_FLAGS = BGFX_TEXTURE_RT
				| BGFX_TEXTURE_U_CLAMP
				| BGFX_TEXTURE_V_CLAMP
				| BGFX_TEXTURE_MIP_POINT
				| BGFX_TEXTURE_MIN_POINT
				| BGFX_TEXTURE_MAG_POINT;

			bgfx::TextureFormat::Enum m_format;
			bgfx::TextureHandle m_handle;
			RenderBuffer* m_shared = nullptr;

			void parse(lua_State* state);
		};

		struct Declaration
		{
			i32 m_width;
			i32 m_height;
			Vec2 m_size_ratio = Vec2(-1, -1);
			RenderBuffer m_renderbuffers[16];
			i32 m_renderbuffers_count = 0;
			char m_name[64];
		};

	public:
		explicit FrameBuffer(const Declaration& decl);
		FrameBuffer(const char* name, int width, int height, void* window_handle);
		~FrameBuffer();

		bgfx::FrameBufferHandle getHandle() const { return m_handle; }
		int getWidth() const { return m_declaration.m_width; }
		int getHeight() const { return m_declaration.m_height; }
		void resize(int width, int height);
		Vec2 getSizeRatio() const { return m_declaration.m_size_ratio; }
		const char* getName() const { return m_declaration.m_name; }
		int getRenderbuffersCounts() const { return m_declaration.m_renderbuffers_count;  }


		RenderBuffer& getRenderbuffer(int idx)
		{
			ASSERT(idx < m_declaration.m_renderbuffers_count);
			return m_declaration.m_renderbuffers[idx];
		}
		
		
		bgfx::TextureHandle& getRenderbufferHandle(int idx) 
		{
			static bgfx::TextureHandle invalid = BGFX_INVALID_HANDLE;
			if (idx >= m_declaration.m_renderbuffers_count ) return invalid;
			return m_declaration.m_renderbuffers[idx].m_handle;
		}

	private:
		void destroyRenderbuffers();

	private:
		bool m_autodestroy_handle;
		void* m_window_handle;
		bgfx::FrameBufferHandle m_handle;
		Declaration m_declaration;
};


} // namespace Lumix

