using System.Numerics;
using EidolonCore.Math;

namespace EidolonEngine;

public class Camera
{
    public Vector3 Position { get; set; } = new(0, 5, 5);
    public Vector3 Target { get; set; } = new(0, 0, 0);
    public Vector3 Up { get; set; } = new(0, 1, 0);

    public float FieldOfView { get; set; } = 45.0f * (MathF.PI / 180.0f);
    public float NearPlane { get; set; } = 0.1f;
    public float FarPlane { get; set; } = 250f;
    
    public float AspectRatio { get; set; } = 16.0f / 9.0f;
    
    public Matrix4x4 ViewMatrix
    {
        get
        {
            var direction = Vector3.Normalize(Target - Position);

            // Fix for when direction is parallel/anti-parallel to up vector
            if (MathF.Abs(Vector3.Dot(direction, Up)) > 0.9999f)
            {
                // Use a different up vector temporarily
                var tempUp = (Mathf.Abs(direction.X) > 0.9999f) ? Vector3.UnitZ : Vector3.UnitY;

                return Matrix4x4.CreateLookAt(Position, Target, tempUp);
            }

            return Matrix4x4.CreateLookAt(Position, Target, Up);
        }
    }
    
    public Matrix4x4 ViewProjectionMatrix => ViewMatrix * ProjectionMatrix;

    public Camera()
    {
        CreateLookAt();
        
    }

    public void Move(Vector3 delta)
    {
        Position += delta;
        Target += delta;
    }
    
    public Matrix4x4 CreateLookAt()
    {
        return Matrix4x4.CreateLookAt(Position, Target, Up);
    }

    public Matrix4x4 BuildProjectionMatrix(float aspectRatio)
    {
        var safeAspect = Mathf.Max(aspectRatio, 0.1f);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI * FieldOfView / 180,
            safeAspect,
            NearPlane,
            FarPlane
        );

        projection.M22 = -projection.M22;
        projection.M32 = -projection.M32;
        return projection;
    }
    
    
    public Matrix4x4 BuildViewProjection(float aspectRatio)
    {
        // System.Numerics composes transforms for row-vector math (v * M).
        // We transpose before upload for GLSL column-vector consumption,
        // so the correct pre-transpose composition is View * Projection.
        return CreateLookAt() * BuildProjectionMatrix(aspectRatio);
    }
    
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
}