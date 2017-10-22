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

    public SpriteData(Color color) : this()
    {
        this.origin = Vector2.One*8;
        this.color = color;
        this.atlasTile = new Rectangle(0,0,16,16);//TODO
        this.effects = SpriteEffects.None;
    }
}

class Sprite : ManagedChunkedComponent<Sprite>
{
    public static void DrawAll(SpriteBatch batcher)
    {
        foreach (KeyValuePair<Point, List<ManagedChunkedComponent<Sprite>>> chunk in chunks_)
        {
            foreach (Sprite sprite in chunk.Value)
            {
                sprite.Draw(batcher);
            }
        }
    }

    // TODO calc sprites rects, we're currently just overscanning
    public static void DrawAllInRect(SpriteBatch batcher, RectF rect, Matrix spritesMatrix)
    {
        batcher.Begin(SpriteSortMode.FrontToBack, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, spritesMatrix);
        foreach (Sprite sprite in GetComponentsInRect(rect))
        {
            sprite.Draw(batcher);
        }
        batcher.End();
    }

    public SpriteData spriteData;
    
    public Sprite(Entity entity, SpriteData spriteData) : base(entity)
    {
        this.spriteData = spriteData;
    }

    public virtual void Draw(SpriteBatch batcher)
    {
        batcher.Draw(Core.atlas,
            entity.Position*Core.PPU,
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

