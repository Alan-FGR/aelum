using System;
using System.Collections.Generic;
using System.Diagnostics;
using FarseerPhysics;
using FarseerPhysics.DebugView;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class DebugHelper
{
    private readonly DebugViewXNA dbgv_;
    private static SpriteFont dbgFont;
    private BasicEffect dbgLinesEffect_;
    private static readonly List<VertexPositionColor> dbgLines_ = new List<VertexPositionColor>();
    private static readonly List<dbgText> dbgTexts_ = new List<dbgText>();

    public static DebugHelper instance;
    public RenderTarget2D dbgRT;

    public static Action OnBeforeDrawing;

    public DebugHelper(World physWorld)
    {
        instance = this;

        // debug shit
        dbgFont = Core.ContentManager.Load<SpriteFont>("Font");
        
        //lines
        dbgLinesEffect_ = new BasicEffect(Core.GraphicsDevice)
        {
            Projection = Matrix.Identity,
            VertexColorEnabled = true
        };

        dbgv_ = new DebugViewXNA(physWorld);
        dbgv_.LoadContent(Core.GraphicsDevice, Core.ContentManager);
        dbgv_.RemoveFlags((DebugViewFlags)int.MaxValue);
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

    private class dbgText
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

        public bool Draw()
        {
            Core.spriteBatch.DrawString(dbgFont, text, position * Core.PPU * Camera.INVDEBUGMULT,
                color, 0, Vector2.Zero, Vector2.One, SpriteEffects.FlipVertically, 0);

            frames--;
            return frames <= 0;
        }
    }

    public static void AddDebugText(string text, Vector2 position, Color color, int frames = 1)
    {
        if(instance==null) return;
        dbgTexts_.Add(new dbgText(text, Core.SnapToPixel(position), color, frames));
    }
    public static void AddDebugLine(Vector2 start, Vector2 end, Color color)
    {
        if(instance==null) return;
        dbgLines_.Add(new VertexPositionColor(start.ToVector3(), color));
        dbgLines_.Add(new VertexPositionColor(end.ToVector3(), color));
    }
    public static void AddDebugRect(Rectangle rect, Color color, float inflate = 0)
    {
        AddDebugRect(new RectF(rect), color, inflate);
    }
    public static void AddDebugRect(RectF rect, Color color, float inflate = 0)
    {
        if(instance==null) return;
        Vector2 pos = rect.Position-Vector2.One*inflate/2;
        Vector2 posbr = pos+Vector2.UnitX*(rect.width+inflate);
        Vector2 postl = pos+Vector2.UnitY*(rect.height+inflate);
        Vector2 postr = pos+Vector2.UnitY*(rect.height+inflate)+Vector2.UnitX*(rect.width+inflate);
        AddDebugLine(pos, posbr, color);
        AddDebugLine(pos, postl, color);
        AddDebugLine(postr, postl, color);
        AddDebugLine(postr, posbr, color);
    }
    public void DrawDebug(Camera cam)
    {
        if (dbgRT == null || dbgRT.Width != cam.RT(0).Width || dbgRT.Height != cam.RT(0).Height)
            dbgRT = new RenderTarget2D(Core.GraphicsDevice, cam.RT(0).Width, cam.RT(0).Height);

        Core.GraphicsDevice.SetRenderTarget(dbgRT);
        Core.Graphics.GraphicsDevice.Clear(Color.Transparent);

#if ORIGIN_SHIFT
        Entity.DebugDrawEntityChunks();
#endif

        OnBeforeDrawing?.Invoke();

        //debug physx
        Matrix globalMatrix = cam.GetGlobalViewMatrix();
        dbgv_.RenderDebugData(ref globalMatrix);

        //debug lines
        dbgLinesEffect_.View = globalMatrix;
        dbgLinesEffect_.CurrentTechnique.Passes[0].Apply();
        Core.GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, dbgLines_.ToArray(), 0, dbgLines_.Count / 2);
        dbgLines_.Clear();

        //debug text
        Core.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, cam.GetSpritesViewMatrix());
        for (var i = dbgTexts_.Count - 1; i >= 0; i--)
        {
            if (dbgTexts_[i].Draw())
                dbgTexts_.RemoveAt(i);
        }
        Core.spriteBatch.End();

    }
}