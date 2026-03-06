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
    public float FarPlane { get; set; } = 100f;
    
    public float AspectRatio { get; set; } = 16.0f / 9.0f;
    
    public Matrix4x4 ViewMatrix
    {
        get
        {
            var forward = Target - Position;
            if (forward.LengthSquared() < 1e-8f)
            {
                forward = -Vector3.UnitZ;
            }

            var direction = Vector3.Normalize(forward);

            // Fix for when direction is parallel/anti-parallel to up vector
            // if (MathF.Abs(Vector3.Dot(direction, Up)) > 0.9999f)
            // {
            //     // Use a different up vector temporarily
            //     var tempUp = (Mathf.Abs(direction.X) > 0.9999f) ? Vector3.UnitZ : Vector3.UnitY;
            //
            //     return Matrix4x4.CreateLookAt(Position, Position + direction, tempUp);
            // }

            return Matrix4x4.CreateLookAt(Position, Position + direction, Up);
        }
    }
    
    // public Matrix4x4 ViewProjectionMatrix => ViewMatrix * ProjectionMatrix;

    public Camera()
    {
        CreateLookAt();

        // BuildProjectionMatrix(AspectRatio);

    }

    public void Move(Vector3 delta)
    {
        Position += delta;
        Target += delta;
    }

    private Matrix4x4 CreateLookAt()
    {
        return Matrix4x4.CreateLookAt(Position, Target, Up);
    }

    private Matrix4x4 BuildProjectionMatrix(float aspectRatio)
    {
        var safeAspect = Mathf.Max(aspectRatio, 0.1f);
        var near = Mathf.Max(NearPlane, 0.0001f);
        var far = Mathf.Max(FarPlane, near + 0.0001f);
        var tanHalfFov = MathF.Tan(FieldOfView * 0.5f);

        var projection = Matrix4x4.Identity;
        projection.M11 = 1.0f / (safeAspect * tanHalfFov);
        projection.M22 = -1.0f / tanHalfFov;                 // Vulkan Y flip
        projection.M33 = far / (near - far);                 // depth scale
        projection.M34 = (near * far) / (near - far);        // depth translation
        projection.M43 = -1.0f;                               // perspective divide
        projection.M44 = 0.0f;

        return projection;
    }
    
    public Matrix4x4 BuildViewProjection(float aspectRatio)
    {
        // System.Numerics composes transforms for row-vector math (v * M).
        // We transpose before upload for GLSL column-vector consumption,
        // so the correct pre-transpose composition is View * Projection.
        return ViewMatrix * BuildProjectionMatrix(aspectRatio);
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