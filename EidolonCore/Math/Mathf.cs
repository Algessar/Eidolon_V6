using System;
using System.Runtime.CompilerServices;


namespace EidolonCore.Math;

public static class Mathf
{
	
	/// <summary>
	/// Converts degrees to radians.
	/// <c>rad = deg × (π / 180)</c>
	/// </summary>
	/// <param name="deg">Angle in degrees</param>
	/// <returns>Angle in radians</returns>
	public static float DegreesToRadians(float deg) => deg * (pi / 180.0f);
	    
	/// <summary>
	/// Returns pi
	/// </summary>
	public const float pi = MathF.PI;
	public const float Infinity = float.PositiveInfinity;
	    
	/// <summary>
	/// Returns negative infinity of float [float.NegativeInfinity]
	/// </summary>
	public const float NegativeInfinity = float.NegativeInfinity;

	/// <summary>
	/// const float Deg2Rad
	/// </summary>
	public const float Deg2Rad = pi / 180f;

	/// <summary>
	/// Radians to Degrees
	/// </summary>
	public const float Rad2Deg = 57.29578f;
	    
	public static readonly float Epsilon = (MathfInternal.IsFlushToZeroEnabled ? MathfInternal.FloatMinNormal : MathfInternal.FloatMinDenormal);

	    
	public static float Sin(float f)
	{
		var somethinng = System.Math.Sin(f);
		return (float)MathF.Sin(f);
	}
	
	public static float Cos(float f) => MathF.Cos(f);

// If you need a double version, implement it:
	// public static double Cos(double d) =>(double)MathF.Cos(d);	    

	    
	public static float Tan(float f)
	{
		return (float)MathF.Tan(f);
	}
	    
	public static float Asin(float f)
	{
		return (float)MathF.Asin(f);
	}


	public static float Acos(float f)
	{
		return (float)MathF.Acos(f);
	}


	public static float Atan(float f)
	{
		return (float)MathF.Atan(f);
	}


	public static float Atan2(float y, float x)
	{
		return (float)MathF.Atan2(y, x);
	}
	    
	public static float Sqrt(float f)
	{
		return (float)MathF.Sqrt(f);
	}
	
	public static float Sqrt(float pow, float f, float pow1)
	{
		throw new NotImplementedException();
	}

	public static float Sqrt(double vectorX, params float[] values)
	{
		return Sqrt(vectorX, values);
	}
	    
	public static float Abs(float f)
	{
		return MathF.Abs(f);
	}
	    
	public static int Abs(int value)
	{
		return (int)MathF.Abs(value);
	}
	    
	/// <summary>
	/// Returns the smallest of a range of floats
	/// </summary>
	/// <param name="values"></param>
	/// <returns></returns>
	public static float MinRange(params float[] values)
	{
		    
		int num = values.Length;
		if (num == 0)
		{
			return 0f;
		}

		float num2 = values[0];
		for (int i = 1; i < num; i++)
		{
			if (values[i] < num2)
			{
				num2 = values[i];
			}
		}

		return num2;
	}
	    
	public static int MinRange(int a, int b)
	{
		return (a < b) ? a : b;
	}
	    
	    
	/// <summary>
	/// Returns the smallest of a range of ints
	/// </summary>
	/// <param name="values"></param>
	/// <returns></returns>
	public static int Min(params int[] values)
	{
		int num = values.Length;
		if (num == 0)
		{
			return 0;
		}

		int num2 = values[0];
		for (int i = 1; i < num; i++)
		{
			if (values[i] < num2)
			{
				num2 = values[i];
			}
		}

		return num2;
	}
	    
	public static float Max(float a, float b)
	{
		return (a > b) ? a : b;
	}
	    
	public static float MaxRange(params float[] values)
	{
		int num = values.Length;
		if (num == 0)
		{
			return 0f;
		}

		float num2 = values[0];
		for (int i = 1; i < num; i++)
		{
			if (values[i] > num2)
			{
				num2 = values[i];
			}
		}

		return num2;
	}
	    
	public static int Max(int a, int b)
	{
		return (a > b) ? a : b;
	}
	    
	public static int MaxRange(params int[] values)
	{
		int num = values.Length;
		if (num == 0)
		{
			return 0;
		}

		int num2 = values[0];
		for (int i = 1; i < num; i++)
		{
			if (values[i] > num2)
			{
				num2 = values[i];
			}
		}

		return num2;
	}
	    
	public static float Pow(float f, float p)
	{
		return (float)MathF.Pow(f, p);
	}
	    
	/// <summary>
	/// Returns the logarithm of a specified number in a specified base.
	/// </summary>
	/// <param name="f"></param>
	/// <param name="p"></param>
	/// <returns></returns>
	public static float Log(float f, float p)
	{
		return (float)MathF.Log(f, p);
	}

	/// <summary>
	/// Returns the natural (base e) logarithm of a specified number.
	/// </summary>
	/// <param name="f"></param>
	/// <returns></returns>
	public static float Log(float f)
	{
		return (float)MathF.Log(f);
	}
	    
	public static float Log10(float f)
	{
		return (float)MathF.Log10(f);
	}
	    
	public static float Ceil(float f)
	{
		return (float)MathF.Ceiling(f);
	}
	    
	public static float Floor(float f)
	{
		return (float)MathF.Floor(f);
	}
	    
	public static float Round(float f)
	{
		return (float)MathF.Round(f);
	}
	    
	public static int CeilToInt(float f)
	{
		return (int)MathF.Ceiling(f);
	}
	    
	public static int FloorToInt(float f)
	{
		return (int)MathF.Floor(f);
	}
	    
	public static int RoundToInt(float f)
	{
		return (int)MathF.Round(f);
	}
	    
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float Sign(float f)
	{
		return (f >= 0f) ? 1f : (-1f);
	}
	    
	public static float Clamp(float value, float min, float max)
	{
		if (value < min)
		{
			value = min;
		}
		else if (value > max)
		{
			value = max;
		}

		return value;
	}
	    
	public static int Clamp(int value, int min, int max)
	{
		if (value < min)
		{
			value = min;
		}
		else if (value > max)
		{
			value = max;
		}

		return value;
	}
	    
	public static float Clamp01(float value)
	{
		if (value < 0f)
		{
			return 0f;
		}

		if (value > 1f)
		{
			return 1f;
		}

		return value;
	}
	    
	public static float Lerp(float a, float b, float time)
	{
		return a + (b - a) * Clamp01(time);
	}
	    
	public static float LerpAngle(float a, float b, float t)
	{
		float num = Repeat(b - a, 360f);
		if (num > 180f)
		{
			num -= 360f;
		}

		return a + num * Clamp01(t);
	}
	    
	public static float MoveTowards(float current, float target, float maxDelta)
	{
		if (Abs(target - current) <= maxDelta)
		{
			return target;
		}

		return current + Sign(target - current) * maxDelta;
	}
	    
	public static float MoveTowardsAngle(float current, float target, float maxDelta)
	{
		float num = DeltaAngle(current, target);
		if (0f - maxDelta < num && num < maxDelta)
		{
			return target;
		}

		target = current + num;
		return MoveTowards(current, target, maxDelta);
	}
	    
	public static float SmoothStep(float from, float to, float t)
	{
		t = Clamp01(t);
		t = -2f * t * t * t + 3f * t * t;
		return to * t + from * (1f - t);
	}

	public static float Gamma(float value, float absmax, float gamma)
	{
		bool flag = value < 0f;
		float num = Abs(value);
		if (num > absmax)
		{
			return flag ? (0f - num) : num;
		}

		float num2 = Pow(num / absmax, gamma) * absmax;
		return flag ? (0f - num2) : num2;
	}
	    
	public static bool Approximately(float a, float b)
	{
		return Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), Epsilon * 8f);
	}
	    
	public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
	{
		smoothTime = Max(0.0001f, smoothTime);
		float num = 2f / smoothTime;
		float num2 = num * deltaTime;
		float num3 = 1f / (1f + num2 + 0.48f * num2 * num2 + 0.235f * num2 * num2 * num2);
		float value = current - target;
		float num4 = target;
		float num5 = maxSpeed * smoothTime;
		value = Clamp(value, 0f - num5, num5);
		target = current - value;
		float num6 = (currentVelocity + num * value) * deltaTime;
		currentVelocity = (currentVelocity - num * num6) * num3;
		float num7 = target + (value + num6) * num3;
		if (num4 - current > 0f == num7 > num4)
		{
			num7 = num4;
			currentVelocity = (num7 - num4) / deltaTime;
		}

		return num7;
	}
	    
	public static float SmoothDampAngle(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed,  float deltaTime)
	{
		target = current + DeltaAngle(current, target);
		return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
	}
	    
	public static float Repeat(float t, float length)
	{
		return Clamp(t - Floor(t / length) * length, 0f, length);
	}
	    
	public static float PingPong(float t, float length)
	{
		t = Repeat(t, length * 2f);
		return length - Abs(t - length);
	}
	    
	public static float InverseLerp(float a, float b, float value)
	{
		if (a != b)
		{
			return Clamp01((value - a) / (b - a));
		}

		return 0f;
	}
	    
	public static float DeltaAngle(float current, float target)
	{
		float num = Repeat(target - current, 360f);
		if (num > 180f)
		{
			num -= 360f;
		}

		return num;
	}
	//     
	// internal static bool LineIntersection(Vec2 p1, Vec2 p2, Vec2 p3, Vec2 p4, ref Vec2 result)
	// {
	// 	float num = p2.x - p1.x;
	// 	float num2 = p2.y - p1.y;
	// 	float num3 = p4.x - p3.x;
	// 	float num4 = p4.y - p3.y;
	// 	float num5 = num * num4 - num2 * num3;
	// 	if (num5 == 0f)
	// 	{
	// 		return false;
	// 	}
	//
	// 	float num6 = p3.x - p1.x;
	// 	float num7 = p3.y - p1.y;
	// 	float num8 = (num6 * num4 - num7 * num3) / num5;
	// 	result.x = p1.x + num8 * num;
	// 	result.y = p1.y + num8 * num2;
	// 	return true;
	// }
	//
	// internal static bool LineSegmentIntersection(Vec2 p1, Vec2 p2, Vec2 p3, Vec2 p4, ref Vec2 result)
	// {
	// 	float num = p2.x - p1.x;
	// 	float num2 = p2.y - p1.y;
	// 	float num3 = p4.x - p3.x;
	// 	float num4 = p4.y - p3.y;
	// 	float num5 = num * num4 - num2 * num3;
	// 	if (num5 == 0f)
	// 	{
	// 		return false;
	// 	}
	//
	// 	float num6 = p3.x - p1.x;
	// 	float num7 = p3.y - p1.y;
	// 	float num8 = (num6 * num4 - num7 * num3) / num5;
	// 	if (num8 < 0f || num8 > 1f)
	// 	{
	// 		return false;
	// 	}
	//
	// 	float num9 = (num6 * num2 - num7 * num) / num5;
	// 	if (num9 < 0f || num9 > 1f)
	// 	{
	// 		return false;
	// 	}
	//
	// 	result.x = p1.x + num8 * num;
	// 	result.y = p1.y + num8 * num2;
	// 	return true;
	// }

	internal static long RandomToLong(System.Random r)
	{
		byte[] array = new byte[8];
		r.NextBytes(array);
		return (long)(BitConverter.ToUInt64(array, 0) & 0x7FFFFFFFFFFFFFFFL);
	}
	    
	internal static float ClampToFloat(double value)
	{
		if (double.IsPositiveInfinity(value))
		{
			return float.PositiveInfinity;
		}

		if (double.IsNegativeInfinity(value))
		{
			return float.NegativeInfinity;
		}

		if (value < -3.4028234663852886E+38)
		{
			return float.MinValue;
		}

		if (value > 3.4028234663852886E+38)
		{
			return float.MaxValue;
		}

		return (float)value;
	}
	    
	internal static int ClampToInt(long value)
	{
		if (value < int.MinValue)
		{
			return int.MinValue;
		}

		if (value > int.MaxValue)
		{
			return int.MaxValue;
		}

		return (int)value;
	}

	internal static uint ClampToUInt(long value)
	{
		if (value < 0)
		{
			return 0u;
		}

		if (value > uint.MaxValue)
		{
			return uint.MaxValue;
		}

		return (uint)value;
	}

	internal static float RoundToMultipleOf(float value, float roundingValue)
	{
		if (roundingValue == 0f)
		{
			return value;
		}

		return Round(value / roundingValue) * roundingValue;
	}

	internal static float GetClosestPowerOfTen(float positiveNumber)
	{
		if (positiveNumber <= 0f)
		{
			return 1f;
		}

		return Pow(10f, RoundToInt(Log10(positiveNumber)));
	}

	internal static int GetNumberOfDecimalsForMinimumDifference(float minDifference)
	{
		return Clamp(-FloorToInt(Log10(Abs(minDifference))), 0, 15);
	}

	internal static int GetNumberOfDecimalsForMinimumDifference(double minDifference)
	{
		return (int)MathF.Max(0.0f, (float)(0.0 - MathF.Floor(MathF.Log10(MathF.Abs((int)minDifference)))));
	}

	internal static float RoundBasedOnMinimumDifference(float valueToRound, float minDifference)
	{
		if (minDifference == 0f)
		{
			return DiscardLeastSignificantDecimal(valueToRound);
		}

		return MathF.Round((float)valueToRound, GetNumberOfDecimalsForMinimumDifference(minDifference), MidpointRounding.AwayFromZero);
	}

	internal static double RoundBasedOnMinimumDifference(double valueToRound, double minDifference)
	{
		if (minDifference == 0.0)
		{
			return DiscardLeastSignificantDecimal(valueToRound);
		}

		return MathF.Round((float)valueToRound, GetNumberOfDecimalsForMinimumDifference(minDifference), MidpointRounding.AwayFromZero);
	}

	internal static float DiscardLeastSignificantDecimal(float v)
	{
		int digits = Clamp((int)(5f - Log10(Abs(v))), 0, 15);
		return (float)MathF.Round(v, digits, MidpointRounding.AwayFromZero);
	}

	internal static double DiscardLeastSignificantDecimal(double v)
	{
		float digits = MathF.Max(0, (int)(5.0 - MathF.Log10(MathF.Abs((float)v))));
		try
		{
			return MathF.Round((float)v, (int)digits);
		}
		catch (ArgumentOutOfRangeException)
		{
			return 0.0;
		}
	}
        
        
	public static int NextPowerOfTwo(int value)
	{
		value--;
		value |= value >> 16;
		value |= value >> 8;
		value |= value >> 4;
		value |= value >> 2;
		value |= value >> 1;
		return value + 1;
	}
	public static int ClosestPowerOfTwo(int value)
	{
		int num = NextPowerOfTwo(value);
		int num2 = num >> 1;
		if (value - num2 < num - value)
		{
			return num2;
		}

		return num;
	}
        
	public static bool IsPowerOfTwo(int value)
	{
		return (value & (value - 1)) == 0;
	}


	
}
public struct MathfInternal
{
	public static volatile float FloatMinNormal = 1.17549435E-38f;

	public static volatile float FloatMinDenormal = float.Epsilon;

	public static bool IsFlushToZeroEnabled = FloatMinDenormal == 0f;
}