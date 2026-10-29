using EidolonCore.ECS;
using EidolonCore.Rendering;
using EidolonCore.Simulation;

namespace EidolonCore.Nexus.Animation;

/// <summary>Deforms skinned meshes from their bones' current transforms.</summary>
/// <remarks>
/// Runs after the pose has been resolved, because the bones are ordinary entities that animation drove;
/// skinning is the last stage of turning a pose into drawable geometry. It runs on the CPU: the vertex
/// count never changes, so the renderer copies the new vertices into the buffers it already has.
/// </remarks>
public sealed class SkinningSystem : ISystem
{
    /// <summary>Vertices deformed during the last step, useful for diagnostics and tests.</summary>
    public int DeformedVertices { get; private set; }

    public void Update(World world, in FrameTime time)
    {
        var deformed = 0;

        foreach (var entity in world.GetEntitiesWith<SkinnedMesh>())
            deformed += world.GetComponent<SkinnedMesh>(entity).Deform(world);

        DeformedVertices = deformed;
    }
}
