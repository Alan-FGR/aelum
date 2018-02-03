#pragma once


#include "animation/animation_scene.h"
#include "animation/state_machine.h"
#include "engine/array.h"
#include "engine/path.h"
#include "imgui/imgui.h"


namespace Lumix
{

class ResourceManagerBase;
namespace Anim
{
	class ControllerResource;
	struct EventArray;
}


namespace AnimEditor
{


struct IAnimationEditor;
class Container;
class ControllerResource;
class Edge;
struct EntryNode;
class EventArray;


class Component
{
public:
	Component(Anim::Component* _engine_cmp, Container* parent, ControllerResource& controller)
		: engine_cmp(_engine_cmp)
		, m_parent(parent)
		, m_controller(controller)
	{
	}

	virtual ~Component();
	virtual void destroy() {}
	virtual bool draw(ImDrawList* draw, const ImVec2& canvas_screen_pos, bool selected) = 0;
	virtual void onGUI() {}
	virtual void serialize(OutputBlob& blob) = 0;
	virtual void deserialize(InputBlob& blob) = 0;
	virtual bool hitTest(const ImVec2& on_canvas_pos) const { return false; }
	virtual bool isNode() const = 0;
	virtual bool isContainer() const { return false; }
	virtual void drawInside(ImDrawList* draw, const ImVec2& canvas_screen_pos) {}
	Container* getParent() { return m_parent; }
	virtual void compile() {}
	virtual void debug(ImDrawList* draw, const ImVec2& canvas_screen_pos, Anim::ComponentInstance* runtime) {}
	virtual Component* getByUID(int uid) { return engine_cmp && uid == engine_cmp->uid ? this : nullptr; }
	virtual void debugInside(ImDrawList* draw,
		const ImVec2& canvas_screen_pos,
		Anim::ComponentInstance* runtime,
		Container* current)
	{
	}
	ControllerResource& getController() { return m_controller; }

	Anim::Component* engine_cmp;

protected:
	Container* m_parent;
	ControllerResource& m_controller;
};


class Node : public Component
{
public:
	Node(Anim::Component* engine_cmp, Container* parent, ControllerResource& controller);
	~Node();

	bool isNode() const override { return true; }
	void destroy() override;
	bool hitTest(const ImVec2& on_canvas_pos) const override;
	void onGUI() override;
	void serialize(OutputBlob& blob) override;
	void deserialize(InputBlob& blob) override;
	bool draw(ImDrawList* draw, const ImVec2& canvas_screen_pos, bool selected) override;
	void addEdge(Edge* edge) { m_edges.push(edge); }
	void addInEdge(Edge* edge) { m_in_edges.push(edge); }
	void removeEdge(Edge* edge) { m_edges.eraseItemFast(edge); }
	void removeInEdge(Edge* edge) { m_in_edges.eraseItemFast(edge); }
	void removeEvent(Anim::EventArray& events, int index);
	const Array<Edge*>& getEdges() { return m_edges; }
	const Array<Edge*>& getInEdges() { return m_in_edges; }

protected:
	void onGuiEvents(Anim::EventArray& events, const char* label);

public:
	ImVec2 pos;
	ImVec2 size;
	StaticString<64> name;

protected:
	Array<Edge*> m_edges;
	Array<Edge*> m_in_edges;
	IAllocator& m_allocator;
};


class Container : public Node
{
public:
	Container(Anim::Component* engine_cmp, Container* parent, ControllerResource& controller);
	~Container();
	Component* childrenHitTest(const ImVec2& pos);
	Component* getChildByUID(int uid);
	Component* getSelectedComponent() const { return m_selected_component; }
	void deserialize(InputBlob& blob) override;
	void serialize(OutputBlob& blob) override;
	void compile() override;
	Component* getByUID(int uid) override;
	virtual void dropSlot(const char* name, u32 slot, const ImVec2& canvas_screen_pos) {}
	virtual void removeChild(Component* component);
	bool isContainer() const override { return true; }
	void createEdge(int from_uid, int to_uid, int edge_uid);
	void destroyChild(int child_uid);
	virtual void createNode(Anim::Node::Type type, int uid, const ImVec2& pos) = 0;

protected:
	void contextMenu(const ImVec2& canvas_screen_pos);
	virtual bool isFixed(Node& node) const;
	void pasteNode(const ImVec2& pos_on_canvas);

protected:
	Array<Component*> m_editor_cmps;
	Component* m_selected_component = nullptr;
	Component* m_context_cmp = nullptr;
	Node* m_drag_source = nullptr;
};



class Edge : public Component
{
public:
	Edge(Anim::Edge* engine_cmp, Container* parent, ControllerResource& controller);
	~Edge();

	bool isNode() const override { return false; }
	void destroy() override;
	void onGUI() override;
	bool draw(ImDrawList* draw, const ImVec2& canvas_screen_pos, bool selected) override;
	void serialize(OutputBlob& blob) override;
	void deserialize(InputBlob& blob) override;
	void compile() override;
	bool hitTest(const ImVec2& on_canvas_pos) const override;
	const char* getExpression() const { return m_expression; }
	void debug(ImDrawList* draw, const ImVec2& canvas_screen_pos, Anim::ComponentInstance* runtime) override;
	Node* getFrom() const { return m_from; }
	Node* getTo() const { return m_to; }

private:
	Node* m_from;
	Node* m_to;
	StaticString<128> m_expression;
	Anim::Condition::Error m_expression_error;
};


class AnimationNode : public Node
{
public:
	struct AnimationProxy
	{
		explicit AnimationProxy(AnimationNode& node) : node(node) {}
		AnimationNode& node;

		u32 getValue() const;
		void setValue(u32 value);

		template <typename Root>
		void ui(IAnimationEditor& editor, const Root& root);
	};

	AnimationNode(Anim::Component* engine_cmp, Container* parent, ControllerResource& controller);

	float getSpeedMultiplier() const;
	void setSpeedMultiplier(float value);
	bool isLooped() const;
	void setIsLooped(bool is_looped);
	bool isNewSelectionOnLoop() const;
	void setIsNewSelectionOnLoop(bool is);
	void compile() override;
	void onGUI() override;
	void debug(ImDrawList* draw, const ImVec2& canvas_screen_pos, Anim::ComponentInstance* runtime) override;
	void deserialize(InputBlob& blob) override;
	void addAnimation(int idx);
	void removeAnimation(int idx);
	Array<AnimationProxy>& getAnimations() { return m_animations; }
	const Array<AnimationProxy>& getAnimations() const { return m_animations; }

	int root_rotation_input = -1;
	Array<AnimationProxy> m_animations;
};


class LayersNode : public Container
{
public:
	LayersNode(Anim::Component* engine_cmp, Container* parent, ControllerResource& controller);

	void compile() override;
	void onGUI() override;
	void serialize(OutputBlob& blob) override;
	void deserialize(InputBlob& blob) override;
	void drawInside(ImDrawList* draw, const ImVec2& canvas_screen_pos) override;
	void dropSlot(const char* name, u32 slot, const ImVec2& canvas_screen_pos) override;

private:
	void createNode(Anim::Component::Type type, int uid, const ImVec2& pos) override;

	Array<u32> m_masks;
};


class Blend1DNode : public Container
{
public:
	struct RootEdge;
	struct RootNode : public Node
	{
		RootNode(Container* parent, ControllerResource& controller);

		Array<RootEdge*> edges;
	};

public:
	Blend1DNode(Anim::Component* engine_cmp, Container* parent, ControllerResource& controller);

	void compile() override;
	void onGUI() override;
	void debug(ImDrawList* draw, const ImVec2& canvas_screen_pos, Anim::ComponentInstance* runtime) override;
	void serialize(OutputBlob& blob) override;
	void deserialize(InputBlob& blob) override;
	void drawInside(ImDrawList* draw, const ImVec2& canvas_screen_pos) override;
	RootNode* getRootNode() const { return m_root_node; }
	void removeChild(Component* component) override;
	void dropSlot(const char* name, u32 slot, const ImVec2& canvas_screen_pos) override;
	void debugInside(ImDrawList* draw,
		const ImVec2& canvas_screen_pos,
		Anim::ComponentInstance* runtime,
		Container* current) override;

private:
	void createNode(Anim::Component::Type type, int uid, const ImVec2& pos) override;
	RootEdge* createRootEdge(Node* node);
	bool isFixed(Node& node) const override;

private:
	enum MouseStatus
	{
		NONE,
		DOWN_LEFT,
		DOWN_RIGHT,
		DRAG_NODE,
		NEW_EDGE
	} m_mouse_status;
	int m_input = -1;
	RootNode* m_root_node;
};


struct EntryNode : public Node
{
	EntryNode(Container* parent, ControllerResource& controller);

	Array<struct EntryEdge*> entries;
};


class StateMachine : public Container
{
public:
	StateMachine(Anim::Component* engine_cmp, Container* parent, ControllerResource& controller);

	void drawInside(ImDrawList* draw, const ImVec2& canvas_screen_pos) override;
	void onGUI() override;
	void debugInside(ImDrawList* draw,
		const ImVec2& canvas_screen_pos,
		Anim::ComponentInstance* runtime,
		Container* current) override;
	void debug(ImDrawList* draw, const ImVec2& canvas_screen_pos, Anim::ComponentInstance* runtime) override;
	void deserialize(InputBlob& blob) override;
	void serialize(OutputBlob& blob) override;
	EntryNode* getEntryNode() const { return m_entry_node; }
	void removeChild(Component* component) override;
	void compile() override;
	void removeEntry(EntryEdge& entry);
	void dropSlot(const char* name, u32 slot, const ImVec2& canvas_screen_pos) override;

private:
	void createNode(Anim::Component::Type type, int uid, const ImVec2& pos) override;
	EntryEdge* createEntryEdge(Node* node);
	bool isFixed(Node& node) const override;

private:
	enum MouseStatus
	{
		NONE,
		DOWN_LEFT,
		DOWN_RIGHT,
		DRAG_NODE,
		NEW_EDGE
	} m_mouse_status;
	EntryNode* m_entry_node;
};


class ControllerResource
{
public:
	struct Mask
	{
		class Bone
		{
			public:
				explicit Bone(ControllerResource& _controller) 
					: controller(_controller)
					, name("", _controller.m_allocator) {}

				void setName(const string& name);
				const string& getName() const { return name; }

			private:
				string name;
				ControllerResource& controller;
		};

		explicit Mask(ControllerResource& _controller) 
			: controller(_controller)
			, bones(controller.m_allocator)
		{}

		void addBone(int index);
		void removeBone(int index);
		const StaticString<32>& getName() const { return name; }
		void setName(const StaticString<32>& value);
		
		ControllerResource& controller;
		Array<Bone> bones;
	
	
	private:
		StaticString<32> name;
	};

	struct InputProxy
	{
		struct ValueProxy
		{
			explicit ValueProxy(InputProxy& input) : input(input) {}
			InputProxy& input;
		};

		InputProxy(ControllerResource& resource)
			: resource(resource)
			, value_proxy(*this)
		{}

		void setName(const StaticString<32>& name);
		const StaticString<32>& getName() const;

		int getEngineIdx() const { return engine_idx; }
		void setEngineIdx(int idx);

		Anim::InputDecl::Type getType() const;
		void setType(Anim::InputDecl::Type type);

		ValueProxy& getValue() { return value_proxy; }
		const ValueProxy& getValue() const { return value_proxy; }

		ValueProxy value_proxy;
		int engine_idx;
		ControllerResource& resource;
	};

	struct ConstantProxy
	{
		struct ValueProxy
		{
			explicit ValueProxy(ConstantProxy& input) : input(input) {}
			ConstantProxy& input;
		};

		ConstantProxy(ControllerResource& resource)
			: resource(resource)
			, value_proxy(*this)
		{}

		void setName(const StaticString<32>& name);
		const StaticString<32>& getName() const;

		int getEngineIdx() const { return engine_idx; }
		void setEngineIdx(int idx);

		Anim::InputDecl::Type getType() const;
		void setType(Anim::InputDecl::Type type);

		ValueProxy& getValue() { return value_proxy; }
		const ValueProxy& getValue() const { return value_proxy; }

		ValueProxy value_proxy;
		int engine_idx;
		ControllerResource& resource;
	};

	struct AnimationSlot
	{
		explicit AnimationSlot(ControllerResource& controller) 
			: controller(controller) 
			, values(controller.getAllocator())
		{}

		const StaticString<32>& getName() const { return name; }
		void setName(const StaticString<32>& name);

		/*void serialize(OutputBlob& blob);
		void deserialize(InputBlob& blob);*/

		struct Value
		{
			explicit Value(AnimationSlot& slot) : slot(slot) {}

			const Path& get() const;
			void set(const Path& anim);

			Animation* anim = nullptr;
			AnimationSlot& slot;
		};

		Array<Value> values;
		StaticString<32> name;
		ControllerResource& controller;
	};


	struct AnimationSet
	{
		explicit AnimationSet(ControllerResource& controller) 
			: controller(controller)
			, values(controller.getAllocator())
		{}

		const StaticString<32>& getName() const;
		void setName(const StaticString<32>& name);

		struct Value
		{
			explicit Value(AnimationSet& set) : set(set) {}

			const Path& getValue() const;
			void setValue(const Path& anim);

			Animation* anim = nullptr;
			AnimationSet& set;
		};

		Array<Value> values;
		ControllerResource& controller;
	};

public:
	ControllerResource(IAnimationEditor& editor,
		ResourceManagerBase& manager,
		IAllocator& allocator);
	~ControllerResource();

	void serialize(OutputBlob& blob);
	bool deserialize(InputBlob& blob, Engine& engine, IAllocator& allocator);
	Component* getRoot() { return m_root; }
	
	const Array<AnimationSlot>& getAnimationSlots() const { return m_animation_slots; }
	Array<AnimationSlot>& getAnimationSlots() { return m_animation_slots; }
	void addSlot(int idx);
	void removeSlot(int idx);

	const Array<AnimationSet>& getAnimationSets() const { return m_animation_sets; }
	Array<AnimationSet>& getAnimationSets() { return m_animation_sets; }
	void addAnimationSet(int idx);
	void removeAnimationSet(int idx);

	const Array<Mask>& getMasks() const { return m_masks; }
	Array<Mask>& getMasks() { return m_masks; }
	void addMask(int index);
	void removeMask(int index);

	const Array<InputProxy>& getInputs() const { return m_inputs; }
	Array<InputProxy>& getInputs() { return m_inputs; }
	void addInput(int index);
	void removeInput(int index);

	const Array<ConstantProxy>& getConstants() const { return m_constants; }
	Array<ConstantProxy>& getConstants() { return m_constants; }
	void addConstant(int index);
	void removeConstant(int index);

	IAllocator& getAllocator() { return m_allocator; }
	Anim::ControllerResource* getEngineResource() { return m_engine_resource; }
	IAnimationEditor& getEditor() { return m_editor; }
	int createUID() { ++m_last_uid; return m_last_uid; }
	const char* getAnimationSlot(u32 slot_hash) const;
	Component* getByUID(int uid);

private:
	ControllerResource(const ControllerResource& rhs);

	int m_last_uid = 0;
	IAnimationEditor& m_editor;
	IAllocator& m_allocator;
	Component* m_root;
	Anim::ControllerResource* m_engine_resource;
	Array<Mask> m_masks;
	Array<InputProxy> m_inputs;
	Array<ConstantProxy> m_constants;
	Array<AnimationSlot> m_animation_slots;
	Array<AnimationSet> m_animation_sets;
};


} // namespace AnimEditor

} // namespace Lumix