﻿#include "gui_system.h"
#include "engine/crc32.h"
#include "engine/delegate.h"
#include "engine/engine.h"
#include "engine/iallocator.h"
#include "engine/input_system.h"
#include "engine/lua_wrapper.h"
#include "engine/matrix.h"
#include "engine/path.h"
#include "engine/plugin_manager.h"
#include "engine/reflection.h"
#include "engine/resource_manager.h"
#include "engine/universe/universe.h"
#include "gui/gui_scene.h"
#include "gui/sprite_manager.h"
#include "renderer/font_manager.h"
#include "renderer/material.h"
#include "renderer/material_manager.h"
#include "renderer/pipeline.h"
#include "renderer/renderer.h"
#include "renderer/render_scene.h"
#include "renderer/shader.h"
#include "renderer/texture.h"
#include <bgfx/bgfx.h>
#include <imgui/imgui.h>


namespace Lumix
{


struct GUISystemImpl;


struct GUISystemImpl LUMIX_FINAL : public GUISystem
{
	static const char* getTextHAlignName(int index)
	{
		switch ((GUIScene::TextHAlign) index)
		{
		case GUIScene::TextHAlign::LEFT: return "left";
		case GUIScene::TextHAlign::RIGHT: return "right";
		case GUIScene::TextHAlign::CENTER: return "center";
		default: ASSERT(false); return "Unknown";
		}
	}


	explicit GUISystemImpl(Engine& engine)
		: m_engine(engine)
		, m_interface(nullptr)
		, m_sprite_manager(engine.getAllocator())
	{
		registerLuaAPI();

		using namespace Reflection;

		static auto textHAlignDesc = enumDesciptor<GUIScene::TextHAlign>(
			LUMIX_ENUM_VALUE(GUIScene::TextHAlign::LEFT),
			LUMIX_ENUM_VALUE(GUIScene::TextHAlign::RIGHT),
			LUMIX_ENUM_VALUE(GUIScene::TextHAlign::CENTER)
		);
		registerEnum(textHAlignDesc);

		static auto lua_scene = scene("gui",
			component("gui_text",
				property("Text", LUMIX_PROP(GUIScene, Text)),
				property("Font", LUMIX_PROP(GUIScene, TextFontPath),
					ResourceAttribute("Font (*.ttf)", FontResource::TYPE)),
				property("Font Size", LUMIX_PROP(GUIScene, TextFontSize)),
				enum_property("Horizontal align", LUMIX_PROP(GUIScene, TextHAlign), textHAlignDesc),
				property("Color", LUMIX_PROP(GUIScene, TextColorRGBA),
					ColorAttribute())
			),
			component("gui_input_field"),
			component("gui_button",
				property("Normal color", LUMIX_PROP(GUIScene, ButtonNormalColorRGBA),
					ColorAttribute()),
				property("Hovered color", LUMIX_PROP(GUIScene, ButtonHoveredColorRGBA),
					ColorAttribute())
			),
			component("gui_image",
				property("Enabled", LUMIX_PROP_FULL(GUIScene, isImageEnabled, enableImage)),
				property("Color", LUMIX_PROP(GUIScene, ImageColorRGBA),
					ColorAttribute()),
				property("Sprite", LUMIX_PROP(GUIScene, ImageSprite),
					ResourceAttribute("Sprite (*.spr)", Sprite::TYPE))
			),
			component("gui_rect",
				property("Enabled", LUMIX_PROP_FULL(GUIScene, isRectEnabled, enableRect)),
				property("Clip content", LUMIX_PROP(GUIScene, RectClip)),
				property("Top Points", LUMIX_PROP(GUIScene, RectTopPoints)),
				property("Top Relative", LUMIX_PROP(GUIScene, RectTopRelative)),
				property("Right Points", LUMIX_PROP(GUIScene, RectRightPoints)),
				property("Right Relative", LUMIX_PROP(GUIScene, RectRightRelative)),
				property("Bottom Points", LUMIX_PROP(GUIScene, RectBottomPoints)),
				property("Bottom Relative", LUMIX_PROP(GUIScene, RectBottomRelative)),
				property("Left Points", LUMIX_PROP(GUIScene, RectLeftPoints)),
				property("Left Relative", LUMIX_PROP(GUIScene, RectLeftRelative))
			)
		);
		registerScene(lua_scene);
		m_sprite_manager.create(Sprite::TYPE, m_engine.getResourceManager());
	}


	~GUISystemImpl()
	{
		m_sprite_manager.destroy();
	}


	Engine& getEngine() override { return m_engine; }


	void createScenes(Universe& universe) override
	{
		IAllocator& allocator = m_engine.getAllocator();
		auto* scene = GUIScene::createInstance(*this, universe, allocator);
		universe.addScene(scene);
	}


	void destroyScene(IScene* scene) override
	{
		GUIScene::destroyInstance(static_cast<GUIScene*>(scene));
	}


	static int LUA_GUIRect_getScreenRect(lua_State* L)
	{
		GUIScene* scene = LuaWrapper::checkArg<GUIScene*>(L, 1);
		Entity e = LuaWrapper::checkArg<Entity>(L, 2);
		GUIScene::Rect rect = scene->getRect(e);
		lua_newtable(L);
		LuaWrapper::push(L, rect.x);
		lua_setfield(L, -2, "x");
		LuaWrapper::push(L, rect.y);
		lua_setfield(L, -2, "y");
		LuaWrapper::push(L, rect.w);
		lua_setfield(L, -2, "w");
		LuaWrapper::push(L, rect.h);
		lua_setfield(L, -2, "h");
		return 1;
	}


	void registerLuaAPI()
	{
		lua_State* L = m_engine.getState();
		
		#define REGISTER_FUNCTION(name) \
			do {\
				auto f = &LuaWrapper::wrapMethod<GUISystemImpl, decltype(&GUISystemImpl::name), \
					&GUISystemImpl::name>; \
				LuaWrapper::createSystemFunction(L, "Gui", #name, f); \
			} while(false) \

		REGISTER_FUNCTION(enableCursor);

		LuaWrapper::createSystemFunction(L, "Gui", "getScreenRect", LUA_GUIRect_getScreenRect);

		LuaWrapper::createSystemVariable(L, "Gui", "instance", this);

		#undef REGISTER_FUNCTION
	}


	void enableCursor(bool enable)
	{
		if (m_interface) m_interface->enableCursor(enable);
	}


	void setInterface(Interface* interface) override
	{
		m_interface = interface;
		
		if (!m_interface) return;

		auto* pipeline = m_interface->getPipeline();
		pipeline->addCustomCommandHandler("renderIngameGUI")
			.callback.bind<GUISystemImpl, &GUISystemImpl::pipelineCallback>(this);
	}


	void pipelineCallback()
	{
		if (!m_interface) return;

		Pipeline* pipeline = m_interface->getPipeline();
		Draw2D& draw2d = pipeline->getDraw2D();
		auto* scene = (GUIScene*)pipeline->getScene()->getUniverse().getScene(crc32("gui"));
		Vec2 size = { (float)pipeline->getWidth(), (float)pipeline->getHeight() };
		scene->render(*pipeline, size);
	}


	void renderNewUI()
	{
		Pipeline* pipeline = m_interface->getPipeline();
		Draw2D& draw2d = pipeline->getDraw2D();
		auto* scene = (GUIScene*)pipeline->getScene()->getUniverse().getScene(crc32("gui"));
		Vec2 size = {(float)pipeline->getWidth(), (float)pipeline->getHeight()};
		scene->render(*pipeline, size);
	}


	void stopGame() override {}


	const char* getName() const override { return "gui"; }


	Engine& m_engine;
	SpriteManager m_sprite_manager;
	Interface* m_interface;
};


LUMIX_PLUGIN_ENTRY(gui)
{
	return LUMIX_NEW(engine.getAllocator(), GUISystemImpl)(engine);
}


} // namespace Lumix
