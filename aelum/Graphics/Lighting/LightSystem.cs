using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class LightSystem : ChunkedComponentSystem<LightProjector, LightSystem>, IRenderableSystem
{
   private static SpriteBatch accumulationBatch_;

   public OccluderSystem OccluderSystem { get; set; } = LightOccluder.DEFAULT_SYSTEM;

   public BlendState BlendState { get; set; } = BlendStateExtra.Max;

   private Effect shadowsEffect_;
   private Effect shadowsBlur_;
   public int shadowsQuality = 1;

   static LightSystem()
   {
      accumulationBatch_ = new SpriteBatch(Graphics.Device);
   }

   public LightSystem()
   {

   }

   public void LoadContent() //TODO
   {
      shadowsEffect_ = Content.Manager.Load<Effect>("ExtrudeShadows");
      shadowsBlur_ = Content.Manager.Load<Effect>("ShadowsBlur");
   }
   
   public void Draw(Camera camera, RenderTarget2D renderTarget)
   {
      var globalProjMatrix = camera.GetGlobalViewMatrix();
      var viewRect = camera.GetCullRect();
      
      var occludersBuffers = OccluderSystem.GetOccludersBuffers(viewRect);
      
      shadowsEffect_.Parameters["Projection"].SetValue(globalProjMatrix);
 
      //temporary buffer to render each pass before accumulating into result
      RenderTarget2D tempRawLight = new RenderTarget2D(Graphics.Device, renderTarget.Width, renderTarget.Height,
         false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

      int c = 0;
      foreach (LightProjector light in GetComponentsInRect(viewRect))
      {
         Graphics.Device.SetRenderTarget(tempRawLight);
         Graphics.Device.BlendState = BlendState.Opaque;
         Graphics.Device.Clear(Color.Black);
         
         //don't draw, only set the buffers, we tweak the shader parameters and draw on the light component
         occludersBuffers = OccluderSystem.GetOccludersBuffers(viewRect);
         Graphics.Device.Indices = occludersBuffers.Item2;
         Graphics.Device.SetVertexBuffer(occludersBuffers.Item3);
         var occludersSegmentsCount = occludersBuffers.Item1;

         // render single light
         light.RenderProjector(shadowsEffect_, occludersSegmentsCount);
         
         //accumulate lights into a buffer
         Graphics.Device.SetRenderTarget(renderTarget);
            
         if(c==0)
            Graphics.Device.Clear(Color.Black);
         c++;

         //float blurRadius = 2 / 3f;
         //shadowsBlur_.Parameters["pixelDimension"].SetValue(new Vector2(blurRadius / renderTarget.Width, blurRadius / renderTarget.Height));
         //accumulationBatch_.Begin(SpriteSortMode.Deferred, BlendState, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, shadowsBlur_);
         accumulationBatch_.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone);
         accumulationBatch_.Draw(tempRawLight, Vector2.Zero, light.cfg.lightColor);
         accumulationBatch_.End();
      
      }

      tempRawLight.Dispose();

      //shadowsBlur.Parameters["pixelDimension"].SetValue(new Vector2(1f/Core.mainCam.RT(0).Width,1f/Core.mainCam.RT(0).Height));
      //return new Result { texture = accumulation_, lastBlurEffect = shadowsBlur };


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
