using System.Numerics;

namespace EidolonCore.ECS;

/// <summary>
/// Local position, rotation and scale of an entity, plus an optional parent.
/// </summary>
/// <remarks>
/// This is a struct, so <c>default(Transform)</c> has a zero quaternion and a zero scale and
/// is NOT a usable transform. Use <see cref="Identity"/> or <c>new Transform()</c> instead.
/// </remarks>
public struct Transform : IComponent
{
    /// <summary>How far up the parent chain <see cref="GetWorldMatrix"/> will walk.</summary>
    public const int MaxHierarchyDepth = 64;

    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;

    /// <summary>Parent entity, or the default (invalid) entity for a root.</summary>
    public Entity Parent;

    public static Transform Identity => new(Vector3.Zero, Quaternion.Identity, Vector3.One);

    public Transform()
    {
        Position = Vector3.Zero;
        Rotation = Quaternion.Identity;
        Scale = Vector3.One;
    }

    public Transform(Vector3 position)
        : this()
    {
        Position = position;
    }

    public Transform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }

    /// <summary>
    /// Local-to-parent matrix, in System.Numerics' row-vector convention (scale, then rotate,
    /// then translate).
    /// </summary>
    public Matrix4x4 GetLocalMatrix() =>
        Matrix4x4.CreateScale(Scale) *
        Matrix4x4.CreateFromQuaternion(Rotation) *
        Matrix4x4.CreateTranslation(Position);

    /// <summary>
    /// Local-to-world matrix: this transform composed with every ancestor's local transform.
    /// </summary>
    /// <remarks>
    /// The walk is bounded so a malformed hierarchy (a cycle, or a parent that has since been
    /// destroyed) cannot hang the renderer.
    /// </remarks>
    public Matrix4x4 GetWorldMatrix(World world)
    {
        var matrix = GetLocalMatrix();
        var parent = Parent;

        for (var depth = 0; parent.IsValid && depth < MaxHierarchyDepth; depth++)
        {
            if (!world.TryGetComponent<Transform>(parent, out var parentTransform))
                break;

            matrix *= parentTransform.GetLocalMatrix();
            parent = parentTransform.Parent;
        }

        return matrix;
    }
}
