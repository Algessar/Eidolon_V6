using System.Numerics;
using EidolonCore.Math;

namespace Eidolon.Vulkan;

public class Camera
{
    private const float DegreesToRadians = MathF.PI / 180f;
    public Vector3 Position { get; set; } = new(0, 0, 3f);
    public Vector3 Forward { get; private set; } = -Vector3.UnitZ;
    public Vector3 Target { get; private set; } = Vector3.Zero;
    public Vector3 Up { get; set; } = Vector3.UnitY;

    public float FieldOfViewRadians { get; set; } = 60f * DegreesToRadians;    public float NearPlane { get; set; } = 0.1f;
    public float FarPlane { get; set; } = 1000f;
    public float AspectRatio { get; set; } = 16f / 9f;
    
    public bool UseYawPitch { get; set; }
    public float YawRadians { get; set; } = -90f * DegreesToRadians;
    public float PitchRadians { get; set; }

    private bool FlipYForVulkan { get; set; }

    public void SetAspect(float width, float height)
    {
        //NOTE: This might be an issue if the window is minimized?
        if (height <= 0f)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");

        if (width <= 0f)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");

        AspectRatio = width / height;
    }

    public void SetTarget(Vector3 target)
    {
        Target = target;
        Forward = Vector3.Normalize(Target - Position);
    }

    public Matrix4x4 GetViewMatrix()
    {
        if (UseYawPitch)
        {
            
        }

        return Matrix4x4.CreateLookAt(Position, Target, Up);
    }

    public Matrix4x4 GetProjectionMatrix()
    {
        var fov = Math.Clamp(FieldOfViewRadians, 0.001f, MathF.PI - 0.001f);
        var near = Math.Max(NearPlane, 0.0001f);
        var far = Math.Max(FarPlane, near + 0.0001f);

        var yScale = 1f / MathF.Tan(fov * 0.5f);
        var xScale = yScale / AspectRatio;
        if (FlipYForVulkan)
        {
            yScale = -yScale;
        }
        
        return new Matrix4x4(
            xScale, 0f, 0f, 0f,
            0f, yScale, 0f, 0f,
            0f, 0f, far / (near - far), -1f,
            0f, 0f, (near * far) / (near - far), 0f
            );
    }

    public Matrix4x4 GetViewProjectionMatrix()
    {
        return GetProjectionMatrix() * GetViewMatrix();
    }

    private void UpdateTargetFromYawPitch()
    {
        var clampedPitch = Math.Clamp(PitchRadians, -89 * DegreesToRadians, 89f * DegreesToRadians);
        var forward = new Vector3(
            MathF.Cos(YawRadians) * MathF.Cos(clampedPitch),
            MathF.Sin(clampedPitch),
            MathF.Sin(YawRadians) * MathF.Cos(clampedPitch));

        Forward = Vector3.Normalize(forward);
        Target = Position * Forward;
    }
}