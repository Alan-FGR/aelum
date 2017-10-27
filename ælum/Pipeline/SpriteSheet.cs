
using System.Collections.Generic;

public static class Sheet
{
    public static readonly Dictionary<int, RectF> Sprites = new Dictionary<int, RectF>();
    public const int MISSING_SPRITE = 0;

    static Sheet()
    {
        Sprites[MISSING_SPRITE] = new RectF(0,0,1,1);
    }

    public static int AddSprite(string name, RectF sprite)
    {
        int hash = name.GetHashCode();
        Sprites[hash] = sprite;
        return hash;
    }

    public static RectF Get(int hash)
    {
        return Sprites[hash];
    }

    public static RectF Get(string name)
    {
        return Sprites[name.GetHashCode()];
    }
}
