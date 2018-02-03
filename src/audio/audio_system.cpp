#include "audio_system.h"
#include "animation/animation_scene.h"
#include "audio_device.h"
#include "audio_scene.h"
#include "clip_manager.h"
#include "engine/engine.h"
#include "engine/iplugin.h"
#include "engine/reflection.h"
#include "engine/resource_manager.h"
#include "engine/universe/universe.h"


namespace Lumix
{


static void registerProperties(IAllocator& allocator)
{
	using namespace Reflection;
	static auto audio_scene = scene("audio",
		component("ambient_sound",
			property("3D", LUMIX_PROP_FULL(AudioScene, isAmbientSound3D, setAmbientSound3D)),
			dyn_enum_property("Sound", LUMIX_PROP(AudioScene, AmbientSoundClipIndex), &AudioScene::getClipCount, &AudioScene::getClipName)
		),
		component("audio_listener"),
		component("echo_zone",
			property("Radius", LUMIX_PROP(AudioScene, EchoZoneRadius),
				MinAttribute(0)),
			property("Delay (ms)", LUMIX_PROP(AudioScene, EchoZoneDelay),
				MinAttribute(0))),
		component("chorus_zone",
			property("Radius", LUMIX_PROP(AudioScene, ChorusZoneRadius),
				MinAttribute(0)),
			property("Delay (ms)", LUMIX_PROP(AudioScene, ChorusZoneDelay),
				MinAttribute(0))
		)
	);
	registerScene(audio_scene);
}


struct AudioSystemImpl LUMIX_FINAL : public AudioSystem
{
	explicit AudioSystemImpl(Engine& engine)
		: m_engine(engine)
		, m_manager(engine.getAllocator())
		, m_device(nullptr)
	{
		registerProperties(engine.getAllocator());
		AudioScene::registerLuaAPI(m_engine.getState());
		m_device = AudioDevice::create(m_engine);
		m_manager.create(Clip::TYPE, m_engine.getResourceManager());
	}


	~AudioSystemImpl()
	{
		AudioDevice::destroy(*m_device);
		m_manager.destroy();
	}


	Engine& getEngine() override { return m_engine; }
	AudioDevice& getDevice() override { return *m_device; }
	ClipManager& getClipManager() override { return m_manager; }


	const char* getName() const override { return "audio"; }


	void createScenes(Universe& ctx) override
	{
		auto* scene = AudioScene::createInstance(*this, ctx, m_engine.getAllocator());
		ctx.addScene(scene);
	}


	void destroyScene(IScene* scene) override { AudioScene::destroyInstance(static_cast<AudioScene*>(scene)); }


	ClipManager m_manager;
	Engine& m_engine;
	AudioDevice* m_device;
};


LUMIX_PLUGIN_ENTRY(audio)
{
	return LUMIX_NEW(engine.getAllocator(), AudioSystemImpl)(engine);
}


} // namespace Lumix

