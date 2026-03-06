using System.Numerics;
using EidolonCore.Math;

namespace EidolonEngine;

public class Camera
{
    public Vector3 Position { get; set; } = new(0, 0, 0);
    public Vector3 Target { get; set; } = new(0, 0, 0);
    public Vector3 Up { get; set; } = new(0, 0, 0);

    public float FieldOfViewDegrees { get; set; } = 75f;
    public float NearPlane { get; set; } = 0.1f;
    public float FarPlane { get; set; } = 250f;

    public Matrix4x4 BuildViewMatrix()
    {
        return Matrix4x4.CreateLookAt(Position, Target, Up);
    }

    public Matrix4x4 BuildProjectionMatrix(float aspectRatio)
    {
        var safeAspect = Mathf.Max(aspectRatio, 0.01f);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI * FieldOfViewDegrees / 180,
            safeAspect,
            NearPlane,
            FarPlane
        );

        projection.M22 *= -1;
        return projection;
    }
    
    
    public Matrix4x4 BuildViewProjection(float aspectRatio)
    {
        // System.Numerics composes transforms for row-vector math (v * M).
        // We transpose before upload for GLSL column-vector consumption,
        // so the correct pre-transpose composition is View * Projection.
        return BuildViewMatrix() * BuildProjectionMatrix(aspectRatio);
    }
}