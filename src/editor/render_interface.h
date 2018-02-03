#pragma once


#include "engine/lumix.h"
#include "engine/geometry.h"
#include "engine/path.h"
#include "engine/vec.h"
#include "editor/world_editor.h"
#include "imgui/imgui.h"


namespace Lumix
{


struct Pose;


class RenderInterface
{
public:
	typedef int ModelHandle;

	struct Vertex
	{
		Vec3 position;
		u32 color;
		float u, v;
	};

public:
	virtual ~RenderInterface() {}

	virtual AABB getEntityAABB(Universe& universe, Entity entity) = 0;
	virtual float getCameraFOV(Entity entity) = 0;
	virtual bool isCameraOrtho(Entity entity) = 0;
	virtual float getCameraOrthoSize(Entity entity) = 0;
	virtual Vec2 getCameraScreenSize(Entity entity) = 0;
	virtual Entity getCameraInSlot(const char* slot) = 0;
	virtual void setCameraSlot(Entity entity, const char* slot) = 0;
	virtual void getRay(Entity entity, const Vec2& screen_pos, Vec3& origin, Vec3& dir) = 0;
	virtual float castRay(ModelHandle model, const Vec3& origin, const Vec3& dir, const Matrix& mtx, const Pose* pose) = 0;
	virtual void renderModel(ModelHandle model, const Matrix& mtx) = 0;
	virtual ModelHandle loadModel(Path& path) = 0;
	virtual void unloadModel(ModelHandle handle) = 0;
	virtual Vec3 getModelCenter(Entity entity) = 0;
	virtual bool saveTexture(Engine& engine, const char* path, const void* pixels, int w, int h) = 0;
	virtual ImTextureID createTexture(const char* name, const void* pixels, int w, int h) = 0;
	virtual void destroyTexture(ImTextureID handle) = 0;
	virtual ImTextureID loadTexture(const Path& path) = 0;
	virtual void unloadTexture(ImTextureID handle) = 0;
	virtual void addDebugCube(const Vec3& minimum, const Vec3& maximum, u32 color, float life) = 0;
	virtual void addDebugCross(const Vec3& pos, float size, u32 color, float life) = 0;
	virtual void addDebugLine(const Vec3& from, const Vec3& to, u32 color, float life) = 0;
	virtual WorldEditor::RayHit castRay(const Vec3& origin, const Vec3& dir, Entity ignored) = 0;
	virtual Path getModelInstancePath(Entity entity) = 0;
	virtual void render(const Matrix& mtx,
		u16* indices,
		int indices_count,
		Vertex* vertices,
		int vertices_count,
		bool lines) = 0;
	virtual ImFont* addFont(const char* filename, int size) = 0;
	virtual Vec3 getClosestVertex(Universe* universe, Entity entity, const Vec3& pos) = 0;
	virtual void addText2D(float x, float y, float font_size, u32 color, const char* text) = 0;
	virtual void addRect2D(const Vec2& a, const Vec2& b, u32 color) = 0;
	virtual void addRectFilled2D(const Vec2& a, const Vec2& b, u32 color) = 0;
	virtual void getModelInstaces(Array<Entity>& entity, const Frustum& frustum, const Vec3& lod_ref_point, Entity camera) = 0;
	virtual Frustum getFrustum(Entity camera, const Vec2& a, const Vec2& b) = 0;
	virtual Vec2 worldToScreenPixels(const Vec3& world) = 0;
};


}