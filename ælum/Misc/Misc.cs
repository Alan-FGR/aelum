using System;
using System.Collections.Generic;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public enum Cardinal : byte {North,South,West,East}
public enum Quadrant : byte {TL,TR,BR,BL}

public static class BlendStateExtra
{
    public static BlendState Multiply = new BlendState {
        ColorSourceBlend = Blend.Zero, ColorDestinationBlend = Blend.SourceColor, ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.One, AlphaDestinationBlend = Blend.One, AlphaBlendFunction = BlendFunction.Add
    };
}

public struct NeighborsState
{
    public bool top, right, down, left;
    public static bool operator ==(NeighborsState a, NeighborsState b)
    {
        return a.Equals(b); // :trollface: TODO?
    }
    public static bool operator !=(NeighborsState a, NeighborsState b)
    {
        return !(a == b);
    }
}

public static partial class Extensions
{

    public static float ToFloat(this double d){return (float) d;}
    public static int ToInt(this float f){return (int) f;}

    // this is guaranteed to round to the nearest integer (p+o), use for precise comprehension of intervals when appropriate
    // this is good to go up to 16777217 on ieee754 single
    public static int Settle(this float f)
    {
        return (int)Math.Floor(f);
    }

    public static Point Settle(this Vector2 v)
    {
        return new Point(v.X.Settle(), v.Y.Settle());
    }
    
    public static int CeilToInt(this float f)
    {
        return (int)Math.Round(f);
    }

    public static Point ToPointCeil(this Vector2 v)
    {
        return new Point(v.X.CeilToInt(), v.Y.CeilToInt());
    }

    public static int RoundToInt(this float f)
    {
        return (int)Math.Round(f);
    }

    public static Point RoundToPoint(this Vector2 v)
    {
        return new Point(v.X.RoundToInt(), v.Y.RoundToInt());
    }

    public static Vector3 ToVector3(this Vector2 v, float z = 0)
    {
        return new Vector3(v.X, v.Y, z);
    }

    public static Point ToPoint(this Vector2 v)
    {
        return new Point((int) v.X, (int) v.Y);
    }

    public static Vector2 ToVector2(this Vector3 v)
    {
        return new Vector2(v.X, v.Y);
    }
    
    public static Vector2 ToVector2(this Point p)
    {
        return new Vector2(p.X, p.Y);
    }

    
    public static Point ToPoint(this Cardinal c)
    {
        if (c==Cardinal.North)
            return new Point(0,1);
        if (c==Cardinal.South)
            return new Point(0,-1);
        if (c==Cardinal.West)
            return new Point(-1,0);
        return new Point(1,0);
    }

    public static Quadrant GetQuadrant(this Vector2 vector)
    {
        if (vector.Y > 0)
        {
            if (vector.X>0)
                return Quadrant.TR;
            return Quadrant.TL;
        }
        if (vector.X > 0)
            return Quadrant.BR;
        return Quadrant.BL;
    }



    //spritesheet TODO: get rid of this bullcrap BREAKING!
    public static RectF GetSprite(this int id)
    {
        return Sheet.Get(id);
    }

    ///<summary> subdivide rect by given cols/rows(optional), and returns the tile in provided position </summary>
    public static RectF GetSubTile(this RectF r, Point pos, float cols, float rows = -1)
    {
        if (rows < 0) rows = cols;

        float tileWidth = r.width / cols;
        float tileHeight = r.height / rows;

        return new RectF(r.X+tileWidth*pos.X, r.Y+tileHeight*pos.Y, tileWidth, tileHeight);
    }
    
    public static Point Dimensions(this RenderTarget2D rt)
    {
        return new Point(rt.Width, rt.Height);
    }

    // graphics device states
    public static void SetStates(this GraphicsDevice d, GraphicsDeviceState state)
    {
        state.SetGraphicsDeviceToStates(d);
    }
    
    public static void SetStatesToDefault(this GraphicsDevice d)
    {
        GraphicsDeviceState.SetGraphicsDeviceToDefaultStates(d);
    }

    //physics
    public static KeyValuePair<Fixture, Vector2> RayCastSingle(this World w, Vector2 point1, Vector2 point2)
    {
        Fixture fixture = null;
        Vector2 point = point2;

        w.RayCast((f, p, n, fr) =>
        {
            fixture = f;
            point = p;
            return fr;
        }, point1, point2);

        return new KeyValuePair<Fixture, Vector2>(fixture, point);
    }
    
    public static PhysicalBody GetPhysicalBody(this Body b)
    {
        return b.UserData as PhysicalBody;
    }

    public static PhysicalBody GetPhysicalBody(this Fixture f)
    {
        return f.Body.GetPhysicalBody();
    }

    

    //rectangle

    public static Rectangle Clone(this Rectangle r)
    {
        return new Rectangle(r.X, r.Y, r.Width, r.Height);
    }

    public static Rectangle InflateClone(this Rectangle r, int horiz, int vert)
    {
        var newr = r.Clone();
        newr.Inflate(horiz, vert);
        return newr;
    }
    
    public static Rectangle OffsetClone(this Rectangle r, Point offset)
    {
        var newr = r.Clone();
        newr.Offset(offset);
        return newr;
    }

    public static Rectangle PositionClone(this Rectangle r, Point position)
    {
        var newr = new Rectangle(position.X, position.Y, r.Width, r.Height);
        return newr;
    }

    public static Rectangle MultiplyBy(this Rectangle r, int val)
    {
        var newr = new Rectangle(r.X*val, r.Y*val, r.Width*val, r.Height*val);
        return newr;
    }

    public static Rectangle RandomShrinkClone(this Rectangle r, int rx, int ry, int rxm, int rym)
    {
        var newr = r.Clone();
        newr.Width -= Randy.NextInt(rxm*2);
        newr.Height -= Randy.NextInt(rym*2);
        newr.Location += new Point(Randy.NextInt(rx),Randy.NextInt(ry));
        return Rectangle.Intersect(newr, r);
    }


    ///<summary> does this rectangle have a valid area? </summary>
    public static bool HasArea(this Rectangle r)
    {
        if (r.Width <= 0 || r.Height <= 0) return false;
        return true;
    }

    //rectangle loops
    public static IEnumerable<Point> LoopArea(this Rectangle r)
    {
        for (int y = r.Top; y < r.Bottom; y++)
        for (int x = r.Left; x < r.Right; x++)
        {
            yield return new Point(x,y);
        }
    }

    public static IEnumerable<Point> LoopBoundary(this Rectangle r)
    {
        for (int y = r.Top; y < r.Bottom; y+=r.Height-1)
        for (int x = r.Left; x < r.Right; x++)
        {
            yield return new Point(x,y);
        }
        for (int y = r.Top; y < r.Bottom; y++)
        for (int x = r.Left; x < r.Right; x+=r.Width-1)
        {
            yield return new Point(x,y);
        }
    }


    public static T ElementAt2D<T>(this T[,] a, Point pos)
    {
        return a[pos.X,pos.Y];
    }
    

    public static Color MultiplyRGB(this Color c, float mult)
    {
        Vector3 v = c.ToVector3()*mult;
        Color rc = new Color(v);
        rc.A = c.A;
        return rc;
    }

    public static Point Size(this Viewport v)
    {
        return new Point(v.Width, v.Height);
    }

    ///<summary> multiple that fits without overflowing </summary>
    public static Point FittingMultiple(this Point p, int mult)
    {
        return new Point((p.X/mult)*mult, (p.Y/mult)*mult);
    }

    public static Point DividedBy(this Point p, int value)
    {
        return new Point(p.X/value, p.Y/value);
    }
    
    ///<summary> clamps both members of the point by a given number </summary>
    public static Point ClampBoth(this Point p, int min, int max = Int32.MaxValue)
    {
        return new Point(MathUtils.ClampInt(p.X,min,max), MathUtils.ClampInt(p.Y,min,max)); //TODO call other
    }

    ///<summary> creates a rectangle using point as size </summary>
    public static Rectangle FromSize(this Point p, Point? position = null)
    {
        Point pos = position ?? Point.Zero;
        return new Rectangle(pos.X, pos.Y, p.X, p.Y);
    }

    public static Rectangle UvToPixels(this RectF spr, Texture2D texture = null)
    {
        if (texture == null)
            texture = Core.atlas;
        //TODO FIXME fix all this pile of horsecrap!
        Rectangle atlasRect = new Rectangle(
            (texture.Width * spr.X).Settle(),
            (texture.Height * spr.Y).Settle(),
            (texture.Width * spr.width).CeilToInt()+1,
            (texture.Height * spr.height).CeilToInt()+1
        );
        return atlasRect;
    }



}

public class GraphicsDeviceState
{
    public readonly BlendState blendState;
    public readonly SamplerState samplerState;
    public readonly DepthStencilState depthStencilState;
    public readonly RasterizerState rasterizerState;
    
    private static GraphicsDeviceState defaultState_;
    public static GraphicsDeviceState DefaultState => defaultState_ ?? (defaultState_ = new GraphicsDeviceState());

    public static void SetDefaultState(GraphicsDeviceState defaultState)
    {
        defaultState_ = defaultState;
    }

    public void SetGraphicsDeviceToStates(GraphicsDevice device)
    {
        device.BlendState = blendState;
        device.SamplerStates[0] = samplerState;
        device.DepthStencilState = depthStencilState;
        device.RasterizerState = rasterizerState;
    }

    public static void SetGraphicsDeviceToDefaultStates(GraphicsDevice device)
    {
        DefaultState.SetGraphicsDeviceToStates(device);
    }

    public GraphicsDeviceState(BlendState blendState = null, SamplerState samplerState = null,
        DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null)
    {
        this.blendState = blendState ?? BlendState.Opaque;
        this.samplerState = samplerState ?? SamplerState.PointWrap;
        this.depthStencilState = depthStencilState ?? DepthStencilState.None;
        this.rasterizerState = rasterizerState ?? RasterizerState.CullNone;
    }
    
}

static class MathUtils
{
    public static Vector2 ClampMagnitude(this Vector2 v, float maxMagnitude)
    {
        if (v.Length() <= maxMagnitude)
            return v;
        return Vector2.Normalize(v) * maxMagnitude;
    }
    
    public static Vector2 Lerp(Vector2 value1, Vector2 value2, float amount)
    {
        float retX = MathHelper.Lerp(value1.X, value2.X, amount);
        float retY = MathHelper.Lerp(value1.Y, value2.Y, amount);
        return new Vector2(retX, retY);
    }

    public static float DirectionToAngle(Vector2 direction)
    {
        return (float) Math.Atan2(-direction.X, direction.Y);
    }

    public static Vector2 AngleToDirection(float radians)
    {
        return new Vector2(
            (float) -Math.Sin(radians),
            (float) Math.Cos(radians)
        );
    }

    public static Vector2 RotateRadians(this Vector2 v, float radians)
    {
        float sin = (float) Math.Sin(radians);
        float cos = (float) Math.Cos(radians);
        return new Vector2(cos*v.X - sin*v.Y, sin*v.X + cos*v.Y);
    }

    public static int ClampInt(int value, int min, int max = Int32.MaxValue)  
    {  
        return value < min ? min : value > max ? max : value;  
    }

    public static float Min(float a, float b, float c, float d)
    {
        return Math.Min(a, Math.Min(b, Math.Min(c, d)));
    }

    public static float Max(float a, float b, float c, float d)
    {
        return Math.Max(a, Math.Max(b, Math.Max(c, d)));
    }

}



static class GeneralUtils
{
    
    public static void Swap<T> (ref T lhs, ref T rhs) {
        T temp = lhs;
        lhs = rhs;
        rhs = temp;
    }

}