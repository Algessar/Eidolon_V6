using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Eidolon.Vulkan;
using EidolonCore.Assets;
using EidolonCore.ECS;
using EidolonCore.Rendering;
using EidolonCore.Simulation;

namespace Eidolon.Editor;

/// <summary>
/// Saves and loads a scene as JSON. Entity ids are preserved, so parent links and any editor
/// selection survive a round trip.
/// </summary>
/// <remarks>
/// Meshes are referenced by name: the built-in primitives by their fixed names ("Cube", "Plane"),
/// imported models by their asset name, which is resolved through the <see cref="AssetLibrary"/>.
/// A model that cannot be resolved still loads its entity, just without a mesh.
/// </remarks>
public static class SceneSerializer
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Writes the whole world to <paramref name="path"/>.</summary>
    public static void Save(World world, string path, ViewportSettings? viewport = null)
    {
        var scene = new SceneFile
        {
            Version = CurrentVersion,
            Metadata = viewport is null ? null : new SceneMetadata
            {
                ShowGrid = viewport.ShowGrid,
                GridHeight = viewport.GridHeight,
                GridExtent = viewport.GridExtent,
                GridSpacing = viewport.GridSpacing,
            },
            Entities = world.Entities
                .OrderBy(entity => entity.Id)
                .Select(entity => Capture(world, entity))
                .ToList(),
        };

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, JsonSerializer.Serialize(scene, Options));
    }

    /// <summary>
    /// Replaces the contents of <paramref name="world"/> with the scene in <paramref name="path"/>.
    /// </summary>
    public static void Load(string path, World world, AssetLibrary assets, ViewportSettings? viewport = null)
    {
        var scene = JsonSerializer.Deserialize<SceneFile>(File.ReadAllText(path), Options)
                    ?? throw new InvalidDataException("The scene file is empty.");

        if (scene.Version > CurrentVersion)
            throw new InvalidDataException($"Scene version {scene.Version} is newer than supported ({CurrentVersion}).");

        if (viewport is not null && scene.Metadata is { } metadata)
        {
            viewport.ShowGrid = metadata.ShowGrid;
            viewport.GridHeight = metadata.GridHeight;
            viewport.GridExtent = metadata.GridExtent;
            viewport.GridSpacing = metadata.GridSpacing;
        }

        world.Clear();

        foreach (var saved in scene.Entities)
        {
            var entity = world.CreateEntity(saved.Id);

            if (saved.Transform is { } transform)
            {
                world.SetComponent(entity, new Transform(ToVector(transform.Position),
                    ToQuaternion(transform.Rotation), ToVector(transform.Scale))
                {
                    Parent = saved.Parent is { } parentId ? new Entity(parentId) : default,
                });
            }

            if (saved.Mesh is { } mesh)
            {
                world.SetComponent(entity, new MeshRenderer(ResolveMesh(mesh, assets),
                    new Material(mesh.Material, ToVector4(mesh.Color, Vector4.One))));
            }

            if (saved.Camera is { } camera)
            {
                world.SetComponent(entity, CaptureCamera(camera));
            }

            if (saved.Spin is { } spin)
            {
                world.SetComponent(entity, new Spin
                {
                    Axis = ToVector(spin.Axis),
                    RadiansPerSecond = spin.RadiansPerSecond,
                });
            }
        }
    }

    private static SceneEntity Capture(World world, Entity entity)
    {
        var saved = new SceneEntity { Id = entity.Id };

        if (world.TryGetComponent<Transform>(entity, out var transform))
        {
            saved.Transform = new TransformData
            {
                Position = ToArray(transform.Position),
                Rotation = ToArray(transform.Rotation),
                Scale = ToArray(transform.Scale),
            };

            if (transform.Parent.IsValid)
                saved.Parent = transform.Parent.Id;
        }

        if (world.TryGetComponent<MeshRenderer>(entity, out var renderer))
        {
            saved.Mesh = new MeshData
            {
                Name = renderer.Mesh?.Name ?? string.Empty,
                Material = renderer.Material.Name,
                Color = ToArray(renderer.Material.BaseColor),
            };
        }

        if (world.TryGetComponent<Camera>(entity, out var camera))
        {
            saved.Camera = new CameraData
            {
                Kind = camera.Kind.ToString(),
                Position = ToArray(camera.Position),
                Target = ToArray(camera.Target),
                Up = ToArray(camera.Up),
                NearPlane = camera.NearPlane,
                FarPlane = camera.FarPlane,
                FieldOfView = camera.FieldOfView,
                UseYawPitch = camera.UseYawPitch,
                Yaw = camera.YawRadians,
                Pitch = camera.PitchRadians,
            };
        }

        if (world.TryGetComponent<Spin>(entity, out var spin))
        {
            saved.Spin = new SpinData
            {
                Axis = ToArray(spin.Axis),
                RadiansPerSecond = spin.RadiansPerSecond,
            };
        }

        return saved;
    }

    private static Mesh? ResolveMesh(MeshData mesh, AssetLibrary assets)
    {
        if (string.IsNullOrEmpty(mesh.Name))
            return null;

        if (string.Equals(mesh.Name, PrimitiveMeshes.Cube.Name, StringComparison.OrdinalIgnoreCase))
            return PrimitiveMeshes.Cube;

        if (string.Equals(mesh.Name, PrimitiveMeshes.Plane.Name, StringComparison.OrdinalIgnoreCase))
            return PrimitiveMeshes.Plane;

        foreach (var asset in assets.Catalog)
        {
            if (string.Equals(asset.Name, mesh.Name, StringComparison.OrdinalIgnoreCase) &&
                assets.TryLoad(asset, out var loaded, out _))
            {
                return loaded;
            }
        }

        Debug.Log($"Scene load: no mesh named '{mesh.Name}'", VALIDATION_LAYERS.WARNING);
        return null;
    }

    private static Camera CaptureCamera(CameraData data)
    {
        var camera = new Camera
        {
            Kind = Enum.TryParse<CameraKind>(data.Kind, out var kind) ? kind : CameraKind.Game,
            NearPlane = data.NearPlane,
            FarPlane = data.FarPlane,
            FieldOfView = data.FieldOfView,
            UseYawPitch = data.UseYawPitch,
            YawRadians = data.Yaw,
            PitchRadians = data.Pitch,
            Up = ToVector(data.Up),
        };

        camera.SetTarget(ToVector(data.Target));
        camera.Position = ToVector(data.Position);
        return camera;
    }

    private static float[] ToArray(Vector3 value) => [value.X, value.Y, value.Z];

    private static float[] ToArray(Vector4 value) => [value.X, value.Y, value.Z, value.W];

    private static float[] ToArray(Quaternion value) => [value.X, value.Y, value.Z, value.W];

    private static Vector3 ToVector(float[]? values) =>
        values is { Length: >= 3 } ? new Vector3(values[0], values[1], values[2]) : Vector3.Zero;

    private static Vector4 ToVector4(float[]? values, Vector4 fallback) =>
        values is { Length: >= 4 } ? new Vector4(values[0], values[1], values[2], values[3]) : fallback;

    private static Quaternion ToQuaternion(float[]? values) =>
        values is { Length: >= 4 } ? Quaternion.Normalize(new Quaternion(values[0], values[1], values[2], values[3]))
                                   : Quaternion.Identity;

    private sealed class SceneFile
    {
        public int Version { get; set; }

        public SceneMetadata? Metadata { get; set; }

        public List<SceneEntity> Entities { get; set; } = [];
    }

    private sealed class SceneMetadata
    {
        public bool ShowGrid { get; set; } = true;

        public float GridHeight { get; set; } = -0.498f;

        public float GridExtent { get; set; } = 20f;

        public float GridSpacing { get; set; } = 1f;
    }

    private sealed class SceneEntity
    {
        public uint Id { get; set; }

        public uint? Parent { get; set; }

        public TransformData? Transform { get; set; }

        public MeshData? Mesh { get; set; }

        public CameraData? Camera { get; set; }

        public SpinData? Spin { get; set; }
    }

    private sealed class TransformData
    {
        public float[]? Position { get; set; }

        public float[]? Rotation { get; set; }

        public float[]? Scale { get; set; }
    }

    private sealed class MeshData
    {
        public string Name { get; set; } = string.Empty;

        public string Material { get; set; } = "Material";

        public float[]? Color { get; set; }
    }

    private sealed class CameraData
    {
        public string Kind { get; set; } = "Game";

        public float[]? Position { get; set; }

        public float[]? Target { get; set; }

        public float[]? Up { get; set; }

        public float NearPlane { get; set; } = 0.1f;

        public float FarPlane { get; set; } = 1000f;

        public float FieldOfView { get; set; }

        public bool UseYawPitch { get; set; }

        public float Yaw { get; set; }

        public float Pitch { get; set; }
    }

    private sealed class SpinData
    {
        public float[]? Axis { get; set; }

        public float RadiansPerSecond { get; set; }
    }
}
