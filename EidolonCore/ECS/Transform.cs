using System.Numerics;

namespace EidolonCore.ECS;
public struct Transform
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;

    public Transform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Position = position;
        Rotation = Quaternion.Identity;
        Scale = Vector3.One;
    }

    public Matrix4x4 GetWorldMatrix()
    {
        return Matrix4x4.CreateScale(Scale) *
               Matrix4x4.CreateFromQuaternion(Rotation) *
               Matrix4x4.CreateTranslation(Position);
    }
}