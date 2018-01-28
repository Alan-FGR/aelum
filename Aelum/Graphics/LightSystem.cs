using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using FarseerPhysics.Collision;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Dynamics;
using MessagePack;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


[SuppressMessage("ReSharper", "NotAccessedField.Local")] [SuppressMessage("ReSharper", "InconsistentNaming")]
struct OccluderVertexFormat
{
   private Vector3 position;
   private float extrude;
   private float isProjector;

   public const float NoCorner = 0f;
   public const float Corner0 = 0.15f;
   public const float Corner1 = 0.25f;
   public const float Corner2 = 0.35f;
   public const float Corner3 = 0.45f;

   public OccluderVertexFormat(Vector3 position, float extrude, float isProjector = NoCorner)
   {
      this.position = position;
      this.extrude = extrude;
      this.isProjector = isProjector;
   }

   public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration
   (
       new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
       new VertexElement(sizeof(float) * 3, VertexElementFormat.Single, VertexElementUsage.BlendWeight, 0),
       new VertexElement(sizeof(float) * 4, VertexElementFormat.Single, VertexElementUsage.BlendWeight, 1)
   );
}

public class OccluderSystem : ChunkedComponentSystem<LightOccluder, OccluderSystem>
{
   public float shadowBias = 0.00005f;

   //TODO start with some reasonable numbers
   private IndexBuffer ib = new IndexBuffer(Graphics.Device, IndexElementSize.ThirtyTwoBits, 3, BufferUsage.WriteOnly);
   private DynamicVertexBuffer vb = new DynamicVertexBuffer(Graphics.Device, VertexPositionTexture.VertexDeclaration, 2, BufferUsage.WriteOnly);

   private RectF lastRectF_;
   private bool isDirty_; //TODO
   private List<OccluderSegment> allOccludersSegments = new List<OccluderSegment>();
   
   public Tuple<int, IndexBuffer, DynamicVertexBuffer> GetOccludersBuffers(RectF rect)
   {
      if (!isDirty_ && rect.Equals(lastRectF_)) //TODO rectf ==, TODO mark dirty when occluders change/ctor
      {
         Dbg.Log("returning cached occluders data");
         goto ReturnData; //TODO
      }
      
      isDirty_ = false;

      allOccludersSegments.Clear(); //TODO use array - low/med prior

      //collect all occluders segments in range
      foreach (LightOccluder occluder in GetComponentsInRect(rect))
      {
         allOccludersSegments.AddRange(occluder.GlobalSegments); //TODO slooooow, pass list?
      }

      int verticesNeeded = allOccludersSegments.Count * 4 + 4; //each segment is a quad in vbo, PLUS the projector (first 4)

      // each miss we double our buffers ;)
      while (verticesNeeded > vb.VertexCount)
      {
         int newVtxQty = vb.VertexCount * 2;
         int newIdxQty = ib.IndexCount * 2;
         vb = new DynamicVertexBuffer(Graphics.Device, OccluderVertexFormat.VertexDeclaration, newVtxQty, BufferUsage.WriteOnly);
         ib = new IndexBuffer(Graphics.Device, IndexElementSize.ThirtyTwoBits, newIdxQty, BufferUsage.WriteOnly);
         int[] indices = new int[newIdxQty];
         for (int i = 0, v = 0; i < newIdxQty; i += 6, v += 4)
         {
            indices[i + 0] = v + 0;
            indices[i + 1] = v + 1;
            indices[i + 2] = v + 2;
            indices[i + 3] = v + 0;
            indices[i + 4] = v + 2;
            indices[i + 5] = v + 3;
         }
         ib.SetData(indices); // copy to gfx, create actual IBO, once per resize
      }

      List<OccluderVertexFormat> verts = new List<OccluderVertexFormat>(verticesNeeded);

      //add projector (first 4 verts)
      verts.Add(new OccluderVertexFormat(new Vector3(0, 0, 0), 0, OccluderVertexFormat.Corner0)); //TODO use vert idx for id?
      verts.Add(new OccluderVertexFormat(new Vector3(1, 0, 0), 0, OccluderVertexFormat.Corner1));
      verts.Add(new OccluderVertexFormat(new Vector3(1, 1, 0), 0, OccluderVertexFormat.Corner2));
      verts.Add(new OccluderVertexFormat(new Vector3(0, 1, 0), 0, OccluderVertexFormat.Corner3));

      //add shadow casters
      foreach (OccluderSegment segment in allOccludersSegments)
      {
         verts.Add(new OccluderVertexFormat(segment.A.ToVector3(), shadowBias));
         verts.Add(new OccluderVertexFormat(segment.B.ToVector3(), shadowBias));
         verts.Add(new OccluderVertexFormat(segment.B.ToVector3(), 1));
         verts.Add(new OccluderVertexFormat(segment.A.ToVector3(), 1));

         Dbg.AddDebugLine(segment.A, segment.B, Color.Cyan);
      }

      vb.SetData(verts.ToArray()); // copy to gfx, once per draw call

      ReturnData:
      return new Tuple<int, IndexBuffer, DynamicVertexBuffer>(allOccludersSegments.Count, ib, vb); //TODO descriptive object

      //don't draw, only set the buffers, we tweak the shader parameters and draw on the light component
//      Graphics.Device.Indices = ib;
//      Graphics.Device.SetVertexBuffer(vb);
//
//      //draw in projector
//      Graphics.Device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, allOccludersSegments.Count * 4 + 4, 0, allOccludersSegments.Count * 2 + 2);

   }
   
}

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
      UpdateGlobalSegments();
   }

   public LightOccluder(Entity entity, OccluderShape shape, float occluderSize) : this(entity) //TODO system
   {
      if (shape == OccluderShape.Cross)
      {
         segments.Add(new OccluderSegment(-Vector2.UnitX * occluderSize / 2, Vector2.UnitX * occluderSize / 2));
         segments.Add(new OccluderSegment(-Vector2.UnitY * occluderSize / 2, Vector2.UnitY * occluderSize / 2));
         return;
      }
      if (shape == OccluderShape.Horizontal)
      {
         segments.Add(new OccluderSegment(-Vector2.UnitX * occluderSize / 2, Vector2.UnitX * occluderSize / 2));
         return;
      }
      segments.Add(new OccluderSegment(-Vector2.UnitY * occluderSize / 2, Vector2.UnitY * occluderSize / 2));
   }

   public LightOccluder(Entity entity, List<OccluderSegment> occluderSegments) : this(entity) //TODO system
   {
      segments = occluderSegments;
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


public class LightSystem : ChunkedComponentSystem<LightProjector, LightSystem>, IRenderableSystem
{
   private static SpriteBatch accumulationBatch_;

   public OccluderSystem OccluderSystem { get; set; } = LightOccluder.SYSTEM;

   public BlendState BlendState { get; set; } = BlendStateExtra.Max;

   private Effect shadowsEffect;
   private Effect shadowsBlur;

   static LightSystem()
   {
      accumulationBatch_ = new SpriteBatch(Graphics.Device);
   }

   public void LoadContent() //TODO
   {
      shadowsEffect = Content.Manager.Load<Effect>("ExtrudeShadows");
      shadowsBlur = Content.Manager.Load<Effect>("ShadowsBlur");
   }
   
   public void Draw(Camera camera, RenderTarget2D renderTarget)
   {
      return;
      var globalProjMatrix = camera.GetGlobalViewMatrix();
      var viewRect = camera.GetCullRect();

      

      //init/resize render buffer if necessary
      
//      if (Core.mainCam.RenderTargets(0).Dimensions() != sizeLastCheck)
//      {
//         sizeLastCheck = Core.mainCam.RenderTargets(0).Dimensions();
//         accumulation_?.Dispose();
//         accumulation_ = new RenderTarget2D(Graphics.Device, Core.mainCam.RenderTargets(0).Width / shadowsQuality_, Core.mainCam.RenderTargets(0).Height / shadowsQuality_);
//         foreach (LightProjector light in GetAllComponents())
//         {
//            light.InitRT();
//         }
//      }

      var geoBuffers = OccluderSystem.GetOccludersBuffers(viewRect);
 
      shadowsEffect.Parameters["Projection"].SetValue(globalProjMatrix);
 
      //        Core.device.RasterizerState = RasterizerState.CullNone;
      //        Core.device.BlendState = BlendState.Opaque;
      //        Core.device.DepthStencilState = DepthStencilState.Default;
 
      foreach (LightProjector light in GetComponentsInRect(viewRect))
      {
         // render single light into a buffer
         light.RenderProjector(shadowsEffect);
      }
 
      //accumulate lights into a buffer
//      Graphics.Device.SetRenderTarget(accumulation_);
//      Graphics.Device.Clear(Color.Black);
//      float blurRadius = 2 / 3f;
//      shadowsBlur.Parameters["pixelDimension"].SetValue(new Vector2(blurRadius / accumulation_.Width, blurRadius / accumulation_.Height));
//      accumulationBatch_.Begin(SpriteSortMode.Deferred, BlendState, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, shadowsBlur);
//      foreach (LightProjector light in GetComponentsInRect(viewRect)) //TODO make a list of projs to render
//      {
//         light.Accumulate(accumulationBatch_);
//      }
//      accumulationBatch_.End();
 
      //shadowsBlur.Parameters["pixelDimension"].SetValue(new Vector2(1f/Core.mainCam.RT(0).Width,1f/Core.mainCam.RT(0).Height));
      //return new Result { texture = accumulation_, lastBlurEffect = shadowsBlur };



   }

}   

public class LightProjector : ManagedChunkComponent<LightProjector, LightSystem>
{
   private RenderTarget2D lightProjectorRT_;
   private int shadowsQuality_ = 1;

//   public struct Result
//   {
//      public Texture2D texture;
//      public Effect lastBlurEffect;
//   }

   static LightProjector()
   {
      Camera.DEFAULT_RENDER_PATH.Enqueue(new Camera.RenderLayer(SYSTEM,0), 500);
   }
   
   public LightProjector(Entity entity, byte system = 0) : base(entity, system)
   {}
   
   public LightProjector(Entity entity, float size, float centerOffset, Texture2D lightTexture, Color? color = null) : this(entity)
   {
      cfg.size = size;
      cfg.centerOffset = centerOffset;
      cfg.lightTexture = lightTexture;
      cfg.lightColor = color ?? Color.White;
      InitProjectorRT();
   }
   
   [MessagePackObject]
   public struct LightProjectorConfig
   {
      [Key(0)] public float size;
      [Key(1)] public float centerOffset;
      [IgnoreMember] public Texture2D lightTexture;
      [Key(2)] public string TextureName => lightTexture.Name;
      [Key(3)] public Color lightColor;

      //TODO FIX ALL THIS SHIT
      [SerializationConstructor]
      public LightProjectorConfig(float size, float centerOffset, string TextureName, Color lightColor) //WTH!
      {
         this.size = size;
         this.centerOffset = centerOffset;
         this.lightTexture = Content.Manager.Load<Texture2D>(TextureName); //WTFH!! TODO
         this.lightColor = lightColor;
      }

      public LightProjectorConfig(float size, float centerOffset, Texture2D lightTexture, Color lightColor)
      {
         this.size = size;
         this.centerOffset = centerOffset;
         this.lightTexture = lightTexture;
         this.lightColor = lightColor;
      }
   }
   public LightProjectorConfig cfg;

   public LightProjector(Entity entity, byte[] serialData) : this(entity)
   {
      cfg = MessagePackSerializer.Deserialize<LightProjectorConfig>(serialData);
      InitProjectorRT();
   }

   void InitProjectorRT()
   {
      lightProjectorRT_?.Dispose();
      lightProjectorRT_ = new RenderTarget2D(Graphics.Device, Core.mainCam.GetRenderTarget(0).Width / shadowsQuality_, Core.mainCam.GetRenderTarget(0).Height / shadowsQuality_);
   }


   


   public void Accumulate(SpriteBatch accumulationBatch)
   {
      accumulationBatch.Draw(lightProjectorRT_, new Vector2(0, 0), cfg.lightColor);
   }

   public virtual void RenderProjector(Effect shadowsEffect)
   {
      Graphics.Device.SetRenderTarget(lightProjectorRT_);
      Graphics.Device.Clear(Color.Black);

      //set projector corners
      float sinT = (float)Math.Sin(entity.Rotation + Math.PI / 4);
      float cosT = (float)Math.Cos(entity.Rotation + Math.PI / 4);

      Vector3 fwdCenter = MathUtils.AngleToDirection(entity.Rotation).ToVector3() * -cfg.centerOffset;

      Vector3 localCorner0 = fwdCenter + new Vector3(cfg.size * -sinT, cfg.size * cosT, 0);
      Vector3 localCorner1 = fwdCenter + new Vector3(cfg.size * cosT, cfg.size * sinT, 0);
      Vector3 localCorner2 = fwdCenter + new Vector3(cfg.size * sinT, cfg.size * -cosT, 0);
      Vector3 localCorner3 = fwdCenter + new Vector3(cfg.size * -cosT, cfg.size * -sinT, 0);

      shadowsEffect.Parameters["Origin"].SetValue(entity.Position); //set shadows extrude origin
      shadowsEffect.Parameters["Texture"].SetValue(cfg.lightTexture);
      shadowsEffect.Parameters["projCorner0"].SetValue(entity.Position.ToVector3(-9) + localCorner0);
      shadowsEffect.Parameters["projCorner1"].SetValue(entity.Position.ToVector3(-9) + localCorner1);
      shadowsEffect.Parameters["projCorner2"].SetValue(entity.Position.ToVector3(-9) + localCorner2);
      shadowsEffect.Parameters["projCorner3"].SetValue(entity.Position.ToVector3(-9) + localCorner3);

      shadowsEffect.CurrentTechnique.Passes[0].Apply();

      //Core.GraphicsDevice.SamplerStates[0] = SamplerState.PointWrap;

//      LightOccluder.DrawBuffers();

   }

   public override void FinalizeComponent()
   {
      lightProjectorRT_.Dispose();
      base.FinalizeComponent();
   }

   public override ComponentData GetSerialData()
   {
      return new ComponentData(ComponentTypes.LightProjector, MessagePackSerializer.Serialize(cfg));
   }

   
   
}




// OLD SYSTEM USING FARSEER, slow as heck
/*
class RaycastingLight : ManagedChunkedComponent<RaycastingLight>
{
    private readonly float size_ = 17;
    public RaycastingLight(float size)
    {
        size_ = size;
        ResizeUniqueVBO();
    }

    public static void DrawAllInRect(RectF rect)
    {
        foreach (RaycastingLight light in GetComponentsInRect(rect))
            light.Draw();
    }
    
    public static IndexBuffer ib;
    private const int IBO_INCR_AMNT = 4*3;//512
    private static int iboIncrs = 0;
    
    public DynamicVertexBuffer vb;
    private const int VBO_INCR_AMNT = 4;//128
    private int vboIncrs = 0;
    
    private const float SHADOW_BIAS = 0.5f;

    public virtual void Draw()
    {
        //rendering lights
        Vector2 center = entity.Position;
        
        //get colliders in range
        AABB rect = new AABB(entity.Position, size_*2f-(2+SHADOW_BIAS), size_*2f-(2+SHADOW_BIAS));
        List<Fixture> cols = Core.physWorld.QueryAABB(ref rect);

        // build list of points in radius to add to the perimeter
        List<Vector2> radiusPoints = new List<Vector2>();
        foreach (Fixture col in cols)
        {
            PolygonShape shape = col.Shape as PolygonShape;

            // skip the light emitter
            if((col.Body.UserData as Component).entity == entity) continue;
            
            //cast one ray for each vertex of shapes in range
            foreach (Vector2 v in shape.Vertices)
            {
                var worldPoint = col.Body.GetWorldPoint(v);

                Vector2 rayDirection = worldPoint-center;
                Quadrant quadrant = rayDirection.GetQuadrant();

                //get offset for auxiliary rays (fast)
                Vector2 auxOffset = new Vector2(-1,1);
                if (quadrant == Quadrant.TR)
                    auxOffset = new Vector2(1, -1);
                else if (quadrant == Quadrant.TL)
                    auxOffset = Vector2.One;
                else if (quadrant == Quadrant.BR)
                    auxOffset = -Vector2.One;

                var hit = Core.physWorld.RayCastSingle(center, worldPoint);

                float distMult = (center-hit.Value).Length();
                Vector2 normalizedDirection = Vector2.Normalize(rayDirection);
                
                float auxOffsetAmount = distMult*0.001f;
                Vector2 longRay = center+normalizedDirection*size_*1.4f; //ray besides points

                var auxA = Core.physWorld.RayCastSingle(center, longRay+auxOffset*auxOffsetAmount);
                var auxB = Core.physWorld.RayCastSingle(center, longRay-auxOffset*auxOffsetAmount);
                
//                Core.AddDebugLine(center, hit.Value, Color.Yellow);
//                Core.AddDebugLine(center, auxA.Value, Color.Red);
//                Core.AddDebugLine(center, auxB.Value, Color.Green);

                //if we hit something, propagate light a tad bit
                radiusPoints.Add(hit.Value+normalizedDirection *SHADOW_BIAS);
                radiusPoints.Add(auxA.Value+normalizedDirection*SHADOW_BIAS);
                radiusPoints.Add(auxB.Value+normalizedDirection*SHADOW_BIAS);
            }
        }

        //cast one ray for each corner of the quad
        radiusPoints.Add(Core.physWorld.RayCastSingle(center, center+Vector2.One*      size_).Value+Vector2.One*0.71f*SHADOW_BIAS);
        radiusPoints.Add(Core.physWorld.RayCastSingle(center, center-Vector2.One*      size_).Value-Vector2.One*0.71f*SHADOW_BIAS);
        radiusPoints.Add(Core.physWorld.RayCastSingle(center, center+new Vector2(-1,1)*size_).Value+new Vector2(-1,1)*0.71f*SHADOW_BIAS);
        radiusPoints.Add(Core.physWorld.RayCastSingle(center, center+new Vector2(1,-1)*size_).Value+new Vector2(1,-1)*0.71f*SHADOW_BIAS);
        
        //sort radius points by angle
        radiusPoints.Sort((a,b) => MathUtils.DirectionToAngle(center-a).CompareTo(MathUtils.DirectionToAngle(center-b)));
        
        //if index buffer can't hold the number of points, expand it
        CheckAndResizeIBO(radiusPoints);
        
        //insert center point at zero
        radiusPoints.Insert(0,center);

        //create vbo data to be passed to gpu
        List<VertexPositionTexture> verts = new List<VertexPositionTexture>();
        foreach (Vector2 point in radiusPoints)
        {
            Vector2 pointRelative = ((point-center)+Vector2.One*size_)/(size_*2);
            verts.Add(new VertexPositionTexture(point.ToVector3(), pointRelative));
        }

        //re-add first so we don't change ibo
        Vector2 point1 = radiusPoints[1];
        Vector2 point1Relative = ((point1-center)+Vector2.One*size_)/(size_*2);
        verts.Add(new VertexPositionTexture(point1.ToVector3(), point1Relative));

        while (vb.VertexCount < verts.Count)
        {
            ResizeUniqueVBO();
        }

        vb.SetData(verts.ToArray());
        
        Core.device.Indices = ib;
        Core.device.SetVertexBuffer(vb);
        Core.device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, vb.VertexCount, 0, verts.Count-2);
    }

    private static void CheckAndResizeIBO(List<Vector2> radiusPoints)
    {
        if (ib == null || ib.IndexCount < radiusPoints.Count * 3)
        {
            while (iboIncrs * IBO_INCR_AMNT < radiusPoints.Count * 3)
            {
                iboIncrs++;
            }
            List<int> inds = new List<int>();
            for (var i = 1; i < (iboIncrs * IBO_INCR_AMNT / 3)+1; i++)
            {
                inds.Add(0); //center
                inds.Add(i);
                inds.Add(i+1);
            }
            ib = new IndexBuffer(Core.device, IndexElementSize.ThirtyTwoBits, iboIncrs * IBO_INCR_AMNT+1,BufferUsage.WriteOnly);
            ib.SetData(inds.ToArray());
        }
    }

    private void ResizeUniqueVBO()
    {
        vboIncrs++;
        vb = new DynamicVertexBuffer(Core.device,VertexPositionTexture.VertexDeclaration,vboIncrs*VBO_INCR_AMNT,BufferUsage.WriteOnly);
    }
    
}
*/
