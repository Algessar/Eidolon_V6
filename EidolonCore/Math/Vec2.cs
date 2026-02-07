

using System.Numerics;
using System.Runtime.CompilerServices;

public struct Vec2
{
    public float x;
    public float y;
    
    public Vec2 (float x, float y)
    {
        this.x = x;
        this.y = y;
    }
    
    public static implicit operator Vector2(Vec2 v) 
        => new(v.x, v.y);
    
    public static implicit operator Vec2(Vector2 v) 
        => new(v.X, v.Y);
    
    private static readonly Vec2 zeroVector  =  new (0.0f, 0.0f);
    private static readonly Vec2 oneVector   =  new (1f, 1f);
    private static readonly Vec2 upVector    =  new (0.0f, 1f);
    private static readonly Vec2 downVector  =  new (0.0f, -1f);
    private static readonly Vec2 leftVector  =  new (-1f, 0.0f);
    private static readonly Vec2 rightVector =  new (1f, 0.0f);
    private static readonly Vec2 positiveInfinityVector = new (float.PositiveInfinity, float.PositiveInfinity);
    private static readonly Vec2 negativeInfinityVector = new (float.NegativeInfinity, float.NegativeInfinity);

    public static Vec2 zero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => zeroVector;
    }

    public static Vec2 one
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => oneVector;
    }

    public static Vec2 up
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => upVector;
    }

    public static Vec2 down
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => downVector;
    }

    public static Vec2 left
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => leftVector;
    }

    public static Vec2 right
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => rightVector;
    }

    public static Vec2 positiveInfinity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => positiveInfinityVector;
    }

    public static Vec2 negativeInfinity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => negativeInfinityVector;
    }
    
    #region Operators
    
    public static Vec2 operator +(Vec2 a, Vec2 b) =>
        new(a.x + b.x, a.y + b.y);

    public static Vec2 operator -(Vec2 a, Vec2 b)
        => new Vec2(a.x - b.x, a.y - b.y);

    public static Vec2 operator *(Vec2 v, float s)
        => new Vec2(v.x * s, v.y * s);

    public static Vec2 operator *(float s, Vec2 v)
        => v * s;

    public static Vec2 operator /(Vec2 v, float s)
        => new Vec2(v.x / s, v.y / s);

    /// <summary>
    /// Returns a new Vec2 with x/y summed by x/y of Vec3. Vec3.z is discarded
    /// </summary>
    /// <param name="a">Vec2</param>
    /// <param name="b">Vec3</param>
    /// <returns></returns>
    public static Vec2 operator +(Vec2 a, Vec3 b)
        => new(a.x + b.x, a.y + b.y);

    public static Vector2 AsVector2(Vec2 vec2)
    {
        return new Vector2(vec2.x, vec2.y);
    }

    public override string ToString() => $"Vec2({x}, {y})";
    
    #endregion
}