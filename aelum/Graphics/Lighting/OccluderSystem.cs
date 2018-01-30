using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

   }
   
}