using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MessagePack;
using Microsoft.Xna.Framework;

// this sucks, here's what a decent ECS looks like:
// you keep entities in a fast pool, entites simply hold components (incl. position)
// components don't know about the entities they're attached to
// you loop the pool and pass the entity to each component - where you do shit with them
// entities[data_holder] -> components[data] -> systems[data_processors]

public sealed partial class Entity
{
    private static readonly List<Entity> entities_ = new List<Entity>();
    
    // instance fields
    private Vector2 position_;
    private float rotation_;

    public bool shifts
#if ORIGIN_SHIFT
    {
        private get
        {
            return shifts_;
        }
        set
        {
            shifts_ = value;
        }
    }
    private bool shifts_ = true; // not all entities should be directly origin shifted, e.g.: parented entities 

    public Vector2 LastPosition { get; private set; } // this is used in the region and chunk system

#else
    { private get { return false; } set {}}
#endif

    public bool persistent = true; // not all entities should be saved to disk, e.g.: parented entities are saved by their parents, bullets

    // accessors
    public Vector2 Position {
        get => position_;
        set
        {
#if ORIGIN_SHIFT      
            LastPosition = position_;
#endif
            position_ = value; InformSpatialChange();
        }
    }
    public float Rotation {
        get => rotation_;
        set { rotation_ = value; InformSpatialChange(); }
    }
    public Vector2 Direction {
        get => MathUtils.AngleToDirection(rotation_);
        set => Rotation = MathUtils.DirectionToAngle(value);
    }
    
    public void SetPositionAndRotation(Vector2 position, float rotation)
    {
#if ORIGIN_SHIFT      
        LastPosition = position_;
#endif
        position_ = position;
        rotation_ = rotation;
        InformSpatialChange();
    }
    public void SetPositionAndDirection(Vector2 position, Vector2 direction)
    {
#if ORIGIN_SHIFT      
        LastPosition = position_;
#endif
        position_ = position;
        Direction = direction; //this will call Rotation which will call InformSpatialChange
    }
    
    private void InformSpatialChange()
    {
#if ORIGIN_SHIFT
        EntityChunkRegionSystem.UpdateChunkSystemForEntity(this);
#endif
        foreach (Component component in components)
            component.EntityChanged();
    }

    private List<Component> components = new List<Component>();

    public Entity(Vector2 position, float rotation, List<ComponentData> components = null) // def constr
    {
#if ORIGIN_SHIFT
        LastPosition = new Vector2(float.MinValue, float.MaxValue); 
#endif
        position_ = position;
        rotation_ = rotation;
        entities_.Add(this);

        if (components != null)
        {
            foreach (ComponentData data in components)
            {
                ComponentFactory.CreateFromData(this, data);
            }
        }

        InformSpatialChange();
    }
    // overloads for convenience, REMEMBER to always call def from them
    public Entity(Vector2 position) : this(position, 0){}
    public Entity(Vector2 position, Vector2 direction) : this(position, MathUtils.DirectionToAngle(direction)){}
    public Entity() : this(Vector2.Zero, 0){}
    
//    public T AddComponent<T>(T component) where T : Component
//    {
//        component.InitializeComponent(this);
//        components.Add(component);
//        return component;
//    }

    public void RemoveComponent(Component component)
    {
        if (components.Contains(component))
        {
            components.Remove(component);
            component.FinalizeComponent();
        }
    }

    public T GetComponent<T>() where T:Component
    {
        foreach (Component c in components)
        {
            T cast = c as T;
            if (cast != null) return cast;
        }
        return null;
    }

    public bool GetComponent<T>(out T component) where T : Component
    {
        component = GetComponent<T>();
        return component != null;
    }

    internal Entity RegisterComponentInternalEcsCall(Component c)
    {
        components.Add(c);
        return this;
    }

    private bool destroying_ = false;
    public void Destroy() // no way to have reliable deterministic finalization :(
    {
      //don't destroy twice
       if (destroying_) return;
        destroying_ = true;

        entities_.Remove(this);
#if ORIGIN_SHIFT
        EntityChunkRegionSystem.RemoveEntityFromSystem(this);
#endif
        // we use this so we don't have to use weakrefs in the systems
        foreach (Component component in components)
            component.FinalizeComponent();
    }

#if DEBUG
    ~Entity()
    {
        if (!destroying_)
            throw new Exception("entity was finalized (by GC probably) without being destroyed");
        //Q: why can't we just do this for release so when entity isn't destroyed manually it's destroyed here (dtor)?
        //A: because this isn't deterministic (i.e.: unreliable) so we gotta make sure to destroy entities manually
        //what we could do however is use weak references, but that's slow and unpleasant to deal with
    }
#endif


#if ORIGIN_SHIFT
    public static void ShiftAllEntities(Vector2 shift)
    {
        foreach (Entity entity in entities_)
            if(entity.shifts)
                entity.Position += shift;
    }
    public Entity(entityData ed, Vector2 positionOffset) : this(positionOffset+ed.position.ToVector2(), ed.rotation, ed.components){}
    public entityData GetCereal(Vector2 positionOffset) //entities are serialized relative to their chunks
    {
        return new entityData{position = vec2fixed.FromVec2(position_-positionOffset), rotation = rotation_, components = GetComponentsCerealList()};
    }
#else
    public Entity(entityData ed) : this(ed.position, ed.rotation, ed.components){}
    public entityData GetCereal() //entities are serialized relative to their chunks
    {
        return new entityData{position = position_, rotation = rotation_, components = GetComponentsCerealList()};
    }

    public static void DestroyAll()
    {
        for (var i = entities_.Count - 1; i >= 0; i--)
        {
            entities_[i].Destroy();
        }
    }

    public static void SaveAll(string path)
    {
        var ecds = new List<entityData>(entities_.Count);
        foreach (Entity entity in entities_)
        {
            if(entity.persistent)
                ecds.Add(entity.GetCereal());
        }
        File.WriteAllBytes(path, MessagePackSerializer.Serialize(ecds));
    }

    public static void LoadAll(string path)
    {
        if (!File.Exists(path)) return;
        var ecds = MessagePackSerializer.Deserialize<List<entityData>>(File.ReadAllBytes(path));
        foreach (entityData entityData in ecds)
        {
            new Entity(entityData);
        }
    }
#endif

    List<ComponentData> GetComponentsCerealList()
    {
        List<ComponentData> retList = new List<ComponentData>();
        foreach (Component c in components)
        {
            retList.Add(c.GetSerialData());
        }
        return retList;
    }

}