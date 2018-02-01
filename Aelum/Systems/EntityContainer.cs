using System.Collections.Generic;
using MessagePack;
using Microsoft.Xna.Framework;

public class EntityContainer : Component
{

    //TODO when you remove a child and it ends up in an unloaded chunk, it won't persist...
    // we need a way to insert objects that fall outside the chunks bounds into chunks data
    // say for example an NPC walks to an unloaded zone, we should store it there

    //TODO static container for all entitycontainers so you can ask entities if they're parented to any?

    private List<ChildEntityData> children = new List<ChildEntityData>();
    
    public EntityContainer(Entity entity) : base(entity)
    {

    }

    public override void FinalizeComponent()
    {
        foreach (ChildEntityData ced in children)
        {
            ced.entity.Destroy();
        }
    }

    public void AddChild(Entity child, bool inheritRotation = true, bool inheritPosition = true)
    {
        foreach (ChildEntityData curChild in children)
        {
            if(curChild.entity == child)
                return; //already contains entity
            //TODO: check all others instead of just this?
        }

        child.shifts = false; // parent shifts it TODO breaks if doesn't inherit position - removed that mode?
        child.persistent = false; // persists via parent

        children.Add(
            new ChildEntityData(child, child.Position-entity.Position, child.Rotation-entity.Rotation, inheritPosition, inheritRotation)
        );
    }

    public void RemoveChild(Entity entity)
    {
        for (var i = children.Count - 1; i >= 0; i--)
        {
            if (children[i].entity == entity)
            {
                children.RemoveAt(i);
                return;
            }
        }
    }

    public override void EntityChanged()
    {
        base.EntityChanged();
        foreach (ChildEntityData child in children)
        {
            if(child.inheritPosition && child.inheritRotation) //todo use enum
            {
                child.entity.SetPositionAndRotation(
                    entity.Position + child.relativePosition.RotateRadians(entity.Rotation),
                    child.entity.Rotation = entity.Rotation + child.relativeRotation
                );
            }
            else if(child.inheritPosition)
            {
                child.entity.Position = entity.Position + child.relativePosition.RotateRadians(entity.Rotation);
            }
            else if(child.inheritRotation)
            {
                child.entity.Rotation = entity.Rotation + child.relativeRotation;
            }
        }
    }
    
    //serialization
    public override ComponentData GetSerialData()
    {
        Dictionary<entityData, ChildEntityData> cd = new Dictionary<entityData, ChildEntityData>();

        foreach (ChildEntityData ced in children)
        {
#if ORIGIN_SHIFT
            //we pass zero because the entity position isn't used so it doesn't matter
            cd.Add(ced.entity.GetCereal(Vector2.Zero), ced);
#else
            cd.Add(ced.entity.GetCereal(), ced);
#endif
        }
        
        return new ComponentData(ComponentTypes.EntityContainer, MessagePackSerializer.Serialize(cd));
    }
    public EntityContainer(Entity entity, byte[] serialData) : this(entity)
    {
        Dictionary<entityData, ChildEntityData> cd = MessagePackSerializer.Deserialize<Dictionary<entityData, ChildEntityData>>(serialData);

        foreach (KeyValuePair<entityData, ChildEntityData> pair in cd)
        {
            entityData ed = pair.Key;
            ChildEntityData ced = pair.Value;

            // we manually call this because we store position in relative coords (raw vec2), so we don't want to convert to fixed
            // we basically discard the position and rotation of the entityData when it's parented
            var ne = new Entity(entity.Position+ced.relativePosition, entity.Rotation+ced.relativeRotation, ed.components);

            AddChild(ne, ced.inheritRotation, ced.inheritPosition);
        }

    }


}