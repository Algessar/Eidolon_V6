using System.Numerics;
using System.Runtime.InteropServices;
using EidolonCore.ECS;

namespace EidolonCore.Rendering;

/// <summary>Up to four bones per vertex, with their blend weights.</summary>
/// <remarks>
/// Four is the usual limit because it fits a single vector pair on the GPU, and because an exporter that
/// needs more renormalises down to four anyway. Unused slots hold index -1 and weight 0.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SkinInfluence
{
    public int Bone0;
    public int Bone1;
    public int Bone2;
    public int Bone3;

    public float Weight0;
    public float Weight1;
    public float Weight2;
    public float Weight3;

    public static SkinInfluence None => new() { Bone0 = -1, Bone1 = -1, Bone2 = -1, Bone3 = -1 };

    /// <summary>Adds an influence, keeping the four heaviest.</summary>
    public void Add(int bone, float weight)
    {
        if (bone < 0 || weight <= 0f)
            return;

        if (Weight0 == 0f) { Bone0 = bone; Weight0 = weight; return; }
        if (Weight1 == 0f) { Bone1 = bone; Weight1 = weight; return; }
        if (Weight2 == 0f) { Bone2 = bone; Weight2 = weight; return; }
        if (Weight3 == 0f) { Bone3 = bone; Weight3 = weight; return; }

        // Already full: replace the lightest slot if this one would win.
        var lightest = Weight0 <= Weight1 && Weight0 <= Weight2 && Weight0 <= Weight3 ? 0
            : Weight1 <= Weight2 && Weight1 <= Weight3 ? 1
            : Weight2 <= Weight3 ? 2
            : 3;

        if (weight <= GetWeight(lightest))
            return;

        Set(lightest, bone, weight);
    }

    /// <summary>Scales the weights so they sum to one, leaving a vertex with no bones untouched.</summary>
    public void Normalize()
    {
        var total = Weight0 + Weight1 + Weight2 + Weight3;

        if (total <= 1e-6f)
            return;

        Weight0 /= total;
        Weight1 /= total;
        Weight2 /= total;
        Weight3 /= total;
    }

    private float GetWeight(int slot) => slot switch
    {
        0 => Weight0,
        1 => Weight1,
        2 => Weight2,
        _ => Weight3,
    };

    private void Set(int slot, int bone, float weight)
    {
        switch (slot)
        {
            case 0: Bone0 = bone; Weight0 = weight; break;
            case 1: Bone1 = bone; Weight1 = weight; break;
            case 2: Bone2 = bone; Weight2 = weight; break;
            default: Bone3 = bone; Weight3 = weight; break;
        }
    }
}

/// <summary>A bone the skeleton deforms against, addressed by its hierarchy path.</summary>
public readonly record struct SkeletonBone(string Path, Matrix4x4 InverseBind);

/// <summary>
/// Bind-pose geometry plus the bones and weights that deform it.
/// </summary>
/// <remarks>
/// <para>
/// The bind pose lives in the mesh; deforming produces a second vertex buffer that the renderer can draw.
/// Bone transforms come from ordinary entities, so an imported skeleton is animated by the same clips as
/// everything else, and the skinning step is just the last stage of evaluating a pose.
/// </para>
/// <para>
/// Skinning uses world matrices rather than the file's inverse-bind data: a vertex is taken from bind
/// world space into bone space with the bone's rest matrix, then placed with its current matrix.
/// </para>
/// </remarks>
public sealed class SkinnedMesh : IComponent
{
    /// <summary>Geometry in its bind pose.</summary>
    public required Mesh BindPose { get; init; }

    /// <summary>The entity whose transform places this geometry, so deformation stays in its local space.</summary>
    public required Entity MeshEntity { get; init; }

    /// <summary>The mesh node's world matrix when the skin was bound.</summary>
    public required Matrix4x4 BindWorld { get; init; }

    /// <summary>Bone paths and the inverse of their bind-pose world matrices, in influence order.</summary>
    public required SkeletonBone[] Bones { get; init; }

    /// <summary>Entities the bones resolved to. A path that did not resolve hides that bone.</summary>
    public required Entity[] BoneEntities { get; init; }

    /// <summary>Per bind-pose vertex bone blend.</summary>
    public required SkinInfluence[] Influences { get; init; }

    /// <summary>
    /// The mesh the renderer draws: a copy of the bind pose that <see cref="Deform"/> rewrites. Made on
    /// first use when the caller did not supply one, so a skin can be built without a renderer at all.
    /// </summary>
    public Mesh? Deformed { get; set; }

    /// <summary>Scratch buffer for deformation; copied into <see cref="Deformed"/> each pass.</summary>
    public Vertex[] Vertices { get; init; } = [];

    /// <summary>How many vertices the last deformation wrote.</summary>
    public int DeformedCount { get; private set; }

    /// <summary>Recomputes the deformed vertices from the bones' current transforms.</summary>
    /// <returns>The number of vertices written.</returns>
    public int Deform(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var bind = BindPose.Vertices;
        var count = System.Math.Min(bind.Length, Influences.Length);

        if (Vertices.Length < count)
            return 0;

        // Deformation is expressed in the mesh node's own space, so the renderer can keep drawing it with
        // that node's world matrix: vertex -> bind world -> bone space -> current bone -> back to local.
        var meshWorld = MeshEntity.IsValid && world.HasComponent<Transform>(MeshEntity)
            ? world.GetComponent<Transform>(MeshEntity).GetWorldMatrix(world)
            : Matrix4x4.Identity;

        var toLocal = Matrix4x4.Invert(meshWorld, out var inverseMeshWorld)
            ? inverseMeshWorld
            : Matrix4x4.Identity;

        Span<Matrix4x4> matrices = Bones.Length <= 64 ? stackalloc Matrix4x4[Bones.Length] : new Matrix4x4[Bones.Length];

        for (var bone = 0; bone < Bones.Length; bone++)
        {
            var entity = BoneEntities[bone];

            var current = entity.IsValid && world.HasComponent<Transform>(entity)
                ? world.GetComponent<Transform>(entity).GetWorldMatrix(world)
                : Matrix4x4.Identity;

            matrices[bone] = BindWorld * Bones[bone].InverseBind * current * toLocal;
        }

        for (var vertex = 0; vertex < count; vertex++)
        {
            var source = bind[vertex];
            var influence = Influences[vertex];

            var position = Vector3.Zero;
            var normal = Vector3.Zero;
            var weightTotal = 0f;

            Accumulate(matrices, influence.Bone0, influence.Weight0, source.Position, source.Normal,
                ref position, ref normal, ref weightTotal);
            Accumulate(matrices, influence.Bone1, influence.Weight1, source.Position, source.Normal,
                ref position, ref normal, ref weightTotal);
            Accumulate(matrices, influence.Bone2, influence.Weight2, source.Position, source.Normal,
                ref position, ref normal, ref weightTotal);
            Accumulate(matrices, influence.Bone3, influence.Weight3, source.Position, source.Normal,
                ref position, ref normal, ref weightTotal);

            // A vertex with no surviving influence stays where the bind pose put it.
            Vertices[vertex] = weightTotal > 1e-6f
                ? new Vertex(position / weightTotal,
                    normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : source.Normal,
                    source.TexCoord)
                : new Vertex(source.Position, source.Normal, source.TexCoord);
        }

        DeformedCount = count;

        var renderable = Deformed ??= new Mesh($"{BindPose.Name} (skinned)",
            (Vertex[])BindPose.Vertices.Clone(), (uint[])BindPose.Indices.Clone());

        renderable.UpdateVertices(Vertices.AsSpan(0, count));
        return count;
    }

    private static void Accumulate(ReadOnlySpan<Matrix4x4> matrices, int bone, float weight,
        Vector3 bindPosition, Vector3 bindNormal, ref Vector3 position, ref Vector3 normal, ref float weightTotal)
    {
        if (bone < 0 || bone >= matrices.Length || weight <= 0f)
            return;

        var matrix = matrices[bone];

        position += Vector3.Transform(bindPosition, matrix) * weight;

        // Normals take the rotation only; translation and scale must not tilt them.
        normal += Vector3.Normalize(Vector3.TransformNormal(bindNormal, matrix)) * weight;
        weightTotal += weight;
    }
}
