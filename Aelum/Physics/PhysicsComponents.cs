﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Factories;
using MessagePack;
using Microsoft.Xna.Framework;

public static class cc //collision categories
{
   static cc() { }

   public static Category none = Category.None;
   public static Category all = Category.All;

   public static Category walls = Category.Cat1;
   public static Category players = Category.Cat2;
   public static Category enemies = Category.Cat3;
   public static Category vehicles = Category.Cat4;
   //    public static Category c5 = Category.Cat5;
   //    public static Category c6 = Category.Cat6;
   //    public static Category c7 = Category.Cat7;
   //    public static Category c8 = Category.Cat8;
   //    public static Category c9 = Category.Cat9;
   //    public static Category c10 = Category.Cat10;
   //    public static Category c11 = Category.Cat11;
   //    public static Category c12 = Category.Cat12;
   //    public static Category c13 = Category.Cat13;
   //    public static Category c14 = Category.Cat14;
   //    public static Category c15 = Category.Cat15;
   //    public static Category c16 = Category.Cat16;
   //    public static Category c17 = Category.Cat17;
   //    public static Category c18 = Category.Cat18;
   //    public static Category c19 = Category.Cat19;
   //    public static Category c20 = Category.Cat20;
   //    public static Category c21 = Category.Cat21;
   //    public static Category c22 = Category.Cat22;
   //    public static Category c23 = Category.Cat23;
   //    public static Category c24 = Category.Cat24;
   //    public static Category c25 = Category.Cat25;
   //    public static Category c26 = Category.Cat26;
   //    public static Category c27 = Category.Cat27;
   //    public static Category c28 = Category.Cat28;
   //    public static Category c29 = Category.Cat29;
   //    public static Category c30 = Category.Cat30;
   //    public static Category c31 = Category.Cat31;
}

public static class Physics
{
   public static World World { get; private set; }

   internal static void TickWorld(float deltaTime)
   {
      World?.Step(deltaTime);
   }

   internal static void InitWorld(Vector2? gravity = null)
   {
      World = new World(gravity ?? new Vector2(0, -9.8f));
   }
}

public abstract class PhysicalBody : Component
{
   static PhysicalBody()
   {
      if (Physics.World == null)
      {
         Debug.WriteLine("starting physics world with default values, no setting provided");
         Physics.InitWorld();
      }
   }

   protected Body Body { get; }
   protected List<colliderData> CollidersConfs { get; } = new List<colliderData>();

   protected PhysicalBody(Entity entity) : base(entity)
   {
      Body = new Body(Physics.World, entity.Position, entity.Rotation, this);
   }

   public override void FinalizeComponent()
   {
      Physics.World.RemoveBody(Body);
   }

   public PhysicalBody CreateCollider(colliderData data)
   {
      data.AddColliderToBody(Body);
      CollidersConfs.Add(data);
      return this;
   }

   protected void CreateColliders(List<colliderData> cols)
   {
      foreach (colliderData col in cols)
         CreateCollider(col);
   }

   public override void EntityChanged()
   {
      base.EntityChanged();
      Body.Position = entity.Position;
      Body.Rotation = entity.Rotation;
   }

   public void UpdatePositionFromBody()
   {
      entity.SetPositionAndRotation(Body.Position, Body.Rotation);
   }
}

/// <summary> body that never updates node from physWorld data </summary>
public class StaticBody : PhysicalBody
{
   public StaticBody(Entity entity) : base(entity)
   {
      Body.BodyType = BodyType.Static;
   }

   public StaticBody(Entity entity, byte[] data) : this(entity)
   {
      List<colliderData> cols = MessagePackSerializer.Deserialize<List<colliderData>>(data);
      CreateColliders(cols);
   }

   public override ComponentData GetSerialData()
   {
      return new ComponentData(ComponentTypes.StaticBody, MessagePackSerializer.Serialize(CollidersConfs));
   }
}

/// <summary> body that updates node position from physWorld data </summary>
public class DynamicBody : PhysicalBody
{
   public bool IsKinematic
   {
      get => Body.BodyType == BodyType.Kinematic;
      set => Body.BodyType = value ? BodyType.Kinematic : BodyType.Dynamic;
   }

   public DynamicBody(Entity entity) : base(entity)
   {
      Body.BodyType = BodyType.Dynamic;
   }

   public DynamicBody(Entity entity, byte[] data) : this(entity)
   {
      DynamicBodyData dbd = MessagePackSerializer.Deserialize<DynamicBodyData>(data);
      IsKinematic = dbd.kinematic;
      CreateColliders(dbd.colliders);
   }

   public DynamicBody(Entity entity, bool kinematic) : this(entity)
   {
      if (kinematic)
         IsKinematic = true;
   }

   public static void UpdateAllBodies()
   {
      foreach (Body body in Physics.World.BodyList)
      {
         if (body.BodyType != BodyType.Static && body.Awake)
         {
            (body.UserData as PhysicalBody)?.UpdatePositionFromBody();
         }
      }
   }

   public override ComponentData GetSerialData()
   {
      return new ComponentData(ComponentTypes.DynamicBody, MessagePackSerializer.Serialize(new DynamicBodyData { colliders = CollidersConfs, kinematic = IsKinematic }));
   }
}