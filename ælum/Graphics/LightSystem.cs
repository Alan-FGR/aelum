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

[SuppressMessage("ReSharper", "NotAccessedField.Local")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
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

class LightOccluder : ManagedChunkedComponent<LightOccluder>
{
    public LightOccluder(Entity entity) : base(entity)
    {
        
    }

    public enum OccluderShape{Cross,Vertical,Horizontal}
    public LightOccluder(Entity entity, OccluderShape shape, float occluderSize) : this(entity)
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

    public LightOccluder(Entity entity, List<OccluderSegment> occluderSegments) : this(entity)
    {
        segments = occluderSegments;
    }

    public LightOccluder(Entity entity, byte[] sd) : this(entity, MessagePackSerializer.Deserialize<List<OccluderSegment>>(sd))
    {}

    public override ComponentData GetSerialData()
    {
        return new ComponentData(ComponentTypes.LightOccluder, MessagePackSerializer.Serialize(segments));
    }

    //TODO start with some reasonable numbers
    public static IndexBuffer ib = new IndexBuffer(Core.GraphicsDevice, IndexElementSize.ThirtyTwoBits, 3, BufferUsage.WriteOnly);
    public static DynamicVertexBuffer vb = new DynamicVertexBuffer(Core.GraphicsDevice,VertexPositionTexture.VertexDeclaration,2,BufferUsage.WriteOnly);
    
    private const float SHADOW_BIAS = 0.5f;
    
    private readonly List<OccluderSegment> segments = new List<OccluderSegment>();
    private List<OccluderSegment> globalSegments;
    private void UpdateGlobalSegments()
    {
        globalSegments = new List<OccluderSegment>(segments.Count);
        foreach (OccluderSegment segment in segments)
        {
            //TODO apply rotation too (sin/cos)
            globalSegments.Add(new OccluderSegment(entity.Position + segment.A, entity.Position + segment.B));
        }
    }

    private static List<OccluderSegment> allOccludersSegments = new List<OccluderSegment>();

    public static void PrepareOccludersBuffers(RectF rect) //CALL this BEFORE rendering lights
    {
        allOccludersSegments.Clear();

        //collect all occluders segments in range
        foreach (LightOccluder occluder in GetComponentsInRect(rect))
        {
            allOccludersSegments.AddRange(occluder.globalSegments);
        }

        int verticesNeeded = allOccludersSegments.Count * 4 + 4; //each segment is a quad in vbo, PLUS the projector (first 4)

        // each miss we double our buffers ;)
        while (verticesNeeded > vb.VertexCount)
        {
            int newVtxQty = vb.VertexCount * 2;
            int newIdxQty = ib.IndexCount * 2;
            vb = new DynamicVertexBuffer(Core.GraphicsDevice, OccluderVertexFormat.VertexDeclaration, newVtxQty, BufferUsage.WriteOnly);
            ib = new IndexBuffer(Core.GraphicsDevice, IndexElementSize.ThirtyTwoBits, newIdxQty, BufferUsage.WriteOnly);
            int[] indices = new int[newIdxQty];
            for (int i = 0, v=0; i < newIdxQty; i += 6, v+=4)
            {
                indices[i+0] = v + 0;
                indices[i+1] = v + 1;
                indices[i+2] = v + 2;
                indices[i+3] = v + 0;
                indices[i+4] = v + 2;
                indices[i+5] = v + 3;
            }
            ib.SetData(indices); // copy to gfx, create actual IBO, once per resize
        }

        List<OccluderVertexFormat> verts = new List<OccluderVertexFormat>(verticesNeeded);

        //add projector (first 4 verts)
        verts.Add(new OccluderVertexFormat(new Vector3(0,0,0), 0, OccluderVertexFormat.Corner0));
        verts.Add(new OccluderVertexFormat(new Vector3(1,0,0), 0, OccluderVertexFormat.Corner1));
        verts.Add(new OccluderVertexFormat(new Vector3(1,1,0), 0, OccluderVertexFormat.Corner2));
        verts.Add(new OccluderVertexFormat(new Vector3(0,1,0), 0, OccluderVertexFormat.Corner3));

        //add shadow casters
        foreach (OccluderSegment segment in allOccludersSegments)
        {
            verts.Add(new OccluderVertexFormat(segment.A.ToVector3(), 0.00005f)); //shadow biasing
            verts.Add(new OccluderVertexFormat(segment.B.ToVector3(), 0.00005f));
            verts.Add(new OccluderVertexFormat(segment.B.ToVector3(), 1));
            verts.Add(new OccluderVertexFormat(segment.A.ToVector3(), 1));

            DebugHelper.AddDebugLine(segment.A, segment.B, Color.Cyan);

        }

        vb.SetData(verts.ToArray()); // copy to gfx, once per draw call

        //don't draw, only set the buffers, we tweak the shader parameters and draw on the light component
        Core.GraphicsDevice.Indices = ib;
        Core.GraphicsDevice.SetVertexBuffer(vb);
    }

    public static void DrawBuffers()
    {
        Core.GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, allOccludersSegments.Count*4+4, 0, allOccludersSegments.Count*2+2);
    }

    public override void EntityChanged()
    {
        base.EntityChanged();
        UpdateGlobalSegments();
    }
}

class LightProjector : ManagedChunkedComponent<LightProjector>
{
    private float size_ = 17;
    private float centerOffset_ = 0;
    private Texture2D lightTexture_;
    private Color lightColor_ = Color.White;
    private RenderTarget2D renderTarget_;
    
    private static RenderTarget2D accumulation_;
    private static int shadowsQuality_ = 4;
    private static Point sizeLastCheck = Point.Zero;
    private static Effect shadowsEffect;
    private static Effect shadowsBlur;

    static LightProjector()
    {
        CHUNK_SIZE = 19;
    }

    public struct Result
    {
        public Texture2D texture;
        public Effect lastBlurEffect;
    }
    
    static readonly BlendState Max = new BlendState {
        ColorSourceBlend = Blend.DestinationColor, ColorDestinationBlend = Blend.Zero, ColorBlendFunction = BlendFunction.Max,
        AlphaSourceBlend = Blend.One, AlphaDestinationBlend = Blend.Zero, AlphaBlendFunction = BlendFunction.Add
    };

    public static void LoadContent()
    {
        shadowsEffect = Core.ContentManager.Load<Effect>("ExtrudeShadows");
        shadowsBlur = Core.ContentManager.Load<Effect>("ShadowsBlur");
    }
    
    public LightProjector(Entity entity) : base(entity)
    {
    }

    public LightProjector(Entity entity, byte[] serialData) : this(entity)
    {
    }

    public LightProjector(Entity entity, float size, float centerOffset, Texture2D lightTexture) : this(entity)
    {
        size_ = size;
        centerOffset_ = centerOffset;
        lightTexture_ = lightTexture;
    }

//    public LightProjector(Entity entity, Texture2D lightTexture, float size, Color? lightColor = null, float centerOffset = 0) : base()
//    {
//        lightTexture_ = lightTexture;
//        size_ = size;
//        centerOffset_ = centerOffset;
//        lightColor_ = lightColor ?? Color.White;
//    }
    
    public static Result DrawAllInRect(RectF rect, Matrix globalProjMatrix)
    {
        //init/resize render buffer if necessary
        if (Core.mainCam.RT(0).Dimensions() != sizeLastCheck)
        {
            sizeLastCheck = Core.mainCam.RT(0).Dimensions();
            accumulation_?.Dispose();
            accumulation_ = new RenderTarget2D(Core.GraphicsDevice,Core.mainCam.RT(0).Width/shadowsQuality_,Core.mainCam.RT(0).Height/shadowsQuality_);
            foreach (LightProjector light in GetAllComponents())
            {
                light.renderTarget_?.Dispose();
                light.renderTarget_ = new RenderTarget2D(Core.GraphicsDevice,Core.mainCam.RT(0).Width/shadowsQuality_,Core.mainCam.RT(0).Height/shadowsQuality_);
            }
        }

        LightOccluder.PrepareOccludersBuffers(rect);

        shadowsEffect.Parameters["Projection"].SetValue(globalProjMatrix);
        
//        Core.device.RasterizerState = RasterizerState.CullNone;
//        Core.device.BlendState = BlendState.Opaque;
//        Core.device.DepthStencilState = DepthStencilState.Default;

        foreach (LightProjector light in GetComponentsInRect(rect))
        {
            // render single light into a buffer
            light.Draw();
        }

        //accumulate lights into a buffer
        Core.GraphicsDevice.SetRenderTarget(accumulation_);
        Core.GraphicsDevice.Clear(Color.Black);
        float blurRadius = 2/3f;
        shadowsBlur.Parameters["pixelDimension"].SetValue(new Vector2(blurRadius/accumulation_.Width,blurRadius/accumulation_.Height));
        Core.spriteBatch.Begin(SpriteSortMode.Deferred, Max, SamplerState.PointWrap, DepthStencilState.None,
            RasterizerState.CullNone, shadowsBlur);
        foreach (LightProjector light in GetComponentsInRect(rect))
        {
            light.Accumulate();
        }
        Core.spriteBatch.End();

        //shadowsBlur.Parameters["pixelDimension"].SetValue(new Vector2(1f/Core.mainCam.RT(0).Width,1f/Core.mainCam.RT(0).Height));

        return new Result {texture = accumulation_, lastBlurEffect = shadowsBlur};
    }

    public void Accumulate()
    {
        Core.spriteBatch.Draw(renderTarget_, new Vector2(0,0), lightColor_);
    }

    public virtual void Draw()
    {
        Core.GraphicsDevice.SetRenderTarget(renderTarget_);
        Core.GraphicsDevice.Clear(Color.Black);

        //set projector corners
        float sinT = (float) Math.Sin(entity.Rotation+Math.PI/4);
        float cosT = (float) Math.Cos(entity.Rotation+Math.PI/4);
        
        Vector3 fwdCenter = MathUtils.AngleToDirection(entity.Rotation).ToVector3()*-centerOffset_;

        Vector3 localCorner0 = fwdCenter + new Vector3(size_*-sinT,size_*cosT,0);
        Vector3 localCorner1 = fwdCenter + new Vector3(size_*cosT, size_*sinT,0);
        Vector3 localCorner2 = fwdCenter + new Vector3(size_*sinT, size_*-cosT,0);
        Vector3 localCorner3 = fwdCenter + new Vector3(size_*-cosT,size_*-sinT,0);

        shadowsEffect.Parameters["Origin"].SetValue(entity.Position); //set shadows extrude origin
        shadowsEffect.Parameters["Texture"].SetValue(lightTexture_);
        shadowsEffect.Parameters["projCorner0"].SetValue(entity.Position.ToVector3(-9)+localCorner0);
        shadowsEffect.Parameters["projCorner1"].SetValue(entity.Position.ToVector3(-9)+localCorner1);
        shadowsEffect.Parameters["projCorner2"].SetValue(entity.Position.ToVector3(-9)+localCorner2);
        shadowsEffect.Parameters["projCorner3"].SetValue(entity.Position.ToVector3(-9)+localCorner3);
        
        shadowsEffect.CurrentTechnique.Passes[0].Apply();

        //Core.GraphicsDevice.SamplerStates[0] = SamplerState.PointWrap;

        LightOccluder.DrawBuffers();
        
    }

    public override void FinalizeComponent()
    {
        renderTarget_.Dispose();
        base.FinalizeComponent();
    }

    public override ComponentData GetSerialData()
    {
        throw new NotImplementedException();
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