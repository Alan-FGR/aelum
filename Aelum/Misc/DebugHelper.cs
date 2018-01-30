using System;
using System.Collections.Generic;
using System.Diagnostics;
using FarseerPhysics;
using FarseerPhysics.DebugView;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public static class Dbg
{
   private static DebugHelper helper_;
   public static Action onBeforeDebugDrawing;

   static Dbg()
   {
      Log("DBG CCTOR");
      helper_ = new DebugHelper();
   }
   
   public static void AddDebugText(string text, Vector2 position, Color color, int frames = 1)
   {
      helper_.CheckTexts();
      helper_.dbgTexts_.Add(new DebugHelper.dbgText(text, Core.SnapToPixel(position), color, frames));
   }

   public static void AddDebugLine(Vector2 start, Vector2 end, Color color)
   {
      helper_.CheckLines();
      helper_.dbgLines_.Add(new VertexPositionColor(start.ToVector3(), color));
      helper_.dbgLines_.Add(new VertexPositionColor(end.ToVector3(), color));
   }

   public static void AddDebugRect(Rectangle rect, Color color, float inflate = 0)
   {
      AddDebugRect(new RectF(rect), color, inflate);
   }

   public static void AddDebugRect(RectF rect, Color color, float inflate = 0)
   {
      helper_.CheckLines();
      Vector2 pos = rect.Position - Vector2.One * inflate / 2;
      Vector2 posbr = pos + Vector2.UnitX * (rect.width + inflate);
      Vector2 postl = pos + Vector2.UnitY * (rect.height + inflate);
      Vector2 postr = pos + Vector2.UnitY * (rect.height + inflate) + Vector2.UnitX * (rect.width + inflate);
      AddDebugLine(pos, posbr, color);
      AddDebugLine(pos, postl, color);
      AddDebugLine(postr, postl, color);
      AddDebugLine(postr, posbr, color);
   }

   public static void Log(string text)
   {
      Debug.WriteLine(text);
   }

   public static RenderTarget2D RenderDebug(Camera cam)
   {
      helper_.DrawDebug(cam);
      return helper_.DbgRenderTarget;
   }

   private class DebugHelper
   {
      private DebugViewXNA dbgv_;
      private readonly SpriteFont dbgFont_;
      private readonly SpriteBatch textBatch_;
      private readonly BasicEffect dbgLinesEffect_;

      internal readonly List<VertexPositionColor> dbgLines_ = new List<VertexPositionColor>();
      internal readonly List<dbgText> dbgTexts_ = new List<dbgText>();

      internal RenderTarget2D DbgRenderTarget { get; private set; }

      public void CheckLines()
      {
         if (dbgLines_.Count > 50000)
         {
            dbgLines_.Clear();
            Debug.WriteLine("too many lines, clearing... you may not be drawing debug");
         }
      }

      public void CheckTexts()
      {
         if (dbgTexts_.Count > 50000)
         {
            dbgTexts_.Clear();
            Debug.WriteLine("too many texts, clearing... you may not be drawing debug");
         }
      }


      public DebugHelper()
      {
         textBatch_ = new SpriteBatch(Graphics.Device);

         // debug shit
         dbgFont_ = Content.Manager.Load<SpriteFont>("Font");

         //lines
         dbgLinesEffect_ = new BasicEffect(Graphics.Device)
         {
            Projection = Matrix.Identity,
            VertexColorEnabled = true
         };

         TryInitPhysDbg();
      }

      private void TryInitPhysDbg()
      {
         if (Physics.World == null)
         {
            dbgv_?.Dispose();
            return;
         }

         if (dbgv_ != null) return;

         dbgv_ = new DebugViewXNA(Physics.World);
         dbgv_.LoadContent(Graphics.Device, Content.Manager);
         dbgv_.RemoveFlags((DebugViewFlags) Int32.MaxValue);
         //        dbgv_.AppendFlags(DebugViewFlags.AABB);
         dbgv_.AppendFlags(DebugViewFlags.CenterOfMass);
         //        dbgv_.AppendFlags(DebugViewFlags.ContactNormals);
         dbgv_.AppendFlags(DebugViewFlags.ContactPoints);
         //        dbgv_.AppendFlags(DebugViewFlags.Controllers);
         //        dbgv.AppendFlags(DebugViewFlags.DebugPanel);
         dbgv_.AppendFlags(DebugViewFlags.Joint);
         //        dbgv.AppendFlags(DebugViewFlags.PerformanceGraph);
         dbgv_.AppendFlags(DebugViewFlags.PolygonPoints);
         dbgv_.AppendFlags(DebugViewFlags.Shape);
      }

      public void DrawDebug(Camera cam)
      {
         if (DbgRenderTarget == null || DbgRenderTarget.Width != cam.MainRenderTarget.Width || DbgRenderTarget.Height != cam.MainRenderTarget.Height)
            DbgRenderTarget = new RenderTarget2D(Graphics.Device, cam.MainRenderTarget.Width, cam.MainRenderTarget.Height);

         Graphics.Device.SetRenderTarget(DbgRenderTarget);
         Graphics.Manager.GraphicsDevice.Clear(Color.Transparent);

#if ORIGIN_SHIFT
      Entity.DebugDrawEntityChunks();
#endif

         onBeforeDebugDrawing?.Invoke();

         Matrix globalMatrix = cam.GetGlobalViewMatrix();

         //debug physx
         TryInitPhysDbg();
         dbgv_?.RenderDebugData(ref globalMatrix);

         //debug lines
         dbgLinesEffect_.View = globalMatrix;
         dbgLinesEffect_.CurrentTechnique.Passes[0].Apply();
         Graphics.Device.DrawUserPrimitives(PrimitiveType.LineList, dbgLines_.ToArray(), 0, dbgLines_.Count / 2);
         dbgLines_.Clear();

         //debug text
         if (textBatch_ != null)
         {
            textBatch_.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
               DepthStencilState.Default, RasterizerState.CullNone, null, cam.GetSpritesViewMatrix());
            for (var i = dbgTexts_.Count-1; i >= 0; i--)
            {
               if (dbgTexts_[i].Draw(textBatch_))
                  dbgTexts_.RemoveAt(i);
            }
            textBatch_.End();
         }
      }

      internal class dbgText
      {
         private readonly string text;
         private readonly Vector2 position;
         private readonly Color color;
         public int frames;
         public dbgText(string text, Vector2 position, Color color, int frames)
         {
            this.text = text;
            this.position = position;
            this.color = color;
            this.frames = frames;
         }

         public bool Draw(SpriteBatch textSpriteBatch)
         {
            textSpriteBatch.DrawString(helper_.dbgFont_, text, position * Graphics.PixelsPerUnit * Camera.INVDEBUGMULT,
               color, 0, Vector2.Zero, Vector2.One, SpriteEffects.FlipVertically, 0);
            frames--;
            return frames <= 0;
         }
      }
   }
}

