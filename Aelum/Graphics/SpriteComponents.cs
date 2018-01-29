using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public struct SpriteData
{
   public Rectangle atlasTile;
   public Vector2 origin;
   public Color color;
   public SpriteEffects effects;

   public SpriteData(Vector2 origin, Color color, Rectangle atlasTile, SpriteEffects effects = SpriteEffects.None)
   {
      this.origin = origin;
      this.color = color;
      this.atlasTile = atlasTile;
      this.effects = effects;
   }

   public SpriteData(Color color) : this() //TODO rem this
   {
      this.origin = Vector2.One * 8;
      this.color = color;
      this.atlasTile = new Rectangle(0, 0, 16, 16);//TODO
      this.effects = SpriteEffects.None;
   }
}

public class SpriteSystem : ChunkedComponentSystem<Sprite, SpriteSystem>, IRenderableSystem
{
   private readonly SpriteBatch batch_ = new SpriteBatch(Graphics.Device);

   public GraphicsDeviceState drawState = new GraphicsDeviceState(BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
   public Effect drawEffect;
   public SpriteSortMode sortMode = SpriteSortMode.Texture;

   static SpriteSystem()
   {
      //CHUNK_SIZE
   }

   public void Draw(Camera camera, RenderTarget2D renderTarget)
   {
      batch_.Begin(sortMode, drawState.blendState, drawState.samplerState, drawState.depthStencilState, drawState.rasterizerState, drawEffect, camera.GetSpritesViewMatrix());
      foreach (Sprite sprite in GetComponentsInRect(camera.GetCullRect(CHUNK_SIZE)))
      {
         sprite.DrawSprite(batch_, camera.GetCullRect());
      }
      batch_.End();
   }
}

public class Sprite : ManagedChunkComponent<Sprite, SpriteSystem>
{
   private SpriteData spriteData;

   static Sprite()
   {
      Camera.DEFAULT_RENDER_PATH.Enqueue(new Camera.RenderLayer(DEFAULT_SYSTEM,0),200);
   }

   public Sprite(Entity entity, SpriteData spriteData) : base(entity)
   {
      this.spriteData = spriteData;
   }

   public virtual void DrawSprite(SpriteBatch batcher, RectF drawRect)
   {
      //calc aabb
      RectF wr = spriteData.atlasTile.PixelsToWorld();
      wr.Position = spriteData.origin * -Graphics.PixelsToWorld;

      float sin = (float)Math.Sin(entity.Rotation);
      float cos = (float)Math.Cos(entity.Rotation);

      Vector2 corner0 = new Vector2(cos * wr.X - sin * wr.Y, sin * wr.X + cos * wr.Y);
      Vector2 corner1 = new Vector2(cos * wr.X - sin * wr.Bottom, sin * wr.X + cos * wr.Bottom);
      Vector2 corner2 = new Vector2(cos * wr.Right - sin * wr.Bottom, sin * wr.Right + cos * wr.Bottom);
      Vector2 corner3 = new Vector2(cos * wr.Right - sin * wr.Y, sin * wr.Right + cos * wr.Y);

      float minX = MathUtils.Min(corner0.X, corner1.X, corner2.X, corner3.X);
      float minY = MathUtils.Min(corner0.Y, corner1.Y, corner2.Y, corner3.Y);

      RectF finalRect = new RectF(
          entity.Position.X + minX,
          entity.Position.Y + minY,
          Math.Abs(minX - MathUtils.Max(corner0.X, corner1.X, corner2.X, corner3.X)),
          Math.Abs(minY - MathUtils.Max(corner0.Y, corner1.Y, corner2.Y, corner3.Y))
          );

      //if aabb not in view, skip
      if (!drawRect.Intersects(finalRect)) return;
      Dbg.AddDebugRect(finalRect, Color.GreenYellow, 1);

      batcher.Draw(Core.atlas,
         entity.Position * Graphics.PixelsPerUnit,
         spriteData.atlasTile,
         spriteData.color,
         entity.Rotation,
         spriteData.origin,
         1, //TODO
         spriteData.effects,
         0//entity.Position.Y//Core.mainCam.GetSpriteZ(entity.Position)//TODO
         );
   }

   public override ComponentData GetSerialData()
   {
      throw new NotImplementedException();
   }

}

