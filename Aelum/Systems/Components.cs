using System;
using System.Collections.Generic;
using System.Diagnostics;
using MessagePack;

public enum ComponentTypes : byte
{
    Script, //this guy is special :trollface:

    // physics
    DynamicBody,
    StaticBody,

    // 2d lights
    LightOccluder,
    LightProjector,

    // rendering
    QuadComponent,

    //misc
    EntityContainer,
    TileMapChunk,

    //audio
    SoundPlayer,

}

public static class ComponentFactory
{
    public static Component CreateFromData(Entity entity, ComponentData componentData)
    {
        switch (componentData.typeId)
        {
            case ComponentTypes.DynamicBody: return new DynamicBody(entity, componentData.serialData);
            case ComponentTypes.StaticBody: return new StaticBody(entity, componentData.serialData);
            case ComponentTypes.LightOccluder: return new LightOccluder(entity, componentData.serialData);
            case ComponentTypes.LightProjector: return new LightProjector(entity, componentData.serialData);
            case ComponentTypes.QuadComponent: return new Quad(entity, componentData.serialData);
            case ComponentTypes.EntityContainer: return new EntityContainer(entity, componentData.serialData);
//            case ComponentTypes.TileMapChunk: return new TileMapOfEntities(entity, componentData.serialData);
            case ComponentTypes.SoundPlayer: return new SoundPlayer(entity, componentData.serialData);
        }

        //scripts
        if (componentData.typeId == ComponentTypes.Script)
        {
            ScriptTypeAndData stad = MessagePackSerializer.Deserialize<ScriptTypeAndData>(componentData.serialData);
            return Activator.CreateInstance(Type.GetType(stad.ScriptType), new ScriptSerialData(entity, stad.ScriptData)) as Component;
        }

        throw new Exception("component type couldn't be resolved; make sure to add all component types to this method");
    }
}

public abstract class Component
{
    public readonly Entity entity;

    public virtual void FinalizeComponent(){}
    public virtual void EntityChanged(){}

    public abstract ComponentData GetSerialData();
    
    public Component(Entity entity)
    {
        this.entity = entity.RegisterComponentInternalEcsCall(this);
    }
}

