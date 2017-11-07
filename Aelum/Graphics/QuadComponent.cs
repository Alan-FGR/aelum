using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MessagePack;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

[MessagePackObject]
public struct QuadData
{
    [Key(0)] public int SpriteId;
    [Key(1)] public Vector2 origin;
    [Key(2)] public Vector2 scale;
    [Key(3)] public float topShear;
    [Key(4)] public float rightShear;
    [Key(5)] public bool flipX;
    [IgnoreMember] public RectF atlasTile;

//    public QuadData(RectF atlasTile, Vector2 origin, Vector2? scale = null, bool flipX = false, float topShear = 0, float rightShear = 0)
//    {
//        this.atlasTile = atlasTile;
//        this.origin = Core.SnapToPixel(origin);
//        this.flipX = flipX;
//        this.topShear = topShear;
//        this.rightShear = rightShear;
//        this.scale = scale ?? Vector2.One;
//        SetPPScaleFromRect();
//    }

    [SerializationConstructor]
    public QuadData(int spriteId, Vector2 origin, Vector2 scale, float topShear, float rightShear, bool flipX) : this()
    {
        SpriteId = spriteId;
        atlasTile = spriteId.GetSprite();
        this.origin = origin;
        this.scale = scale;
        this.topShear = topShear;
        this.rightShear = rightShear;
        this.flipX = flipX;
    }

    public QuadData(int sprite, Vector2? scale = null, bool centered = true, bool flipX = false, float topShear = 0, float rightShear = 0) : this()
    {
        SpriteId = sprite;
        atlasTile = sprite.GetSprite();
        this.topShear = topShear;
        this.rightShear = rightShear;
        this.flipX = flipX;
        if (centered)
        {
            origin = Core.SnapToPixel(new Vector2((atlasTile.width*Core.ATLAS_TO_WORLD)/2,(atlasTile.height*Core.ATLAS_TO_WORLD)/2));
        }
        this.scale = scale ?? Vector2.One;
        SetPPScaleFromRect();
    }
    
    void SetPPScaleFromRect()
    {
        //scale = new Vector2(atlasTile.width * Core.ATLAS_TO_WORLD * scale.X, atlasTile.height * Core.ATLAS_TO_WORLD * scale.Y);
    }

}

public class Quad : ManagedChunkedComponent<Quad>
{
//    [StructLayout(LayoutKind.Sequential, Pack = 1)]
//    private struct VertexTexture4 : IVertexType
//    {
//        public const int RealStride = 96;
//        VertexDeclaration IVertexType.VertexDeclaration => throw new NotImplementedException();
//        public Vector3 Position0;
//        public Vector2 TextureCoordinate0;
//        public Vector3 Position1;
//        public Vector2 TextureCoordinate1;
//        public Vector3 Position2;
//        public Vector2 TextureCoordinate2;
//        public Vector3 Position3;
//        public Vector2 TextureCoordinate3;
//    } //TODO USE THIS?

    static Quad()
    {
        CHUNK_SIZE = 10;
        debugColor = Color.Cyan;
    }

    public static void DrawAllInRect(RectF rect)
    {
        verts.Clear();
        
        //broadphase culling (https://github.com/Alan-FGR/aelum/issues/17)
        foreach (Quad quad in GetComponentsInRect(rect.InflateClone(CHUNK_SIZE,CHUNK_SIZE)))
            quad.Draw(rect);

        BuildBuffersAndRender();
    }

    public QuadData quadData;

    public Quad(Entity entity, QuadData quadData) : base(entity)
    {
        this.quadData = quadData;
    }

    public static IndexBuffer ib =
        new IndexBuffer(Graphics.Device, IndexElementSize.ThirtyTwoBits, 3, BufferUsage.WriteOnly);
    public static DynamicVertexBuffer vb = 
        new DynamicVertexBuffer(Graphics.Device,VertexPositionTexture.VertexDeclaration,2,BufferUsage.WriteOnly);

    public static List<VertexPositionTexture> verts = new List<VertexPositionTexture>();

    public static RectF GetAABB(Vector3 corner0, Vector3 corner1, Vector3 corner2, Vector3 corner3)
    {
        float minX = MathUtils.Min(corner0.X, corner1.X, corner2.X, corner3.X);
        float maxX = MathUtils.Max(corner0.X, corner1.X, corner2.X, corner3.X);
        float minY = MathUtils.Min(corner0.Y, corner1.Y, corner2.Y, corner3.Y);
        float maxY = MathUtils.Max(corner0.Y, corner1.Y, corner2.Y, corner3.Y);
        return new RectF(minX, minY, maxX-minX, maxY-minY);
    }

    public virtual void Draw(RectF cullRect)
    {
        float entX = Core.SnapToPixel(entity.Position.X);
        float entY = Core.SnapToPixel(entity.Position.Y);
        float entZ = 0;//entY+entX*0.0001f; //TODO

        float S = 4; // snap positions
        float M = S/(float) Math.PI;
        float visualRotation = (float)Math.Floor((entity.Rotation + Math.PI/S/2 ) * M) / M;

        //visualRotation = entity.Rotation;

        bool flipX = quadData.flipX;

        float scaleX = quadData.atlasTile.width * Core.ATLAS_TO_WORLD * quadData.scale.X;
        float scaleY = quadData.atlasTile.height * Core.ATLAS_TO_WORLD * quadData.scale.Y;

        float originX = quadData.origin.X;
        float originY = quadData.origin.Y;

        float topShear = quadData.topShear;
        float rightShear = quadData.rightShear;

        Vector3 corner0;
        Vector3 corner1;
        Vector3 corner2;
        Vector3 corner3;

        if (Math.Abs(visualRotation) > 0.001f)
        {
            float rotSin = (float) Math.Sin(visualRotation);
            float rotCos = (float) Math.Cos(visualRotation);
            float ooX = rotCos * originX-rotSin * originY;
            float ooY = rotSin * originX+rotCos * originY;
            corner0 = new Vector3(entX-ooX, entY-ooY, 0);
            corner1 = new Vector3(entX-rotSin*scaleY-ooX+topShear, entY+rotCos*scaleY-ooY, 0);
            corner2 = new Vector3(entX+(rotCos*scaleX-rotSin*scaleY-ooX)+topShear, entY+(rotCos*scaleY+rotSin*scaleX)-ooY+rightShear, 0);
            corner3 = new Vector3(entX+rotCos*scaleX-ooX, entY+rotSin*scaleX-ooY+rightShear, 0);
        }
        else
        {
            corner0 = new Vector3(entX-originX,entY-originY,entZ);
            corner1 = new Vector3(entX-originX+topShear,entY+scaleY-originY,entZ);
            corner2 = new Vector3(entX+scaleX-originX+topShear,entY+scaleY-originY+rightShear,entZ);
            corner3 = new Vector3(entX+scaleX-originX,entY-originY+rightShear,entZ);
        }

        //narrowphase culling
        RectF aabb = GetAABB(corner0, corner1, corner2, corner3);
        if(!cullRect.Intersects(aabb)) return;
        Dbg.AddDebugRect(aabb, Color.Yellow, 0.5f);


        //TODO: get verts positions and uv from quaddata method?

        Vector2 corner0uv = new Vector2(quadData.atlasTile.X,quadData.atlasTile.Bottom);
        Vector2 corner1uv = new Vector2(quadData.atlasTile.X,quadData.atlasTile.Y);
        Vector2 corner2uv = new Vector2(quadData.atlasTile.Right,quadData.atlasTile.Y);
        Vector2 corner3uv = new Vector2(quadData.atlasTile.Right,quadData.atlasTile.Bottom);

        if (flipX)
        {
            GeneralUtils.Swap(ref corner0uv, ref corner3uv);
            GeneralUtils.Swap(ref corner1uv, ref corner2uv);
        }

        verts.Add(new VertexPositionTexture(corner0, corner0uv));
        verts.Add(new VertexPositionTexture(corner1, corner1uv));
        verts.Add(new VertexPositionTexture(corner2, corner2uv));
        verts.Add(new VertexPositionTexture(corner3, corner3uv));
        
    }

    public static void BuildBuffersAndRender()
    {
        while (verts.Count > vb.VertexCount)
        {
            // each miss we double our buffers ;)
            int newVtxQty = vb.VertexCount * 2;
            int newIdxQty = ib.IndexCount * 2;
            vb = new DynamicVertexBuffer(Graphics.Device, VertexPositionTexture.VertexDeclaration, newVtxQty, BufferUsage.WriteOnly);
            ib = new IndexBuffer(Graphics.Device, IndexElementSize.ThirtyTwoBits, newIdxQty, BufferUsage.WriteOnly);
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
        
        if(verts.Count < 1) return; // bugfix

        vb.SetData(verts.ToArray()); // copy to gfx, once per draw call

        //actually draw shit
        Graphics.Device.Indices = ib;
        Graphics.Device.SetVertexBuffer(vb);
        Graphics.Device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, verts.Count, 0, verts.Count/2);

    }

    public Quad(Entity entity, byte[] serialData) : this(entity, MessagePackSerializer.Deserialize<QuadData>(serialData))
    {}

    public override ComponentData GetSerialData()
    {
        return new ComponentData(ComponentTypes.QuadComponent, MessagePackSerializer.Serialize(quadData));
    }

    public override void FinalizeComponent()
    {

        base.FinalizeComponent();
    }
}