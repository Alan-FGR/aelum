using System;
using System.Collections.Generic;
using FarseerPhysics.Dynamics;
using MessagePack;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;


public struct ScriptSerialData
{
    public Entity entity;
    public Dictionary<string, object> scriptData;
    public ScriptSerialData(Entity entity, Dictionary<string, object> data)
    {
        this.entity = entity;
        this.scriptData = data;
    }
}

public abstract class Script : Behavior
{
    private Dictionary<string, object> scriptData;

    protected Script(Entity entity) : base(entity)
    {

    }

    protected Script(ScriptSerialData serialData) : this(serialData.entity)
    {
        scriptData = serialData.scriptData;
    }

    protected void StoreScriptData(string key, object data)
    {
        if (scriptData == null) scriptData = new Dictionary<string, object>();
        scriptData[key] = data;
    }

    protected T RetrieveScriptData<T>(string key)
    {
        object outval;
        if (scriptData.TryGetValue(key, out outval))
        {
            return (T) outval;
        }
        return default(T);
    }

//    protected void Put(object variable)
//    {
//
//    }
//
//    protected void Take<T>(ref T variable)
//    {
//
//    }

    public sealed override ComponentData GetSerialData()
    {
        BeforeSerialization();
        ScriptTypeAndData stad = new ScriptTypeAndData{ScriptType = GetType().AssemblyQualifiedName, ScriptData = scriptData};
        return new ComponentData(ComponentTypes.Script, MessagePackSerializer.Serialize(stad));
    }

    protected virtual void BeforeSerialization() //ovrd if you need to update script data before entity gets serialized
    {
        
    }

}

class PlayerController : Script
{
    public PlayerController(Entity entity) : base(entity)
    {

    }

    public override void Update()
    {

        int m = Keys.LeftShift.IsDown() ? 50 : 1;

        entity.Position += new Vector2(
                            Keys.A.IsDown() ? -1 : Keys.D.IsDown() ?  1 : 0,
                            Keys.W.IsDown() ?  1 : Keys.S.IsDown() ? -1 : 0
                            ) * Core.lastDT * 50f * m;

        entity.Rotation += Keys.Q.IsDown() ? 0.025f : Keys.E.IsDown() ? -0.025f : 0;

        

        if (Input.LMB.WasPressed())
        {
            for (int i = 0; i < 1; i++)
            {
//                var newEnt = new Entity(entity.Position);// + new Vector2(r.Next(-10,10)/10f, r.Next(-10,10)/10f));
//                newEnt.Direction = entity.Position-Core.mainCam.WorldMousePosition;
//                newEnt.AddComponent(new Projectile());
                //newEnt.AddComponent(new Sprite(new SpriteData(Color.PaleVioletRed)));
//                newEnt.AddComponent(new Quad(new QuadData(Sheet.Get(Sheet.ID.Obj_crate12), new Vector2(1,1), true)));
            }
        }



        if(Keys.Z.WasPressed())
            entity.Position += Vector2.UnitX*40;
        if(Keys.Z.WasReleased())
            entity.Position += Vector2.UnitX*-40;


        //Entity.KeepOriginForPosition(entity.Position);


        Dbg.AddDebugLine(entity.Position, Core.mainCam.WorldMousePosition, Color.Red);
        Dbg.AddDebugLine(entity.Position, entity.Position+entity.Direction, Color.White);
        Dbg.AddDebugText("PLAYER", entity.Position, Color.Green);

    }
}

class Projectile : Script
{
    public Vector2 lastPos;
    
    //private byte damage = 1;
    public float velocity_;
    public float lifeTime_;
    public float timeLived_;

    public Projectile(Entity entity) : base(entity)
    {
        lastPos = entity.Position;
        entity.persistent = false;
    }
    
    public Projectile(Entity entity, float velocity = 0.2f, float lifeTime = 20) : this(entity)
    {
        lifeTime_ = lifeTime;
        velocity_ = velocity;
    }
    
    public override void Update()
    {
        timeLived_ += Core.lastDT;
        if (timeLived_ > lifeTime_)
        {
            entity.Destroy();
            return;
        }

        entity.Position += entity.Direction*velocity_;

        Vector2 curPos = entity.Position;

        List<Fixture> cols = Physics.World.RayCast(lastPos, curPos);
        if (cols.Count > 0)
        {
            entity.Destroy();

            foreach (Fixture col in cols)
            {
//                FSCollisionShape s = (FSCollisionShape) col.userData;
//                foreach (Component component in s.entity.getComponents<Component>())
//                {
//                    (component as FI_Damageable)?.InformHit(damage);
//                }
            }
        }
        
        Dbg.AddDebugLine(lastPos, curPos, Color.Red);

        lastPos = curPos;
    }
  
}

class Rotator : Script
{
    private float speed;

    public Rotator(Entity entity, float speed) : base(entity)
    {
        this.speed = speed;
        StoreScriptData("spd", speed);
    }


    public Rotator(ScriptSerialData serialData) : base(serialData)
    {
        speed = RetrieveScriptData<float>("spd");
    }
    
    public override void Update()
    {
        entity.Rotation += speed*0.1f;

        if (Keys.X.WasPressed())
        {
            entity.Destroy();
        }

        if (Keys.L.IsDown())
        {
            entity.Position+=Vector2.UnitX*0.1f;
        }
        if (Keys.K.IsDown())
        {
            entity.Position-=Vector2.UnitX*0.1f;
        }

    }
}

class ICANHAZNAME : Script
{
    public string name;
    public ICANHAZNAME(Entity entity, string name) : base(entity)
    {
        this.name = name;
        StoreScriptData("n", name);
    }

    public ICANHAZNAME(ScriptSerialData serialData) : base(serialData)
    {
        name = RetrieveScriptData<string>("n");
    }

    public override void Update()
    {
        Dbg.AddDebugText(name, entity.Position, Color.White);
    }

    public override string ToString()
    {
        return "MYNAMEIS: "+name;
    }
}