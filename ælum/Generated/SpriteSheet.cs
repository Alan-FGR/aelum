
using System.Collections.Generic;

public static class Sheet
{
    public static readonly Dictionary<ID, RectF> Sprites = new Dictionary<ID, RectF>();

    public enum ID
    {
        Obj_big_projectile,
        Obj_enemy_projectile,
        Obj_enemy1,
        Obj_enemy2,
        Obj_medium_projectile,
        Obj_MISSING_SPRITE,
        Obj_player,
        Obj_powerup_projectile,
        Obj_small_projectile,

    }

    static Sheet()
    {
        Sprites[ID.Obj_big_projectile] = new RectF(0f,0.125f,0.265625f,0.1328125f);
        Sprites[ID.Obj_enemy_projectile] = new RectF(0f,0.265625f,0.265625f,0.1328125f);
        Sprites[ID.Obj_enemy1] = new RectF(0.2734375f,0.125f,0.234375f,0.234375f);
        Sprites[ID.Obj_enemy2] = new RectF(0.515625f,0f,0.234375f,0.234375f);
        Sprites[ID.Obj_medium_projectile] = new RectF(0.2734375f,0.3671875f,0.203125f,0.109375f);
        Sprites[ID.Obj_MISSING_SPRITE] = new RectF(0.515625f,0.2421875f,0.125f,0.125f);
        Sprites[ID.Obj_player] = new RectF(0f,0f,0.421875f,0.1171875f);
        Sprites[ID.Obj_powerup_projectile] = new RectF(0.7578125f,0f,0.1171875f,0.1171875f);
        Sprites[ID.Obj_small_projectile] = new RectF(0f,0.40625f,0.0859375f,0.0859375f);

    }
    public static RectF Get(ID id)
    {
        return Sprites[id];
    }
}
