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
    /// distance to the pivot is preserved. Positive pitch rises.
    /// </summary>
    /// <remarks>
    /// Positive yaw swings the camera towards its own left, so the scene turns with the drag, which is
    /// the same grab-the-scene feel as <see cref="Pan"/>. Turning the camera the other way instead
    /// would make a drag right move the scene left while a pan right moves it right.
    /// </remarks>
    public static Vector3 Orbit(Vector3 position, Vector3 pivot, float yawDelta, float pitchDelta)
    {
        var offset = position - pivot;
        var radius = offset.Length();

        if (radius < 1e-4f)
            return position;

        var pitch = MathF.Asin(System.Math.Clamp(offset.Y / radius, -1f, 1f));
        var clampedPitch = System.Math.Clamp(pitch + pitchDelta, -MaxPitch, MaxPitch);
        var appliedPitch = clampedPitch - pitch;

        // Apply real rotations rather than rebuilding the point from independent spherical
        // coordinates. The same yaw/pitch can then be applied to both camera position and
        // target, keeping the view rigid while it orbits an arbitrary pivot. System.Numerics
        // rotates row vectors, so a positive angle about +Y already turns the world the other way;
        // negating it here is what makes positive yaw drag the scene along with the mouse.
        var yawed = Vector3.Transform(offset, Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, -yawDelta));
        var horizontal = new Vector3(yawed.X, 0f, yawed.Z);
        if (horizontal.LengthSquared() < 1e-8f)
            return pivot + yawed;

        var pitchAxis = Vector3.Normalize(Vector3.Cross(horizontal, Vector3.UnitY));
        var rotated = Vector3.Transform(yawed, Matrix4x4.CreateFromAxisAngle(pitchAxis, appliedPitch));
        return pivot + rotated;
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
