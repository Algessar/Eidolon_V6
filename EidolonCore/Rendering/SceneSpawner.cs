using System.Numerics;
using EidolonCore.ECS;

namespace EidolonCore.Rendering;

/// <summary>
/// Creates scene entities. Shared by the game's scene, the editor's asset browser and the drop target,
/// so everything builds drawable objects the same way.
/// </summary>
/// <remarks>
/// Scene authoring belongs to the engine, not to the renderer: it only touches component data, so game
/// code can build a scene while depending on <c>EidolonCore</c> alone. The renderer consumes these
/// components; it does not own them.
/// </remarks>
public static class SceneSpawner
{
    public static Entity SpawnMesh(World world, Mesh mesh, Material material, Vector3 position, Vector3 scale,
        Quaternion? rotation = null)
    {
        var entity = world.CreateEntity();
        world.SetComponent(entity, new Transform(position, rotation ?? Quaternion.Identity, scale));
        world.SetComponent(entity, new MeshRenderer(mesh, material));
        return entity;
    }

    /// <summary>
    /// Spawns a mesh scaled to fit a unit box, with its bounds centre at, <paramref name="center"/>.
    /// </summary>
    /// <remarks>
    /// Imported models come in whatever units they were authored in, so this is what makes an
    /// arbitrary OBJ appear at a predictable size when it is dragged into the scene. Note that
    /// the entity's <see cref="Transform.Position"/> is the origin-adjusted position, not
    /// <paramref name="center"/>; pivots should really be baked at import time.
    /// </remarks>
    public static Entity SpawnFittedMesh(World world, Mesh mesh, Material material, Vector3 center,
        Quaternion? rotation = null)
    {
        var bounds = mesh.LocalBounds;
        var maxDimension = bounds.MaxDimension;

        var scale = maxDimension > 1e-6f ? 1f / maxDimension : 1f;
        var origin = center - bounds.Center * scale;

        return SpawnMesh(world, mesh, material, origin, new Vector3(scale), rotation);
    }

    /// <summary>
    /// Spawns a camera entity. Game cameras drive the game view; editor cameras drive the
    /// viewport (the default scene ships with one).
    /// </summary>
    /// <remarks>
    /// The transform is given the rotation that looks at <paramref name="target"/>, and the camera is
    /// told that same rotation. Otherwise the first sync would see a transform and a view that
    /// disagree and re-aim the camera somewhere the caller did not ask for.
    /// </remarks>
    public static Entity SpawnCamera(World world, Vector3 position, Vector3 target,
        CameraKind kind = CameraKind.Game)
    {
        var rotation = CameraOrientation.RotationFrom(target - position);

        var camera = new Camera
        {
            Kind = kind,
            Position = position,
            TransformRotation = rotation,
        };

        camera.SetTarget(target);

        var entity = world.CreateEntity();
        world.SetComponent(entity, new Transform(position, rotation, Vector3.One));
        world.SetComponent(entity, camera);
        return entity;
    }
}
