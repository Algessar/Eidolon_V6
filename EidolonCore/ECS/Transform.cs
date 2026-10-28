using System.Numerics;

namespace EidolonCore.ECS;

/// <summary>
/// Local position, rotation and scale of an entity, plus an optional parent.
/// </summary>
/// <remarks>
/// <para>
/// This is a struct, so <c>default(Transform)</c> has a zero quaternion and a zero scale and
/// is NOT a usable transform. Use <see cref="Identity"/> or <c>new Transform()</c> instead.
/// </para>
/// <para>
/// The fields are local to the parent, because that is what a hierarchy edits and what animation
/// and scene files store. World-space reads and writes are derived from them, so callers never
/// have to invert a parent matrix themselves: the <c>GetWorld...</c> and <c>TrySetWorld...</c>
/// members are the only place where the two spaces meet.
/// </para>
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
    public readonly Matrix4x4 GetLocalMatrix() =>
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
    public readonly Matrix4x4 GetWorldMatrix(World world)
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

    /// <summary>World-space position of this transform's origin.</summary>
    public readonly Vector3 GetWorldPosition(World world)
    {
        var matrix = GetWorldMatrix(world);
        return new Vector3(matrix.M41, matrix.M42, matrix.M43);
    }

    /// <summary>World-space orientation, with any inherited parent rotation composed in.</summary>
    /// <remarks>
    /// The rotation is extracted from the world matrix, so it is approximate when an ancestor
    /// combines non-uniform scale with rotation (the composed matrix contains shear that no
    /// quaternion can represent).
    /// </remarks>
    public readonly Quaternion GetWorldRotation(World world) =>
        Matrix4x4.Decompose(GetWorldMatrix(world), out _, out var rotation, out _)
            ? rotation
            : Quaternion.Identity;

    /// <summary>
    /// Moves the entity so its origin sits at <paramref name="worldPosition"/>, leaving rotation
    /// and scale alone.
    /// </summary>
    /// <returns>
    /// False when an ancestor has a non-invertible transform (zero scale), in which case the local
    /// transform is left untouched rather than being corrupted with infinities.
    /// </returns>
    public bool TrySetWorldPosition(World world, Vector3 worldPosition)
    {
        var parentMatrix = GetParentWorldMatrix(world);

        if (!Matrix4x4.Invert(parentMatrix, out var inverseParent))
            return false;

        Position = Vector3.Transform(worldPosition, inverseParent);
        return true;
    }

    /// <summary>
    /// Turns the entity to <paramref name="worldRotation"/>, leaving position and scale alone.
    /// </summary>
    /// <returns>False when an ancestor's transform cannot be decomposed.</returns>
    public bool TrySetWorldRotation(World world, Quaternion worldRotation)
    {
        var parentMatrix = GetParentWorldMatrix(world);

        // Decompose accepts a collapsed matrix, so the zero scale has to be rejected explicitly:
        // an inverted parent leaves nothing to express the rotation against.
        if (!Matrix4x4.Decompose(parentMatrix, out var parentScale, out var parentRotation, out _) ||
            IsCollapsed(parentScale))
        {
            return false;
        }

        Rotation = ToLocalRotation(worldRotation, parentRotation);
        return true;
    }

    /// <summary>Sets world position and rotation together, leaving scale alone.</summary>
    /// <remarks>This is the write path for systems that solve in world space, such as XPBD.</remarks>
    /// <returns>False when an ancestor's transform is non-invertible; nothing is written then.</returns>
    public bool TrySetWorldPose(World world, Vector3 worldPosition, Quaternion worldRotation)
    {
        var parentMatrix = GetParentWorldMatrix(world);

        if (!Matrix4x4.Invert(parentMatrix, out var inverseParent) ||
            !Matrix4x4.Decompose(parentMatrix, out _, out var parentRotation, out _))
        {
            return false;
        }

        Position = Vector3.Transform(worldPosition, inverseParent);
        Rotation = ToLocalRotation(worldRotation, parentRotation);
        return true;
    }

    /// <summary>
    /// Replaces the whole world matrix, converting it into the parent's space. Used when an entity
    /// is reparented or orphaned and has to keep its place in the world.
    /// </summary>
    /// <returns>False when the conversion is impossible; nothing is written then.</returns>
    public bool TrySetWorldMatrix(World world, Matrix4x4 worldMatrix)
    {
        var parentMatrix = GetParentWorldMatrix(world);

        if (!Matrix4x4.Invert(parentMatrix, out var inverseParent) ||
            !Matrix4x4.Decompose(worldMatrix * inverseParent, out var scale, out var rotation, out var translation))
        {
            return false;
        }

        Scale = scale;
        Rotation = rotation;
        Position = translation;
        return true;
    }

    /// <summary>Ancestor-to-world matrix, or the identity for a root or a broken parent chain.</summary>
    private readonly Matrix4x4 GetParentWorldMatrix(World world)
    {
        var parent = Parent;

        if (!parent.IsValid || !world.TryGetComponent<Transform>(parent, out var parentTransform))
            return Matrix4x4.Identity;

        return parentTransform.GetWorldMatrix(world);
    }

    /// <summary>True when a scale squashes an axis to nothing, i.e. the matrix cannot be inverted.</summary>
    private static bool IsCollapsed(Vector3 scale) =>
        MathF.Abs(scale.X) < 1e-6f || MathF.Abs(scale.Y) < 1e-6f || MathF.Abs(scale.Z) < 1e-6f;

    /// <summary>
    /// Peels the parent's orientation off a world rotation. Both operands are unit quaternions,
    /// which is what <see cref="Matrix4x4.CreateFromQuaternion"/> and
    /// <see cref="Quaternion.Inverse"/> require.
    /// </summary>
    private static Quaternion ToLocalRotation(Quaternion worldRotation, Quaternion parentWorldRotation)
    {
        var local = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(worldRotation)) *
                    Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(parentWorldRotation));

        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(local));
    }
}
