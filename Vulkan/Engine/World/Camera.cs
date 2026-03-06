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

    
    public Matrix4x4 ProjectionMatrix(float aspect)
    {
        float f = 1f / MathF.Tan(FieldOfView * 0.5f);

        Matrix4x4 m = new();

        m.M11 = f / aspect;
        m.M22 = -f;

        m.M33 = FarPlane / (NearPlane - FarPlane);
        m.M34 = -1f;

        m.M43 = (NearPlane * FarPlane) / (NearPlane - FarPlane);
        m.M44 = 0f;

        return m;
    }
    
    
}