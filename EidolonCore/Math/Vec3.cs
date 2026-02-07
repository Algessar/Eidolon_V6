using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public struct Vec3
{
    public readonly float x;
    public readonly float y;
    public readonly float z;
    
    public Vec3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
    

    public static implicit operator Vector3(Vec3 v) 
      => new(v.x, v.y, v.z);
    
    public static implicit operator Vec3(Vector3 v) 
      => new(v.X, v.Y, v.Z);

    private static readonly Vec3 zeroVector     =  new (0.0f, 0.0f, 0.0f);
    private static readonly Vec3 oneVector      =  new (1f, 1f,1f);
    private static readonly Vec3 forwardVector  =  new (0.0f, 0.0f, 1.0f);
    private static readonly Vec3 downVector     =  new (0.0f, -1f, 0.0f);
    private static readonly Vec3 upVector       =  new (0.0f, 1f, 0f);
    private static readonly Vec3 leftVector     =  new (-1f, 0.0f, 0.0f);
    private static readonly Vec3 rightVector    =  new (1f, 0.0f, 0.0f);
    private static readonly Vec3 backVector     =  new (0.0f, 0.0f, -1.0f);
    
    private static readonly Vec3 positiveInfinityVector = new (
      float.PositiveInfinity,
      float.PositiveInfinity,
      float.PositiveInfinity);
    
    private static readonly Vec3 negativeInfinityVector = new (
      float.NegativeInfinity,
      float.NegativeInfinity,
      float.NegativeInfinity);
    
    public static Vec3 zero
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Vec3.zeroVector;
    }

    /// <summary>
    ///   <para>Shorthand for writing Vec3(1, 1, 1).</para>
    /// </summary>
    public static Vec3 one
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Vec3.oneVector;
    }

    /// <summary>
    ///   <para>Shorthand for writing Vec3(0, 0, 1).</para>
    /// </summary>
    public static Vec3 forward
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Vec3.forwardVector;
    }

    /// <summary>
    ///   <para>Shorthand for writing Vec3(0, 0, -1).</para>
    /// </summary>
    public static Vec3 back
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Vec3.backVector;
    }

    /// <summary>
    ///   <para>Shorthand for writing Vec3(0, 1, 0).</para>
    /// </summary>
    public static Vec3 up
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Vec3.upVector;
    }

    /// <summary>
    ///   <para>Shorthand for writing Vec3(0, -1, 0).</para>
    /// </summary>
    public static Vec3 down
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Vec3.downVector;
    }

    /// <summary>
    ///   <para>Shorthand for writing Vec3(-1, 0, 0).</para>
    /// </summary>
    public static Vec3 left
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Vec3.leftVector;
    }

    /// <summary>
    ///   <para>Shorthand for writing Vec3(1, 0, 0).</para>
    /// </summary>
    public static Vec3 right
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Vec3.rightVector;
    }

    /// <summary>
    ///   <para>Shorthand for writing Vec3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity).</para>
    /// </summary>
    public static Vec3 positiveInfinity
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Vec3.positiveInfinityVector;
    }

    /// <summary>
    ///   <para>Shorthand for writing Vec3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity).</para>
    /// </summary>
    public static Vec3 negativeInfinity
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Vec3.negativeInfinityVector;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator +(Vec3 a, Vec3 b)
    {
      return new Vec3(a.x + b.x, a.y + b.y, a.z + b.z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator -(Vec3 a, Vec3 b)
    {
      return new Vec3(a.x - b.x, a.y - b.y, a.z - b.z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator -(Vec3 a) => new Vec3(-a.x, -a.y, -a.z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator *(Vec3 a, float d) => new Vec3(a.x * d, a.y * d, a.z * d);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator *(float d, Vec3 a) => new Vec3(a.x * d, a.y * d, a.z * d);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator /(Vec3 a, float d) => new Vec3(a.x / d, a.y / d, a.z / d);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vec3 lhs, Vec3 rhs)
    {
      float num1 = lhs.x - rhs.x;
      float num2 = lhs.y - rhs.y;
      float num3 = lhs.z - rhs.z;
      return  num1 *  num1 +  num2 *  num2 +  num3 *  num3 < 9.999999439624929E-11;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vec3 lhs, Vec3 rhs) => !(lhs == rhs);

    public static Vec3 Normalize(Vec3 position)
    {
      throw new NotImplementedException();
    }

#region Math calculations

    
    public Vec3 Normalized()
    {
      var inv = 1f / Length();
      return new(x * inv, y * inv, z * inv);
    }
    
    public float Length() =>
      MathF.Sqrt(x * x + y * y + z * z);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Cross(Vec3 lhs, Vec3 rhs)
    {
       return new Vec3( lhs.y *  rhs.z -  lhs.z *  rhs.y,  lhs.z *  rhs.x -  lhs.x *  rhs.z,  lhs.x *  rhs.y -  lhs.y *  rhs.x);
    }
    
    

#endregion

}
