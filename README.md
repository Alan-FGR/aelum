# Project Status: WIP
*clever code != good code*
#### How to build test game:
1. Clone repository recursively
3. Open Solution in VS2017
3. Build and Run TestGameAssets (this step sucks :worried:, more info below)
3. Setup FNA binaries (deps)
7. Cross fingers and hit Build :trollface:

**NOTE:** 3rd step is required because it generates code that describes the assets, easiest way to rebuild your assets is to build and run in a new instance so you don't have to switch to the project. You can also automate that to automatically rebuild whenever files in your assets directories change.

# Scripting
Basic script:
```C#
public class SampleScript : Script
{
    public SampleScript(Entity entity) : base(entity)
    {
        // INITIALIZATION, you can access entity here
    }

    public override void Update()
    {
        // UPDATING
    }
}
```
This engine is focused on persistency, so it's easy to store script data:
```C#
public class PersistentScript : Script
{
    float someData_;
    
    // this is the normal constructor you call when creating the engine for the first time, it's not
    // called when deserializing the script from persistent storage
    public PersistentScript(Entity entity, float someData) : base(entity)
    {
        someData_ = someData;
    }

    // this is the deserialization ctor, pay no attention to params, just alt-enter it, we do this
    // so we don't need an AfterDeserialization callback what would require us to track initialization
    // state of the object since in C# you can't have a virtual call in the base class ctor
    public PersistentScript(Entity entity, Dictionary<string, object> scriptData) : base(entity, scriptData)
    {
        someData_ = RetrieveScriptData<float>("someKey");
    }

    // data is (de)serialized automatically, but use this to know when script is being serialized
    // so you can update the data before that if necessary (not required)
    protected override void BeforeSerialization()
    {
        StoreScriptData("someKey", someData_);
    }

    public override void Update(){}
}
```
The script above will automatically persist in the entity, it will be saved to disk when the chunk is being serialized, and loaded again when it's being deserialized. All primitive types are supported along with collections and some core types like Vectors and Rectangles.

# Why do we do stuff differently?

Upon a closer look at the sources, the attentive coder will certainly notice that many things are done differently than the way most people are used to. While it's true that a lot of the code simply sucks, we try to keep things as simple as possible by taking advantage of the environment peculiarities (basically outsourcing complexity so we don't have to keep it in our code). Most notably, along with their respective reasoning, we have the following:

- There's no `Entity.AddComponent(new MyComponent())`:
	- We do this in order to reduce complexity by using the constructor for the components initialization as opposed to having separate initialization routines. We don't have a real ECS but rather a plugin system. Most engines out there don't have a real ECS too, including but not limited to Unity, Nez, Otter and Duality; in all of these engines the component stores a reference to the entity, as we do, but that's much more clear when you're passing the entity in the component constructor too. So we don't hide that from you nor pretend we got a [real ECS](https://github.com/nem0/LumixEngine/tree/master/src/engine) under the hood.
- Why sprites are hardcoded? [This is not the 80s :trollface:!](https://gitter.im/nem0/LumixEngine?at=59ec9d075c40c1ba79d07a43)
	- It's true that sucks, but by doing that as opposed to a true data-driven approach we got 'free' stable and efficient serialization, and code tools works for them like autocompletion, refactoring, and compile-time safety checks. That being said it's not an ideal solution by any means, and [we have plans to change that](https://github.com/Alan-FGR/aelum/issues/3). [This is the 80s though!!1!](https://gfycat.com/gifs/detail/WarlikeScornfulBlackfish) :trollface:
- Why so much constructor piping?
    - We [didn't have that once](https://github.com/Alan-FGR/aelum/commit/e3cc8f360f4be1e89b74a2f9bc16332124d1a6ef), but the implications were too bad for a minor annoyance in the user code, so we decided it's not worthy it.

# General Development Directions
## Core
- Components should always initialize all its non-private members on the main constructor, so provide a 'base' constructor that does that and always pipe the others through it (`OtherCtor() : this()`) the idea here is that all members should be ready to be acessed in the subclasses ctors, this doesn't apply to user scripts of course, just engine components.
