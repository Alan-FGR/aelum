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
        this.origin = Vector2.One*8;
        this.color = color;
        this.atlasTile = new Rectangle(0,0,16,16);//TODO
        this.effects = SpriteEffects.None;
    }
}

public class SpriteSystem : ChunkedComponentSystem<Sprite, SpriteSystem>
{
   private readonly SpriteBatch batch_ = new SpriteBatch(Graphics.Device);

   public void DrawAll(SpriteBatch batcher)
   {
      foreach (KeyValuePair<Point, List<Sprite>> chunk in chunks_)
      {
         foreach (Sprite sprite in chunk.Value)
         {
            //                sprite.Draw(batcher);//TODO
         }
      }
   }
   
   public void DrawAllInRect(RectF rect, Matrix spritesMatrix)
   {
      batch_.Begin(SpriteSortMode.FrontToBack, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, spritesMatrix);
      foreach (Sprite sprite in GetComponentsInRect(rect.InflateClone(CHUNK_SIZE,CHUNK_SIZE)))
      {
         sprite.Draw(batch_, rect);
      }
      batch_.End();
   }
}

public class Sprite : ManagedChunkComponent<Sprite, SpriteSystem>
{
    private SpriteData spriteData;
    
    public Sprite(Entity entity, SpriteData spriteData) : base(entity)
    {
        this.spriteData = spriteData;
    }

    public virtual void Draw(SpriteBatch batcher, RectF drawRect)
    {
        RectF wr = spriteData.atlasTile.PixelsToWorld();
        wr.Position = spriteData.origin * -Graphics.PixelsToWorld;

        float sin = (float) Math.Sin(entity.Rotation);
        float cos = (float) Math.Cos(entity.Rotation);

        Vector2 corner0 = new Vector2(cos * wr.X - sin * wr.Y, sin * wr.X + cos * wr.Y);
        Vector2 corner1 = new Vector2(cos * wr.X - sin * wr.Bottom, sin * wr.X + cos * wr.Bottom);
        Vector2 corner2 = new Vector2(cos * wr.Right - sin * wr.Bottom, sin * wr.Right + cos * wr.Bottom);
        Vector2 corner3 = new Vector2(cos * wr.Right - sin * wr.Y, sin * wr.Right + cos * wr.Y);

        float minX = MathUtils.Min(corner0.X,corner1.X,corner2.X,corner3.X);
        float minY = MathUtils.Min(corner0.Y,corner1.Y,corner2.Y,corner3.Y);

        RectF finalRect = new RectF(
            entity.Position.X + minX,
            entity.Position.Y + minY,
            Math.Abs(minX-MathUtils.Max(corner0.X,corner1.X,corner2.X,corner3.X)),
            Math.Abs(minY-MathUtils.Max(corner0.Y,corner1.Y,corner2.Y,corner3.Y))
            );

        if (!drawRect.Intersects(finalRect)) return;
        Dbg.AddDebugRect(finalRect, Color.GreenYellow, 1);

        batcher.Draw(Core.atlas,
            entity.Position*Graphics.PixelsPerUnit,
            spriteData.atlasTile,
            spriteData.color,
            entity.Rotation,
            spriteData.origin,
            1,//TODO
            spriteData.effects,
            Core.mainCam.GetSpriteZ(entity.Position)//TODO
            );
    }

    public override ComponentData GetSerialData()
    {
        throw new NotImplementedException();
    }
    
}

