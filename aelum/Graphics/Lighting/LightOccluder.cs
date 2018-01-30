using System;
using System.Collections.Generic;
using MessagePack;
using Microsoft.Xna.Framework;

public class LightOccluder : ManagedChunkComponent<LightOccluder, OccluderSystem>
{
   public enum OccluderShape { Cross, Vertical, Horizontal }
   
   //segments of this occluder
   private readonly List<OccluderSegment> segments = new List<OccluderSegment>();

   //segments of this occluder in world space (cached for perf)
   private List<OccluderSegment> globalSegments;
   
   public List<OccluderSegment> GlobalSegments
   {
      get
      {
//         if (globalSegments == null)
//            UpdateGlobalSegments(); //TODO FIX THIS HIGH PRIORITY!
         return globalSegments;
      }
      //set => globalSegments = value;
   }

   private void UpdateGlobalSegments()
   {
      globalSegments = new List<OccluderSegment>(segments.Count);
      foreach (OccluderSegment segment in segments)
      {
         //TODO apply rotation too (sin/cos)
         globalSegments.Add(new OccluderSegment(entity.Position + segment.A, entity.Position + segment.B));
      }
   }

   public LightOccluder(Entity entity, byte system = 0) : base(entity, system)
   {
   }

   public LightOccluder(Entity entity, OccluderShape shape, float occluderSize) : this(entity) //TODO system
   {
      if (shape == OccluderShape.Cross)
      {
         segments.Add(new OccluderSegment(-Vector2.UnitX * occluderSize / 2, Vector2.UnitX * occluderSize / 2));
         segments.Add(new OccluderSegment(-Vector2.UnitY * occluderSize / 2, Vector2.UnitY * occluderSize / 2));
      }
      if (shape == OccluderShape.Horizontal)
      {
         segments.Add(new OccluderSegment(-Vector2.UnitX * occluderSize / 2, Vector2.UnitX * occluderSize / 2));
      }
      else
      {
         segments.Add(new OccluderSegment(-Vector2.UnitY * occluderSize / 2, Vector2.UnitY * occluderSize / 2));
      }
      UpdateGlobalSegments();
   }

   public LightOccluder(Entity entity, List<OccluderSegment> occluderSegments) : this(entity) //TODO system
   {
      segments = occluderSegments;
      UpdateGlobalSegments();
   }

   public LightOccluder(Entity entity, byte[] sd) : this(entity, MessagePackSerializer.Deserialize<List<OccluderSegment>>(sd))
   { }

   public override void EntityChanged()
   {
      base.EntityChanged();
      UpdateGlobalSegments();
   }

   public override ComponentData GetSerialData()
   {
      throw new NotImplementedException();
   }
}