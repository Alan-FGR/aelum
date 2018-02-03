#include "physics/physics_scene.h"
#include "engine/blob.h"
#include "engine/crc32.h"
#include "engine/engine.h"
#include "engine/job_system.h"
#include "engine/log.h"
#include "engine/lua_wrapper.h"
#include "engine/matrix.h"
#include "engine/path.h"
#include "engine/profiler.h"
#include "engine/reflection.h"
#include "engine/resource_manager.h"
#include "engine/resource_manager_base.h"
#include "engine/serializer.h"
#include "engine/universe/universe.h"
#include "lua_script/lua_script_system.h"
#include "physics/physics_geometry_manager.h"
#include "physics/physics_system.h"
#include "renderer/model.h"
#include "renderer/pose.h"
#include "renderer/render_scene.h"
#include "renderer/texture.h"
#include <PxPhysicsAPI.h>


using namespace physx;


namespace Lumix
{


static const ComponentType LUA_SCRIPT_TYPE = Reflection::getComponentType("lua_script");
static const ComponentType MODEL_INSTANCE_TYPE = Reflection::getComponentType("renderable");
static const ComponentType RIGID_ACTOR_TYPE = Reflection::getComponentType("rigid_actor");
static const ComponentType BOX_ACTOR_TYPE = Reflection::getComponentType("box_rigid_actor");
static const ComponentType RAGDOLL_TYPE = Reflection::getComponentType("ragdoll");
static const ComponentType SPHERE_ACTOR_TYPE = Reflection::getComponentType("sphere_rigid_actor");
static const ComponentType CAPSULE_ACTOR_TYPE = Reflection::getComponentType("capsule_rigid_actor");
static const ComponentType MESH_ACTOR_TYPE = Reflection::getComponentType("mesh_rigid_actor");
static const ComponentType CONTROLLER_TYPE = Reflection::getComponentType("physical_controller");
static const ComponentType HEIGHTFIELD_TYPE = Reflection::getComponentType("physical_heightfield");
static const ComponentType DISTANCE_JOINT_TYPE = Reflection::getComponentType("distance_joint");
static const ComponentType HINGE_JOINT_TYPE = Reflection::getComponentType("hinge_joint");
static const ComponentType SPHERICAL_JOINT_TYPE = Reflection::getComponentType("spherical_joint");
static const ComponentType D6_JOINT_TYPE = Reflection::getComponentType("d6_joint");
static const u32 RENDERER_HASH = crc32("renderer");


enum class PhysicsSceneVersion
{
	DYNAMIC_TYPE,
	TRIGGERS,
	RIGID_ACTOR,
	CONTROLLER_SHAPE,

	LATEST,
};


struct RagdollBone
{
	enum Type : int
	{
		BOX,
		CAPSULE
	};

	int pose_bone_idx;
	PxRigidDynamic* actor;
	PxJoint* parent_joint;
	RagdollBone* child;
	RagdollBone* next;
	RagdollBone* prev;
	RagdollBone* parent;
	RigidTransform bind_transform;
	RigidTransform inv_bind_transform;
	bool is_kinematic;
};


struct Ragdoll
{
	Entity entity;
	RagdollBone* root = nullptr;
	RigidTransform root_transform;
	int layer;
};


struct OutputStream LUMIX_FINAL : public PxOutputStream
{
	explicit OutputStream(IAllocator& allocator)
		: allocator(allocator)
	{
		data = (u8*)allocator.allocate(sizeof(u8) * 4096);
		capacity = 4096;
		size = 0;
	}

	~OutputStream() { allocator.deallocate(data); }


	PxU32 write(const void* src, PxU32 count) override
	{
		if (size + (int)count > capacity)
		{
			int new_capacity =
				Math::maximum(size + (int)count, capacity + 4096);
			u8* new_data =
				(u8*)allocator.allocate(sizeof(u8) * new_capacity);
			copyMemory(new_data, data, size);
			allocator.deallocate(data);
			data = new_data;
			capacity = new_capacity;
		}
		copyMemory(data + size, src, count);
		size += count;
		return count;
	}

	u8* data;
	IAllocator& allocator;
	int capacity;
	int size;
};


struct InputStream LUMIX_FINAL : public PxInputStream
{
	InputStream(unsigned char* data, int size)
	{
		this->data = data;
		this->size = size;
		pos = 0;
	}

	PxU32 read(void* dest, PxU32 count) override
	{
		if (pos + (int)count <= size)
		{
			copyMemory(dest, data + pos, count);
			pos += count;
			return count;
		}
		else
		{
			copyMemory(dest, data + pos, size - pos);
			int real_count = size - pos;
			pos = size;
			return real_count;
		}
	}


	int pos;
	int size;
	unsigned char* data;
};


static Vec3 fromPhysx(const PxVec3& v) { return Vec3(v.x, v.y, v.z); }
static PxVec3 toPhysx(const Vec3& v) { return PxVec3(v.x, v.y, v.z); }
static Quat fromPhysx(const PxQuat& v) { return Quat(v.x, v.y, v.z, v.w); }
static PxQuat toPhysx(const Quat& v) { return PxQuat(v.x, v.y, v.z, v.w); }
static RigidTransform fromPhysx(const PxTransform& v) { return{ fromPhysx(v.p), fromPhysx(v.q) }; }
static PxTransform toPhysx(const RigidTransform& v) { return {toPhysx(v.pos), toPhysx(v.rot)}; }


struct Joint
{
	Entity connected_body;
	PxJoint* physx;
	PxTransform local_frame0;
};


struct Heightfield
{
	Heightfield();
	~Heightfield();
	void heightmapLoaded(Resource::State, Resource::State new_state, Resource&);

	struct PhysicsSceneImpl* m_scene;
	Entity m_entity;
	PxRigidActor* m_actor;
	Texture* m_heightmap;
	float m_xz_scale;
	float m_y_scale;
	int m_layer;
};


struct PhysicsSceneImpl LUMIX_FINAL : public PhysicsScene
{
	struct CPUDispatcher : physx::PxCpuDispatcher
	{
		void submitTask(PxBaseTask& task) override 
		{
			JobSystem::JobDecl job;
			job.data = &task;
			job.task = [](void* data) {
				PxBaseTask* task = (PxBaseTask*)data;
				PROFILE_FUNCTION();
				task->run();
				task->release();
			};
			JobSystem::runJobs(&job, 1, nullptr);
		}
		PxU32 getWorkerCount() const override 
		{
			return MT::getCPUsCount();
		}
	};


	struct PhysxContactCallback LUMIX_FINAL : public PxSimulationEventCallback
	{
		explicit PhysxContactCallback(PhysicsSceneImpl& scene)
			: m_scene(scene)
		{
		}


		void onContact(const PxContactPairHeader& pairHeader,
			const PxContactPair* pairs,
			PxU32 nbPairs) override
		{
			for (PxU32 i = 0; i < nbPairs; i++)
			{
				const auto& cp = pairs[i];

				if (!(cp.events & PxPairFlag::eNOTIFY_TOUCH_FOUND)) continue;

				PxContactPairPoint contact;
				cp.extractContacts(&contact, 1);

				ContactData contact_data;
				contact_data.position = fromPhysx(contact.position);
				contact_data.e1 = {(int)(intptr_t)(pairHeader.actors[0]->userData)};
				contact_data.e2 = {(int)(intptr_t)(pairHeader.actors[1]->userData)};
				
				m_scene.onContact(contact_data);
			}
		}


		void onTrigger(PxTriggerPair* pairs, PxU32 count) override
		{
			for (PxU32 i = 0; i < count; i++)
			{
				const auto REMOVED_FLAGS = PxTriggerPairFlag::eREMOVED_SHAPE_TRIGGER | PxTriggerPairFlag::eREMOVED_SHAPE_OTHER;
				if (pairs[i].flags & REMOVED_FLAGS) continue;

				Entity e1 = {(int)(intptr_t)(pairs[i].triggerActor->userData)};
				Entity e2 = {(int)(intptr_t)(pairs[i].otherActor->userData)};

				m_scene.onTrigger(e1, e2, pairs[i].status == PxPairFlag::eNOTIFY_TOUCH_LOST);
			}
		}


		void onConstraintBreak(PxConstraintInfo*, PxU32) override {}
		void onWake(PxActor**, PxU32) override {}
		void onSleep(PxActor**, PxU32) override {}


		PhysicsSceneImpl& m_scene;
	};


	class RigidActor
	{
	public:
		RigidActor(PhysicsSceneImpl& _scene, ActorType _type)
			: resource(nullptr)
			, physx_actor(nullptr)
			, scene(_scene)
			, layer(0)
			, entity(INVALID_ENTITY)
			, type(_type)
			, dynamic_type(DynamicType::STATIC)
			, is_trigger(false)
			, scale(1)
		{
		}

		~RigidActor()
		{
			setResource(nullptr);
			if (physx_actor) physx_actor->release();
		}

		void rescale();
		void setResource(PhysicsGeometry* resource);
		void setPhysxActor(PxRigidActor* actor);

		Entity entity;
		float scale;
		int layer;
		PxRigidActor* physx_actor;
		PhysicsGeometry* resource;
		PhysicsSceneImpl& scene;
		ActorType type;
		DynamicType dynamic_type;
		bool is_trigger;

	private:
		void onStateChanged(Resource::State old_state, Resource::State new_state, Resource&);
	};


	PhysicsSceneImpl(Universe& context, IAllocator& allocator)
		: m_allocator(allocator)
		, m_controllers(m_allocator)
		, m_actors(m_allocator)
		, m_ragdolls(m_allocator)
		, m_terrains(m_allocator)
		, m_dynamic_actors(m_allocator)
		, m_universe(context)
		, m_is_game_running(false)
		, m_contact_callback(*this)
		, m_contact_callbacks(m_allocator)
		, m_queued_forces(m_allocator)
		, m_layers_count(2)
		, m_joints(m_allocator)
		, m_script_scene(nullptr)
		, m_debug_visualization_flags(0)
		, m_is_updating_ragdoll(false)
		, m_update_in_progress(nullptr)
	{
		setMemory(m_layers_names, 0, sizeof(m_layers_names));
		for (int i = 0; i < lengthOf(m_layers_names); ++i)
		{
			copyString(m_layers_names[i], "Layer");
			char tmp[3];
			toCString(i, tmp, lengthOf(tmp));
			catString(m_layers_names[i], tmp);
			m_collision_filter[i] = 0xffffFFFF;
		}

		m_queued_forces.reserve(64);

		#define REGISTER_COMPONENT(TYPE, COMPONENT) \
			context.registerComponentType(TYPE \
				, this \
				, &PhysicsSceneImpl::create##COMPONENT \
				, &PhysicsSceneImpl::destroy##COMPONENT \
				, &PhysicsSceneImpl::serialize##COMPONENT \
				, &PhysicsSceneImpl::deserialize##COMPONENT);


		REGISTER_COMPONENT(RIGID_ACTOR_TYPE, RigidActor);
		REGISTER_COMPONENT(BOX_ACTOR_TYPE, BoxActor);
		REGISTER_COMPONENT(MESH_ACTOR_TYPE, MeshActor);
		REGISTER_COMPONENT(CAPSULE_ACTOR_TYPE, CapsuleActor);
		REGISTER_COMPONENT(SPHERE_ACTOR_TYPE, SphereActor);
		REGISTER_COMPONENT(HEIGHTFIELD_TYPE, Heightfield);
		REGISTER_COMPONENT(CONTROLLER_TYPE, Controller);
		REGISTER_COMPONENT(DISTANCE_JOINT_TYPE, DistanceJoint);
		REGISTER_COMPONENT(HINGE_JOINT_TYPE, HingeJoint);
		REGISTER_COMPONENT(SPHERICAL_JOINT_TYPE, SphericalJoint);
		REGISTER_COMPONENT(D6_JOINT_TYPE, D6Joint);
		REGISTER_COMPONENT(RAGDOLL_TYPE, Ragdoll);

		#undef REGISTER_COMPONENT
	}


	~PhysicsSceneImpl()
	{
		m_controller_manager->release();
		m_default_material->release();
		m_dummy_actor->release();
		m_scene->release();
	}


	int getVersion() const override
	{
		return (int)PhysicsSceneVersion::LATEST;
	}


	void clear() override
	{
		for (auto& controller : m_controllers)
		{
			controller.m_controller->release();
		}
		m_controllers.clear();

		for (auto& ragdoll : m_ragdolls)
		{
			destroySkeleton(ragdoll.root);
		}
		m_ragdolls.clear();

		for (auto& joint : m_joints)
		{
			joint.physx->release();
		}
		m_joints.clear();

		for (auto* actor : m_actors)
		{
			LUMIX_DELETE(m_allocator, actor);
		}
		m_actors.clear();
		m_dynamic_actors.clear();

		m_terrains.clear();
	}


	void onTrigger(Entity e1, Entity e2, bool touch_lost)
	{
		if (!m_script_scene) return;

		auto send = [this, touch_lost](Entity e1, Entity e2)
		{
			if (!m_script_scene->getUniverse().hasComponent(e1, LUA_SCRIPT_TYPE)) return;

			for (int i = 0, c = m_script_scene->getScriptCount(e1); i < c; ++i)
			{
				auto* call = m_script_scene->beginFunctionCall(e1, i, "onTrigger");
				if (!call) continue;

				call->add(e2.index);
				call->add(touch_lost);
				m_script_scene->endFunctionCall();
			}
		};

		send(e1, e2);
		send(e2, e1);
	}


	void onContact(const ContactData& contact_data)
	{
		if (!m_script_scene) return;

		auto send = [this](Entity e1, Entity e2, const Vec3& position)
		{
			if (!m_script_scene->getUniverse().hasComponent(e1, LUA_SCRIPT_TYPE)) return;

			for (int i = 0, c = m_script_scene->getScriptCount(e1); i < c; ++i)
			{
				auto* call = m_script_scene->beginFunctionCall(e1, i, "onContact");
				if (!call) continue;

				call->add(e2.index);
				call->add(position.x);
				call->add(position.y);
				call->add(position.z);
				m_script_scene->endFunctionCall();
			}
		};

		send(contact_data.e1, contact_data.e2, contact_data.position);
		send(contact_data.e2, contact_data.e1, contact_data.position);
		m_contact_callbacks.invoke(contact_data);
	}


	u32 getDebugVisualizationFlags() const override
	{
		return m_debug_visualization_flags;
	}


	void setDebugVisualizationFlags(u32 flags) override
	{
		if (flags == m_debug_visualization_flags) return;

		m_debug_visualization_flags = flags;

		m_scene->setVisualizationParameter(PxVisualizationParameter::eSCALE, flags != 0 ? 1.0f : 0.0f);

		auto setFlag = [this, flags](int flag) {
			m_scene->setVisualizationParameter(PxVisualizationParameter::Enum(flag), flags & (1 << flag) ? 1.0f : 0.0f);
		};

		setFlag(PxVisualizationParameter::eBODY_AXES);
		setFlag(PxVisualizationParameter::eBODY_MASS_AXES);
		setFlag(PxVisualizationParameter::eBODY_LIN_VELOCITY);
		setFlag(PxVisualizationParameter::eBODY_ANG_VELOCITY);
		setFlag(PxVisualizationParameter::eCONTACT_NORMAL);
		setFlag(PxVisualizationParameter::eCONTACT_ERROR);
		setFlag(PxVisualizationParameter::eCONTACT_FORCE);
		setFlag(PxVisualizationParameter::eCOLLISION_AXES);
		setFlag(PxVisualizationParameter::eJOINT_LOCAL_FRAMES);
		setFlag(PxVisualizationParameter::eJOINT_LIMITS);
		setFlag(PxVisualizationParameter::eCOLLISION_SHAPES);
		setFlag(PxVisualizationParameter::eACTOR_AXES);
		setFlag(PxVisualizationParameter::eCOLLISION_AABBS);
		setFlag(PxVisualizationParameter::eWORLD_AXES);
		setFlag(PxVisualizationParameter::eCONTACT_POINT);
	}


	void setVisualizationCullingBox(const Vec3& min, const Vec3& max) override
	{
		PxBounds3 box(toPhysx(min), toPhysx(max));
		m_scene->setVisualizationCullingBox(box);
	}


	Universe& getUniverse() override { return m_universe; }


	IPlugin& getPlugin() const override { return *m_system; }


	int getControllerLayer(Entity entity) override
	{
		return m_controllers[entity].m_layer;
	}


	void setControllerLayer(Entity entity, int layer) override
	{
		ASSERT(layer < lengthOf(m_layers_names));
		auto& controller = m_controllers[entity];
		controller.m_layer = layer;

		PxFilterData data;
		data.word0 = 1 << layer;
		data.word1 = m_collision_filter[layer];
		controller.m_filter_data = data;
		PxShape* shapes[8];
		int shapes_count = controller.m_controller->getActor()->getShapes(shapes, lengthOf(shapes));
		for (int i = 0; i < shapes_count; ++i)
		{
			shapes[i]->setSimulationFilterData(data);
		}
		controller.m_controller->invalidateCache();
	}


	void setRagdollLayer(Entity entity, int layer) override
	{
		auto& ragdoll = m_ragdolls[entity];
		ragdoll.layer = layer;
		struct Tmp
		{
			void operator()(RagdollBone* bone, int layer)
			{
				if (!bone) return;
				if (bone->actor) scene.updateFilterData(bone->actor, layer);
				(*this)(bone->child, layer);
				(*this)(bone->next, layer);
			}

			PhysicsSceneImpl& scene;
		};
		Tmp tmp{*this};
		tmp(ragdoll.root, layer);
	}


	int getRagdollLayer(Entity entity) override
	{
		return m_ragdolls[entity].layer;
	}


	void setActorLayer(Entity entity, int layer) override
	{
		ASSERT(layer < lengthOf(m_layers_names));
		auto* actor = m_actors[entity];
		actor->layer = layer;
		if (actor->physx_actor)
		{
			updateFilterData(actor->physx_actor, actor->layer);
		}
	}


	int getActorLayer(Entity entity) override { return m_actors[entity]->layer; }


	float getSphereRadius(Entity entity) override
	{
		PxRigidActor* actor = m_actors[entity]->physx_actor;
		PxShape* shapes;
		ASSERT(actor->getNbShapes() == 1);
		if (actor->getShapes(&shapes, 1) != 1) ASSERT(false);

		return shapes->getGeometry().sphere().radius;
	}


	void setSphereRadius(Entity entity, float value) override
	{
		if (value == 0) return;
		PxRigidActor* actor = m_actors[entity]->physx_actor;
		PxShape* shapes;
		if (actor->getNbShapes() == 1 && actor->getShapes(&shapes, 1))
		{
			PxSphereGeometry sphere;
			bool is_sphere = shapes->getSphereGeometry(sphere);
			ASSERT(is_sphere);
			sphere.radius = value;
			shapes->setGeometry(sphere);
		}
	}


	float getCapsuleRadius(Entity entity) override
	{
		PxRigidActor* actor = m_actors[entity]->physx_actor;
		PxShape* shapes;
		ASSERT(actor->getNbShapes() == 1);
		if (actor->getShapes(&shapes, 1) != 1) ASSERT(false);

		return shapes->getGeometry().capsule().radius;
	}


	void setCapsuleRadius(Entity entity, float value) override
	{
		if (value <= 0) return;
		PxRigidActor* actor = m_actors[entity]->physx_actor;
		PxShape* shapes;
		if (actor->getNbShapes() == 1 && actor->getShapes(&shapes, 1))
		{
			PxCapsuleGeometry capsule;
			bool is_capsule = shapes->getCapsuleGeometry(capsule);
			ASSERT(is_capsule);
			capsule.radius = value;
			shapes->setGeometry(capsule);
		}
	}
	
	
	float getCapsuleHeight(Entity entity) override
	{
		PxRigidActor* actor = m_actors[entity]->physx_actor;
		PxShape* shapes;
		ASSERT(actor->getNbShapes() == 1);
		if (actor->getShapes(&shapes, 1) != 1) ASSERT(false);

		return shapes->getGeometry().capsule().halfHeight * 2;
	}


	void setCapsuleHeight(Entity entity, float value) override
	{
		if (value <= 0) return;
		PxRigidActor* actor = m_actors[entity]->physx_actor;
		PxShape* shapes;
		if (actor->getNbShapes() == 1 && actor->getShapes(&shapes, 1))
		{
			PxCapsuleGeometry capsule;
			bool is_capsule = shapes->getCapsuleGeometry(capsule);
			ASSERT(is_capsule);
			capsule.halfHeight = value * 0.5f;
			shapes->setGeometry(capsule);
		}
	}

	
	int getHeightfieldLayer(Entity entity) override { return m_terrains[entity].m_layer; }


	void setHeightfieldLayer(Entity entity, int layer) override
	{
		ASSERT(layer < lengthOf(m_layers_names));
		auto& terrain = m_terrains[entity];
		terrain.m_layer = layer;

		if (terrain.m_actor)
		{
			PxFilterData data;
			data.word0 = 1 << layer;
			data.word1 = m_collision_filter[layer];
			PxShape* shapes[8];
			int shapes_count = terrain.m_actor->getShapes(shapes, lengthOf(shapes));
			for (int i = 0; i < shapes_count; ++i)
			{
				shapes[i]->setSimulationFilterData(data);
			}
		}
	}


	void updateHeighfieldData(Entity entity,
		int x,
		int y,
		int width,
		int height,
		const u8* src_data,
		int bytes_per_pixel) override
	{
		PROFILE_FUNCTION();
		Heightfield& terrain = m_terrains[entity];
		
		PxShape* shape;
		terrain.m_actor->getShapes(&shape, 1);
		PxHeightFieldGeometry geom;
		shape->getHeightFieldGeometry(geom);

		Array<PxHeightFieldSample> heights(m_allocator);

		heights.resize(width * height);
		if (bytes_per_pixel == 2)
		{
			const i16* LUMIX_RESTRICT data = (const i16*)src_data;
			for (int j = 0; j < height; ++j)
			{
				for (int i = 0; i < width; ++i)
				{
					int idx = j + i * height;
					int idx2 = i + j * width;
					heights[idx].height = PxI16((i32)data[idx2] - 0x7fff);
					heights[idx].materialIndex0 = heights[idx].materialIndex1 = 0;
					heights[idx].setTessFlag();
				}
			}
		}
		else
		{
			ASSERT(bytes_per_pixel == 1);
			const u8* LUMIX_RESTRICT data = src_data;
			for (int j = 0; j < height; ++j)
			{
				for (int i = 0; i < width; ++i)
				{
					int idx = j + i * height;
					int idx2 = i + j * width;
					heights[idx].height = PxI16((i32)data[idx2] - 0x7f);
					heights[idx].materialIndex0 = heights[idx].materialIndex1 = 0;
					heights[idx].setTessFlag();
				}
			}
		}

		PxHeightFieldDesc hfDesc;
		hfDesc.format = PxHeightFieldFormat::eS16_TM;
		hfDesc.nbColumns = height;
		hfDesc.nbRows = width;
		hfDesc.samples.data = &heights[0];
		hfDesc.samples.stride = sizeof(PxHeightFieldSample);
		hfDesc.thickness = -1;

		geom.heightField->modifySamples(y, x, hfDesc);
		shape->setGeometry(geom);
	}


	int getJointCount() override { return m_joints.size(); }
	Entity getJointEntity(int index) override { return {m_joints.getKey(index).index}; }


	PxDistanceJoint* getDistanceJoint(Entity entity)
	{
		return static_cast<PxDistanceJoint*>(m_joints[entity].physx);
	}


	Vec3 getDistanceJointLinearForce(Entity entity) override
	{
		PxVec3 linear, angular;
		getDistanceJoint(entity)->getConstraint()->getForce(linear, angular);
		return Vec3(linear.x, linear.y, linear.z);
	}


	float getDistanceJointDamping(Entity entity) override { return getDistanceJoint(entity)->getDamping(); }


	void setDistanceJointDamping(Entity entity, float value) override
	{
		getDistanceJoint(entity)->setDamping(value);
	}


	float getDistanceJointStiffness(Entity entity) override { return getDistanceJoint(entity)->getStiffness(); }


	void setDistanceJointStiffness(Entity entity, float value) override
	{
		getDistanceJoint(entity)->setStiffness(value);
	}


	float getDistanceJointTolerance(Entity entity) override { return getDistanceJoint(entity)->getTolerance(); }


	void setDistanceJointTolerance(Entity entity, float value) override
	{
		getDistanceJoint(entity)->setTolerance(value);
	}


	Vec2 getDistanceJointLimits(Entity entity) override
	{
		auto* joint = getDistanceJoint(entity);
		return {joint->getMinDistance(), joint->getMaxDistance()};
	}


	void setDistanceJointLimits(Entity entity, const Vec2& value) override
	{
		auto* joint = getDistanceJoint(entity);
		joint->setMinDistance(value.x);
		joint->setMaxDistance(value.y);
		joint->setDistanceJointFlag(PxDistanceJointFlag::eMIN_DISTANCE_ENABLED, value.x > 0);
		joint->setDistanceJointFlag(PxDistanceJointFlag::eMAX_DISTANCE_ENABLED, value.y > 0);
	}


	PxD6Joint* getD6Joint(Entity entity)
	{
		return static_cast<PxD6Joint*>(m_joints[entity].physx);
	}


	float getD6JointDamping(Entity entity) override
	{
		return getD6Joint(entity)->getLinearLimit().damping;
	}


	void setD6JointDamping(Entity entity, float value) override 
	{
		PxD6Joint* joint = getD6Joint(entity);
		PxJointLinearLimit limit = joint->getLinearLimit();
		limit.damping = value;
		joint->setLinearLimit(limit);
	}


	float getD6JointStiffness(Entity entity) override
	{
		return getD6Joint(entity)->getLinearLimit().stiffness;
	}


	void setD6JointStiffness(Entity entity, float value) override
	{
		PxD6Joint* joint = getD6Joint(entity);
		PxJointLinearLimit limit = joint->getLinearLimit();
		limit.stiffness = value;
		joint->setLinearLimit(limit);
	}


	float getD6JointRestitution(Entity entity) override
	{
		return getD6Joint(entity)->getLinearLimit().restitution;
	}


	void setD6JointRestitution(Entity entity, float value) override
	{
		PxD6Joint* joint = getD6Joint(entity);
		PxJointLinearLimit limit = joint->getLinearLimit();
		limit.restitution = value;
		joint->setLinearLimit(limit);
	}


	Vec2 getD6JointTwistLimit(Entity entity) override
	{
		auto limit = getD6Joint(entity)->getTwistLimit();
		return {limit.lower, limit.upper};
	}


	void setD6JointTwistLimit(Entity entity, const Vec2& limit) override
	{
		auto* joint = getD6Joint(entity);
		auto px_limit = joint->getTwistLimit();
		px_limit.lower = limit.x;
		px_limit.upper = limit.y;
		joint->setTwistLimit(px_limit);
	}


	Vec2 getD6JointSwingLimit(Entity entity) override
	{
		auto limit = getD6Joint(entity)->getSwingLimit();
		return {limit.yAngle, limit.zAngle};
	}


	void setD6JointSwingLimit(Entity entity, const Vec2& limit) override
	{
		auto* joint = getD6Joint(entity);
		auto px_limit = joint->getSwingLimit();
		px_limit.yAngle = Math::maximum(0.0f, limit.x);
		px_limit.zAngle = Math::maximum(0.0f, limit.y);
		joint->setSwingLimit(px_limit);
	}


	D6Motion getD6JointXMotion(Entity entity) override
	{
		return (D6Motion)getD6Joint(entity)->getMotion(PxD6Axis::eX);
	}


	void setD6JointXMotion(Entity entity, D6Motion motion) override
	{
		getD6Joint(entity)->setMotion(PxD6Axis::eX, (PxD6Motion::Enum)motion);
	}


	D6Motion getD6JointYMotion(Entity entity) override
	{
		return (D6Motion)getD6Joint(entity)->getMotion(PxD6Axis::eY);
	}


	void setD6JointYMotion(Entity entity, D6Motion motion) override
	{
		getD6Joint(entity)->setMotion(PxD6Axis::eY, (PxD6Motion::Enum)motion);
	}


	D6Motion getD6JointSwing1Motion(Entity entity) override
	{
		return (D6Motion)getD6Joint(entity)->getMotion(PxD6Axis::eSWING1);
	}


	void setD6JointSwing1Motion(Entity entity, D6Motion motion) override
	{
		getD6Joint(entity)->setMotion(PxD6Axis::eSWING1, (PxD6Motion::Enum)motion);
	}


	D6Motion getD6JointSwing2Motion(Entity entity) override
	{
		return (D6Motion)getD6Joint(entity)->getMotion(PxD6Axis::eSWING2);
	}


	void setD6JointSwing2Motion(Entity entity, D6Motion motion) override
	{
		getD6Joint(entity)->setMotion(PxD6Axis::eSWING2, (PxD6Motion::Enum)motion);
	}


	D6Motion getD6JointTwistMotion(Entity entity) override
	{
		return (D6Motion)getD6Joint(entity)->getMotion(PxD6Axis::eTWIST);
	}


	void setD6JointTwistMotion(Entity entity, D6Motion motion) override
	{
		getD6Joint(entity)->setMotion(PxD6Axis::eTWIST, (PxD6Motion::Enum)motion);
	}


	D6Motion getD6JointZMotion(Entity entity) override
	{
		return (D6Motion)getD6Joint(entity)->getMotion(PxD6Axis::eZ);
	}


	void setD6JointZMotion(Entity entity, D6Motion motion) override
	{
		getD6Joint(entity)->setMotion(PxD6Axis::eZ, (PxD6Motion::Enum)motion);
	}


	float getD6JointLinearLimit(Entity entity) override
	{
		return getD6Joint(entity)->getLinearLimit().value;
	}


	void setD6JointLinearLimit(Entity entity, float limit) override
	{
		auto* joint = getD6Joint(entity);
		auto px_limit = joint->getLinearLimit();
		px_limit.value = limit;
		joint->setLinearLimit(px_limit);
	}


	Entity getJointConnectedBody(Entity entity) override
	{
		return m_joints[entity].connected_body;
	}


	void setJointConnectedBody(Entity joint_entity, Entity connected_body) override
	{
		int idx = m_joints.find(joint_entity);
		Joint& joint = m_joints.at(idx);
		joint.connected_body = connected_body;
		if (m_is_game_running) initJoint(joint_entity, joint);
	}


	void setJointAxisPosition(Entity entity, const Vec3& value) override
	{
		auto& joint = m_joints[entity];
		joint.local_frame0.p = toPhysx(value);
		joint.physx->setLocalPose(PxJointActorIndex::eACTOR0, joint.local_frame0);
	}


	void setJointAxisDirection(Entity entity, const Vec3& value) override
	{
		auto& joint = m_joints[entity];
		joint.local_frame0.q = toPhysx(Quat::vec3ToVec3(Vec3(1, 0, 0), value));
		joint.physx->setLocalPose(PxJointActorIndex::eACTOR0, joint.local_frame0);
	}


	Vec3 getJointAxisPosition(Entity entity) override
	{
		return fromPhysx(m_joints[entity].local_frame0.p);
	}


	Vec3 getJointAxisDirection(Entity entity) override
	{
		return fromPhysx(m_joints[entity].local_frame0.q.rotate(PxVec3(1, 0, 0)));
	}


	bool getSphericalJointUseLimit(Entity entity) override
	{
		return static_cast<PxSphericalJoint*>(m_joints[entity].physx)
			->getSphericalJointFlags()
			.isSet(PxSphericalJointFlag::eLIMIT_ENABLED);
	}


	void setSphericalJointUseLimit(Entity entity, bool use_limit) override
	{
		return static_cast<PxSphericalJoint*>(m_joints[entity].physx)
			->setSphericalJointFlag(PxSphericalJointFlag::eLIMIT_ENABLED, use_limit);
	}


	Vec2 getSphericalJointLimit(Entity entity) override
	{
		auto cone = static_cast<PxSphericalJoint*>(m_joints[entity].physx)->getLimitCone();
		return {cone.yAngle, cone.zAngle};
	}


	void setSphericalJointLimit(Entity entity, const Vec2& limit) override
	{
		auto* joint = static_cast<PxSphericalJoint*>(m_joints[entity].physx);
		auto limit_cone = joint->getLimitCone();
		limit_cone.yAngle = limit.x;
		limit_cone.zAngle = limit.y;
		joint->setLimitCone(limit_cone);
	}


	RigidTransform getJointLocalFrame(Entity entity) override
	{
		return fromPhysx(m_joints[entity].local_frame0);
	}


	PxJoint* getJoint(Entity entity) override
	{
		return m_joints[entity].physx;
	}


	RigidTransform getJointConnectedBodyLocalFrame(Entity entity) override
	{
		auto& joint = m_joints[entity];
		if (!joint.connected_body.isValid()) return {Vec3(0, 0, 0), Quat(0, 0, 0, 1)};
		
		PxRigidActor* a0, *a1;
		joint.physx->getActors(a0, a1);
		if (a1) return fromPhysx(joint.physx->getLocalPose(PxJointActorIndex::eACTOR1));

		Transform connected_body_tr = m_universe.getTransform(joint.connected_body);
		RigidTransform unscaled_connected_body_tr = {connected_body_tr.pos, connected_body_tr.rot};
		Transform tr = m_universe.getTransform(entity);
		RigidTransform unscaled_tr = {tr.pos, tr.rot};

		return unscaled_connected_body_tr.inverted() * unscaled_tr * fromPhysx(joint.local_frame0);
	}


	void setHingeJointUseLimit(Entity entity, bool use_limit) override
	{
		auto* joint = static_cast<PxRevoluteJoint*>(m_joints[entity].physx);
		joint->setRevoluteJointFlag(PxRevoluteJointFlag::eLIMIT_ENABLED, use_limit);
	}


	bool getHingeJointUseLimit(Entity entity) override
	{
		auto* joint = static_cast<PxRevoluteJoint*>(m_joints[entity].physx);
		return joint->getRevoluteJointFlags().isSet(PxRevoluteJointFlag::eLIMIT_ENABLED);
	}


	Vec2 getHingeJointLimit(Entity entity) override
	{
		auto* joint = static_cast<PxRevoluteJoint*>(m_joints[entity].physx);
		PxJointAngularLimitPair limit = joint->getLimit();
		return {limit.lower, limit.upper};
	}


	void setHingeJointLimit(Entity entity, const Vec2& limit) override
	{
		auto* joint = static_cast<PxRevoluteJoint*>(m_joints[entity].physx);
		PxJointAngularLimitPair px_limit = joint->getLimit();
		px_limit.lower = limit.x;
		px_limit.upper = limit.y;
		joint->setLimit(px_limit);
	}


	float getHingeJointDamping(Entity entity) override
	{
		auto* joint = static_cast<PxRevoluteJoint*>(m_joints[entity].physx);
		return joint->getLimit().damping;
	}


	void setHingeJointDamping(Entity entity, float value) override
	{
		auto* joint = static_cast<PxRevoluteJoint*>(m_joints[entity].physx);
		PxJointAngularLimitPair px_limit = joint->getLimit();
		px_limit.damping = value;
		joint->setLimit(px_limit);

	}


	float getHingeJointStiffness(Entity entity) override
	{
		auto* joint = static_cast<PxRevoluteJoint*>(m_joints[entity].physx);
		return joint->getLimit().stiffness;
	}


	void setHingeJointStiffness(Entity entity, float value) override
	{
		auto* joint = static_cast<PxRevoluteJoint*>(m_joints[entity].physx);
		PxJointAngularLimitPair px_limit = joint->getLimit();
		px_limit.stiffness = value;
		joint->setLimit(px_limit);
	}


	void destroyHeightfield(Entity entity)
	{
		m_terrains.erase(entity);
		m_universe.onComponentDestroyed(entity, HEIGHTFIELD_TYPE, this);
	}


	void destroyController(Entity entity)
	{
		m_controllers[entity].m_controller->release();
		m_controllers.erase(entity);
		m_universe.onComponentDestroyed(entity, CONTROLLER_TYPE, this);
	}


	void destroyRagdoll(Entity entity)
	{
		int idx = m_ragdolls.find(entity);
		destroySkeleton(m_ragdolls.at(idx).root);
		m_ragdolls.eraseAt(idx);
		m_universe.onComponentDestroyed(entity, RAGDOLL_TYPE, this);
	}


	void destroyRigidActor(Entity entity) { destroyActorGeneric(entity, RIGID_ACTOR_TYPE); }
	void destroyMeshActor(Entity entity) { destroyActorGeneric(entity, MESH_ACTOR_TYPE); }
	void destroyBoxActor(Entity entity) { destroyActorGeneric(entity, BOX_ACTOR_TYPE); }
	void destroySphereActor(Entity entity) { destroyActorGeneric(entity, SPHERE_ACTOR_TYPE); }
	void destroyCapsuleActor(Entity entity) { destroyActorGeneric(entity, CAPSULE_ACTOR_TYPE); }


	void destroySphericalJoint(Entity entity) { destroyJointGeneric(entity, SPHERE_ACTOR_TYPE); }
	void destroyHingeJoint(Entity entity) { destroyJointGeneric(entity, SPHERE_ACTOR_TYPE); }
	void destroyD6Joint(Entity entity) { destroyJointGeneric(entity, SPHERE_ACTOR_TYPE); }
	void destroyDistanceJoint(Entity entity) { destroyJointGeneric(entity, SPHERE_ACTOR_TYPE); }


	void destroyActorGeneric(Entity entity, ComponentType type)
	{
		auto* actor = m_actors[entity];
		actor->setPhysxActor(nullptr);
		LUMIX_DELETE(m_allocator, actor);
		m_actors.erase(entity);
		m_dynamic_actors.eraseItem(actor);
		m_universe.onComponentDestroyed(entity, type, this);
		if (m_is_game_running)
		{
			for (int i = 0, c = m_joints.size(); i < c; ++i)
			{
				Joint& joint = m_joints.at(i);
				if (m_joints.getKey(i) == entity || joint.connected_body == entity)
				{
					if (joint.physx) joint.physx->release();
					joint.physx = PxDistanceJointCreate(m_scene->getPhysics(),
						m_dummy_actor,
						PxTransform::createIdentity(),
						nullptr,
						PxTransform::createIdentity());
				}
			}
		}

	}


	void destroyJointGeneric(Entity entity, ComponentType type)
	{
		auto& joint = m_joints[entity];
		if (joint.physx) joint.physx->release();
		m_joints.erase(entity);
		m_universe.onComponentDestroyed(entity, type, this);
	}


	void createDistanceJoint(Entity entity)
	{
		if (m_joints.find(entity) >= 0) return;
		Joint& joint = m_joints.insert(entity);
		joint.connected_body = INVALID_ENTITY;
		joint.local_frame0.p = PxVec3(0, 0, 0);
		joint.local_frame0.q = PxQuat(0, 0, 0, 1);
		joint.physx = PxDistanceJointCreate(m_scene->getPhysics(),
			m_dummy_actor,
			PxTransform::createIdentity(),
			nullptr,
			PxTransform::createIdentity());
		joint.physx->setConstraintFlag(PxConstraintFlag::eVISUALIZATION, true);
		static_cast<PxDistanceJoint*>(joint.physx)->setDistanceJointFlag(PxDistanceJointFlag::eSPRING_ENABLED, true);

		m_universe.onComponentCreated(entity, DISTANCE_JOINT_TYPE, this);
	}


	void createSphericalJoint(Entity entity)
	{
		if (m_joints.find(entity) >= 0) return;
		Joint& joint = m_joints.insert(entity);
		joint.connected_body = INVALID_ENTITY;
		joint.local_frame0.p = PxVec3(0, 0, 0);
		joint.local_frame0.q = PxQuat(0, 0, 0, 1);
		joint.physx = PxSphericalJointCreate(m_scene->getPhysics(),
			m_dummy_actor,
			PxTransform::createIdentity(),
			nullptr,
			PxTransform::createIdentity());
		joint.physx->setConstraintFlag(PxConstraintFlag::eVISUALIZATION, true);

		m_universe.onComponentCreated(entity, SPHERICAL_JOINT_TYPE, this);
	}


	void createD6Joint(Entity entity)
	{
		if (m_joints.find(entity) >= 0) return;
		Joint& joint = m_joints.insert(entity);
		joint.connected_body = INVALID_ENTITY;
		joint.local_frame0.p = PxVec3(0, 0, 0);
		joint.local_frame0.q = PxQuat(0, 0, 0, 1);
		joint.physx = PxD6JointCreate(m_scene->getPhysics(),
			m_dummy_actor,
			PxTransform::createIdentity(),
			nullptr,
			PxTransform::createIdentity());
		auto* d6_joint = static_cast<PxD6Joint*>(joint.physx);
		auto linear_limit = d6_joint->getLinearLimit();
		linear_limit.value = 1.0f;
		d6_joint->setLinearLimit(linear_limit);
		joint.physx->setConstraintFlag(PxConstraintFlag::eVISUALIZATION, true);

		m_universe.onComponentCreated(entity, D6_JOINT_TYPE, this);
	}


	void createHingeJoint(Entity entity)
	{
		if (m_joints.find(entity) >= 0) return;
		Joint& joint = m_joints.insert(entity);
		joint.connected_body = INVALID_ENTITY;
		joint.local_frame0.p = PxVec3(0, 0, 0);
		joint.local_frame0.q = PxQuat(0, 0, 0, 1);
		joint.physx = PxRevoluteJointCreate(m_scene->getPhysics(),
			m_dummy_actor,
			PxTransform::createIdentity(),
			nullptr,
			PxTransform::createIdentity());
		joint.physx->setConstraintFlag(PxConstraintFlag::eVISUALIZATION, true);

		m_universe.onComponentCreated(entity, HINGE_JOINT_TYPE, this);
	}


	void createHeightfield(Entity entity)
	{
		Heightfield& terrain = m_terrains.insert(entity);
		terrain.m_heightmap = nullptr;
		terrain.m_scene = this;
		terrain.m_actor = nullptr;
		terrain.m_entity = entity;

		m_universe.onComponentCreated(entity, HEIGHTFIELD_TYPE, this);
	}


	void initControllerDesc(PxCapsuleControllerDesc& desc)
	{
		static struct CB : PxControllerBehaviorCallback
		{
			PxControllerBehaviorFlags getBehaviorFlags(const PxShape& shape, const PxActor& actor) override
			{
				return PxControllerBehaviorFlag::eCCT_CAN_RIDE_ON_OBJECT | PxControllerBehaviorFlag::eCCT_SLIDE;
			}


			PxControllerBehaviorFlags getBehaviorFlags(const PxController& controller) override
			{
				return PxControllerBehaviorFlag::eCCT_CAN_RIDE_ON_OBJECT;
			}


			PxControllerBehaviorFlags getBehaviorFlags(const PxObstacle& obstacle) override
			{
				return PxControllerBehaviorFlag::eCCT_CAN_RIDE_ON_OBJECT;
			}
		} cb;

		desc.material = m_default_material;
		desc.height = 1.8f;
		desc.radius = 0.25f;
		desc.slopeLimit = 0.0f;
		desc.contactOffset = 0.1f;
		desc.stepOffset = 0.02f;
		desc.behaviorCallback = &cb;
	}


	void createController(Entity entity)
	{
		PxCapsuleControllerDesc cDesc;
		initControllerDesc(cDesc);
		Vec3 position = m_universe.getPosition(entity);
		cDesc.position.set(position.x, position.y, position.z);
		Controller& c = m_controllers.insert(entity);
		c.m_controller = m_controller_manager->createController(cDesc);
		c.m_entity = entity;
		c.m_frame_change.set(0, 0, 0);
		c.m_radius = cDesc.radius;
		c.m_height = cDesc.height;
		c.m_layer = 0;

		PxFilterData data;
		int controller_layer = c.m_layer;
		data.word0 = 1 << controller_layer;
		data.word1 = m_collision_filter[controller_layer];
		c.m_filter_data = data;
		PxShape* shapes[8];
		int shapes_count = c.m_controller->getActor()->getShapes(shapes, lengthOf(shapes));
		c.m_controller->getActor()->userData = (void*)(intptr_t)entity.index;
		for (int i = 0; i < shapes_count; ++i)
		{
			shapes[i]->setSimulationFilterData(data);
		}

		m_universe.onComponentCreated(entity, CONTROLLER_TYPE, this);
	}


	void createCapsuleActor(Entity entity)
	{
		if (m_actors.find(entity) >= 0) return;
		RigidActor* actor = LUMIX_NEW(m_allocator, RigidActor)(*this, ActorType::CAPSULE);
		m_actors.insert(entity, actor);
		actor->entity = entity;

		PxCapsuleGeometry geom;
		geom.radius = 0.5f;
		geom.halfHeight = 1;
		Transform transform = m_universe.getTransform(entity);
		PxTransform px_transform = toPhysx({transform.pos, transform.rot});

		PxRigidStatic* physx_actor =
			PxCreateStatic(*m_system->getPhysics(), px_transform, geom, *m_default_material);
		actor->setPhysxActor(physx_actor);

		m_universe.onComponentCreated(entity, CAPSULE_ACTOR_TYPE, this);
	}


	void createRagdoll(Entity entity)
	{
		Ragdoll& ragdoll = m_ragdolls.insert(entity);
		ragdoll.entity = entity;
		ragdoll.root = nullptr;
		ragdoll.layer = 0;
		ragdoll.root_transform.pos.set(0, 0, 0);
		ragdoll.root_transform.rot.set(0, 0, 0, 1);

		m_universe.onComponentCreated(entity, RAGDOLL_TYPE, this);
	}


	void createRigidActor(Entity entity)
	{
		if (m_actors.find(entity) >= 0) return;
		RigidActor* actor = LUMIX_NEW(m_allocator, RigidActor)(*this, ActorType::RIGID);
		m_actors.insert(entity, actor);
		actor->entity = entity;

		Transform transform = m_universe.getTransform(entity);
		PxTransform px_transform = toPhysx(transform.getRigidPart());

		PxRigidStatic* physx_actor = m_system->getPhysics()->createRigidStatic(px_transform);
		actor->setPhysxActor(physx_actor);

		m_universe.onComponentCreated(entity, RIGID_ACTOR_TYPE, this);
	}


	void createBoxActor(Entity entity)
	{
		if (m_actors.find(entity) >= 0) return;
		RigidActor* actor = LUMIX_NEW(m_allocator, RigidActor)(*this, ActorType::BOX);
		m_actors.insert(entity, actor);
		actor->entity = entity;

		PxBoxGeometry geom;
		geom.halfExtents.x = 1;
		geom.halfExtents.y = 1;
		geom.halfExtents.z = 1;
		Transform transform = m_universe.getTransform(entity);
		PxTransform px_transform = toPhysx({transform.pos, transform.rot});

		PxRigidStatic* physx_actor =
			PxCreateStatic(*m_system->getPhysics(), px_transform, geom, *m_default_material);
		actor->setPhysxActor(physx_actor);

		m_universe.onComponentCreated(entity, BOX_ACTOR_TYPE, this);
	}


	void createSphereActor(Entity entity)
	{
		if (m_actors.find(entity) >= 0) return;
		RigidActor* actor = LUMIX_NEW(m_allocator, RigidActor)(*this, ActorType::SPHERE);
		m_actors.insert(entity, actor);
		actor->entity = entity;

		PxSphereGeometry geom;
		geom.radius = 1;
		Transform tr = m_universe.getTransform(entity);
		PxTransform transform = toPhysx({tr.pos, tr.rot});

		PxRigidStatic* physx_actor =
			PxCreateStatic(*m_system->getPhysics(), transform, geom, *m_default_material);
		actor->setPhysxActor(physx_actor);

		m_universe.onComponentCreated(entity, SPHERE_ACTOR_TYPE, this);
	}


	void createMeshActor(Entity entity)
	{
		if (m_actors.find(entity) >= 0) return;
		RigidActor* actor = LUMIX_NEW(m_allocator, RigidActor)(*this, ActorType::MESH);
		m_actors.insert(entity, actor);
		actor->entity = entity;

		m_universe.onComponentCreated(entity, MESH_ACTOR_TYPE, this);
	}


	Path getHeightmapSource(Entity entity) override
	{
		auto& terrain = m_terrains[entity];
		return terrain.m_heightmap ? terrain.m_heightmap->getPath() : Path("");
	}


	float getHeightmapXZScale(Entity entity) override { return m_terrains[entity].m_xz_scale; }


	void setHeightmapXZScale(Entity entity, float scale) override
	{
		if (scale == 0) return;
		auto& terrain = m_terrains[entity];
		if (scale != terrain.m_xz_scale)
		{
			terrain.m_xz_scale = scale;
			if (terrain.m_heightmap && terrain.m_heightmap->isReady())
			{
				heightmapLoaded(terrain);
			}
		}
	}


	float getHeightmapYScale(Entity entity) override
	{
		return m_terrains[entity].m_y_scale;
	}


	void setHeightmapYScale(Entity entity, float scale) override
	{
		if (scale == 0) return;
		auto& terrain = m_terrains[entity];
		if (scale != terrain.m_y_scale)
		{
			terrain.m_y_scale = scale;
			if (terrain.m_heightmap && terrain.m_heightmap->isReady())
			{
				heightmapLoaded(terrain);
			}
		}
	}


	void setHeightmapSource(Entity entity, const Path& str) override
	{
		auto& resource_manager = m_engine->getResourceManager();
		auto& terrain = m_terrains[entity];
		auto* old_hm = terrain.m_heightmap;
		if (old_hm)
		{
			resource_manager.get(Texture::TYPE)->unload(*old_hm);
			auto& cb = old_hm->getObserverCb();
			cb.unbind<Heightfield, &Heightfield::heightmapLoaded>(&terrain);
		}
		auto* texture_manager = resource_manager.get(Texture::TYPE);
		if (str.isValid())
		{
			auto* new_hm = static_cast<Texture*>(texture_manager->load(str));
			terrain.m_heightmap = new_hm;
			new_hm->onLoaded<Heightfield, &Heightfield::heightmapLoaded>(&terrain);
			new_hm->addDataReference();
		}
		else
		{
			terrain.m_heightmap = nullptr;
		}
	}


	Path getShapeSource(Entity entity) override
	{
		return m_actors[entity]->resource ? m_actors[entity]->resource->getPath() : Path("");
	}


	void setShapeSource(Entity entity, const Path& str) override
	{
		ASSERT(m_actors[entity]);
		auto& actor = *m_actors[entity];
		if (actor.resource && actor.resource->getPath() == str)
		{
			bool is_kinematic = actor.physx_actor->isRigidBody()->getRigidBodyFlags().isSet(PxRigidBodyFlag::eKINEMATIC);
			if (actor.dynamic_type == DynamicType::KINEMATIC && is_kinematic) return;
			if (actor.dynamic_type == DynamicType::DYNAMIC && actor.physx_actor->isRigidDynamic()) return;
			if (actor.dynamic_type == DynamicType::STATIC && actor.physx_actor->isRigidStatic()) return;
		}

		ResourceManagerBase* manager = m_engine->getResourceManager().get(PhysicsGeometry::TYPE);
		PhysicsGeometry* geom_res = static_cast<PhysicsGeometry*>(manager->load(str));

		actor.setPhysxActor(nullptr);
		actor.setResource(geom_res);
	}


	int getActorCount() const override { return m_actors.size(); }
	Entity getActorEntity(int index) override { return m_actors.at(index)->entity; }
	ActorType getActorType(int index) override { return m_actors.at(index)->type; }


	bool isActorDebugEnabled(int index) const override
	{
		auto* px_actor = m_actors.at(index)->physx_actor;
		if (!px_actor) return false;
		return px_actor->getActorFlags().isSet(PxActorFlag::eVISUALIZATION);
	}


	void enableActorDebug(int index, bool enable) const override
	{
		auto* px_actor = m_actors.at(index)->physx_actor;
		if (!px_actor) return;
		px_actor->setActorFlag(PxActorFlag::eVISUALIZATION, enable);
		PxShape* shape;
		int count = px_actor->getShapes(&shape, 1);
		ASSERT(count > 0);
		shape->setFlag(PxShapeFlag::eVISUALIZATION, enable);
	}


	void render() override
	{
		auto& render_scene = *static_cast<RenderScene*>(m_universe.getScene(crc32("renderer")));
		const PxRenderBuffer& rb = m_scene->getRenderBuffer();
		const PxU32 num_lines = Math::minimum(100000U, rb.getNbLines());
		if (num_lines)
		{
			const PxDebugLine* PX_RESTRICT lines = rb.getLines();
			for (PxU32 i = 0; i < num_lines; ++i)
			{
				const PxDebugLine& line = lines[i];
				Vec3 from = fromPhysx(line.pos0);
				Vec3 to = fromPhysx(line.pos1);
				render_scene.addDebugLine(from, to, line.color0, 0);
			}
		}
		const PxU32 num_tris = rb.getNbTriangles();
		if (num_tris)
		{
			const PxDebugTriangle* PX_RESTRICT tris = rb.getTriangles();
			for (PxU32 i = 0; i < num_tris; ++i)
			{
				const PxDebugTriangle& tri = tris[i];
				render_scene.addDebugTriangle(fromPhysx(tri.pos0), fromPhysx(tri.pos1), fromPhysx(tri.pos2), tri.color0, 0);
			}
		}
	}


	void updateDynamicActors()
	{
		PROFILE_FUNCTION();
		for (auto* actor : m_dynamic_actors)
		{
			m_update_in_progress = actor;
			PxTransform trans = actor->physx_actor->getGlobalPose();
			m_universe.setTransform(actor->entity, fromPhysx(trans));
		}
		m_update_in_progress = nullptr;
	}


	void simulateScene(float time_delta)
	{
		PROFILE_FUNCTION();
		m_scene->simulate(time_delta);
	}


	void fetchResults()
	{
		PROFILE_FUNCTION();
		m_scene->fetchResults(true);
	}


	bool isControllerCollisionDown(Entity entity) const override
	{
		const Controller& ctrl = m_controllers[entity];
		PxControllerState state;
		ctrl.m_controller->getState(state);
		return (state.collisionFlags & PxControllerCollisionFlag::eCOLLISION_DOWN) != 0;
	}


	void updateControllers(float time_delta)
	{
		PROFILE_FUNCTION();
		for (auto& controller : m_controllers)
		{
			Vec3 dif = controller.m_frame_change;
			controller.m_frame_change.set(0, 0, 0);

			PxControllerState state;
			controller.m_controller->getState(state);
			bool apply_gravity = (state.collisionFlags & PxControllerCollisionFlag::eCOLLISION_DOWN) == 0;
			if(apply_gravity)
			{
				dif.y += controller.gravity_speed * time_delta;
				controller.gravity_speed += time_delta * -9.8f;
			}
			else
			{
				controller.gravity_speed = 0;
			}

			PxControllerFilters filters(nullptr, &controller.m_filter_callback);
			controller.m_controller->move(toPhysx(dif), 0.001f, time_delta, filters);
			PxExtendedVec3 p = controller.m_controller->getFootPosition();

			m_universe.setPosition(controller.m_entity, (float)p.x, (float)p.y, (float)p.z);
		}
	}


	void applyQueuedForces()
	{
		for (auto& i : m_queued_forces)
		{
			auto* actor = m_actors[i.entity];
			if (actor->dynamic_type != DynamicType::DYNAMIC)
			{
				g_log_warning.log("Physics") << "Trying to apply force to static object";
				return;
			}

			auto* physx_actor = static_cast<PxRigidDynamic*>(actor->physx_actor);
			if (!physx_actor) return;
			PxVec3 f(i.force.x, i.force.y, i.force.z);
			physx_actor->addForce(f);
		}
		m_queued_forces.clear();
	}


	static RagdollBone* getBone(RagdollBone* bone, int pose_bone_idx)
	{
		if (!bone) return nullptr;
		if (bone->pose_bone_idx == pose_bone_idx) return bone;

		auto* handle = getBone(bone->child, pose_bone_idx);
		if (handle) return handle;
			
		handle = getBone(bone->next, pose_bone_idx);
		if (handle) return handle;

		return nullptr;
	}


	PxCapsuleGeometry getCapsuleGeometry(RagdollBone* bone)
	{
		PxShape* shape;
		int count = bone->actor->getShapes(&shape, 1);
		ASSERT(count == 1);

		PxCapsuleGeometry geom;
		bool is_capsule = shape->getCapsuleGeometry(geom);
		ASSERT(is_capsule);

		return geom;
	}


	PxJoint* getRagdollBoneJoint(RagdollBone* bone) const override
	{
		return bone->parent_joint;
	}


	RagdollBone* getRagdollRootBone(Entity entity) const override
	{
		return m_ragdolls[entity].root;
	}


	RagdollBone* getRagdollBoneChild(RagdollBone* bone) override
	{
		return bone->child;
	}


	RagdollBone* getRagdollBoneSibling(RagdollBone* bone) override
	{
		return bone->next;
	}


	float getRagdollBoneHeight(RagdollBone* bone) override
	{
		return getCapsuleGeometry(bone).halfHeight * 2.0f;
	}


	float getRagdollBoneRadius(RagdollBone* bone) override
	{
		return getCapsuleGeometry(bone).radius;
	}


	void setRagdollBoneHeight(RagdollBone* bone, float value) override
	{
		if (value < 0) return;
		auto geom = getCapsuleGeometry(bone);
		geom.halfHeight = value * 0.5f;
		PxShape* shape;
		bone->actor->getShapes(&shape, 1);
		shape->setGeometry(geom);
	}


	void setRagdollBoneRadius(RagdollBone* bone, float value) override
	{
		if (value < 0) return;
		auto geom = getCapsuleGeometry(bone);
		geom.radius = value;
		PxShape* shape;
		bone->actor->getShapes(&shape, 1);
		shape->setGeometry(geom);
	}


	RigidTransform getRagdollBoneTransform(RagdollBone* bone) override
	{
		auto px_pose = bone->actor->getGlobalPose();
		return fromPhysx(px_pose);
	}


	void setRagdollBoneTransform(RagdollBone* bone, const RigidTransform& transform) override
	{
		Entity entity = {(int)(intptr_t)bone->actor->userData};
		
		auto* render_scene = static_cast<RenderScene*>(m_universe.getScene(RENDERER_HASH));
		if (!render_scene) return;

		if (!render_scene->getUniverse().hasComponent(entity, MODEL_INSTANCE_TYPE)) return;

		Model* model = render_scene->getModelInstanceModel(entity);
		RigidTransform entity_transform = m_universe.getTransform(entity).getRigidPart();

		bone->bind_transform =
			(entity_transform.inverted() * transform).inverted() * model->getBone(bone->pose_bone_idx).transform;
		bone->inv_bind_transform = bone->bind_transform.inverted();
		PxTransform delta = toPhysx(transform).getInverse() * bone->actor->getGlobalPose();
		bone->actor->setGlobalPose(toPhysx(transform));

		if (bone->parent_joint)
		{
			PxTransform local_pose1 = bone->parent_joint->getLocalPose(PxJointActorIndex::eACTOR1);
			bone->parent_joint->setLocalPose(PxJointActorIndex::eACTOR1, delta * local_pose1);
		}
		auto* child = bone->child;
		while (child && child->parent_joint)
		{
			PxTransform local_pose0 = child->parent_joint->getLocalPose(PxJointActorIndex::eACTOR0);
			child->parent_joint->setLocalPose(PxJointActorIndex::eACTOR0, delta * local_pose0);
			child = child->next;
		}
	}


	const char* getRagdollBoneName(RagdollBone* bone) override
	{
		Entity entity = {(int)(intptr_t)(void*)bone->actor->userData};
		auto* render_scene = static_cast<RenderScene*>(m_universe.getScene(RENDERER_HASH));
		ASSERT(render_scene);

		Model* model = render_scene->getModelInstanceModel(entity);
		ASSERT(model && model->isReady());

		return model->getBone(bone->pose_bone_idx).name.c_str();
	}


	RagdollBone* getRagdollBoneByName(Entity entity, u32 bone_name_hash) override
	{
		auto* render_scene = static_cast<RenderScene*>(m_universe.getScene(RENDERER_HASH));
		ASSERT(render_scene);

		Model* model = render_scene->getModelInstanceModel(entity);
		ASSERT(model && model->isReady());

		auto iter = model->getBoneIndex(bone_name_hash);
		ASSERT(iter.isValid());

		return getBone(m_ragdolls[entity].root, iter.value());
	}


	RagdollBone* getPhyParent(Entity entity, Model* model, int bone_index)
	{
		auto* bone = &model->getBone(bone_index);
		if (bone->parent_idx < 0) return nullptr;
		RagdollBone* phy_bone;
		do
		{
			bone = &model->getBone(bone->parent_idx);
			phy_bone = getRagdollBoneByName(entity, crc32(bone->name.c_str()));
		} while (!phy_bone && bone->parent_idx >= 0);
		return phy_bone;
	}


	void destroyRagdollBone(Entity entity, RagdollBone* bone) override
	{
		disconnect(m_ragdolls[entity], bone);
		bone->actor->release();
		LUMIX_DELETE(m_allocator, bone);
	}


	void getRagdollData(Entity entity, OutputBlob& blob) override
	{
		auto& ragdoll = m_ragdolls[entity];
		serializeRagdollBone(ragdoll, ragdoll.root, blob);
	}


	void setRagdollRoot(Ragdoll& ragdoll, RagdollBone* bone) const
	{
		ragdoll.root = bone;
		if (!bone) return;
		RigidTransform root_transform = fromPhysx(ragdoll.root->actor->getGlobalPose());
		RigidTransform entity_transform = m_universe.getTransform(ragdoll.entity).getRigidPart();
		ragdoll.root_transform = root_transform.inverted() * entity_transform;
	}

	
	void setRagdollData(Entity entity, InputBlob& blob) override
	{
		auto& ragdoll = m_ragdolls[entity];
		setRagdollRoot(ragdoll, deserializeRagdollBone(ragdoll, nullptr, blob));
	}


	void setRagdollBoneKinematic(RagdollBone* bone, bool is_kinematic) override
	{
		bone->is_kinematic = is_kinematic;
		bone->actor->isRigidBody()->setRigidBodyFlag(PxRigidBodyFlag::eKINEMATIC, is_kinematic);
	}


	void setRagdollBoneKinematicRecursive(RagdollBone* bone, bool is_kinematic) override
	{
		if (!bone) return;
		bone->is_kinematic = is_kinematic;
		bone->actor->isRigidBody()->setRigidBodyFlag(PxRigidBodyFlag::eKINEMATIC, is_kinematic);
		setRagdollBoneKinematicRecursive(bone->child, is_kinematic);
		setRagdollBoneKinematicRecursive(bone->next, is_kinematic);
	}



	bool isRagdollBoneKinematic(RagdollBone* bone) override
	{
		return bone->is_kinematic;
	}


	void changeRagdollBoneJoint(RagdollBone* child, int type) override
	{
		if (child->parent_joint) child->parent_joint->release();

		PxJointConcreteType::Enum px_type = (PxJointConcreteType::Enum)type;
		auto d1 = child->actor->getGlobalPose().q.rotate(PxVec3(1, 0, 0));
		auto d2 = child->parent->actor->getGlobalPose().q.rotate(PxVec3(1, 0, 0));
		auto axis = d1.cross(d2).getNormalized();
		auto pos = child->parent->actor->getGlobalPose().p;
		auto diff = (pos - child->actor->getGlobalPose().p).getNormalized();
		if (diff.dot(d2) < 0) d2 = -d2;

		PxShape* shape;
		if (child->parent->actor->getShapes(&shape, 1) == 1)
		{
			PxCapsuleGeometry capsule;
			if (shape->getCapsuleGeometry(capsule))
			{
				pos -= (capsule.halfHeight + capsule.radius) * d2;
			}
		}

		PxMat44 mat(d1, axis, d1.cross(axis).getNormalized(), pos);
		PxTransform tr0 = child->parent->actor->getGlobalPose().getInverse() * PxTransform(mat);
		PxTransform tr1 = child->actor->getGlobalPose().getInverse() * child->parent->actor->getGlobalPose() * tr0;

		PxJoint* joint = nullptr;
		switch (px_type)
		{
			case PxJointConcreteType::eFIXED:
				joint = PxFixedJointCreate(m_scene->getPhysics(), child->parent->actor, tr0, child->actor, tr1);
				if (joint)
				{
					auto* fixed_joint = static_cast<PxFixedJoint*>(joint);
					fixed_joint->setProjectionLinearTolerance(0.1f);
					fixed_joint->setProjectionAngularTolerance(0.01f);
				}
				break;
			case PxJointConcreteType::eREVOLUTE:
				joint =
					PxRevoluteJointCreate(m_scene->getPhysics(), child->parent->actor, tr0, child->actor, tr1);
				if (joint)
				{
					auto* hinge = static_cast<PxRevoluteJoint*>(joint);
					hinge->setProjectionLinearTolerance(0.1f);
					hinge->setProjectionAngularTolerance(0.01f);
				}
				break;
			case PxJointConcreteType::eSPHERICAL:
				joint =
					PxSphericalJointCreate(m_scene->getPhysics(), child->parent->actor, tr0, child->actor, tr1);
				if (joint)
				{
					auto* spherical = static_cast<PxSphericalJoint*>(joint);
					spherical->setProjectionLinearTolerance(0.1f);
				}
				break;
			case PxJointConcreteType::eD6:
				joint = PxD6JointCreate(m_scene->getPhysics(), child->parent->actor, tr0, child->actor, tr1);
				if (joint)
				{
					PxD6Joint* d6 = ((PxD6Joint*)joint);
					d6->setProjectionLinearTolerance(0.1f);
					d6->setProjectionAngularTolerance(0.01f);
					PxJointLinearLimit l = d6->getLinearLimit();
					l.value = 0.01f;
					d6->setLinearLimit(l);
					d6->setMotion(PxD6Axis::eSWING1, PxD6Motion::eLIMITED);
					d6->setMotion(PxD6Axis::eSWING2, PxD6Motion::eLIMITED);
					d6->setProjectionAngularTolerance(0.01f);
					d6->setProjectionLinearTolerance(0.1f);
					d6->setSwingLimit(PxJointLimitCone(Math::degreesToRadians(30), Math::degreesToRadians(30)));
				}
				break;
			default: ASSERT(false); break;
		}

		if (joint)
		{
			joint->setConstraintFlag(PxConstraintFlag::eVISUALIZATION, true);
			joint->setConstraintFlag(PxConstraintFlag::eCOLLISION_ENABLED, false);
			joint->setConstraintFlag(PxConstraintFlag::ePROJECTION, true);
		}
		child->parent_joint = joint;
	}


	void disconnect(Ragdoll& ragdoll, RagdollBone* bone)
	{
		auto* child = bone->child;
		auto* parent = bone->parent;
		if (parent && parent->child == bone) parent->child = bone->next;
		if (ragdoll.root == bone) setRagdollRoot(ragdoll, bone->next);
		if (bone->prev) bone->prev->next = bone->next;
		if (bone->next) bone->next->prev = bone->prev;

		while (child)
		{
			auto* next = child->next;

			if (child->parent_joint) child->parent_joint->release();
			child->parent_joint = nullptr;

			if (parent)
			{
				child->next = parent->child;
				child->prev = nullptr;
				if (child->next) child->next->prev = child;
				parent->child = child;
				child->parent = parent;
				changeRagdollBoneJoint(child, PxJointConcreteType::eREVOLUTE);
			}
			else
			{
				child->parent = nullptr;
				child->next = ragdoll.root;
				child->prev = nullptr;
				if (child->next) child->next->prev = child;
				setRagdollRoot(ragdoll, child);
			}
			child = next;
		}
		if (bone->parent_joint) bone->parent_joint->release();
		bone->parent_joint = nullptr;

		bone->parent = nullptr;
		bone->child = nullptr;
		bone->prev = nullptr;
		bone->next = ragdoll.root;
		if(bone->next) bone->next->prev = bone;
	}


	void connect(Ragdoll& ragdoll, RagdollBone* child, RagdollBone* parent)
	{
		ASSERT(!child->parent);
		ASSERT(!child->child);
		if (child->next) child->next->prev = child->prev;
		if (child->prev) child->prev->next = child->next;
		if (ragdoll.root == child) setRagdollRoot(ragdoll, child->next);
		child->next = parent->child;
		if (child->next) child->next->prev = child;
		parent->child = child;
		child->parent = parent;
		changeRagdollBoneJoint(child, PxJointConcreteType::eD6);
	}


	void findCloserChildren(Ragdoll& ragdoll, Entity entity, Model* model, RagdollBone* bone)
	{
		for (auto* root = ragdoll.root; root; root = root->next)
		{
			if (root == bone) continue;

			auto* tmp = getPhyParent(entity, model, root->pose_bone_idx);
			if (tmp != bone) continue;

			disconnect(ragdoll, root);
			connect(ragdoll, root, bone);
			break;
		}
		if (!bone->parent) return;

		for (auto* child = bone->parent->child; child; child = child->next)
		{
			if (child == bone) continue;

			auto* tmp = getPhyParent(entity, model, bone->pose_bone_idx);
			if (tmp != bone) continue;

			disconnect(ragdoll, child);
			connect(ragdoll, child, bone);
		}
	}

	RigidTransform getNewBoneTransform(const Model* model, int bone_idx, float& length)
	{
		auto& bone = model->getBone(bone_idx);

		length = 0.1f;
		for (int i = 0; i < model->getBoneCount(); ++i)
		{
			if (model->getBone(i).parent_idx == bone_idx)
			{
				length = (bone.transform.pos - model->getBone(i).transform.pos).length();
				break;
			}
		}

		Matrix mtx = bone.transform.toMatrix();
		if (m_new_bone_orientation == BoneOrientation::X)
		{
			Vec3 x = mtx.getXVector();
			mtx.setXVector(-mtx.getYVector());
			mtx.setYVector(x);
			mtx.setTranslation(mtx.getTranslation() - mtx.getXVector() * length * 0.5f);
			return mtx.toTransform();
		}
		mtx.setTranslation(mtx.getTranslation() + mtx.getXVector() * length * 0.5f);
		return mtx.toTransform();
	}


	BoneOrientation getNewBoneOrientation() const override { return m_new_bone_orientation; }
	void setNewBoneOrientation(BoneOrientation orientation) override { m_new_bone_orientation = orientation; }


	RagdollBone* createRagdollBone(Entity entity, u32 bone_name_hash) override
	{
		auto* render_scene = static_cast<RenderScene*>(m_universe.getScene(RENDERER_HASH));
		ASSERT(render_scene);
		
		Model* model = render_scene->getModelInstanceModel(entity);
		ASSERT(model && model->isReady());
		auto iter = model->getBoneIndex(bone_name_hash);
		ASSERT(iter.isValid());

		auto* new_bone = LUMIX_NEW(m_allocator, RagdollBone);
		new_bone->child = new_bone->next = new_bone->prev = new_bone->parent = nullptr;
		new_bone->parent_joint = nullptr;
		new_bone->is_kinematic = false;
		new_bone->pose_bone_idx = iter.value();
		
		float bone_height;
		RigidTransform transform = getNewBoneTransform(model, iter.value(), bone_height);
		
		new_bone->bind_transform = transform.inverted() * model->getBone(iter.value()).transform;
		new_bone->inv_bind_transform = new_bone->bind_transform.inverted();
		transform = m_universe.getTransform(entity).getRigidPart() * transform;

		PxCapsuleGeometry geom;
		geom.halfHeight = bone_height * 0.3f;
		if (geom.halfHeight < 0.001f) geom.halfHeight = 1.0f;
		geom.radius = geom.halfHeight * 0.5f;

		PxTransform px_transform = toPhysx(transform);
		new_bone->actor = PxCreateDynamic(m_scene->getPhysics(), px_transform, geom, *m_default_material, 1.0f);
		new_bone->actor->isRigidDynamic()->setMass(0.0001f);
		new_bone->actor->userData = (void*)(intptr_t)entity.index;
		new_bone->actor->setActorFlag(PxActorFlag::eVISUALIZATION, true);
		new_bone->actor->isRigidDynamic()->setSolverIterationCounts(8, 8);
		m_scene->addActor(*new_bone->actor);
		updateFilterData(new_bone->actor, 0);

		auto& ragdoll = m_ragdolls[entity];
		new_bone->next = ragdoll.root;
		if (new_bone->next) new_bone->next->prev = new_bone;
		setRagdollRoot(ragdoll, new_bone);
		auto* parent = getPhyParent(entity, model, iter.value());
		if (parent) connect(ragdoll, new_bone, parent);

		findCloserChildren(ragdoll, entity, model, new_bone);

		return new_bone;
	}


	void setSkeletonPose(const RigidTransform& root_transform, RagdollBone* bone, const Pose* pose)
	{
		if (!bone) return;
		
		RigidTransform bone_transform(RigidTransform(pose->positions[bone->pose_bone_idx], pose->rotations[bone->pose_bone_idx]));
		bone->actor->setGlobalPose(toPhysx(root_transform * bone_transform * bone->inv_bind_transform));

		setSkeletonPose(root_transform, bone->next, pose);
		setSkeletonPose(root_transform, bone->child, pose);
	}


	void updateBone(const RigidTransform& root_transform, const RigidTransform& inv_root, RagdollBone* bone, Pose* pose)
	{
		if (!bone) return;
			
		if (bone->is_kinematic)
		{
			RigidTransform bone_transform(RigidTransform(pose->positions[bone->pose_bone_idx], pose->rotations[bone->pose_bone_idx]));
			bone->actor->setKinematicTarget(toPhysx(root_transform * bone_transform * bone->inv_bind_transform));
		}
		else
		{
			PxTransform bone_pose = bone->actor->getGlobalPose();
			auto tr = inv_root * RigidTransform(fromPhysx(bone_pose.p), fromPhysx(bone_pose.q)) * bone->bind_transform;
			pose->rotations[bone->pose_bone_idx] = tr.rot;
			pose->positions[bone->pose_bone_idx] = tr.pos;
		}

		updateBone(root_transform, inv_root, bone->next, pose);
		updateBone(root_transform, inv_root, bone->child, pose);
	}


	void updateRagdolls()
	{
		auto* render_scene = static_cast<RenderScene*>(m_universe.getScene(RENDERER_HASH));
		if (!render_scene) return;

		for (auto& ragdoll : m_ragdolls)
		{
			Entity entity = ragdoll.entity;

			if (!render_scene->getUniverse().hasComponent(entity, MODEL_INSTANCE_TYPE)) continue;
			Pose* pose = render_scene->lockPose(entity);
			if (!pose) continue;

			RigidTransform root_transform;
			root_transform.rot = m_universe.getRotation(ragdoll.entity);
			root_transform.pos = m_universe.getPosition(ragdoll.entity);

			if (ragdoll.root && !ragdoll.root->is_kinematic)
			{
				PxTransform bone_pose = ragdoll.root->actor->getGlobalPose();
				m_is_updating_ragdoll = true;

				RigidTransform rigid_tr = fromPhysx(bone_pose) * ragdoll.root_transform;
				m_universe.setTransform(ragdoll.entity, {rigid_tr.pos, rigid_tr.rot, 1.0f});

				m_is_updating_ragdoll = false;
			}
			updateBone(root_transform, root_transform.inverted(), ragdoll.root, pose);
			render_scene->unlockPose(entity, true);
		}
	}


	void update(float time_delta, bool paused) override
	{
		if (!m_is_game_running || paused) return;
		
		applyQueuedForces();

		time_delta = Math::minimum(1 / 20.0f, time_delta);
		simulateScene(time_delta);
		fetchResults();
		updateRagdolls();
		updateDynamicActors();
		updateControllers(time_delta);
		
		render();
	}


	DelegateList<void(const ContactData&)>& onContact() override
	{
		return m_contact_callbacks;
	}
	

	void initJoint(Entity entity, Joint& joint)
	{
		PxRigidActor* actors[2] = { nullptr, nullptr };
		int idx = m_actors.find(entity);
		if (idx >= 0) actors[0] = m_actors.at(idx)->physx_actor;
		idx = m_actors.find(joint.connected_body);
		if (idx >= 0) actors[1] = m_actors.at(idx)->physx_actor;
		if (!actors[0] || !actors[1]) return;

		Vec3 pos0 = m_universe.getPosition(entity);
		Quat rot0 = m_universe.getRotation(entity);
		Vec3 pos1 = m_universe.getPosition(joint.connected_body);
		Quat rot1 = m_universe.getRotation(joint.connected_body);
		PxTransform entity0_frame(toPhysx(pos0), toPhysx(rot0));
		PxTransform entity1_frame(toPhysx(pos1), toPhysx(rot1));

		PxTransform axis_local_frame1 = entity1_frame.getInverse() * entity0_frame * joint.local_frame0;

		joint.physx->setLocalPose(PxJointActorIndex::eACTOR0, joint.local_frame0);
		joint.physx->setLocalPose(PxJointActorIndex::eACTOR1, axis_local_frame1);
		joint.physx->setActors(actors[0], actors[1]);
		joint.physx->setConstraintFlag(PxConstraintFlag::eVISUALIZATION, true);
	}


	void initJoints()
	{
		for (int i = 0, c = m_joints.size(); i < c; ++i)
		{
			Joint& joint = m_joints.at(i);
			Entity entity = m_joints.getKey(i);
			initJoint(entity, joint);
		}
	}


	void startGame() override
	{
		auto* scene = m_universe.getScene(crc32("lua_script"));
		m_script_scene = static_cast<LuaScriptScene*>(scene);
		m_is_game_running = true;

		initJoints();
	}


	void stopGame() override
	{
		m_is_game_running = false;
	}


	float getControllerRadius(Entity entity) override { return m_controllers[entity].m_radius; }
	float getControllerHeight(Entity entity) override { return m_controllers[entity].m_height; }


	void setControllerRadius(Entity entity, float value) override
	{
		if (value <= 0) return;

		Controller& ctrl = m_controllers[entity];
		ctrl.m_radius = value;

		PxRigidActor* actor = ctrl.m_controller->getActor();
		PxShape* shapes;
		if (actor->getNbShapes() == 1 && actor->getShapes(&shapes, 1))
		{
			PxCapsuleGeometry capsule;
			bool is_capsule = shapes->getCapsuleGeometry(capsule);
			ASSERT(is_capsule);
			capsule.radius = value;
			shapes->setGeometry(capsule);
		}
	}


	void setControllerHeight(Entity entity, float value) override
	{
		if (value <= 0) return;

		Controller& ctrl = m_controllers[entity];
		ctrl.m_height = value;

		PxRigidActor* actor = ctrl.m_controller->getActor();
		PxShape* shapes;
		if (actor->getNbShapes() == 1 && actor->getShapes(&shapes, 1))
		{
			PxCapsuleGeometry capsule;
			bool is_capsule = shapes->getCapsuleGeometry(capsule);
			ASSERT(is_capsule);
			capsule.halfHeight = value * 0.5f;
			shapes->setGeometry(capsule);
		}
	}


	bool isControllerTouchingDown(Entity entity) override
	{
		PxControllerState state;
		m_controllers[entity].m_controller->getState(state);
		return (state.collisionFlags & PxControllerCollisionFlag::eCOLLISION_DOWN) != 0;
	}


	void resizeController(Entity entity, float height) override
	{
		Controller& ctrl = m_controllers[entity];
		ctrl.m_height = height;
		ctrl.m_controller->resize(height);
	}


	void addForceAtPos(Entity entity, const Vec3& force, const Vec3& pos)
	{
		int index = m_actors.find(entity);
		if (index < 0) return;
		
		RigidActor* actor = m_actors.at(index);
		if (!actor->physx_actor) return;
		
		PxRigidBody* rigid_body = actor->physx_actor->isRigidBody();
		if (!rigid_body) return;

		PxRigidBodyExt::addForceAtPos(*rigid_body, toPhysx(force), toPhysx(pos));
	}


	void setRagdollKinematic(Entity entity, bool is_kinematic)
	{
		setRagdollBoneKinematicRecursive(m_ragdolls[entity].root, is_kinematic);
	}


	void moveController(Entity entity, const Vec3& v) override { m_controllers[entity].m_frame_change += v; }


	static int LUA_raycast(lua_State* L)
	{
		auto* scene = LuaWrapper::checkArg<PhysicsSceneImpl*>(L, 1);
		Vec3 origin = LuaWrapper::checkArg<Vec3>(L, 2);
		Vec3 dir = LuaWrapper::checkArg<Vec3>(L, 3);

		RaycastHit hit;
		if (scene->raycastEx(origin, dir, FLT_MAX, hit, INVALID_ENTITY))
		{
			LuaWrapper::push(L, hit.entity != INVALID_ENTITY);
			LuaWrapper::push(L, hit.entity);
			LuaWrapper::push(L, hit.position);
			return 3;
		}
		LuaWrapper::push(L, false);
		return 1;
	}


	Entity raycast(const Vec3& origin, const Vec3& dir, Entity ignore_entity) override
	{
		RaycastHit hit;
		if (raycastEx(origin, dir, FLT_MAX, hit, ignore_entity)) return hit.entity;
		return INVALID_ENTITY;
	}

	struct Filter : public PxQueryFilterCallback
	{
		PxQueryHitType::Enum preFilter(const PxFilterData& filterData,
			const PxShape* shape,
			const PxRigidActor* actor,
			PxHitFlags& queryFlags) override
		{
			if(entity.index == (int)(intptr_t)actor->userData) return PxQueryHitType::eNONE;
			return PxQueryHitType::eBLOCK;
		}


		PxQueryHitType::Enum postFilter(const PxFilterData& filterData, const PxQueryHit& hit) override
		{
			return PxQueryHitType::eBLOCK;
		}

		Entity entity;
	};


	bool raycastEx(const Vec3& origin, const Vec3& dir, float distance, RaycastHit& result, Entity ignored) override
	{
		PxVec3 physx_origin(origin.x, origin.y, origin.z);
		PxVec3 unit_dir(dir.x, dir.y, dir.z);
		PxReal max_distance = distance;

		const PxHitFlags flags =
			PxHitFlag::eDISTANCE | PxHitFlag::ePOSITION | PxHitFlag::eNORMAL;
		PxRaycastBuffer hit;
		
		Filter filter;
		filter.entity = ignored;
		PxQueryFilterData filter_data;
		filter_data.flags = PxQueryFlag::eDYNAMIC | PxQueryFlag::eSTATIC | PxQueryFlag::ePREFILTER;
		bool status =
			m_scene->raycast(physx_origin, unit_dir, max_distance, hit, flags, filter_data, &filter);
		result.normal.x = hit.block.normal.x;
		result.normal.y = hit.block.normal.y;
		result.normal.z = hit.block.normal.z;
		result.position.x = hit.block.position.x;
		result.position.y = hit.block.position.y;
		result.position.z = hit.block.position.z;
		result.entity = INVALID_ENTITY;
		if (hit.block.shape)
		{
			PxRigidActor* actor = hit.block.shape->getActor();
			if (actor) result.entity = {(int)(intptr_t)actor->userData};
		}
		return status;
	}

	void onEntityDestroyed(Entity entity)
	{
		for (int i = 0, c = m_joints.size(); i < c; ++i)
		{
			if (m_joints.at(i).connected_body == entity)
			{
				setJointConnectedBody({m_joints.getKey(i).index}, INVALID_ENTITY);
			}
		}
	}

	void onEntityMoved(Entity entity)
	{
		int ctrl_idx = m_controllers.find(entity);
		if(ctrl_idx >= 0)
		{
			auto& controller = m_controllers.at(ctrl_idx);
			Vec3 pos = m_universe.getPosition(entity);
			PxExtendedVec3 pvec(pos.x, pos.y, pos.z);
			controller.m_controller->setFootPosition(pvec);
		}

		int ragdoll_idx = m_ragdolls.find(entity);
		if (ragdoll_idx >= 0 && !m_is_updating_ragdoll)
		{
			auto* render_scene = static_cast<RenderScene*>(m_universe.getScene(RENDERER_HASH));
			if (!render_scene) return;
			if (!m_universe.hasComponent(entity, MODEL_INSTANCE_TYPE)) return;

			const Pose* pose = render_scene->lockPose(entity);
			if (!pose) return;
			setSkeletonPose(m_universe.getTransform(entity).getRigidPart(), m_ragdolls.at(ragdoll_idx).root, pose);
			render_scene->unlockPose(entity, false);
		}

		int idx = m_actors.find(entity);
		if(idx >= 0)
		{
			RigidActor* actor = m_actors.at(idx);
			if (actor->physx_actor && m_update_in_progress != actor)
			{
				Transform trans = m_universe.getTransform(entity);
				if (actor->dynamic_type == DynamicType::KINEMATIC)
				{
					auto* rigid_dynamic = (PxRigidDynamic*)actor->physx_actor;
					rigid_dynamic->setKinematicTarget(toPhysx(trans.getRigidPart()));
				}
				else
				{
					actor->physx_actor->setGlobalPose(toPhysx(trans.getRigidPart()), false);
				}
				if (actor->resource && actor->scale != trans.scale)
				{
					actor->rescale();
				}
			}
		}
	}


	void heightmapLoaded(Heightfield& terrain)
	{
		PROFILE_FUNCTION();
		Array<PxHeightFieldSample> heights(m_allocator);

		int width = terrain.m_heightmap->width;
		int height = terrain.m_heightmap->height;
		heights.resize(width * height);
		int bytes_per_pixel = terrain.m_heightmap->bytes_per_pixel;
		if (bytes_per_pixel == 2)
		{
			PROFILE_BLOCK("copyData");
			const i16* LUMIX_RESTRICT data = (const i16*)terrain.m_heightmap->getData();
			for (int j = 0; j < height; ++j)
			{
				int idx = j * width;
				for (int i = 0; i < width; ++i)
				{
					int idx2 = j + i * height;
					heights[idx].height = PxI16((i32)data[idx2] - 0x7fff);
					heights[idx].materialIndex0 = heights[idx].materialIndex1 = 0;
					heights[idx].setTessFlag();
					++idx;
				}
			}
		}
		else
		{
			PROFILE_BLOCK("copyData");
			const u8* data = terrain.m_heightmap->getData();
			for (int j = 0; j < height; ++j)
			{
				for (int i = 0; i < width; ++i)
				{
					int idx = i + j * width;
					int idx2 = j + i * height;
					heights[idx].height = PxI16((i32)data[idx2 * bytes_per_pixel] - 0x7f);
					heights[idx].materialIndex0 = heights[idx].materialIndex1 = 0;
					heights[idx].setTessFlag();
				}
			}
		}

		{ // PROFILE_BLOCK scope
			PROFILE_BLOCK("PhysX");
			PxHeightFieldDesc hfDesc;
			hfDesc.format = PxHeightFieldFormat::eS16_TM;
			hfDesc.nbColumns = width;
			hfDesc.nbRows = height;
			hfDesc.samples.data = &heights[0];
			hfDesc.samples.stride = sizeof(PxHeightFieldSample);
			hfDesc.thickness = -1;

			PxHeightField* heightfield = m_system->getPhysics()->createHeightField(hfDesc);
			float height_scale = bytes_per_pixel == 2 ? 1 / (256 * 256.0f - 1) : 1 / 255.0f;
			PxHeightFieldGeometry hfGeom(heightfield,
				PxMeshGeometryFlags(),
				height_scale * terrain.m_y_scale,
				terrain.m_xz_scale,
				terrain.m_xz_scale);
			if (terrain.m_actor)
			{
				PxRigidActor* actor = terrain.m_actor;
				m_scene->removeActor(*actor);
				actor->release();
				terrain.m_actor = nullptr;
			}

			PxTransform transform = toPhysx(m_universe.getTransform(terrain.m_entity).getRigidPart());
			transform.p.y += terrain.m_y_scale * 0.5f;

			PxRigidActor* actor = PxCreateStatic(*m_system->getPhysics(), transform, hfGeom, *m_default_material);
			if (actor)
			{
				actor->userData = (void*)(intptr_t)terrain.m_entity.index;
				m_scene->addActor(*actor);
				terrain.m_actor = actor;

				PxFilterData data;
				int terrain_layer = terrain.m_layer;
				data.word0 = 1 << terrain_layer;
				data.word1 = m_collision_filter[terrain_layer];
				PxShape* shapes[8];
				int shapes_count = actor->getShapes(shapes, lengthOf(shapes));
				for (int i = 0; i < shapes_count; ++i)
				{
					shapes[i]->setSimulationFilterData(data);
				}
				terrain.m_actor->setActorFlag(PxActorFlag::eVISUALIZATION, true);
			}
			else
			{
				g_log_error.log("Physics") << "Could not create PhysX heightfield " << terrain.m_heightmap->getPath();
			}
		}
	}


	void addCollisionLayer() override 
	{
		m_layers_count = Math::minimum(lengthOf(m_layers_names), m_layers_count + 1);
	}


	void removeCollisionLayer() override
	{
		m_layers_count = Math::maximum(0, m_layers_count - 1);
		for (auto* actor : m_actors)
		{
			actor->layer = Math::minimum(m_layers_count - 1, actor->layer);
		}
		for (auto& controller : m_controllers)
		{
			controller.m_layer = Math::minimum(m_layers_count - 1, controller.m_layer);
		}
		for (auto& terrain : m_terrains)
		{
			terrain.m_layer = Math::minimum(m_layers_count - 1, terrain.m_layer);
		}

		updateFilterData();
	}


	void setCollisionLayerName(int index, const char* name) override
	{
		copyString(m_layers_names[index], name);
	}


	const char* getCollisionLayerName(int index) override
	{
		return m_layers_names[index];
	}


	bool canLayersCollide(int layer1, int layer2) override
	{
		return (m_collision_filter[layer1] & (1 << layer2)) != 0;
	}


	void setLayersCanCollide(int layer1, int layer2, bool can_collide) override
	{
		if (can_collide)
		{
			m_collision_filter[layer1] |= 1 << layer2;
			m_collision_filter[layer2] |= 1 << layer1;
		}
		else
		{
			m_collision_filter[layer1] &= ~(1 << layer2);
			m_collision_filter[layer2] &= ~(1 << layer1);
		}

		updateFilterData();
	}


	void updateFilterData(PxRigidActor* actor, int layer)
	{
		PxFilterData data;
		data.word0 = 1 << layer;
		data.word1 = m_collision_filter[layer];
		PxShape* shapes[8];
		int shapes_count = actor->getShapes(shapes, lengthOf(shapes));
		for (int i = 0; i < shapes_count; ++i)
		{
			shapes[i]->setSimulationFilterData(data);
		}
	}


	void updateFilterData()
	{
		for (auto& ragdoll : m_ragdolls)
		{
			struct Tmp
			{
				void operator()(RagdollBone* bone)
				{
					if (!bone) return;
					int shapes_count = bone->actor->getShapes(shapes, lengthOf(shapes));
					for (int i = 0; i < shapes_count; ++i)
					{
						shapes[i]->setSimulationFilterData(data);
					}
					(*this)(bone->child);
					(*this)(bone->next);
				}
				PxShape* shapes[8];
				PxFilterData data;
			};

			Tmp tmp;
			int layer = ragdoll.layer;
			tmp.data.word0 = 1 << layer;
			tmp.data.word1 = m_collision_filter[layer];
			tmp(ragdoll.root);
		}

		for (auto* actor : m_actors)
		{
			if (!actor->physx_actor) continue;
			PxFilterData data;
			int actor_layer = actor->layer;
			data.word0 = 1 << actor_layer;
			data.word1 = m_collision_filter[actor_layer];
			PxShape* shapes[8];
			int shapes_count = actor->physx_actor->getShapes(shapes, lengthOf(shapes));
			for (int i = 0; i < shapes_count; ++i)
			{
				shapes[i]->setSimulationFilterData(data);
			}
		}

		for (auto& controller : m_controllers)
		{
			PxFilterData data;
			int controller_layer = controller.m_layer;
			data.word0 = 1 << controller_layer;
			data.word1 = m_collision_filter[controller_layer];
			controller.m_filter_data = data;
			PxShape* shapes[8];
			int shapes_count = controller.m_controller->getActor()->getShapes(shapes, lengthOf(shapes));
			for (int i = 0; i < shapes_count; ++i)
			{
				shapes[i]->setSimulationFilterData(data);
			}
			controller.m_controller->invalidateCache();
		}

		for (auto& terrain : m_terrains)
		{
			if (!terrain.m_actor) continue;

			PxFilterData data;
			int terrain_layer = terrain.m_layer;
			data.word0 = 1 << terrain_layer;
			data.word1 = m_collision_filter[terrain_layer];
			PxShape* shapes[8];
			int shapes_count = terrain.m_actor->getShapes(shapes, lengthOf(shapes));
			for (int i = 0; i < shapes_count; ++i)
			{
				shapes[i]->setSimulationFilterData(data);
			}
		}
	}


	int getCollisionsLayersCount() const override
	{
		return m_layers_count;
	}


	bool getIsTrigger(Entity entity) override
	{
		return m_actors[entity]->is_trigger;
	}


	void setIsTrigger(Entity entity, bool is_trigger) override
	{
		RigidActor* actor = m_actors[entity];
		actor->is_trigger = is_trigger;
		if (actor->physx_actor)
		{
			PxShape* shape;
			if (actor->physx_actor->getShapes(&shape, 1) == 1)
			{
				if (is_trigger)
				{
					shape->setFlag(PxShapeFlag::eSIMULATION_SHAPE, false); // must set false first
					shape->setFlag(PxShapeFlag::eTRIGGER_SHAPE, true);
				}
				else
				{
					shape->setFlag(PxShapeFlag::eTRIGGER_SHAPE, false);
					shape->setFlag(PxShapeFlag::eSIMULATION_SHAPE, true);
				}
			}
		}
	}


	DynamicType getDynamicType(Entity entity) override
	{
		return m_actors[entity]->dynamic_type;
	}


	Vec3 getHalfExtents(Entity entity) override
	{
		Vec3 size;
		PxRigidActor* actor = m_actors[entity]->physx_actor;
		PxShape* shapes;
		if (actor->getNbShapes() == 1 && actor->getShapes(&shapes, 1))
		{
			PxVec3& half = shapes->getGeometry().box().halfExtents;
			size.x = half.x;
			size.y = half.y;
			size.z = half.z;
		}
		return size;
	}


	void moveShapeIndices(Entity entity, int index, PxGeometryType::Enum type)
	{
		int count = getGeometryCount(entity, type);
		for (int i = index; i < count; ++i)
		{
			PxShape* shape = getShape(entity, i, type);
			shape->userData = (void*)(intptr_t)(i + 1);
		}
	}


	void addBoxGeometry(Entity entity, int index) override
	{
		if (index == -1) index = getBoxGeometryCount(entity);
		moveShapeIndices(entity, index, PxGeometryType::eBOX);
		PxRigidActor* actor = m_actors[entity]->physx_actor;
		PxBoxGeometry geom;
		geom.halfExtents.x = 1;
		geom.halfExtents.y = 1;
		geom.halfExtents.z = 1;
		PxShape* shape = actor->createShape(geom, *m_default_material);
		shape->userData = (void*)(intptr_t)index;
	}


	void removeGeometry(Entity entity, int index, PxGeometryType::Enum type)
	{
		int count = getGeometryCount(entity, type);
		PxShape* shape = getShape(entity, index, type);
		shape->getActor()->detachShape(*shape);

		for (int i = index + 1; i < count; ++i)
		{
			PxShape* shape = getShape(entity, i, type);
			shape->userData = (void*)(intptr_t)(i - 1);
		}
	}


	void removeBoxGeometry(Entity entity, int index) override
	{
		removeGeometry(entity, index, PxGeometryType::eBOX);
	}


	Vec3 getBoxGeomHalfExtents(Entity entity, int index) override
	{
		PxShape* shape = getShape(entity, index, PxGeometryType::eBOX);
		PxBoxGeometry box = shape->getGeometry().box();
		return fromPhysx(box.halfExtents);
	}


	PxShape* getShape(Entity entity, int index, PxGeometryType::Enum type)
	{
		PxRigidActor* actor = m_actors[entity]->physx_actor;
		int shape_count = actor->getNbShapes();
		PxShape* shape;
		for (int i = 0; i < shape_count; ++i)
		{
			actor->getShapes(&shape, 1, i);
			if (shape->getGeometryType() == type)
			{
				if (shape->userData == (void*)(intptr_t)index)
				{
					return shape;
				}
			}
		}
		ASSERT(false);
		return nullptr;
	}


	void setBoxGeomHalfExtents(Entity entity, int index, const Vec3& size) override
	{
		PxShape* shape = getShape(entity, index, PxGeometryType::eBOX);
		PxBoxGeometry box = shape->getGeometry().box();
		box.halfExtents = toPhysx(size);
		shape->setGeometry(box);
	}


	Vec3 getGeomOffsetPosition(Entity entity, int index, PxGeometryType::Enum type)
	{
		PxShape* shape = getShape(entity, index, type);
		PxTransform tr = shape->getLocalPose();
		return fromPhysx(tr.p);
	}


	Vec3 getGeomOffsetRotation(Entity entity, int index, PxGeometryType::Enum type)
	{
		PxShape* shape = getShape(entity, index, type);
		PxTransform tr = shape->getLocalPose();
		return fromPhysx(tr.q).toEuler();
	}


	Vec3 getBoxGeomOffsetRotation(Entity entity, int index) override
	{
		return getGeomOffsetRotation(entity, index, PxGeometryType::eBOX);
	}


	Vec3 getBoxGeomOffsetPosition(Entity entity, int index) override
	{
		return getGeomOffsetPosition(entity, index, PxGeometryType::eBOX);
	}


	void setGeomOffsetPosition(Entity entity, int index, const Vec3& pos, PxGeometryType::Enum type)
	{
		PxShape* shape = getShape(entity, index, type);
		PxTransform tr = shape->getLocalPose();
		tr.p = toPhysx(pos);
		shape->setLocalPose(tr);
	}


	void setGeomOffsetRotation(Entity entity, int index, const Vec3& rot, PxGeometryType::Enum type)
	{
		PxShape* shape = getShape(entity, index, type);
		PxTransform tr = shape->getLocalPose();
		Quat q;
		q.fromEuler(rot);
		tr.q = toPhysx(q);
		shape->setLocalPose(tr);
	}


	void setBoxGeomOffsetPosition(Entity entity, int index, const Vec3& pos) override
	{
		setGeomOffsetPosition(entity, index, pos, PxGeometryType::eBOX);
	}


	void setBoxGeomOffsetRotation(Entity entity, int index, const Vec3& rot) override
	{
		setGeomOffsetRotation(entity, index, rot, PxGeometryType::eBOX);
	}


	int getGeometryCount(Entity entity, PxGeometryType::Enum type)
	{
		PxRigidActor* actor = m_actors[entity]->physx_actor;
		int shape_count = actor->getNbShapes();
		PxShape* shape;
		int count = 0;
		for (int i = 0; i < shape_count; ++i)
		{
			actor->getShapes(&shape, 1, i);
			if (shape->getGeometryType() == type) ++count;
		}
		return count;
	}


	int getBoxGeometryCount(Entity entity) override
	{
		return getGeometryCount(entity, PxGeometryType::eBOX);
	}


	void addSphereGeometry(Entity entity, int index) override 
	{
		if (index == -1) index = getSphereGeometryCount(entity);
		moveShapeIndices(entity, index, PxGeometryType::eSPHERE);
		PxRigidActor* actor = m_actors[entity]->physx_actor;
		PxSphereGeometry geom;
		geom.radius = 1;
		PxShape* shape = actor->createShape(geom, *m_default_material);
		shape->userData = (void*)(intptr_t)index;
	}


	void removeSphereGeometry(Entity entity, int index) override 
	{
		removeGeometry(entity, index, PxGeometryType::eSPHERE);
	}
	
	
	int getSphereGeometryCount(Entity entity) override 
	{
		return getGeometryCount(entity, PxGeometryType::eSPHERE);
	}


	float getSphereGeomRadius(Entity entity, int index) override
	{
		PxShape* shape = getShape(entity, index, PxGeometryType::eSPHERE);
		PxSphereGeometry geom = shape->getGeometry().sphere();
		return geom.radius;
	}


	void setSphereGeomRadius(Entity entity, int index, float radius) override
	{
		PxShape* shape = getShape(entity, index, PxGeometryType::eSPHERE);
		PxSphereGeometry geom = shape->getGeometry().sphere();
		geom.radius = radius;
		shape->setGeometry(geom);
	}


	Vec3 getSphereGeomOffsetPosition(Entity entity, int index) override
	{
		return getGeomOffsetPosition(entity, index, PxGeometryType::eSPHERE);
	}


	void setSphereGeomOffsetPosition(Entity entity, int index, const Vec3& pos) override
	{
		setGeomOffsetPosition(entity, index, pos, PxGeometryType::eSPHERE);
	}


	Vec3 getSphereGeomOffsetRotation(Entity entity, int index) override
	{
		return getGeomOffsetRotation(entity, index, PxGeometryType::eSPHERE);
	}


	void setSphereGeomOffsetRotation(Entity entity, int index, const Vec3& euler_angles) override
	{
		setGeomOffsetRotation(entity, index, euler_angles, PxGeometryType::eSPHERE);
	}


	void setHalfExtents(Entity entity, const Vec3& size) override
	{
		PxRigidActor* actor = m_actors[entity]->physx_actor;
		PxShape* shapes;
		if (actor->getNbShapes() == 1 && actor->getShapes(&shapes, 1))
		{
			PxBoxGeometry box;
			bool is_box = shapes->getBoxGeometry(box);
			ASSERT(is_box);
			PxVec3& half = box.halfExtents;
			half.x = Math::maximum(0.01f, size.x);
			half.y = Math::maximum(0.01f, size.y);
			half.z = Math::maximum(0.01f, size.z);
			shapes->setGeometry(box);
		}
	}


	void setDynamicType(Entity entity, DynamicType new_value) override
	{
		RigidActor* actor = m_actors[entity];
		if (actor->dynamic_type == new_value) return;

		if (actor->type == ActorType::MESH && new_value != DynamicType::STATIC && actor->physx_actor)
		{
			PxShape* shape;
			if (actor->physx_actor->getShapes(&shape, 1, 0) == 1)
			{
				PxTriangleMeshGeometry geom;
				if (shape->getTriangleMeshGeometry(geom))
				{
					g_log_error.log("Physics") << "Triangles mesh can only be static";
					return;
				}
			}
		}

		actor->dynamic_type = new_value;
		if (new_value == DynamicType::DYNAMIC)
		{
			m_dynamic_actors.push(actor);
		}
		else
		{
			m_dynamic_actors.eraseItemFast(actor);
		}
		if (!actor->physx_actor) return;
		
		PxTransform transform = toPhysx(m_universe.getTransform(actor->entity).getRigidPart());
		PxRigidActor* new_physx_actor;
		switch (actor->dynamic_type)
		{
			case DynamicType::DYNAMIC:
				new_physx_actor = m_system->getPhysics()->createRigidDynamic(transform);
				break;
			case DynamicType::KINEMATIC:
				new_physx_actor = m_system->getPhysics()->createRigidDynamic(transform);
				new_physx_actor->isRigidBody()->setRigidBodyFlag(PxRigidBodyFlag::eKINEMATIC, true);
				break;
			case DynamicType::STATIC:
				new_physx_actor = m_system->getPhysics()->createRigidStatic(transform);
				break;
		}
		for(int i = 0, c = actor->physx_actor->getNbShapes(); i < c; ++i)
		{
			PxShape* shape;
			actor->physx_actor->getShapes(&shape, 1, i);
			duplicateShape(shape, new_physx_actor);
		}
		PxRigidBodyExt::updateMassAndInertia(*new_physx_actor->isRigidBody(), 1);
		actor->setPhysxActor(new_physx_actor);
	}


	void duplicateShape(PxShape* shape, PxRigidActor* actor)
	{
		PxShape* new_shape;
		switch (shape->getGeometryType())
		{
			case PxGeometryType::eBOX:
				{
					PxBoxGeometry geom;
					shape->getBoxGeometry(geom);
					new_shape = actor->createShape(geom, *m_default_material, shape->getLocalPose());
					break;
				}
			case PxGeometryType::eSPHERE:
				{
					PxSphereGeometry geom;
					shape->getSphereGeometry(geom);
					new_shape = actor->createShape(geom, *m_default_material, shape->getLocalPose());
					break;
				}
			case PxGeometryType::eCONVEXMESH:
				{
					PxConvexMeshGeometry geom;
					shape->getConvexMeshGeometry(geom);
					new_shape = actor->createShape(geom, *m_default_material, shape->getLocalPose());
					break;
				}
			default:
				ASSERT(false);
				return;
		}
		new_shape->userData = shape->userData;
	}


	void serialize(ISerializer& serializer) override
	{
		serializer.write("layers_count", m_layers_count);
		for (int i = 0; i < m_layers_count; ++i)
		{
			serializer.write("name", m_layers_names[i]);
			serializer.write("collision_matrix", m_collision_filter[i]);
		}
	}


	void deserialize(IDeserializer& serializer) override
	{
		serializer.read(&m_layers_count);
		for (int i = 0; i < m_layers_count; ++i)
		{
			serializer.read(m_layers_names[i], lengthOf(m_layers_names[i]));
			serializer.read(&m_collision_filter[i]);
		}
	}


	void serializeHeightfield(ISerializer& serializer, Entity entity)
	{
		Heightfield& terrain = m_terrains[entity];
		serializer.write("heightmap", terrain.m_heightmap ? terrain.m_heightmap->getPath().c_str() : "");
		serializer.write("xz_scale", terrain.m_xz_scale);
		serializer.write("y_scale", terrain.m_y_scale);
		serializer.write("layer", terrain.m_layer);
	}

	void deserializeHeightfield(IDeserializer& serializer, Entity entity, int /*scene_version*/)
	{
		Heightfield terrain;
		terrain.m_scene = this;
		terrain.m_entity = entity;
		char tmp[MAX_PATH_LENGTH];
		serializer.read(tmp, MAX_PATH_LENGTH);
		serializer.read(&terrain.m_xz_scale);
		serializer.read(&terrain.m_y_scale);
		serializer.read(&terrain.m_layer);

		m_terrains.insert(terrain.m_entity, terrain);
		if (terrain.m_heightmap == nullptr || !equalStrings(tmp, terrain.m_heightmap->getPath().c_str()))
		{
			setHeightmapSource(terrain.m_entity, Path(tmp));
		}
		m_universe.onComponentCreated(terrain.m_entity, HEIGHTFIELD_TYPE, this);
	}


	void serializeController(ISerializer& serializer, Entity entity)
	{
		Controller& controller = m_controllers[entity];
		serializer.write("layer", controller.m_layer);
		serializer.write("radius", controller.m_radius);
		serializer.write("height", controller.m_height);
	}


	void deserializeController(IDeserializer& serializer, Entity entity, int /*scene_version*/)
	{
		Controller& c = m_controllers.insert(entity);
		c.m_frame_change.set(0, 0, 0);

		serializer.read(&c.m_layer);
		serializer.read(&c.m_radius);
		serializer.read(&c.m_height);

		PxCapsuleControllerDesc cDesc;
		initControllerDesc(cDesc);
		cDesc.height = c.m_height;
		cDesc.radius = c.m_radius;
		Vec3 position = m_universe.getPosition(entity);
		cDesc.position.set(position.x, position.y - cDesc.height * 0.5f, position.z);
		c.m_controller = m_controller_manager->createController(cDesc);
		c.m_controller->getActor()->userData = (void*)(intptr_t)entity.index;
		c.m_entity = entity;
		
		PxFilterData data;
		int controller_layer = c.m_layer;
		data.word0 = 1 << controller_layer;
		data.word1 = m_collision_filter[controller_layer];
		c.m_filter_data = data;
		PxShape* shapes[8];
		int shapes_count = c.m_controller->getActor()->getShapes(shapes, lengthOf(shapes));
		for (int i = 0; i < shapes_count; ++i)
		{
			shapes[i]->setSimulationFilterData(data);
		}
		c.m_controller->invalidateCache();
		c.m_controller->setFootPosition({position.x, position.y, position.z});

		m_universe.onComponentCreated(entity, CONTROLLER_TYPE, this);
	}


	void serializeRagdoll(ISerializer& serializer, Entity entity)
	{
		const Ragdoll& ragdoll = m_ragdolls[entity];
		serializer.write("layer", ragdoll.layer);
		
		serializeRagdollBone(ragdoll, ragdoll.root, serializer);
	}


	void deserializeRagdoll(IDeserializer& serializer, Entity entity, int /*scene_version*/)
	{
		Ragdoll& ragdoll = m_ragdolls.insert(entity);
		
		ragdoll.entity = entity;
		ragdoll.root_transform.pos.set(0, 0, 0);
		ragdoll.root_transform.rot.set(0, 0, 0, 1);
		serializer.read(&ragdoll.layer);

		setRagdollRoot(ragdoll, deserializeRagdollBone(ragdoll, nullptr, serializer));
		m_universe.onComponentCreated(ragdoll.entity, RAGDOLL_TYPE, this);
	}
	

	static void serializeJoint(ISerializer& serializer, PxSphericalJoint* px_joint)
	{
		u32 flags = (u32)px_joint->getSphericalJointFlags();
		serializer.write("flags", flags);
		PxJointLimitCone limit = px_joint->getLimitCone();
		serializer.write("bounce_threshold", limit.bounceThreshold);
		serializer.write("contact_distance", limit.contactDistance);
		serializer.write("damping", limit.damping);
		serializer.write("restitution", limit.restitution);
		serializer.write("stiffness", limit.stiffness);
		serializer.write("y_angle", limit.yAngle);
		serializer.write("z_angle", limit.zAngle);
	}


	void serializeSphericalJoint(ISerializer& serializer, Entity entity)
	{
		Joint& joint = m_joints[entity];
		serializer.write("connected_body", joint.connected_body);
		RigidTransform tr = fromPhysx(joint.local_frame0);
		serializer.write("local_frame", tr);
		serializeJoint(serializer, static_cast<PxSphericalJoint*>(joint.physx));
	}
	

	static void deserializeJoint(IDeserializer& serializer, PxSphericalJoint* px_joint)
	{
		u32 flags;
		serializer.read(&flags);
		px_joint->setSphericalJointFlags(PxSphericalJointFlags(flags));
		PxJointLimitCone limit(0, 0);
		serializer.read(&limit.bounceThreshold);
		serializer.read(&limit.contactDistance);
		serializer.read(&limit.damping);
		serializer.read(&limit.restitution);
		serializer.read(&limit.stiffness);
		serializer.read(&limit.yAngle);
		serializer.read(&limit.zAngle);
		px_joint->setLimitCone(limit);
	}


	void deserializeSphericalJoint(IDeserializer& serializer, Entity entity, int /*scene_version*/)
	{
		Joint& joint = m_joints.insert(entity);
		serializer.read(&joint.connected_body);
		RigidTransform tr;
		serializer.read(&tr);
		joint.local_frame0 = toPhysx(tr);
		auto* px_joint = PxSphericalJointCreate(m_scene->getPhysics(),
			m_dummy_actor,
			joint.local_frame0,
			nullptr,
			PxTransform::createIdentity());
		joint.physx = px_joint;
		deserializeJoint(serializer, px_joint);
		m_universe.onComponentCreated(entity, SPHERICAL_JOINT_TYPE, this);
	}


	static void serializeJoint(ISerializer& serializer, PxDistanceJoint* px_joint)
	{
		u32 flags = (u32)px_joint->getDistanceJointFlags();
		serializer.write("flags", flags);
		serializer.write("damping", px_joint->getDamping());
		serializer.write("stiffness", px_joint->getStiffness());
		serializer.write("tolerance", px_joint->getTolerance());
		serializer.write("min_distance", px_joint->getMinDistance());
		serializer.write("max_distance", px_joint->getMaxDistance());
	}


	void serializeDistanceJoint(ISerializer& serializer, Entity entity)
	{
		Joint& joint = m_joints[entity];
		serializer.write("connected_body", joint.connected_body);
		RigidTransform tr = fromPhysx(joint.local_frame0);
		serializer.write("local_frame", tr);
		serializeJoint(serializer, static_cast<PxDistanceJoint*>(joint.physx));
	}


	static void deserializeJoint(IDeserializer& serializer, PxDistanceJoint* px_joint)
	{
		u32 flags;
		serializer.read(&flags);
		px_joint->setDistanceJointFlags((PxDistanceJointFlags)flags);
		PxReal value;
		serializer.read(&value);
		px_joint->setDamping(value);
		serializer.read(&value);
		px_joint->setStiffness(value);
		serializer.read(&value);
		px_joint->setTolerance(value);
		serializer.read(&value);
		px_joint->setMinDistance(value);
		serializer.read(&value);
		px_joint->setMaxDistance(value);
	}


	void deserializeDistanceJoint(IDeserializer& serializer, Entity entity, int /*scene_version*/)
	{
		Joint& joint = m_joints.insert(entity);
		serializer.read(&joint.connected_body);
		RigidTransform tr;
		serializer.read(&tr);
		joint.local_frame0 = toPhysx(tr);
		auto* px_joint = PxDistanceJointCreate(m_scene->getPhysics(),
			m_dummy_actor,
			joint.local_frame0,
			nullptr,
			PxTransform::createIdentity());
		joint.physx = px_joint;
		deserializeJoint(serializer, px_joint);
		m_universe.onComponentCreated(entity, DISTANCE_JOINT_TYPE, this);
	}


	static void serializeJoint(ISerializer& serializer, PxD6Joint* px_joint)
	{
		serializer.write("z", (int)px_joint->getMotion(PxD6Axis::eX));
		serializer.write("y", (int)px_joint->getMotion(PxD6Axis::eY));
		serializer.write("z", (int)px_joint->getMotion(PxD6Axis::eZ));
		serializer.write("swing1", (int)px_joint->getMotion(PxD6Axis::eSWING1));
		serializer.write("swing2", (int)px_joint->getMotion(PxD6Axis::eSWING2));
		serializer.write("twist", (int)px_joint->getMotion(PxD6Axis::eTWIST));

		PxJointLinearLimit linear = px_joint->getLinearLimit();
		serializer.write("bounce_threshold", linear.bounceThreshold);
		serializer.write("contact_distance", linear.contactDistance);
		serializer.write("damping", linear.damping);
		serializer.write("restitution", linear.restitution);
		serializer.write("stiffness", linear.stiffness);
		serializer.write("value", linear.value);

		PxJointLimitCone swing = px_joint->getSwingLimit();
		serializer.write("bounce_threshold", swing.bounceThreshold);
		serializer.write("contact_distance", swing.contactDistance);
		serializer.write("damping", swing.damping);
		serializer.write("restitution", swing.restitution);
		serializer.write("stiffness", swing.stiffness);
		serializer.write("y_angle", swing.yAngle);
		serializer.write("z_angle", swing.zAngle);

		PxJointAngularLimitPair twist = px_joint->getTwistLimit();
		serializer.write("bounce_threshold", twist.bounceThreshold);
		serializer.write("contact_distance", twist.contactDistance);
		serializer.write("damping", twist.damping);
		serializer.write("restitution", twist.restitution);
		serializer.write("stiffness", twist.stiffness);
		serializer.write("lower", twist.lower);
		serializer.write("upper", twist.upper);
	}


	void serializeD6Joint(ISerializer& serializer, Entity entity)
	{
		Joint& joint = m_joints[entity];
		serializer.write("connected_body", joint.connected_body);
		RigidTransform tr = fromPhysx(joint.local_frame0);
		serializer.write("local_frame", tr);
		serializeJoint(serializer, static_cast<PxD6Joint*>(joint.physx));
	}


	static void deserializeJoint(IDeserializer& serializer, PxD6Joint* px_joint)
	{
		int tmp;
		serializer.read(&tmp); 	px_joint->setMotion(PxD6Axis::eX, (PxD6Motion::Enum)tmp);
		serializer.read(&tmp); 	px_joint->setMotion(PxD6Axis::eY, (PxD6Motion::Enum)tmp);
		serializer.read(&tmp); 	px_joint->setMotion(PxD6Axis::eZ, (PxD6Motion::Enum)tmp);
		serializer.read(&tmp); 	px_joint->setMotion(PxD6Axis::eSWING1, (PxD6Motion::Enum)tmp);
		serializer.read(&tmp); 	px_joint->setMotion(PxD6Axis::eSWING2, (PxD6Motion::Enum)tmp);
		serializer.read(&tmp); 	px_joint->setMotion(PxD6Axis::eTWIST, (PxD6Motion::Enum)tmp);

		PxJointLinearLimit linear(0, PxSpring(0, 0));
		serializer.read(&linear.bounceThreshold);
		serializer.read(&linear.contactDistance);
		serializer.read(&linear.damping);
		serializer.read(&linear.restitution);
		serializer.read(&linear.stiffness);
		serializer.read(&linear.value);
		px_joint->setLinearLimit(linear);

		PxJointLimitCone swing(0, 0);
		serializer.read(&swing.bounceThreshold);
		serializer.read(&swing.contactDistance);
		serializer.read(&swing.damping);
		serializer.read(&swing.restitution);
		serializer.read(&swing.stiffness);
		serializer.read(&swing.yAngle);
		serializer.read(&swing.zAngle);
		px_joint->setSwingLimit(swing);

		PxJointAngularLimitPair twist(0, 0);
		serializer.read(&twist.bounceThreshold);
		serializer.read(&twist.contactDistance);
		serializer.read(&twist.damping);
		serializer.read(&twist.restitution);
		serializer.read(&twist.stiffness);
		serializer.read(&twist.lower);
		serializer.read(&twist.upper);
		px_joint->setTwistLimit(twist);
	}


	void deserializeD6Joint(IDeserializer& serializer, Entity entity, int /*scene_version*/)
	{
		Joint& joint = m_joints.insert(entity);
		serializer.read(&joint.connected_body);
		RigidTransform tr;
		serializer.read(&tr);
		joint.local_frame0 = toPhysx(tr);
		auto* px_joint = PxD6JointCreate(m_scene->getPhysics(),
			m_dummy_actor,
			joint.local_frame0,
			nullptr,
			PxTransform::createIdentity());
		joint.physx = px_joint;

		deserializeJoint(serializer, px_joint);

		m_universe.onComponentCreated(entity, D6_JOINT_TYPE, this);
	}


	static void serializeJoint(ISerializer& serializer, PxRevoluteJoint* px_joint)
	{
		u32 flags = (u32)px_joint->getRevoluteJointFlags();
		serializer.write("flags", flags);
		PxJointAngularLimitPair limit = px_joint->getLimit();
		serializer.write("bounce_threshold", limit.bounceThreshold);
		serializer.write("contact_distance", limit.contactDistance);
		serializer.write("damping", limit.damping);
		serializer.write("restitution", limit.restitution);
		serializer.write("stiffness", limit.stiffness);
		serializer.write("lower", limit.lower);
		serializer.write("upper", limit.upper);
	}


	void serializeHingeJoint(ISerializer& serializer, Entity entity)
	{
		Joint& joint = m_joints[entity];
		serializer.write("connected_body", joint.connected_body);
		RigidTransform tr = fromPhysx(joint.local_frame0);
		serializer.write("local_frame", tr);
		serializeJoint(serializer, static_cast<PxRevoluteJoint*>(joint.physx));
	}


	static void deserializeJoint(IDeserializer& serializer, PxRevoluteJoint* px_joint)
	{
		u32 flags;
		serializer.read(&flags);
		px_joint->setRevoluteJointFlags(PxRevoluteJointFlags(flags));
		PxJointAngularLimitPair limit(0, 0);
		serializer.read(&limit.bounceThreshold);
		serializer.read(&limit.contactDistance);
		serializer.read(&limit.damping);
		serializer.read(&limit.restitution);
		serializer.read(&limit.stiffness);
		serializer.read(&limit.lower);
		serializer.read(&limit.upper);
		px_joint->setLimit(limit);
	}


	void deserializeHingeJoint(IDeserializer& serializer, Entity entity, int /*scene_version*/)
	{
		Joint& joint = m_joints.insert(entity);
		serializer.read(&joint.connected_body);
		RigidTransform tr;
		serializer.read(&tr);
		joint.local_frame0 = toPhysx(tr);
		auto* px_joint = PxRevoluteJointCreate(m_scene->getPhysics(),
			m_dummy_actor,
			joint.local_frame0,
			nullptr,
			PxTransform::createIdentity());
		joint.physx = px_joint;
		deserializeJoint(serializer, px_joint);
		m_universe.onComponentCreated(entity, HINGE_JOINT_TYPE, this);
	}


	void serializeMeshActor(ISerializer& serializer, Entity entity)
	{
		RigidActor* actor = m_actors[entity];
		serializer.write("layer", actor->layer);
		serializer.write("dynamic_type", (int)actor->dynamic_type);
		serializer.write("trigger", actor->is_trigger);
		serializer.write("source", actor->resource ? actor->resource->getPath().c_str() : "");
	}


	void deserializeCommonRigidActorProperties(IDeserializer& serializer, RigidActor* actor, int scene_version)
	{
		if (scene_version <= (int)PhysicsSceneVersion::DYNAMIC_TYPE)
		{
			bool is_dynamic;
			serializer.read(&is_dynamic);
			actor->dynamic_type = is_dynamic ? DynamicType::DYNAMIC : DynamicType::STATIC;
		}
		else
		{
			serializer.read((int*)&actor->dynamic_type);
		}
		if (actor->dynamic_type == DynamicType::DYNAMIC) m_dynamic_actors.push(actor);
		if (scene_version > (int)PhysicsSceneVersion::TRIGGERS)
		{
			serializer.read(&actor->is_trigger);
		}
		else
		{
			actor->is_trigger = false;
		}
	}


	void deserializeMeshActor(IDeserializer& serializer, Entity entity, int scene_version)
	{
		RigidActor* actor = LUMIX_NEW(m_allocator, RigidActor)(*this, ActorType::MESH);
		actor->entity = entity;
		serializer.read(&actor->layer);
		deserializeCommonRigidActorProperties(serializer, actor, scene_version);
		m_actors.insert(actor->entity, actor);

		char tmp[MAX_PATH_LENGTH];
		serializer.read(tmp, sizeof(tmp));
		ResourceManagerBase* manager = m_engine->getResourceManager().get(PhysicsGeometry::TYPE);
		auto* geometry = manager->load(Path(tmp));
		actor->setResource(static_cast<PhysicsGeometry*>(geometry));
		
		m_universe.onComponentCreated(actor->entity, MESH_ACTOR_TYPE, this);
	}


	void serializeRigidActor(ISerializer& serializer, Entity entity)
	{
		RigidActor* actor = m_actors[entity];
		serializer.write("layer", actor->layer);
		serializer.write("dynamic_type", (int)actor->dynamic_type);
		serializer.write("trigger", actor->is_trigger);
		PxShape* shape;
		int shape_count = actor->physx_actor->getNbShapes();

		serializer.write("count", shape_count);
		for (int i = 0; i < shape_count; ++i)
		{
			actor->physx_actor->getShapes(&shape, 1, i);
			int type = shape->getGeometryType();
			int index = (int)(intptr_t)shape->userData;
			serializer.write("type", type);
			serializer.write("index", index);
			RigidTransform tr = fromPhysx(shape->getLocalPose());
			serializer.write("tr", tr);
			switch (shape->getGeometryType())
			{
				case PxGeometryType::eBOX:
					{
						PxBoxGeometry geom;
						shape->getBoxGeometry(geom);
						serializer.write("x", geom.halfExtents.x);
						serializer.write("y", geom.halfExtents.y);
						serializer.write("z", geom.halfExtents.z);
					}
					break;
				case PxGeometryType::eSPHERE:
					{
						PxSphereGeometry geom;
						shape->getSphereGeometry(geom);
						serializer.write("radius", geom.radius);
					}
					break;
				default:
					ASSERT(false);
					break;
			}
		}
	}


	void serializeBoxActor(ISerializer& serializer, Entity entity)
	{
		RigidActor* actor = m_actors[entity];
		serializer.write("layer", actor->layer);
		serializer.write("dynamic_type", (int)actor->dynamic_type);
		serializer.write("trigger", actor->is_trigger);
		PxShape* shape;
		ASSERT(actor->physx_actor->getNbShapes() == 1);
		actor->physx_actor->getShapes(&shape, 1);
		PxBoxGeometry geom;
		if (!shape->getBoxGeometry(geom)) ASSERT(false);
		serializer.write("x", geom.halfExtents.x);
		serializer.write("y", geom.halfExtents.y);
		serializer.write("z", geom.halfExtents.z);
	}


	void deserializeRigidActor(IDeserializer& serializer, Entity entity, int scene_version)
	{
		RigidActor* actor = LUMIX_NEW(m_allocator, RigidActor)(*this, ActorType::RIGID);
		actor->entity = entity;
		serializer.read(&actor->layer);
		deserializeCommonRigidActorProperties(serializer, actor, scene_version);
		m_actors.insert(actor->entity, actor);

		PxTransform transform = toPhysx(m_universe.getTransform(actor->entity).getRigidPart());
		PxRigidActor* physx_actor;
		switch (actor->dynamic_type)
		{
			case DynamicType::DYNAMIC:
				physx_actor = m_system->getPhysics()->createRigidDynamic(transform);
				break;
			case DynamicType::KINEMATIC:
				physx_actor = m_system->getPhysics()->createRigidDynamic(transform);
				physx_actor->isRigidBody()->setRigidBodyFlag(PxRigidBodyFlag::eKINEMATIC, true);
				break;
			case DynamicType::STATIC:
				physx_actor = m_system->getPhysics()->createRigidStatic(transform);
				break;
		}

		int count;
		serializer.read(&count);
		for (int i = 0; i < count; ++i)
		{
			int type;
			serializer.read(&type);
			int index;
			serializer.read(&index);
			PxShape* shape = nullptr;
			RigidTransform tr;
			serializer.read(&tr);
			PxTransform local_pos = toPhysx(tr);
			switch (type)
			{
				case PxGeometryType::eBOX:
					{
						PxBoxGeometry geom;
						serializer.read(&geom.halfExtents.x);
						serializer.read(&geom.halfExtents.y);
						serializer.read(&geom.halfExtents.z);
						shape = physx_actor->createShape(geom, *m_default_material, local_pos);
					}
					break;
				case PxGeometryType::eSPHERE:
					{
						PxSphereGeometry geom;
						serializer.read(&geom.radius);
						shape = physx_actor->createShape(geom, *m_default_material, local_pos);
					}
					break;
				default:
					ASSERT(false);
					break;
			}
			if(shape) shape->userData = (void*)(intptr_t)index;
		}
		actor->setPhysxActor(physx_actor);

		m_universe.onComponentCreated(entity, RIGID_ACTOR_TYPE, this);

	}


	void deserializeBoxActor(IDeserializer& serializer, Entity entity, int scene_version)
	{
		RigidActor* actor = LUMIX_NEW(m_allocator, RigidActor)(*this, ActorType::BOX);
		actor->entity = entity;
		serializer.read(&actor->layer);
		deserializeCommonRigidActorProperties(serializer, actor, scene_version);
		m_actors.insert(actor->entity, actor);

		PxBoxGeometry box_geom;
		PxTransform transform = toPhysx(m_universe.getTransform(actor->entity).getRigidPart());
		serializer.read(&box_geom.halfExtents.x);
		serializer.read(&box_geom.halfExtents.y);
		serializer.read(&box_geom.halfExtents.z);
		PxRigidActor* physx_actor;
		switch (actor->dynamic_type)
		{
		case DynamicType::DYNAMIC:
			physx_actor = PxCreateDynamic(*m_system->getPhysics(), transform, box_geom, *m_default_material, 1.0f);
			break;
		case DynamicType::KINEMATIC:
			physx_actor = PxCreateDynamic(*m_system->getPhysics(), transform, box_geom, *m_default_material, 1.0f);
			physx_actor->isRigidBody()->setRigidBodyFlag(PxRigidBodyFlag::eKINEMATIC, true);
			break;
		case DynamicType::STATIC:
			physx_actor = PxCreateStatic(*m_system->getPhysics(), transform, box_geom, *m_default_material);
			break;
		}
		actor->setPhysxActor(physx_actor);

		m_universe.onComponentCreated(entity, BOX_ACTOR_TYPE, this);
	}


	void serializeCapsuleActor(ISerializer& serializer, Entity entity)
	{
		RigidActor* actor = m_actors[entity];
		serializer.write("layer", actor->layer);
		serializer.write("dynamic_type", (int)actor->dynamic_type);
		serializer.write("trigger", actor->is_trigger);
		PxShape* shape;
		ASSERT(actor->physx_actor->getNbShapes() == 1);
		actor->physx_actor->getShapes(&shape, 1);
		PxCapsuleGeometry geom;
		if (!shape->getCapsuleGeometry(geom)) ASSERT(false);
		serializer.write("half_height", geom.halfHeight);
		serializer.write("radius", geom.radius);
	}


	void deserializeCapsuleActor(IDeserializer& serializer, Entity entity, int scene_version)
	{
		RigidActor* actor = LUMIX_NEW(m_allocator, RigidActor)(*this, ActorType::CAPSULE);
		actor->entity = entity;
		serializer.read(&actor->layer);
		deserializeCommonRigidActorProperties(serializer, actor, scene_version);
		m_actors.insert(actor->entity, actor);

		PxCapsuleGeometry capsule_geom;
		PxTransform transform = toPhysx(m_universe.getTransform(actor->entity).getRigidPart());
		serializer.read(&capsule_geom.halfHeight);
		serializer.read(&capsule_geom.radius);
		PxRigidActor* physx_actor;
		switch (actor->dynamic_type)
		{
			case DynamicType::DYNAMIC:
				physx_actor =
					PxCreateDynamic(*m_system->getPhysics(), transform, capsule_geom, *m_default_material, 1.0f);
				break;
			case DynamicType::KINEMATIC:
				physx_actor =
					PxCreateDynamic(*m_system->getPhysics(), transform, capsule_geom, *m_default_material, 1.0f);
				physx_actor->isRigidBody()->setRigidBodyFlag(PxRigidBodyFlag::eKINEMATIC, true);
				break;
			case DynamicType::STATIC:
				physx_actor = PxCreateStatic(*m_system->getPhysics(), transform, capsule_geom, *m_default_material);
				break;
		}
		actor->setPhysxActor(physx_actor);

		m_universe.onComponentCreated(entity, CAPSULE_ACTOR_TYPE, this);
	}


	void serializeSphereActor(ISerializer& serializer, Entity entity)
	{
		RigidActor* actor = m_actors[entity];
		serializer.write("layer", actor->layer);
		serializer.write("dynamic_type", (int)actor->dynamic_type);
		serializer.write("trigger", actor->is_trigger);
		PxShape* shape;
		ASSERT(actor->physx_actor->getNbShapes() == 1);
		actor->physx_actor->getShapes(&shape, 1);
		PxSphereGeometry geom;
		if (!shape->getSphereGeometry(geom)) ASSERT(false);
		serializer.write("radius", geom.radius);
	}


	void deserializeSphereActor(IDeserializer& serializer, Entity entity, int scene_version)
	{
		RigidActor* actor = LUMIX_NEW(m_allocator, RigidActor)(*this, ActorType::SPHERE);
		actor->entity = entity;
		serializer.read(&actor->layer);
		deserializeCommonRigidActorProperties(serializer, actor, scene_version);
		m_actors.insert(actor->entity, actor);

		PxSphereGeometry sphere_geom;
		PxTransform transform = toPhysx(m_universe.getTransform(actor->entity).getRigidPart());
		serializer.read(&sphere_geom.radius);
		PxRigidActor* physx_actor;
		switch (actor->dynamic_type)
		{
			case DynamicType::DYNAMIC:
				physx_actor =
					PxCreateDynamic(*m_system->getPhysics(), transform, sphere_geom, *m_default_material, 1.0f);
				break;
			case DynamicType::KINEMATIC:
				physx_actor =
					PxCreateDynamic(*m_system->getPhysics(), transform, sphere_geom, *m_default_material, 1.0f);
				physx_actor->isRigidBody()->setRigidBodyFlag(PxRigidBodyFlag::eKINEMATIC, true);
				break;
			case DynamicType::STATIC:
				physx_actor = PxCreateStatic(*m_system->getPhysics(), transform, sphere_geom, *m_default_material);
				break;
		}
		actor->setPhysxActor(physx_actor);

		m_universe.onComponentCreated(entity, SPHERE_ACTOR_TYPE, this);
	}


	void serializeActor(OutputBlob& serializer, RigidActor* actor)
	{
		serializer.write(actor->layer);
		PxShape* shapes;
		auto* px_actor = actor->physx_actor;
		auto* resource = actor->resource;
		serializer.write((i32)actor->type);
		switch (actor->type)
		{
			case ActorType::RIGID:
			{
				PxShape* shape;
				int shape_count = px_actor->getNbShapes();
				serializer.write(shape_count);
				for (int i = 0; i < shape_count; ++i)
				{
					px_actor->getShapes(&shape, 1, i);
					int type = shape->getGeometryType();
					serializer.write(type);
					serializer.write((int)(intptr_t)shape->userData);
					RigidTransform tr = fromPhysx(shape->getLocalPose());
					serializer.write(tr);
					switch(type)
					{
						case PxGeometryType::eBOX:
							{
								PxBoxGeometry geom;
								shape->getBoxGeometry(geom);
								serializer.write(geom.halfExtents.x);
								serializer.write(geom.halfExtents.y);
								serializer.write(geom.halfExtents.z);
							}
							break;
						case PxGeometryType::eSPHERE:
							{
								PxSphereGeometry geom;
								shape->getSphereGeometry(geom);
								serializer.write(geom.radius);
							}
							break;
						default:
							ASSERT(false);
							break;
					}
				}
				break;
			}
			case ActorType::BOX:
			{
				ASSERT(px_actor->getNbShapes() == 1);
				px_actor->getShapes(&shapes, 1);
				PxBoxGeometry geom;
				if (!shapes->getBoxGeometry(geom)) ASSERT(false);
				serializer.write(geom.halfExtents.x);
				serializer.write(geom.halfExtents.y);
				serializer.write(geom.halfExtents.z);
				break;
			}
			case ActorType::SPHERE:
			{
				ASSERT(px_actor->getNbShapes() == 1);
				px_actor->getShapes(&shapes, 1);
				PxSphereGeometry geom;
				if (!shapes->getSphereGeometry(geom)) ASSERT(false);
				serializer.write(geom.radius);
				break;
			}
			case ActorType::CAPSULE:
			{
				ASSERT(px_actor->getNbShapes() == 1);
				px_actor->getShapes(&shapes, 1);
				PxCapsuleGeometry geom;
				if (!shapes->getCapsuleGeometry(geom)) ASSERT(false);
				serializer.write(geom.halfHeight);
				serializer.write(geom.radius);
				break;
			}
			case ActorType::MESH: serializer.writeString(resource ? resource->getPath().c_str() : ""); break;
			default: ASSERT(false);
		}
	}

	PxRigidActor* createPhysXActor(RigidActor* actor, const PxTransform transform, const PxGeometry& geometry)
	{
		switch (actor->dynamic_type)
		{
			case DynamicType::DYNAMIC:
				return PxCreateDynamic(*m_system->getPhysics(), transform, geometry, *m_default_material, 1.0f);
			case DynamicType::KINEMATIC:
			{
				PxRigidDynamic* physx_actor =
					PxCreateDynamic(*m_system->getPhysics(), transform, geometry, *m_default_material, 1.0f);
				physx_actor->isRigidBody()->setRigidBodyFlag(PxRigidBodyFlag::eKINEMATIC, true);
				return physx_actor;
			}
			case DynamicType::STATIC:
				return PxCreateStatic(*m_system->getPhysics(), transform, geometry, *m_default_material);
		}
		return nullptr;
	}


	void deserializeActor(InputBlob& serializer, RigidActor* actor)
	{
		Entity entity = actor->entity;
		actor->layer = 0;
		serializer.read(actor->layer);

		serializer.read((i32&)actor->type);

		PxTransform transform = toPhysx(m_universe.getTransform(actor->entity).getRigidPart());
		switch (actor->type)
		{
			case ActorType::RIGID:
			{
				PxRigidActor* physx_actor = actor->dynamic_type == DynamicType::STATIC 
					? (PxRigidActor*)m_system->getPhysics()->createRigidStatic(transform) 
					: (PxRigidActor*)m_system->getPhysics()->createRigidDynamic(transform);
				int count = serializer.read<int>();
				for (int i = 0; i < count; ++i)
				{
					int type = serializer.read<int>();
					int index = serializer.read<int>();
					PxTransform tr = toPhysx(serializer.read<RigidTransform>());
					PxShape* shape = nullptr;
					switch (type)
					{
						case PxGeometryType::eBOX:
							{
								PxBoxGeometry box_geom;
								serializer.read(box_geom.halfExtents.x);
								serializer.read(box_geom.halfExtents.y);
								serializer.read(box_geom.halfExtents.z);
								shape = physx_actor->createShape(box_geom, *m_default_material, tr);
							}
							break;
						case PxGeometryType::eSPHERE:
							{
								PxSphereGeometry geom;
								serializer.read(geom.radius);
								shape = physx_actor->createShape(geom, *m_default_material, tr);
							}
							break;
						default:
							ASSERT(false);
							break;
					}
					if (shape) shape->userData = (void*)(intptr_t)index;
				}
				actor->setPhysxActor(physx_actor);
				m_universe.onComponentCreated(actor->entity, RIGID_ACTOR_TYPE, this);
			}
			break;
			case ActorType::BOX:
			{
				PxBoxGeometry box_geom;
				PxTransform transform = toPhysx(m_universe.getTransform(actor->entity).getRigidPart());
				serializer.read(box_geom.halfExtents);
				PxRigidActor* physx_actor = createPhysXActor(actor, transform, box_geom);

				actor->setPhysxActor(physx_actor);
				m_universe.onComponentCreated(actor->entity, BOX_ACTOR_TYPE, this);
			}
			break;
			case ActorType::SPHERE:
			{
				PxSphereGeometry sphere_geom;
				PxTransform transform = toPhysx(m_universe.getTransform(actor->entity).getRigidPart());
				serializer.read(sphere_geom.radius);
				PxRigidActor* physx_actor = createPhysXActor(actor, transform, sphere_geom);
				actor->setPhysxActor(physx_actor);
				m_universe.onComponentCreated(actor->entity, SPHERE_ACTOR_TYPE, this);
			}
			break;
			case ActorType::CAPSULE:
			{
				PxCapsuleGeometry capsule_geom;
				PxTransform transform = toPhysx(m_universe.getTransform(actor->entity).getRigidPart());
				serializer.read(capsule_geom.halfHeight);
				serializer.read(capsule_geom.radius);
				PxRigidActor* physx_actor = createPhysXActor(actor, transform, capsule_geom);
				actor->setPhysxActor(physx_actor);
				m_universe.onComponentCreated(actor->entity, CAPSULE_ACTOR_TYPE, this);
			}
			break;
			case ActorType::MESH:
			{
				char tmp[MAX_PATH_LENGTH];
				serializer.readString(tmp, sizeof(tmp));
				ResourceManagerBase* manager = m_engine->getResourceManager().get(PhysicsGeometry::TYPE);
				auto* geometry = manager->load(Path(tmp));
				actor->setResource(static_cast<PhysicsGeometry*>(geometry));
				m_universe.onComponentCreated(actor->entity, MESH_ACTOR_TYPE, this);
			}
			break;
			default:
				ASSERT(false);
				break;
		}
	}


	void serialize(OutputBlob& serializer) override
	{
		serializer.write(m_layers_count);
		serializer.write(m_layers_names);
		serializer.write(m_collision_filter);
		serializer.write((i32)m_actors.size());
		for (auto* actor : m_actors)
		{
			serializer.write(actor->dynamic_type);
			serializer.write(actor->is_trigger);
			serializer.write(actor->entity);
			serializeActor(serializer, actor);
		}
		serializer.write((i32)m_controllers.size());
		for (const auto& controller : m_controllers)
		{
			serializer.write(controller.m_entity);
			serializer.write(controller.m_layer);
			serializer.write(controller.m_radius);
			serializer.write(controller.m_height);
		}
		serializer.write((i32)m_terrains.size());
		for (auto& terrain : m_terrains)
		{
			serializer.write(terrain.m_entity);
			serializer.writeString(terrain.m_heightmap ? terrain.m_heightmap->getPath().c_str() : "");
			serializer.write(terrain.m_xz_scale);
			serializer.write(terrain.m_y_scale);
			serializer.write(terrain.m_layer);
		}
		serializeRagdolls(serializer);
		serializeJoints(serializer);
	}


	void serializeRagdollJoint(RagdollBone* bone, ISerializer& serializer)
	{
		serializer.write("has_joint", bone->parent_joint != nullptr);
		if (!bone->parent_joint) return;

		serializer.write("type", (int)bone->parent_joint->getConcreteType());
		serializer.write("pose0", fromPhysx(bone->parent_joint->getLocalPose(PxJointActorIndex::eACTOR0)));
		serializer.write("pose1", fromPhysx(bone->parent_joint->getLocalPose(PxJointActorIndex::eACTOR1)));

		switch ((PxJointConcreteType::Enum)bone->parent_joint->getConcreteType())
		{
			case PxJointConcreteType::eFIXED: break;
			case PxJointConcreteType::eDISTANCE:
			{
				auto* joint = bone->parent_joint->is<PxDistanceJoint>();
				serializeJoint(serializer, joint);
				break;
			}
			case PxJointConcreteType::eREVOLUTE:
			{
				auto* joint = bone->parent_joint->is<PxRevoluteJoint>();
				serializeJoint(serializer, joint);
				break;
			}
			case PxJointConcreteType::eD6:
			{
				auto* joint = bone->parent_joint->is<PxD6Joint>();
				serializeJoint(serializer, joint);
				break;
			}
			case PxJointConcreteType::eSPHERICAL:
			{
				auto* joint = bone->parent_joint->is<PxSphericalJoint>();
				serializeJoint(serializer, joint);
				break;
			}
			default: ASSERT(false); break;
		}
	}

	void serializeRagdollJoint(RagdollBone* bone, OutputBlob& serializer)
	{
		serializer.write(bone->parent_joint != nullptr);
		if (!bone->parent_joint) return;

		serializer.write((int)bone->parent_joint->getConcreteType());
		serializer.write(bone->parent_joint->getLocalPose(PxJointActorIndex::eACTOR0));
		serializer.write(bone->parent_joint->getLocalPose(PxJointActorIndex::eACTOR1));

		switch ((PxJointConcreteType::Enum)bone->parent_joint->getConcreteType())
		{
			case PxJointConcreteType::eFIXED: break;
			case PxJointConcreteType::eDISTANCE:
			{
				auto* joint = bone->parent_joint->is<PxDistanceJoint>();
				serializer.write(joint->getMinDistance());
				serializer.write(joint->getMaxDistance());
				serializer.write(joint->getTolerance());
				serializer.write(joint->getStiffness());
				serializer.write(joint->getDamping());
				u32 flags = (PxU32)joint->getDistanceJointFlags();
				serializer.write(flags);
				break;
			}
			case PxJointConcreteType::eREVOLUTE:
			{
				auto* joint = bone->parent_joint->is<PxRevoluteJoint>();
				serializer.write(joint->getLimit());
				u32 flags = (PxU32)joint->getRevoluteJointFlags();
				serializer.write(flags);
				break;
			}
			case PxJointConcreteType::eD6:
			{
				auto* joint = bone->parent_joint->is<PxD6Joint>();
				serializer.write(joint->getLinearLimit());
				serializer.write(joint->getSwingLimit());
				serializer.write(joint->getTwistLimit());
				serializer.write(joint->getMotion(PxD6Axis::eX));
				serializer.write(joint->getMotion(PxD6Axis::eY));
				serializer.write(joint->getMotion(PxD6Axis::eZ));
				serializer.write(joint->getMotion(PxD6Axis::eSWING1));
				serializer.write(joint->getMotion(PxD6Axis::eSWING2));
				serializer.write(joint->getMotion(PxD6Axis::eTWIST));
				break;
			}
			case PxJointConcreteType::eSPHERICAL:
			{
				auto* joint = bone->parent_joint->is<PxSphericalJoint>();
				serializer.write(joint->getLimitCone());
				u32 flags = (PxU32)joint->getSphericalJointFlags();
				serializer.write(flags);
				break;
			}
			default: ASSERT(false); break;
		}
	}


	void serializeRagdollBone(const Ragdoll& ragdoll, RagdollBone* bone, ISerializer& serializer)
	{
		if (!bone)
		{
			serializer.write("bone", -1);
			return;
		}
		serializer.write("bone", bone->pose_bone_idx);
		PxTransform pose = bone->actor->getGlobalPose();
		pose = toPhysx(m_universe.getTransform(ragdoll.entity).getRigidPart()).getInverse() * pose;
		serializer.write("pose", fromPhysx(pose));
		serializer.write("bind_transform", bone->bind_transform);

		PxShape* shape;
		int shape_count = bone->actor->getShapes(&shape, 1);
		ASSERT(shape_count == 1);
		PxBoxGeometry box_geom;
		if (shape->getBoxGeometry(box_geom))
		{
			serializer.write("type", RagdollBone::BOX);
			serializer.write("half_extents", fromPhysx(box_geom.halfExtents));
		}
		else
		{
			PxCapsuleGeometry capsule_geom;
			bool is_capsule = shape->getCapsuleGeometry(capsule_geom);
			ASSERT(is_capsule);
			serializer.write("type", RagdollBone::CAPSULE);
			serializer.write("half_height", capsule_geom.halfHeight);
			serializer.write("radius", capsule_geom.radius);
		}
		serializer.write("is_kinematic", bone->actor->isRigidBody()->getRigidBodyFlags().isSet(PxRigidBodyFlag::eKINEMATIC));

		serializeRagdollBone(ragdoll, bone->child, serializer);
		serializeRagdollBone(ragdoll, bone->next, serializer);

		serializeRagdollJoint(bone, serializer);
	}

	void serializeRagdollBone(const Ragdoll& ragdoll, RagdollBone* bone, OutputBlob& serializer)
	{
		if (!bone)
		{
			serializer.write(-1);
			return;
		}
		serializer.write(bone->pose_bone_idx);
		PxTransform pose = bone->actor->getGlobalPose();
		pose = toPhysx(m_universe.getTransform(ragdoll.entity).getRigidPart()).getInverse() * pose;
		serializer.write(fromPhysx(pose));
		serializer.write(bone->bind_transform);

		PxShape* shape;
		int shape_count = bone->actor->getShapes(&shape, 1);
		ASSERT(shape_count == 1);
		PxBoxGeometry box_geom;
		if (shape->getBoxGeometry(box_geom))
		{
			serializer.write(RagdollBone::BOX);
			serializer.write(box_geom.halfExtents);
		}
		else
		{
			PxCapsuleGeometry capsule_geom;
			bool is_capsule = shape->getCapsuleGeometry(capsule_geom);
			ASSERT(is_capsule);
			serializer.write(RagdollBone::CAPSULE);
			serializer.write(capsule_geom.halfHeight);
			serializer.write(capsule_geom.radius);
		}
		serializer.write(bone->actor->isRigidBody()->getRigidBodyFlags().isSet(PxRigidBodyFlag::eKINEMATIC));

		serializeRagdollBone(ragdoll, bone->child, serializer);
		serializeRagdollBone(ragdoll, bone->next, serializer);

		serializeRagdollJoint(bone, serializer);
	}


	void deserializeRagdollJoint(RagdollBone* bone, InputBlob& serializer)
	{
		bool has_joint;
		serializer.read(has_joint);
		if (!has_joint) return;

		int type;
		serializer.read(type);
		changeRagdollBoneJoint(bone, type);

		PxTransform local_poses[2];
		serializer.read(local_poses);
		bone->parent_joint->setLocalPose(PxJointActorIndex::eACTOR0, local_poses[0]);
		bone->parent_joint->setLocalPose(PxJointActorIndex::eACTOR1, local_poses[1]);

		switch ((PxJointConcreteType::Enum)type)
		{
			case PxJointConcreteType::eFIXED: break;
			case PxJointConcreteType::eDISTANCE:
			{
				auto* joint = bone->parent_joint->is<PxDistanceJoint>();
				PxReal value;
				serializer.read(value);
				joint->setMinDistance(value);
				serializer.read(value);
				joint->setMaxDistance(value);
				serializer.read(value);
				joint->setTolerance(value);
				serializer.read(value);
				joint->setStiffness(value);
				serializer.read(value);
				joint->setDamping(value);
				u32 flags;
				serializer.read(flags);
				joint->setDistanceJointFlags((PxDistanceJointFlags)flags);
				break;
			}
			case PxJointConcreteType::eREVOLUTE:
			{
				auto* joint = bone->parent_joint->is<PxRevoluteJoint>();
				PxJointAngularLimitPair limit(0, 0);
				serializer.read(limit);
				joint->setLimit(limit);
				u32 flags;
				serializer.read(flags);
				joint->setRevoluteJointFlags((PxRevoluteJointFlags)flags);
				break;
			}
			case PxJointConcreteType::eSPHERICAL:
			{
				auto* joint = bone->parent_joint->is<PxSphericalJoint>();
				PxJointLimitCone limit(0, 0);
				serializer.read(limit);
				joint->setLimitCone(limit);
				u32 flags;
				serializer.read(flags);
				joint->setSphericalJointFlags((PxSphericalJointFlags)flags);
				break;
			}
			case PxJointConcreteType::eD6:
			{
				auto* joint = bone->parent_joint->is<PxD6Joint>();
				
				PxJointLinearLimit linear_limit(0, PxSpring(0, 0));
				serializer.read(linear_limit);
				joint->setLinearLimit(linear_limit);

				PxJointLimitCone swing_limit(0, 0);
				serializer.read(swing_limit);
				joint->setSwingLimit(swing_limit);

				PxJointAngularLimitPair twist_limit(0, 0);
				serializer.read(twist_limit);
				joint->setTwistLimit(twist_limit);

				PxD6Motion::Enum motions[6];
				serializer.read(motions);
				joint->setMotion(PxD6Axis::eX, motions[0]);
				joint->setMotion(PxD6Axis::eY, motions[1]);
				joint->setMotion(PxD6Axis::eZ, motions[2]);
				joint->setMotion(PxD6Axis::eSWING1, motions[3]);
				joint->setMotion(PxD6Axis::eSWING2, motions[4]);
				joint->setMotion(PxD6Axis::eTWIST, motions[5]);
				break;
			}
			default: ASSERT(false); break;
		}
	}


	void deserializeRagdollJoint(RagdollBone* bone, IDeserializer& serializer)
	{
		bool has_joint;
		serializer.read(&has_joint);
		if (!has_joint) return;

		int type;
		serializer.read(&type);
		changeRagdollBoneJoint(bone, type);

		RigidTransform local_poses[2];
		serializer.read(&local_poses[0]);
		serializer.read(&local_poses[1]);
		bone->parent_joint->setLocalPose(PxJointActorIndex::eACTOR0, toPhysx(local_poses[0]));
		bone->parent_joint->setLocalPose(PxJointActorIndex::eACTOR1, toPhysx(local_poses[1]));

		switch ((PxJointConcreteType::Enum)type)
		{
			case PxJointConcreteType::eFIXED: break;
			case PxJointConcreteType::eDISTANCE:
			{
				auto* joint = bone->parent_joint->is<PxDistanceJoint>();
				deserializeJoint(serializer, joint);
				break;
			}
			case PxJointConcreteType::eREVOLUTE:
			{
				auto* joint = bone->parent_joint->is<PxRevoluteJoint>();
				deserializeJoint(serializer, joint);
				break;
			}
			case PxJointConcreteType::eSPHERICAL:
			{
				auto* joint = bone->parent_joint->is<PxSphericalJoint>();
				deserializeJoint(serializer, joint);
				break;
			}
			case PxJointConcreteType::eD6:
			{
				auto* joint = bone->parent_joint->is<PxD6Joint>();
				deserializeJoint(serializer, joint);
				break;
			}
			default: ASSERT(false); break;
		}
	}


	RagdollBone* deserializeRagdollBone(Ragdoll& ragdoll, RagdollBone* parent, InputBlob& serializer)
	{
		int pose_bone_idx;
		serializer.read(pose_bone_idx);
		if (pose_bone_idx < 0) return nullptr;
		auto* bone = LUMIX_NEW(m_allocator, RagdollBone);
		bone->pose_bone_idx = pose_bone_idx;
		bone->parent_joint = nullptr;
		bone->is_kinematic = false;
		bone->prev = nullptr;
		RigidTransform transform;
		serializer.read(transform);
		serializer.read(bone->bind_transform);
		bone->inv_bind_transform = bone->bind_transform.inverted();

		PxTransform px_transform = toPhysx(m_universe.getTransform(ragdoll.entity).getRigidPart()) * toPhysx(transform);

		RagdollBone::Type type;
		serializer.read(type);
		
		switch (type)
		{
			case RagdollBone::CAPSULE:
			{
				PxCapsuleGeometry shape;
				serializer.read(shape.halfHeight);
				serializer.read(shape.radius);
				bone->actor =
					PxCreateDynamic(m_scene->getPhysics(), px_transform, shape, *m_default_material, 1.0f);
				break;
			}
			case RagdollBone::BOX:
			{
				PxBoxGeometry shape;
				serializer.read(shape.halfExtents);
				bone->actor =
					PxCreateDynamic(m_scene->getPhysics(), px_transform, shape, *m_default_material, 1.0f);
				break;
			}
			default: ASSERT(false); break;
		}
		serializer.read(bone->is_kinematic);
		bone->actor->setRigidBodyFlag(PxRigidBodyFlag::eKINEMATIC, bone->is_kinematic);
		bone->actor->isRigidDynamic()->setSolverIterationCounts(8, 8);
		bone->actor->isRigidDynamic()->setMass(0.0001f);
		bone->actor->userData = (void*)(intptr_t)ragdoll.entity.index;

		bone->actor->setActorFlag(PxActorFlag::eVISUALIZATION, true);
		m_scene->addActor(*bone->actor);
		updateFilterData(bone->actor, ragdoll.layer);

		bone->parent = parent;

		bone->child = deserializeRagdollBone(ragdoll, bone, serializer);
		bone->next = deserializeRagdollBone(ragdoll, parent, serializer);
		if(bone->next) bone->next->prev = bone;

		deserializeRagdollJoint(bone, serializer);

		return bone;
	}


	RagdollBone* deserializeRagdollBone(Ragdoll& ragdoll, RagdollBone* parent, IDeserializer& serializer)
	{
		int pose_bone_idx;
		serializer.read(&pose_bone_idx);
		if (pose_bone_idx < 0) return nullptr;
		auto* bone = LUMIX_NEW(m_allocator, RagdollBone);
		bone->pose_bone_idx = pose_bone_idx;
		bone->parent_joint = nullptr;
		bone->is_kinematic = false;
		bone->prev = nullptr;
		RigidTransform transform;
		serializer.read(&transform);
		serializer.read(&bone->bind_transform);
		bone->inv_bind_transform = bone->bind_transform.inverted();

		PxTransform px_transform = toPhysx(m_universe.getTransform(ragdoll.entity).getRigidPart()) * toPhysx(transform);

		RagdollBone::Type type;
		serializer.read((int*)&type);

		switch (type)
		{
			case RagdollBone::CAPSULE:
			{
				PxCapsuleGeometry shape;
				serializer.read(&shape.halfHeight);
				serializer.read(&shape.radius);
				bone->actor = PxCreateDynamic(m_scene->getPhysics(), px_transform, shape, *m_default_material, 1.0f);
				break;
			}
			case RagdollBone::BOX:
			{
				PxBoxGeometry shape;
				Vec3 e;
				serializer.read(&e);
				shape.halfExtents = toPhysx(e);
				bone->actor = PxCreateDynamic(m_scene->getPhysics(), px_transform, shape, *m_default_material, 1.0f);
				break;
			}
			default: ASSERT(false); break;
		}
		serializer.read(&bone->is_kinematic);
		bone->actor->setRigidBodyFlag(PxRigidBodyFlag::eKINEMATIC, bone->is_kinematic);
		bone->actor->isRigidDynamic()->setSolverIterationCounts(8, 8);
		bone->actor->isRigidDynamic()->setMass(0.0001f);
		bone->actor->userData = (void*)(intptr_t)ragdoll.entity.index;

		bone->actor->setActorFlag(PxActorFlag::eVISUALIZATION, true);
		m_scene->addActor(*bone->actor);
		updateFilterData(bone->actor, ragdoll.layer);

		bone->parent = parent;

		bone->child = deserializeRagdollBone(ragdoll, bone, serializer);
		bone->next = deserializeRagdollBone(ragdoll, parent, serializer);
		if (bone->next) bone->next->prev = bone;

		deserializeRagdollJoint(bone, serializer);

		return bone;
	}


	void serializeRagdolls(OutputBlob& serializer)
	{
		serializer.write(m_ragdolls.size());
		for (int i = 0, c = m_ragdolls.size(); i < c; ++i)
		{
			serializer.write(m_ragdolls.getKey(i));
			const Ragdoll& ragdoll = m_ragdolls.at(i);
			serializer.write(ragdoll.layer);
			serializeRagdollBone(ragdoll, ragdoll.root, serializer);
		}
	}


	void serializeJoints(OutputBlob& serializer)
	{
		serializer.write(m_joints.size());
		for (int i = 0; i < m_joints.size(); ++i)
		{
			const Joint& joint = m_joints.at(i);
			serializer.write(m_joints.getKey(i));
			serializer.write((int)joint.physx->getConcreteType());
			serializer.write(joint.connected_body);
			serializer.write(joint.local_frame0);
			switch ((PxJointConcreteType::Enum)joint.physx->getConcreteType())
			{
				case PxJointConcreteType::eSPHERICAL:
				{
					auto* px_joint = static_cast<PxSphericalJoint*>(joint.physx);
					u32 flags = (u32)px_joint->getSphericalJointFlags();
					serializer.write(flags);
					auto limit = px_joint->getLimitCone();
					serializer.write(limit);
					break;
				}
				case PxJointConcreteType::eREVOLUTE:
				{
					auto* px_joint = static_cast<PxRevoluteJoint*>(joint.physx);
					u32 flags = (u32)px_joint->getRevoluteJointFlags();
					serializer.write(flags);
					auto limit = px_joint->getLimit();
					serializer.write(limit);
					break;
				}
				case PxJointConcreteType::eDISTANCE:
				{
					auto* px_joint = static_cast<PxDistanceJoint*>(joint.physx);
					u32 flags = (u32)px_joint->getDistanceJointFlags();
					serializer.write(flags);
					serializer.write(px_joint->getDamping());
					serializer.write(px_joint->getStiffness());
					serializer.write(px_joint->getTolerance());
					serializer.write(px_joint->getMinDistance());
					serializer.write(px_joint->getMaxDistance());
					break;
				}
				case PxJointConcreteType::eD6:
				{
					auto* px_joint = static_cast<PxD6Joint*>(joint.physx);
					serializer.write(px_joint->getMotion(PxD6Axis::eX));
					serializer.write(px_joint->getMotion(PxD6Axis::eY));
					serializer.write(px_joint->getMotion(PxD6Axis::eZ));
					serializer.write(px_joint->getMotion(PxD6Axis::eSWING1));
					serializer.write(px_joint->getMotion(PxD6Axis::eSWING2));
					serializer.write(px_joint->getMotion(PxD6Axis::eTWIST));
					serializer.write(px_joint->getLinearLimit());
					serializer.write(px_joint->getSwingLimit());
					serializer.write(px_joint->getTwistLimit());
					break;
				}
				default: ASSERT(false); break;
			}
		}
	}



	void deserializeActors(InputBlob& serializer)
	{
		i32 count;
		serializer.read(count);
		m_actors.reserve(count);
		for (int i = 0; i < count; ++i)
		{
			RigidActor* actor = LUMIX_NEW(m_allocator, RigidActor)(*this, ActorType::BOX);
			serializer.read(actor->dynamic_type);
			serializer.read(actor->is_trigger);
			serializer.read(actor->entity);
			if (!actor->entity.isValid())
			{
				LUMIX_DELETE(m_allocator, actor);
				continue;
			}
			if (actor->dynamic_type == DynamicType::DYNAMIC) m_dynamic_actors.push(actor);
			m_actors.insert(actor->entity, actor);
			deserializeActor(serializer, actor);
		}
	}


	void deserializeControllers(InputBlob& serializer)
	{
		i32 count;
		serializer.read(count);
		for (int i = 0; i < count; ++i)
		{
			Entity entity;
			serializer.read(entity);
			Controller& c = m_controllers.insert(entity);
			c.m_frame_change.set(0, 0, 0);

			serializer.read(c.m_layer);
			serializer.read(c.m_radius);
			serializer.read(c.m_height);
			PxCapsuleControllerDesc cDesc;
			initControllerDesc(cDesc);
			cDesc.height = c.m_height;
			cDesc.radius = c.m_radius;
			Vec3 position = m_universe.getPosition(entity);
			cDesc.position.set(position.x, position.y - cDesc.height * 0.5f, position.z);
			c.m_controller = m_controller_manager->createController(cDesc);
			c.m_controller->getActor()->userData = (void*)(intptr_t)entity.index;
			c.m_entity = entity;
			c.m_controller->setFootPosition({position.x, position.y, position.z});
			m_universe.onComponentCreated(entity, CONTROLLER_TYPE, this);
		}
	}


	void destroySkeleton(RagdollBone* bone)
	{
		if (!bone) return;
		destroySkeleton(bone->next);
		destroySkeleton(bone->child);
		if (bone->parent_joint) bone->parent_joint->release();
		if (bone->actor) bone->actor->release();
		LUMIX_DELETE(m_allocator, bone);
	}


	void deserializeRagdolls(InputBlob& serializer)
	{
		int count;
		serializer.read(count);
		m_ragdolls.reserve(count);
		for (int i = 0; i < count; ++i)
		{
			Entity entity;
			serializer.read(entity);
			Ragdoll& ragdoll = m_ragdolls.insert(entity);
			ragdoll.layer = 0;
			ragdoll.root_transform.pos.set(0, 0, 0);
			ragdoll.root_transform.rot.set(0, 0, 0, 1);

			serializer.read(ragdoll.layer);
			ragdoll.entity = entity;
			setRagdollRoot(ragdoll, deserializeRagdollBone(ragdoll, nullptr, serializer));
			m_universe.onComponentCreated(ragdoll.entity, RAGDOLL_TYPE, this);
		}
	}


	void deserializeJoints(InputBlob& serializer)
	{
		int count;
		serializer.read(count);
		m_joints.reserve(count);
		for (int i = 0; i < count; ++i)
		{
			Entity entity;
			serializer.read(entity);
			Joint& joint = m_joints.insert(entity);
			int type;
			serializer.read(type);
			serializer.read(joint.connected_body);
			serializer.read(joint.local_frame0);
			ComponentType cmp_type;
			switch (PxJointConcreteType::Enum(type))
			{
				case PxJointConcreteType::eSPHERICAL:
				{
					cmp_type = SPHERICAL_JOINT_TYPE;
					auto* px_joint = PxSphericalJointCreate(m_scene->getPhysics(),
						m_dummy_actor,
						joint.local_frame0,
						nullptr,
						PxTransform::createIdentity());
					joint.physx = px_joint;
					u32 flags;
					serializer.read(flags);
					px_joint->setSphericalJointFlags(PxSphericalJointFlags(flags));
					PxJointLimitCone limit(0, 0);
					serializer.read(limit);
					px_joint->setLimitCone(limit);
					break;
				}
				case PxJointConcreteType::eREVOLUTE:
				{
					cmp_type = HINGE_JOINT_TYPE;
					auto* px_joint = PxRevoluteJointCreate(m_scene->getPhysics(),
						m_dummy_actor,
						joint.local_frame0,
						nullptr,
						PxTransform::createIdentity());
					joint.physx = px_joint;
					u32 flags;
					serializer.read(flags);
					px_joint->setRevoluteJointFlags(PxRevoluteJointFlags(flags));
					PxJointAngularLimitPair limit(0, 0);
					serializer.read(limit);
					px_joint->setLimit(limit);
					break;
				}
				case PxJointConcreteType::eDISTANCE:
				{
					cmp_type = DISTANCE_JOINT_TYPE;
					auto* px_joint = PxDistanceJointCreate(m_scene->getPhysics(),
						m_dummy_actor,
						joint.local_frame0,
						nullptr,
						PxTransform::createIdentity());
					joint.physx = px_joint;
					u32 flags;
					serializer.read(flags);
					px_joint->setDistanceJointFlags(PxDistanceJointFlags(flags));
					float tmp;
					serializer.read(tmp);
					px_joint->setDamping(tmp);
					serializer.read(tmp);
					px_joint->setStiffness(tmp);
					serializer.read(tmp);
					px_joint->setTolerance(tmp);
					serializer.read(tmp);
					px_joint->setMinDistance(tmp);
					serializer.read(tmp);
					px_joint->setMaxDistance(tmp);
					break;
				}
				case PxJointConcreteType::eD6:
				{
					cmp_type = D6_JOINT_TYPE;
					auto* px_joint = PxD6JointCreate(m_scene->getPhysics(),
						m_dummy_actor,
						joint.local_frame0,
						nullptr,
						PxTransform::createIdentity());
					joint.physx = px_joint;
					int motions[6];
					serializer.read(motions);
					px_joint->setMotion(PxD6Axis::eX, (PxD6Motion::Enum)motions[0]);
					px_joint->setMotion(PxD6Axis::eY, (PxD6Motion::Enum)motions[1]);
					px_joint->setMotion(PxD6Axis::eZ, (PxD6Motion::Enum) motions[2]);
					px_joint->setMotion(PxD6Axis::eSWING1, (PxD6Motion::Enum)motions[3]);
					px_joint->setMotion(PxD6Axis::eSWING2, (PxD6Motion::Enum)motions[4]);
					px_joint->setMotion(PxD6Axis::eTWIST, (PxD6Motion::Enum)motions[5]);
					PxJointLinearLimit linear_limit(0, PxSpring(0, 0));
					serializer.read(linear_limit);
					px_joint->setLinearLimit(linear_limit);
					PxJointLimitCone swing_limit(0, 0);
					serializer.read(swing_limit);
					px_joint->setSwingLimit(swing_limit);
					PxJointAngularLimitPair twist_limit(0, 0);
					serializer.read(twist_limit);
					px_joint->setTwistLimit(twist_limit);
					break;
				}
				default: ASSERT(false); break;
			}

			m_universe.onComponentCreated(entity, cmp_type, this);
		}

	}


	void deserializeTerrains(InputBlob& serializer)
	{
		i32 count;
		serializer.read(count);
		for (int i = 0; i < count; ++i)
		{
			Heightfield terrain;
			terrain.m_scene = this;
			serializer.read(terrain.m_entity);
			char tmp[MAX_PATH_LENGTH];
			serializer.readString(tmp, MAX_PATH_LENGTH);
			serializer.read(terrain.m_xz_scale);
			serializer.read(terrain.m_y_scale);
				serializer.read(terrain.m_layer);

			m_terrains.insert(terrain.m_entity, terrain);
			Entity entity = terrain.m_entity;
			if (terrain.m_heightmap == nullptr || !equalStrings(tmp, terrain.m_heightmap->getPath().c_str()))
			{
				setHeightmapSource(entity, Path(tmp));
			}
			m_universe.onComponentCreated(terrain.m_entity, HEIGHTFIELD_TYPE, this);
		}
	}


	void deserialize(InputBlob& serializer) override
	{
		serializer.read(m_layers_count);
		serializer.read(m_layers_names);
		serializer.read(m_collision_filter);

		deserializeActors(serializer);
		deserializeControllers(serializer);
		deserializeTerrains(serializer);
		deserializeRagdolls(serializer);
		deserializeJoints(serializer);

		updateFilterData();
	}


	PhysicsSystem& getSystem() const override { return *m_system; }


	float getActorSpeed(Entity entity) override
	{
		auto* actor = m_actors[entity];
		if (actor->dynamic_type != DynamicType::DYNAMIC)
		{
			g_log_warning.log("Physics") << "Trying to get speed of static object";
			return 0;
		}

		auto* physx_actor = static_cast<PxRigidDynamic*>(actor->physx_actor);
		if (!physx_actor) return 0;
		return physx_actor->getLinearVelocity().magnitude();
	}


	void putToSleep(Entity entity) override
	{
		auto* actor = m_actors[entity];
		if (actor->dynamic_type != DynamicType::DYNAMIC)
		{
			g_log_warning.log("Physics") << "Trying to put static object to sleep";
			return;
		}

		auto* physx_actor = static_cast<PxRigidDynamic*>(actor->physx_actor);
		if (!physx_actor) return;
		physx_actor->putToSleep();
	}


	void applyForceToActor(Entity entity, const Vec3& force) override
	{
		auto& i = m_queued_forces.emplace();
		i.entity = entity;
		i.force = force;
	}


	static PxFilterFlags filterShader(
		PxFilterObjectAttributes attributes0, PxFilterData filterData0,
		PxFilterObjectAttributes attributes1, PxFilterData filterData1,
		PxPairFlags& pairFlags, const void* constantBlock, PxU32 constantBlockSize)
	{
		if (PxFilterObjectIsTrigger(attributes0) || PxFilterObjectIsTrigger(attributes1))
		{
			pairFlags = PxPairFlag::eTRIGGER_DEFAULT;
			return PxFilterFlag::eDEFAULT;
		}

		if (!(filterData0.word0 & filterData1.word1) || !(filterData1.word0 & filterData0.word1))
		{
			return PxFilterFlag::eKILL;
		}
		pairFlags = PxPairFlag::eCONTACT_DEFAULT | PxPairFlag::eNOTIFY_TOUCH_FOUND |
					PxPairFlag::eNOTIFY_CONTACT_POINTS;
		return PxFilterFlag::eDEFAULT;
	}


	struct QueuedForce
	{
		Entity entity;
		Vec3 force;
	};


	struct Controller
	{
		struct FilterCallback : PxQueryFilterCallback
		{
			FilterCallback(Controller& controller) : controller(controller) {}

			PxQueryHitType::Enum preFilter(const PxFilterData& filterData
				, const PxShape* shape
				, const PxRigidActor* actor
				, PxHitFlags& queryFlags) override 
			{
				PxFilterData fd0 = shape->getSimulationFilterData();
				PxFilterData fd1 = controller.m_filter_data;
				if (!(fd0.word0 & fd1.word1) || !(fd0.word0 & fd1.word1)) return PxQueryHitType::eNONE;
				return PxQueryHitType::eBLOCK;
			}

			PxQueryHitType::Enum postFilter(const PxFilterData& filterData, const PxQueryHit& hit) override
			{
				return PxQueryHitType::eNONE;
			}

			Controller& controller;
		};


		Controller() : m_filter_callback(*this) {}


		PxController* m_controller;
		Entity m_entity;
		Vec3 m_frame_change;
		float m_radius;
		float m_height;
		int m_layer;
		FilterCallback m_filter_callback;
		PxFilterData m_filter_data;

		float gravity_speed = 0;
	};

	IAllocator& m_allocator;

	Universe& m_universe;
	Engine* m_engine;
	PhysxContactCallback m_contact_callback;
	BoneOrientation m_new_bone_orientation = BoneOrientation::X;
	PxScene* m_scene;
	LuaScriptScene* m_script_scene;
	PhysicsSystem* m_system;
	PxRigidDynamic* m_dummy_actor;
	PxControllerManager* m_controller_manager;
	PxMaterial* m_default_material;

	AssociativeArray<Entity, RigidActor*> m_actors;
	AssociativeArray<Entity, Ragdoll> m_ragdolls;
	AssociativeArray<Entity, Joint> m_joints;
	AssociativeArray<Entity, Controller> m_controllers;
	AssociativeArray<Entity, Heightfield> m_terrains;

	Array<RigidActor*> m_dynamic_actors;
	RigidActor* m_update_in_progress;
	DelegateList<void(const ContactData&)> m_contact_callbacks;
	bool m_is_game_running;
	bool m_is_updating_ragdoll;
	u32 m_debug_visualization_flags;
	Array<QueuedForce> m_queued_forces;
	u32 m_collision_filter[32];
	char m_layers_names[32][30];
	int m_layers_count;
	CPUDispatcher m_cpu_dispatcher;
};




PhysicsScene* PhysicsScene::create(PhysicsSystem& system, Universe& context, Engine& engine, IAllocator& allocator)
{
	PhysicsSceneImpl* impl = LUMIX_NEW(allocator, PhysicsSceneImpl)(context, allocator);
	impl->m_universe.entityTransformed().bind<PhysicsSceneImpl, &PhysicsSceneImpl::onEntityMoved>(impl);
	impl->m_universe.entityDestroyed().bind<PhysicsSceneImpl, &PhysicsSceneImpl::onEntityDestroyed>(impl);
	impl->m_engine = &engine;
	PxSceneDesc sceneDesc(system.getPhysics()->getTolerancesScale());
	sceneDesc.gravity = PxVec3(0.0f, -9.8f, 0.0f);
	sceneDesc.cpuDispatcher = &impl->m_cpu_dispatcher;

	sceneDesc.filterShader = impl->filterShader;
	sceneDesc.simulationEventCallback = &impl->m_contact_callback;

	impl->m_scene = system.getPhysics()->createScene(sceneDesc);
	if (!impl->m_scene)
	{
		LUMIX_DELETE(allocator, impl);
		return nullptr;
	}

	impl->m_controller_manager = PxCreateControllerManager(*impl->m_scene);

	impl->m_system = &system;
	impl->m_default_material = impl->m_system->getPhysics()->createMaterial(9999.0f, 9999.0f, 0.1f);
	PxSphereGeometry geom(1);
	impl->m_dummy_actor = PxCreateDynamic(
		impl->m_scene->getPhysics(), PxTransform::createIdentity(), geom, *impl->m_default_material, 1);
	return impl;
}


void PhysicsScene::destroy(PhysicsScene* scene)
{
	PhysicsSceneImpl* impl = static_cast<PhysicsSceneImpl*>(scene);
	
	LUMIX_DELETE(impl->m_allocator, scene);
}


void PhysicsSceneImpl::RigidActor::onStateChanged(Resource::State, Resource::State new_state, Resource&)
{
	if (new_state == Resource::State::READY)
	{
		setPhysxActor(nullptr);

		PxTransform transform = toPhysx(scene.getUniverse().getTransform(entity).getRigidPart());

		scale = scene.getUniverse().getScale(entity);
		PxMeshScale scale(scale);
		PxConvexMeshGeometry convex_geom(resource->convex_mesh, scale);
		PxTriangleMeshGeometry tri_geom(resource->tri_mesh, scale);
		const PxGeometry* geom = resource->convex_mesh ? static_cast<PxGeometry*>(&convex_geom)
															  : static_cast<PxGeometry*>(&tri_geom);

		PxRigidActor* actor = scene.createPhysXActor(this, transform, *geom);

		if (actor)
		{
			setPhysxActor(actor);
		}
		else
		{
			g_log_error.log("Physics") << "Could not create PhysX mesh " << resource->getPath().c_str();
		}
	}
}


void PhysicsSceneImpl::RigidActor::rescale()
{
	if (!resource || !resource->isReady()) return;

	onStateChanged(resource->getState(), resource->getState(), *resource);
}


void PhysicsSceneImpl::RigidActor::setPhysxActor(PxRigidActor* actor)
{
	if (physx_actor)
	{
		scene.m_scene->removeActor(*physx_actor);
		physx_actor->release();
	}
	physx_actor = actor;
	if (actor)
	{
		scene.m_scene->addActor(*actor);
		actor->userData = (void*)(intptr_t)entity.index;
		scene.updateFilterData(actor, layer);
		scene.setIsTrigger({entity.index}, is_trigger);
	}
}


void PhysicsSceneImpl::RigidActor::setResource(PhysicsGeometry* _resource)
{
	if (resource)
	{
		resource->getObserverCb().unbind<RigidActor, &RigidActor::onStateChanged>(this);
		resource->getResourceManager().unload(*resource);
	}
	resource = _resource;
	if (resource)
	{
		resource->onLoaded<RigidActor, &RigidActor::onStateChanged>(this);
	}
}


Heightfield::Heightfield()
{
	m_heightmap = nullptr;
	m_xz_scale = 1.0f;
	m_y_scale = 1.0f;
	m_actor = nullptr;
	m_layer = 0;
}


Heightfield::~Heightfield()
{
	if(m_actor) m_actor->release();
	if (m_heightmap)
	{
		m_heightmap->getResourceManager().unload(*m_heightmap);
		m_heightmap->getObserverCb().unbind<Heightfield, &Heightfield::heightmapLoaded>(this);
	}
}


void Heightfield::heightmapLoaded(Resource::State, Resource::State new_state, Resource&)
{
	if (new_state == Resource::State::READY)
	{
		m_scene->heightmapLoaded(*this);
	}
}


void PhysicsScene::registerLuaAPI(lua_State* L)
{
	#define REGISTER_FUNCTION(name) \
		do {\
			auto f = &LuaWrapper::wrapMethod<PhysicsSceneImpl, decltype(&PhysicsSceneImpl::name), &PhysicsSceneImpl::name>; \
			LuaWrapper::createSystemFunction(L, "Physics", #name, f); \
		} while(false) \

	REGISTER_FUNCTION(putToSleep);
	REGISTER_FUNCTION(getActorSpeed);
	REGISTER_FUNCTION(applyForceToActor);
	REGISTER_FUNCTION(moveController);
	REGISTER_FUNCTION(isControllerCollisionDown);
	REGISTER_FUNCTION(setRagdollKinematic);
	REGISTER_FUNCTION(addForceAtPos);
	
	LuaWrapper::createSystemFunction(L, "Physics", "raycast", &PhysicsSceneImpl::LUA_raycast);

	#undef REGISTER_FUNCTION
}


} // namespace Lumix
