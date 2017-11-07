using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

public static class Randy
{
    public static Random random = new Random();

    public static float NextFloat(float max = Single.MaxValue)
    {
        return (float)random.NextDouble() * max;
    }

    public static int NextInt(int max)
    {
        return random.Next(max);
    }

    public static float NextAngle()
    {
        return (float)random.NextDouble() * MathHelper.TwoPi;
    }

    public static Color NextColor()
    {
        return new Color(NextFloat(1), NextFloat(1), NextFloat(1), 1f);
    }

    public static Color NextSaturatedColor()
    {
        return new Color(NextFloat(1)>0.5f?1:0, NextFloat(1)>0.5f?1:0, NextFloat(1)>0.5f?1:0, 1f);
    }

    public static int Range(int min, int max)
    {
        return random.Next(min, max);
    }

    public static float Range(float min, float max)
    {
        return min + NextFloat(max - min);
    }

    public static Vector2 Range(Vector2 min, Vector2 max)
    {
        return min + new Vector2(NextFloat(max.X - min.X), NextFloat(max.Y - min.Y));
    }

    public static T PickOne<T>(IList<T> collection)
    {
        return collection[NextInt(collection.Count)];
    }

    public static T PickFrom<T>(T[] vals)
    {
        return PickOne(vals);
    }

}