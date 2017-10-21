using System;
using MessagePack;
using Microsoft.Xna.Framework;

[MessagePackObject]
public struct RectF
{
    // member fields
    [Key(0)] public float X;
    [Key(1)] public float Y;
    [Key(2)] public float width;
    [Key(3)] public float height;

    // properties
    [IgnoreMember] public float Top => Y;
    [IgnoreMember] public float Bottom => Y + height;
    [IgnoreMember] public float Left => X;
    [IgnoreMember] public float Right => X + width;

    // ctors
    [SerializationConstructor]
    public RectF(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        this.width = width;
        this.height = height;
    }
    public RectF(Rectangle r) : this(r.X, r.Y, r.Width, r.Height){}

    // convenience
    [IgnoreMember]
    public Vector2 Position {
        get => new Vector2(X, Y);
        set { X = value.X; Y = value.Y; }
    }
    [IgnoreMember]
    public Vector2 Center => new Vector2(X + width / 2, Y + height / 2);

    // use this for comparison (equality), you can implement Equals() and overload == and != 
    public bool IsSimilarTo(RectF other, float tolerance)
    {
        return Math.Abs(X - other.X) < tolerance &&
            Math.Abs(Y - other.Y) < tolerance &&
            Math.Abs(width - other.width) < tolerance &&
            Math.Abs(height - other.height) < tolerance;
    }
    
    // contains point
    public bool Contains(float x, float y)
    {
        return X <= x && x < X + width && Y <= y && y < Y + height;
    }
    public bool Contains(Point p){return Contains(p.X, p.Y);}
    public bool Contains(Vector2 v){return Contains(v.X, v.Y);}
    public bool Contains(int x, int y){return Contains((float)x, (float)y);}

    // contains rect
    public bool Contains(RectF value)
    {
        return X <= value.X && value.X + value.width <= X + width && Y <= value.Y && value.Y + value.height <= Y + height;
    }
    
    public bool Intersects(RectF other)
    {
        return other.Left <= Right &&
               Left <= other.Right &&
               other.Top <= Bottom &&
               Top <= other.Bottom;
    }

    // obj overrides
    public override string ToString()
    {
        return $"X:{X} Y:{Y} Width:{width} Height:{height}";
    }

    public override int GetHashCode()
    {
        return (int)(X * Y * width * height);
    }
}