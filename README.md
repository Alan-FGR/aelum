# Project Status: WIP - [![Build status](https://ci.appveyor.com/api/projects/status/gfmeiefkf3dnffie/branch/master?svg=true)](https://ci.appveyor.com/project/Alan-FGR/aelum/branch/master) [![License](http://img.shields.io/:license-mit-blue.svg)](http://doge.mit-license.org) [![Since](https://img.shields.io/badge/since-3200BC-lightgray.svg)](https://github.com/Alan-FGR/aelum/blob/master/LICENSE)

![logo anim](Docs/aelum.gif)

### How to build the test game:
1. Clone repository recursively  
3. Open Solution in VS2017
3. Setup Pipeline and FNA binaries (deps)
7. Cross fingers and hit Build :trollface:

**NOTE:** 3rd step is required because it generates code that describes the assets, easiest way to rebuild your assets is to build and run in a new instance so you don't have to switch to the project. You can also automate that to automatically rebuild whenever files in your assets directories change.


# Scripting

Basic script:
```C#
public class SampleScript : Script
{
    public SampleScript(Entity entity) : base(entity)
    {
        // INITIALIZATION, code here can already access any component/entity member
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
    
    // this is the normal constructor you call when creating the entity for the first time, it's not
    // called when deserializing the script from persistent storage
    public PersistentScript(Entity entity, float someData) : base(entity)
    {
        someData_ = someData;
    }

    // this is the deserialization ctor, pay no attention to params, just alt-insert them. we do this
    // so we don't need an AfterDeserialization callback what would require us to track initialization
    // state of the object since in C# you can't have a virtual call in the base class ctor
    public PersistentScript(ScriptSerialData serialData) : base(serialData)
    {
        someData_ = RetrieveScriptData<float>("someKey");
    }

    // data is (de/)serialized automatically, but use this to know when script is being serialized
    // so you can update the data before that if necessary (not required)
    protected override void BeforeSerialization()
    {
        StoreScriptData("someKey", someData_);
    }
}
```
The script above will automatically persist in the entity, it will be saved to disk when the chunk is being serialized, and loaded again when it's being deserialized. All primitive types are supported by Store/RetrieveScriptData, along with collections and some core types like Vectors, Rectangles and Colors.


# Pipeline

### Graphical Pipeline Tool (currently Windows only)

ælum has a GUI pipeline tool to make your life easier, the ideal usage is to configure it to monitor your asset source folders and keep it running in the background while you develop your game, so new assets will be automatically imported and copied into the configured output folder, while code will be generated into your project for easy usage.

#### Overview
![Pipeline Tool](Docs/pipeline.png)

- **Top buttons:**
  - Import on Change: when on, assets are imported when changes are detected
  - Build Active: immediately builds all assets in active folders (green)
  - Build All: immediately builds all assets, even from inactive folders
- **Source Paths**
  - Click the left button on each path to de/activate it, the "Browse" button allows you to select a folder in your filesystem that contains the path to the asset sources. Only active paths are monitored for changes, and only when the 'Import on Change' button is on. When folders are being monitored for changes a number cycles in the switch button as an indication of that.
- **Binaries**
  - Allow you to select binaries that are used in the pipeline (currently only DX compiler is used)
- **Output Directories**
  - Should be self-explanatory, they are the folders where output files will be copied to

The pipeline tool is a minimal application (30kb) that loads settings from the working folder at startup and saves them upon exit, so you can keep it in your working directories with no problems and can add the binary in any repository. Full source code is included but it's not necessary to compile it to use the engine.

#### Naming functions

The pipeline is a powerful tool and allows you to code naming functions for your assets, think of them like shaders but in C# and for paths, you get three string vars as the input:

- 'file' which contains the filename without extension
- 'path' which contains the full path to the file
- 'folder' which is the folder where the file is (last in path)

And you have to return a string that will be used as the name your asset resource. By default the filename is returned, but you can use any C# code (.NET 3.5), for example:

![Naming Funcs](Docs/naming.png)

Use the "Preview Results" button to check if the code is correct, don't be afraid of making mistakes, errors in code will be displayed in the output box.

### Supported Formats (Currently - more coming soon!)

[RELATED ISSUE](https://github.com/Alan-FGR/aelum/issues/10) <- Current Features

|TYPE|EXTENSION|Details|
|----|---------|-------|
|Atlased Sprites|PNG|non-atlased textures coming soon|
|Sound Effects|WAV|compression may cause leading/trailing silence|
|Musics|OGG|not only BGM but any long/stream audio|
|Fonts|XNB|WIP, have to be precompiled atm|
|Shaders|HLSL|DirectX Effects v2.0|
|Meshes|---|NOT CURRENTLY SUPPORTED|
|Others|N/A|Files are simply copied|


# Why do we do stuff differently?

Upon a closer look at the sources, the attentive coder will certainly notice that many things are done differently than the way most people are used to. While it's true that a lot of the code simply sucks, we try to keep things as simple as possible by taking advantage of the environment peculiarities (basically outsourcing complexity so we don't have to keep it in our code). Most notably, along with their respective reasoning, we have the following:

- There's no `Entity.AddComponent(new MyComponent())`:
	- We do this in order to reduce complexity by using the constructor for the components initialization as opposed to having separate initialization routines. We don't have a real ECS but rather a plugin system. Most engines out there don't have a real ECS too, including but not limited to Unity, Nez, Otter and Duality; in all of these engines the component stores a reference to the entity, as we do, but that's much more clear when you're passing the entity in the component constructor too. So we don't hide that from you nor pretend we got a [real ECS](https://github.com/nem0/LumixEngine/tree/master/src/engine) under the hood.
- Why sprites are hardcoded? [This is not the 80s :trollface:!](https://gitter.im/nem0/LumixEngine?at=59ec9d075c40c1ba79d07a43)
	- It's true that sucks, but by doing that as opposed to a true data-driven approach we got 'free' stable and efficient serialization, and code tools works for them like autocompletion, refactoring, and compile-time safety checks. That being said it's not an ideal solution by any means, and [we have plans to change that](https://github.com/Alan-FGR/aelum/issues/3). [This is the 80s though!!1!](https://gfycat.com/gifs/detail/WarlikeScornfulBlackfish) :trollface: - Update: this was fixed! Sprites now are stored in a Dictionary and have int keys for faster lookup, pipeline still generates code for easy referencing, but that's not necessary at all, and you can easily modify the importer to generate JSON for example.
- Why so much constructor piping?
    - We [didn't have that once](https://github.com/Alan-FGR/aelum/commit/e3cc8f360f4be1e89b74a2f9bc16332124d1a6ef), but the implications were too bad for a minor annoyance in the user code, so we decided it's not worthy it.


# General Development Directions

### Core

- Components should always initialize all its non-private members on the main constructor, so provide a 'base' constructor that does that and always pipe the others through it (`OtherCtor() : this()`) the idea here is that all members should be ready to be acessed in the subclasses ctors, this doesn't apply to user scripts of course, just engine components.

## Docs

- Comment styling: Keep it simple! XML comments sucks! At most this is fine for methods: `/// <summary> Some actually useful comment. No bullshit here please; only really relevant stuff! </summary>` (on a single line).

# Goodies

Too few badges?

[![We could](http://img.shields.io/:We-could-brightgreen.svg)](https://github.com/Alan-FGR/BogusBadges)
[![have all](http://img.shields.io/:have-all-green.svg)](https://github.com/Alan-FGR/BogusBadges)
[![them badges](http://img.shields.io/:them-badges-yellowgreen.svg)](https://github.com/Alan-FGR/BogusBadges)
[![if we really](http://img.shields.io/:if_we-really-yellow.svg)](https://github.com/Alan-FGR/BogusBadges)
[![wanted (° ͜ʖ°)](https://img.shields.io/:wanted-(%C2%B0%20%CD%9C%CA%96%C2%B0)-blue.svg)](https://github.com/Alan-FGR/BogusBadges)
