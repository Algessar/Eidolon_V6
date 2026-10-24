using EidolonCore.Nexus;
using System.Numerics;
using Eidolon.Vulkan;
using EidolonCore.Assets;
using EidolonCore.Simulation;
using EidolonCore.ECS;
using EidolonCore.Rendering;

namespace Eidolon.Editor;

/// <summary>
/// Builds the scene the editor opens with: a few primitives with distinct materials, plus the
/// sample OBJ that ships in <c>Assets/Models</c>. This stands in for real scene loading.
/// </summary>
public static class DefaultScene
{
    public static void Populate(World world, AssetLibrary assets)
    {
        var camera = new Camera { Position = new Vector3(3.2f, 2.4f, 5.2f) };
        camera.SetTarget(new Vector3(0f, 0.3f, 0f));
        world.SetComponent(world.CreateEntity(), camera);

        // Ground slab; its top face is at y = -0.5, which is where the objects below rest.
        SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Plane,
            new Material("Ground", new Vector4(0.34f, 0.36f, 0.42f, 1f)),
            new Vector3(0f, -0.5f, 0f), new Vector3(12f, 1f, 12f));

        // Lifted and tilted so play mode visibly drops and settles it onto the ground.`
        var fallingBox = SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Cube,
            new Material("Red Plastic", new Vector4(0.85f, 0.25f, 0.28f, 1f)),
            new Vector3(0f, 2.2f, 0f), Vector3.One,
            Quaternion.CreateFromYawPitchRoll(0.3f, 0.2f, 0.5f));

        world.SetComponent(fallingBox, new XpbdBody { HalfExtents = new Vector3(0.5f), ContactRadius = 0.02f });

        SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Cube,
            new Material("Blue Plastic", new Vector4(0.25f, 0.45f, 0.90f, 1f)),
            new Vector3(1.7f, -0.2f, -0.5f), new Vector3(0.6f),
            Quaternion.CreateFromYawPitchRoll(0.6f, 0f, 0.2f));

        SceneSpawner.SpawnMesh(world, PrimitiveMeshes.Cube,
            new Material("Yellow Plastic", new Vector4(0.95f, 0.80f, 0.25f, 1f)),
            new Vector3(-1.5f, -0.325f, 0.9f), new Vector3(0.35f),
            Quaternion.CreateFromYawPitchRoll(-0.4f, 0.9f, 0f));

        AddImportedModel(world, assets);
    }

    /// <summary>Imports the sample OBJ, skipping it when it was not deployed with the editor.</summary>
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
