using System.Numerics;
using EidolonCore.Assets;
using EidolonCore.ECS;
using EidolonCore.Nexus;
using EidolonCore.Rendering;
using EidolonCore.Simulation;

namespace EidolonGame;

/// <summary>
/// Builds the sandbox: a few primitives with distinct materials, a playable camera, plus the sample
/// OBJ that ships in <c>Assets/Models</c>. This stands in for real scene loading.
/// </summary>
public static class DefaultScene
{
    /// <summary>Populates the authored world, returning the camera the player controls.</summary>
    public static Entity Populate(World world, AssetLibrary assets)
    {
        // The viewport camera. The editor keeps its transform in step as you navigate.
        SceneSpawner.SpawnCamera(world, new Vector3(3.2f, 2.4f, 5.2f), new Vector3(0f, 0.3f, 0f),
            CameraKind.Editor);

        // The game camera: what the game view looks through, and what the player flies.
        var player = SceneSpawner.SpawnCamera(world, new Vector3(0f, 2.6f, 7.5f), new Vector3(0f, 0.4f, 0f));

        // Ground slab; its top face is at y = -0.5, which is where the objects below rest.
        SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Plane,
            new Material("Ground", new Vector4(0.34f, 0.36f, 0.42f, 1f)),
            new Vector3(0f, -0.5f, 0f), new Vector3(12f, 1f, 12f));

        // Static bodies: they collide with everything that moves, and nothing can move them.
        var blue = SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Cube,
            new Material("Blue Plastic", new Vector4(0.25f, 0.45f, 0.90f, 1f)),
            new Vector3(1.7f, -0.2f, -0.5f), new Vector3(0.6f),
            Quaternion.CreateFromYawPitchRoll(0.6f, 0f, 0.2f));

        world.SetComponent(blue, new XpbdBody { HalfExtents = new Vector3(0.3f), InverseMass = 0f });

        var yellow = SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Cube,
            new Material("Yellow Plastic", new Vector4(0.95f, 0.80f, 0.25f, 1f)),
            new Vector3(-1.5f, -0.325f, 0.9f), new Vector3(0.35f),
            Quaternion.CreateFromYawPitchRoll(-0.4f, 0.9f, 0f));

        world.SetComponent(yellow, new XpbdBody { HalfExtents = new Vector3(0.175f), InverseMass = 0f });

        // Lifted above the blue cube, so play mode drops it onto another body rather than the floor.
        var fallingBox = SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Cube,
            new Material("Red Plastic", new Vector4(0.85f, 0.25f, 0.28f, 1f)),
            new Vector3(1.7f, 2.4f, -0.5f), Vector3.One,
            Quaternion.CreateFromYawPitchRoll(0.3f, 0.2f, 0.5f));

        world.SetComponent(fallingBox, new XpbdBody { HalfExtents = new Vector3(0.5f), ContactRadius = 0.02f });

        // One of each remaining collision shape, so the shape switch has something to talk about.
        var sphere = SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Sphere,
            new Material("Green Plastic", new Vector4(0.30f, 0.75f, 0.42f, 1f)),
            new Vector3(-0.9f, 2.2f, -1.5f), new Vector3(0.7f));

        world.SetComponent(sphere, new XpbdBody { Shape = XpbdShape.Sphere, Radius = 0.35f });

        var capsule = SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Capsule,
            new Material("Violet Plastic", new Vector4(0.62f, 0.42f, 0.85f, 1f)),
            new Vector3(0.5f, 2.6f, 1.6f), Vector3.One,
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.7f));

        world.SetComponent(capsule, new XpbdBody { Shape = XpbdShape.Capsule, Radius = 0.5f, SegmentLength = 1f });

        AddImportedModel(world, assets);

        return player;
    }

    /// <summary>Imports the sample OBJ, skipping it when it was not deployed with the host.</summary>
    private static void AddImportedModel(World world, AssetLibrary assets)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "icosahedron.obj");

        if (!File.Exists(path))
        {
            Debug.Log($"Default scene: no model at '{path}', skipping the import demo.", VALIDATION_LAYERS.WARNING);
            return;
        }

        var mesh = assets.LoadObj(path);
        Debug.Log($"Default scene: imported {mesh}", VALIDATION_LAYERS.INFO);

        var icosahedron = SceneSpawner.SpawnMesh(world, mesh,
            new Material("Icosahedron", new Vector4(0.30f, 0.80f, 0.72f, 1f)),
            new Vector3(-0.2f, 0.4f, -2.3f), new Vector3(0.9f));

        // Something visibly moving to prove play mode, systems and FrameTime are wired up.
        world.SetComponent(icosahedron, new Spin { Axis = Vector3.UnitY, RadiansPerSecond = 1.2f });
    }
}
