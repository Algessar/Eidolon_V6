using System.Numerics;
using EidolonCore.Math;
using Silk.NET.Maths;

namespace Eidolon.Vulkan;

public class Camera
{
    private const float DegreesToRadians = MathF.PI / 180f;
    public Vector3 Position { get; set; } = new(0, 0, 3f);
    public Vector3 Forward { get; private set; } = -Vector3.UnitZ;
    public Vector3 Target { get; set; } = Vector3.Zero;
    public Vector3 Up { get; set; } = Vector3.UnitY;

    public float FieldOfViewRadians { get; set; } = 60f * DegreesToRadians;    public float NearPlane { get; set; } = 0.1f;
    public float FarPlane { get; set; } = 1000f;
    public float AspectRatio { get; set; } = 16f / 9f;
    
    public float FieldOfView { get; set; } = 45.0f * (MathF.PI / 180.0f);
    
    public bool UseYawPitch { get; set; }
    public float YawRadians { get; set; } = -90f * DegreesToRadians;
    public float PitchRadians { get; set; }


    public void SetAspectRatio(float width, float height)
    {
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
            UpdateTargetFromYawPitch();
        }

        return Matrix4x4.CreateLookAt(Position, Target, Up);
    }
    
    //NOTE: Use this. From old working project.
    public Matrix4x4 ProjectionMatrix
    {
        get
        {
            // Create a right-handed perspective matrix
            float tanHalfFov = MathF.Tan(FieldOfView * 0.5f);
            float aspect = AspectRatio;
                
            var result = Matrix4x4.Identity;
                
            // Standard perspective matrix formula for Vulkan
            // X: 1/(aspect * tan(fov/2))
            result.M11 = 1.0f / (aspect * tanHalfFov);
            // Y: -1/tan(fov/2) (negative for Vulkan Y flip)
            result.M22 = -1.0f / tanHalfFov;
            // Z: far/(near-far)
            result.M33 = FarPlane / (NearPlane - FarPlane);
            // W: -1 (for perspective divide)
            result.M34 = -1.0f;
            // Z translation: (near*far)/(near-far)
            result.M43 = (NearPlane * FarPlane) / (NearPlane - FarPlane);
            result.M44 = 0.0f;
                
            return result;
        }
    }
    
    
    public Matrix4x4 ViewMatrix
    {
        get
        {
            var direction = Vector3.Normalize(Target - Position);
                
            // Fix for when direction is parallel/anti-parallel to up vector
            if (MathF.Abs(Vector3.Dot(direction, Up)) > 0.9999f)
            {
                // Use a different up vector temporarily
                var tempUp = (MathF.Abs(direction.Y) > 0.9999f) ? 
                    Vector3.UnitZ : 
                    Vector3.UnitY;
                    
                return Matrix4x4.CreateLookAt(Position, Target, tempUp);
            }
                
            return Matrix4x4.CreateLookAt(Position, Target, Up);
        }
    }


    public Matrix4x4 GetViewProjectionMatrix()
    {
        return ProjectionMatrix * GetViewMatrix();
    }

    private void UpdateTargetFromYawPitch()
    {
        var clampedPitch = Math.Clamp(PitchRadians, -89 * DegreesToRadians, 89f * DegreesToRadians);
        var forward = new Vector3(
            MathF.Cos(YawRadians) * MathF.Cos(clampedPitch),
            MathF.Sin(clampedPitch),
            MathF.Sin(YawRadians) * MathF.Cos(clampedPitch));

        Forward = Vector3.Normalize(forward);
        Target = Position + Forward;
    }
    
    public void Move(Vector3 delta)
    {
        Position += delta;
        Target += delta;
    }
    
    private float Zoom = 45f;

    public Matrix4x4 Matrix()
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(Mathf.DegreesToRadians(Zoom), AspectRatio, 0.1f, 100.0f);
    }
}