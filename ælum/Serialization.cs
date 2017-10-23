using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Remoting;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Factories;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Microsoft.Xna.Framework;

[MessagePackObject]
public struct entityData
{
    [Key(0)]
#if ORIGIN_SHIFT
    public vec2fixed position;
#else
    public Vector2 position;
#endif
    [Key(1)]
    public float rotation;
    [Key(2)]
    public List<ComponentData> components;
}

[MessagePackObject]
public struct ChildEntityData
{
    [IgnoreMember] public Entity entity;

    [Key(0)] public bool inheritPosition;
    [Key(1)] public bool inheritRotation;
    [Key(2)] public Vector2 relativePosition;
    [Key(3)] public float relativeRotation;

    [SerializationConstructor]
    public ChildEntityData(bool inheritPosition, bool inheritRotation, Vector2 relativePosition, float relativeRotation) : this()
    {
        this.inheritPosition = inheritPosition;
        this.inheritRotation = inheritRotation;
        this.relativePosition = relativePosition;
        this.relativeRotation = relativeRotation;
    }

    public ChildEntityData(Entity entity, Vector2 relativePosition, float relativeRotation, bool inheritPosition = true, bool inheritRotation = true)
    {
        this.entity = entity;
        this.relativePosition = relativePosition;
        this.relativeRotation = relativeRotation;
        this.inheritPosition = inheritPosition;
        this.inheritRotation = inheritRotation;
    }
}

//COMPONENTS
[MessagePackObject]
public struct ComponentData
{
    [Key(0)]
    public ComponentTypes typeId;
    [Key(1)]
    public byte[] serialData;

    public ComponentData(ComponentTypes typeId, byte[] serialData)
    {
        this.typeId = typeId;
        this.serialData = serialData;
    }
}

//Script
[MessagePackObject]
public struct ScriptTypeAndData
{
    [Key(0)]
    public string ScriptType;
    [Key(1)]
    public Dictionary<string, object> ScriptData;
}



//PHYSICAL
[MessagePackObject]
public struct DynamicBodyData
{
    [Key(0)]
    public bool kinematic;
    [Key(1)]
    public List<colliderData> colliders;
}

[Union(0, typeof(rectangleColliderData))]
[Union(1, typeof(circleColliderData))]
public interface colliderData
{
    //colliderType Type(); //type getter method to prevent serialization bugs
//    [Key(0)] //???
    float Density {get;}
//    [Key(1)] //???
    Vector2 Offset {get;}

    void AddColliderToBody(Body body);
}

[MessagePackObject]
public struct rectangleColliderData : colliderData
{
    //public colliderType Type => colliderType.Rectangle; //this is bug prone
    //colliderType colliderData.Type(){return colliderType.Rectangle;}
    [Key(0)] public float Density { get; }
    [Key(1)] public Vector2 Offset { get; }
    [Key(2)] public Vector2 dimensions;

    [SerializationConstructor]
    public rectangleColliderData(float density, Vector2 offset, Vector2 dimensions)
    {
        this.dimensions = dimensions;
        Density = density;
        Offset = offset;
    }

    public rectangleColliderData(Vector2 dimensions, float density = 1, Vector2? offset = null)
    {
        this.dimensions = dimensions;
        Density = density;
        Offset = offset ?? new Vector2();
    }

    public void AddColliderToBody(Body body)
    {
        FixtureFactory.AttachRectangle(dimensions.X, dimensions.Y, Density, Offset, body);
    }
}

[MessagePackObject]
public struct circleColliderData : colliderData
{
    [Key(0)] public float radius;
    [Key(1)] public float Density { get; }
    [Key(2)] public Vector2 Offset { get; }
    
    [SerializationConstructor]
    public circleColliderData(float radius = 0.5f, float density = 1, Vector2 offset = new Vector2())
    {
        this.radius = radius;
        Density = density;
        Offset = offset;
    }

    public void AddColliderToBody(Body body)
    {
        FixtureFactory.AttachCircle(radius, Density, body, Offset);
    }
}


//lights
[MessagePackObject]
public struct OccluderSegment
{
    public Vector2 A;
    public Vector2 B;
    public OccluderSegment(Vector2 a, Vector2 b)
    {
        A = a;
        B = b;
    }
}



[MessagePackObject]
public struct physicalComponentData
{
    
}


//MISC TYPES (MATH, ETC.)

// fixed point 2d vec used for storing relative positions in chunks, you gotta use a value at very most <1/2 of your max epsilon
// so when you do math to calc relative positions from absolute coords, and then convert them back (say loading a region at another
// position), it always rounds to the same fixed point number after any math you have to do, generally this is going to be
// many orders of magnitude bigger than epsilon for a normal game. Say for example your origin shift happens at 2^16 units from origin,
// you'll generally be very safe with 64 discrete values after the fixed point, unless you do really a LOT of math with the fp
// typically your values will be something like shift at 2^12 and 16 or 32 values of fixed precision for pixel-perfect rounding
[MessagePackObject]
public struct vec2fixed
{
    //TODO decouple fixed point number from vector?
    //TODO use uint - we're wasting the sign bit atm
    private const int LOG_PRECISION = 4; // configure here
    private const int PRECISION = 1 << LOG_PRECISION;
    private const int MOD_MASK = PRECISION-1;
    private const float PRECISION_F = PRECISION;
    private const float PRE_CAST_SUM = (1 / PRECISION_F) / 2; //cast rounding - not worky with negatives

    // TODO always settle/round floats before comparing to chunks, etc, so you don't run the risk of getting say 0.99999 <= 1

    public static float FixToFloat(int fix)
    {
        int integral = fix >> LOG_PRECISION;
        float fractional = (fix & MOD_MASK) / PRECISION_F;
        return integral+fractional;
    }

    public static int FloatToFix(float f)
    {
        float precast = f + PRE_CAST_SUM;
        int integral = (int) precast << LOG_PRECISION;
        int fractional = (int) (precast % 1f * PRECISION);
        return integral+fractional;
    }

    [Key(0)]
    public int rawX;
    [Key(1)]
    public int rawY;
    
    public Vector2 ToVector2()
    {
        return new Vector2(FixToFloat(rawX),FixToFloat(rawY));
    }

    public static vec2fixed FromVec2(Vector2 v)
    {
        return new vec2fixed{rawX = FloatToFix(v.X), rawY = FloatToFix(v.Y)};
    }
}


//FIXED POINT TESTS
//class Program
//{
//    static void Main()
//    {
//        float[] testValues =
//        {
//            0,
//            1,
//            2,
//            10000,
//            10001,
//            100000,
//            100001,
//            100005.5f
//        };
//        float testDeviation = 1/10f;
//
//        float cd = 0; //captn disillusion ;P
//        while (cd < 1)
//        {
//            foreach (float value in testValues)
//            {
//                TestConvert(value+cd);
//            }
//            cd += testDeviation;
//        }
//
//        Console.WriteLine($"max dev: {devtn}");
//        Console.ReadKey();
//    }
//
//    private static float devtn;
//    static void TestConvert(float f)
//    {
//        int rawValue = vec2fixed.FloatToFix(f);
//        float convBack = vec2fixed.FixToFloat(rawValue);
//        float deviation = Math.Abs(f-convBack);
//        devtn = Math.Max(devtn, deviation);
//        Console.WriteLine($"DIFF:{deviation:F3}, original: {f:F6}, raw: {rawValue}, convertedback: {convBack:F6}");
//    }
//
//}





//MESSAGEPACK TESTS
//class Program
//{
//    static void Main()
//    {
//        Dictionary<string, object> data = new Dictionary<string, object>();
//
//        data.Add("a", 3);
//        data.Add("f", "string");
//        data.Add("fd", new []{1,2,3});
//        data.Add("n", null);
//
//        var b = MessagePackSerializer.Serialize(data);
//        var d = MessagePackSerializer.Deserialize<Dictionary<string, object>>(b);
//        
//        Console.ReadKey();
//    }
//}

//MESSAGEPACK TESTS
class Program2
{

    static void Main4()
    {
        for (int i = 0; i < Int32.MaxValue; i++)
        {
            float f = i;
            int ii = (int) Math.Round(f);
            if (i != ii)
            {
                Console.WriteLine($"{i} -> {f} -> {ii}, {Int32.MaxValue}");
                break;
            }
        }
        Console.WriteLine("finished");
        Console.ReadKey();
    }

    public class TestCompA : Component
    {
        public TestCompA(Entity entity) : base(entity)
        {
        }

        public override ComponentData GetSerialData()
        {
            throw new NotImplementedException();
        }
    }

    static void Main3()
    {
//        Entity e = new Entity();
//
//        var pc = new PlayerController(e);
//
//        string tn = typeof(PlayerController).FullName;
//
//        tn = pc.FullTypeName();
//
//        var b = Activator.CreateInstance(Type.GetType(tn), e);
//
//        var s = (pc as Script).FullTypeName();

//        CompositeResolver.RegisterAndSetAsDefault(SzMessagePackResolver.Instance, StandardResolver.Instance);
//        
//        Vector2 v = new Vector2(5,10);
//        var b = MessagePackSerializer.Serialize(v);
//
//        Vector2 vb = MessagePackSerializer.Deserialize<Vector2>(b);
//
//
//        int i = 1034;
//        var bi = MessagePackSerializer.Serialize(i);
//        var bb = MessagePackSerializer.Deserialize<int>(bi);
//
//
//
//        Console.ReadKey();
    }
}























public class SzMessagePackResolver : IFormatterResolver
{
    public static IFormatterResolver Instance = new SzMessagePackResolver();

    public IMessagePackFormatter<T> GetFormatter<T>()
    {
        return FormatterCache<T>.formatter;
    }

    static class FormatterCache<T>
    {
        public static readonly IMessagePackFormatter<T> formatter;
        static FormatterCache()
        {
            formatter = (IMessagePackFormatter<T>)SzMessagePackResolverHelper.GetFormatter(typeof(T));
        }
    }
}

internal static class SzMessagePackResolverHelper
{
    static readonly Dictionary<Type, object> formatterMap = new Dictionary<Type, object>()
    {
        {typeof(Vector2), new Vector2Formatter()},
        {typeof(Point), new PointFormatter()},
        // add more your own custom serializers.
    };

    internal static object GetFormatter(Type t)
    {
        object formatter;
        if (formatterMap.TryGetValue(t, out formatter))
        {
            return formatter;
        }
//        if (t.IsGenericParameter && t.GetGenericTypeDefinition() == typeof(ValueTuple<,>))
//        {
//            return Activator.CreateInstance(typeof(ValueTupleFormatter<,>).MakeGenericType(t.GenericTypeArguments));
//        }
        return null;
    }
}

class Vector2Formatter : IMessagePackFormatter<Vector2>
{
    public int Serialize(ref byte[] bytes, int offset, Vector2 value, IFormatterResolver formatterResolver)
    {
        var startOffset = offset;
        offset += MessagePackBinary.WriteSingle(ref bytes, offset, value.X);
        offset += MessagePackBinary.WriteSingle(ref bytes, offset, value.Y);
        return offset - startOffset;
    }

    public Vector2 Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
    {
        var startOffset = offset;
        var x = MessagePackBinary.ReadSingle(bytes, offset, out readSize);
        offset += readSize;
        var y = MessagePackBinary.ReadSingle(bytes, offset, out readSize);
        offset += readSize;
        readSize = offset - startOffset;
        return new Vector2(x, y);
    }
}

class PointFormatter : IMessagePackFormatter<Point>
{
    public int Serialize(ref byte[] bytes, int offset, Point value, IFormatterResolver formatterResolver)
    {
        var startOffset = offset;
        offset += MessagePackBinary.WriteInt32(ref bytes, offset, value.X);
        offset += MessagePackBinary.WriteInt32(ref bytes, offset, value.Y);
        return offset - startOffset;
    }

    public Point Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
    {
        var startOffset = offset;
        var x = MessagePackBinary.ReadInt32(bytes, offset, out readSize);
        offset += readSize;
        var y = MessagePackBinary.ReadInt32(bytes, offset, out readSize);
        offset += readSize;
        readSize = offset - startOffset;
        return new Point(x, y);
    }
}