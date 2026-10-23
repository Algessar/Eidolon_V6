using System.Numerics;

namespace EidolonCore.Rendering;

/// <summary>
/// Blender-style viewport camera moves: orbit around a pivot, pan in the view plane, dolly with the
/// wheel. Pure maths so it can be tested without a window.
/// </summary>
public static class ViewportCamera
{
    /// <summary>How far the orbit may tilt before it flips over the pole.</summary>
    public const float MaxPitch = 1.55f;

    /// <summary>World units covered by one pixel at <paramref name="distance"/> from the camera.</summary>
    public static float WorldPerPixel(float distance, float fieldOfView, float viewportHeight)
    {
        if (viewportHeight <= 0f)
            return 0f;

        return 2f * MathF.Max(0.01f, distance) * MathF.Tan(fieldOfView * 0.5f) / viewportHeight;
    }

    /// <summary>
    /// Rotates <paramref name="position"/> around <paramref name="pivot"/> on a sphere, so the
    /// distance to the pivot is preserved. Positive yaw orbits to the right, positive pitch rises.
    /// </summary>
    public static Vector3 Orbit(Vector3 position, Vector3 pivot, float yawDelta, float pitchDelta)
    {
        var offset = position - pivot;
        var radius = offset.Length();

        if (radius < 1e-4f)
            return position;

        var yaw = MathF.Atan2(offset.Z, offset.X);
        var pitch = MathF.Asin(System.Math.Clamp(offset.Y / radius, -1f, 1f));

        yaw += yawDelta;
        pitch = System.Math.Clamp(pitch + pitchDelta, -MaxPitch, MaxPitch);

        var cosPitch = MathF.Cos(pitch);
        var direction = new Vector3(cosPitch * MathF.Cos(yaw), MathF.Sin(pitch), cosPitch * MathF.Sin(yaw));

        return pivot + direction * radius;
    }

    /// <summary>Screen-space drag converted into a world-space pan offset.</summary>
    public static Vector3 Pan(float deltaX, float deltaY, Vector3 right, Vector3 up, float worldPerPixel) =>
        (-right * deltaX + up * deltaY) * worldPerPixel;

    /// <summary>
    /// Moves along the view direction by a fraction of the current distance. Positive steps move
    /// towards the target (zoom in).
    /// </summary>
    public static Vector3 Dolly(Vector3 position, Vector3 target, float steps)
    {
        var forward = Vector3.Normalize(target - position);
        var distance = Vector3.Distance(position, target);
        var step = MathF.Max(0.05f, distance * 0.12f) * steps;

        return position + forward * step;
    }
}
