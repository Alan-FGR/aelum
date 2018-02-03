#pragma once


#include "engine/binary_array.h"
#include "engine/vec.h"
#include "editor/world_editor.h"
#include "editor/utils.h"


namespace Lumix
{


class Material;
class Model;
struct PrefabResource;
class RenderScene;
class Texture;


class TerrainEditor LUMIX_FINAL : public WorldEditor::Plugin
{
public:
	enum ActionType
	{
		RAISE_HEIGHT,
		LOWER_HEIGHT,
		SMOOTH_HEIGHT,
		FLAT_HEIGHT,
		LAYER,
		ENTITY,
		REMOVE_ENTITY,
		COLOR,
		ADD_GRASS,
		REMOVE_GRASS,
		NOT_SET
	};

	TerrainEditor(WorldEditor& editor, class StudioApp& app);
	~TerrainEditor();

	bool onMouseDown(const WorldEditor::RayHit& hit, int, int) override;
	void onMouseMove(int x, int y, int /*rel_x*/, int /*rel_y*/) override;
	void onMouseUp(int, int, MouseButton::Value) override;
	void onGUI();
	void setComponent(ComponentUID cmp) { m_component = cmp; }

private:
	void splitSplatmap(const char* dir);
	void mergeSplatmap(const char* dir);
	void onUniverseDestroyed();
	void detectModifiers();
	void drawCursor(RenderScene& scene, Entity terrain, const Vec3& center);
	Material* getMaterial();
	void paint(const Vec3& hit, TerrainEditor::ActionType action_type, bool new_stroke);

	void removeEntities(const Vec3& hit);
	void paintEntities(const Vec3& hit);
	void increaseBrushSize();
	void decreaseBrushSize();
	void nextTerrainTexture();
	void prevTerrainTexture();
	u16 getHeight(const Vec3& world_pos);
	Texture* getHeightmap();
	Vec3 getRelativePosition(const Vec3& world_pos) const;

private:
	WorldEditor& m_world_editor;
	StudioApp& m_app;
	ActionType m_action_type;
	ComponentUID m_component;
	float m_terrain_brush_strength;
	float m_terrain_brush_size;
	int m_texture_idx;
	u16 m_grass_mask;
	u16 m_flat_height;
	Vec3 m_color;
	int m_current_brush;
	Array<PrefabResource*> m_selected_prefabs;
	Action* m_increase_brush_size;
	Action* m_decrease_brush_size;
	Action* m_increase_texture_idx;
	Action* m_decrease_texture_idx;
	Action* m_lower_terrain_action;
	Action* m_smooth_terrain_action;
	Action* m_remove_entity_action;
	Action* m_remove_grass_action;
	BinaryArray m_brush_mask;
	Texture* m_brush_texture;
	Vec2 m_size_spread;
	Vec2 m_y_spread;
	bool m_is_align_with_normal;
	bool m_is_rotate_x;
	bool m_is_rotate_y;
	bool m_is_rotate_z;
	bool m_is_enabled;
	Vec2 m_rotate_x_spread;
	Vec2 m_rotate_y_spread;
	Vec2 m_rotate_z_spread;
};


} // namespace Lumix
