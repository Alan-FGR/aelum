
using System.Collections.Generic;

public static class Sheet
{
    public static readonly Dictionary<ID, RectF> Sprites = new Dictionary<ID, RectF>();

    public enum ID
    {
        big_projectile,
        enemy_projectile,
        enemy1,
        enemy2,
        medium_projectile,
        MISSING_SPRITE,
        player,
        powerup_projectile,
        small_projectile,

    }

    static Sheet()
    {
        Sprites[ID.big_projectile] = new RectF(0f,0.125f,0.265625f,0.1328125f);
        Sprites[ID.enemy_projectile] = new RectF(0f,0.265625f,0.265625f,0.1328125f);
        Sprites[ID.enemy1] = new RectF(0.2734375f,0.125f,0.234375f,0.234375f);
        Sprites[ID.enemy2] = new RectF(0.515625f,0f,0.234375f,0.234375f);
        Sprites[ID.medium_projectile] = new RectF(0.2734375f,0.3671875f,0.203125f,0.109375f);
        Sprites[ID.MISSING_SPRITE] = new RectF(0.515625f,0.2421875f,0.125f,0.125f);
        Sprites[ID.player] = new RectF(0f,0f,0.421875f,0.1171875f);
        Sprites[ID.powerup_projectile] = new RectF(0.7578125f,0f,0.1171875f,0.1171875f);
        Sprites[ID.small_projectile] = new RectF(0f,0.40625f,0.0859375f,0.0859375f);

    }
    public static RectF Get(ID id)
    {
        return Sprites[id];
    }
}
