using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using EidolonCore.Assets;
using EidolonCore.ECS;
using EidolonCore.Nexus;
using EidolonCore.Nexus.Animation;
using EidolonCore.Rendering;
using EidolonCore.Simulation;

namespace EidolonCore.Scene;

/// <summary>
/// Saves and loads a scene as JSON. Entity ids are preserved, so parent links and any editor
/// selection survive a round trip.
/// </summary>
/// <remarks>
/// Meshes are referenced by name: the built-in primitives by their fixed names ("Cube", "Plane"),
/// imported models by their asset name, which is resolved through the <see cref="AssetLibrary"/>.
/// A model that cannot be resolved still loads its entity, just without a mesh. An imported model is
/// stored as its source path plus each entity's node index, so loading imports the file again rather
/// than writing megabytes of vertices into the scene.
/// </remarks>
public static class SceneSerializer
{
    // 2 adds rigid bodies, 3 adds names and animation, 4 adds imported model instances. Bumped rather
    // than extended silently: an older build must refuse a newer file instead of loading it with parts of
    // the scene stripped out.
    private const int CurrentVersion = 4;

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
    /// <param name="models">
    /// Reads imported model files again. Without it, a scene's imported entities come back as named,
    /// parented transforms with no geometry, which is what a headless load wants.
    /// </param>
    public static void Load(string path, World world, AssetLibrary assets, ViewportSettings? viewport = null,
        IModelImporter? models = null)
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

        var imported = ImportModels(scene, world, assets, models, path);

        foreach (var saved in scene.Entities)
        {
            // An imported node already exists: the model was spawned onto the ids the scene recorded.
            var known = new Entity(saved.Id);
            var entity = world.IsAlive(known) ? known : world.CreateEntity(saved.Id);

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
                var material = new Material(mesh.Material, ToVector4(mesh.Color, Vector4.One));

                // Imported geometry comes back from the file, so only its colour is restored here.
                if (saved.ModelNode is not null &&
                    world.TryGetComponent<MeshRenderer>(entity, out var importedRenderer))
                {
                    importedRenderer.Material = material;
                }
                else
                {
                    world.SetComponent(entity, new MeshRenderer(ResolveMesh(mesh, assets), material));
                }
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

            if (saved.XpbdBody is { } body)
            {
                world.SetComponent(entity, CaptureBody(body));
            }

            if (saved.Name is { } name && !string.IsNullOrEmpty(name.Value))
            {
                world.SetComponent(entity, new Name(name.Value));
            }

            if (saved.Animation is { } animation)
            {
                // An imported model's player names a clip instead of carrying it: the clip is in the file.
                if (animation.ClipName is { Length: > 0 } clipName)
                {
                    var clip = imported.TryGetValue(saved.Id, out var model)
                        ? model.Clips.FirstOrDefault(candidate =>
                            string.Equals(candidate.Name, clipName, StringComparison.Ordinal))
                        : null;

                    if (clip is null)
                    {
                        Debug.Log($"Scene load: '{clipName}' is no longer in the model, keeping its first clip",
                            VALIDATION_LAYERS.WARNING);
                    }
                    else
                    {
                        world.SetComponent(entity, CaptureAnimation(animation, clip));
                    }
                }
                else
                {
                    world.SetComponent(entity, CaptureAnimation(animation));
                }
            }
        }
    }

    /// <summary>
    /// Re-imports every model instance the scene recorded and spawns it onto the ids in the file.
    /// </summary>
    /// <returns>The imported assets by root entity id, for the clip that has to be picked back up.</returns>
    private static Dictionary<uint, ModelAsset> ImportModels(SceneFile scene, World world,
        AssetLibrary assets, IModelImporter? models, string scenePath)
    {
        var imported = new Dictionary<uint, ModelAsset>();

        foreach (var root in scene.Entities)
        {
            if (root.Model?.SourcePath is not { Length: > 0 } source)
                continue;

            if (models is null)
            {
                Debug.Log($"Scene load: no importer for '{source}', the model loads without geometry",
                    VALIDATION_LAYERS.WARNING);
                continue;
            }

            var resolved = ResolveModelPath(source, assets, scenePath);

            if (resolved is null)
            {
                Debug.Log($"Scene load: model file '{source}' is missing", VALIDATION_LAYERS.WARNING);
                continue;
            }

            ModelAsset? model;

            try
            {
                model = models.Import(resolved);
            }
            catch (Exception exception)
            {
                Debug.Log($"Scene load: could not import '{resolved}': {exception.Message}",
                    VALIDATION_LAYERS.WARNING);
                continue;
            }

            if (model is null)
                continue;

            // The recorded id per node, so the entities the spawner creates are the ones the scene
            // already describes: parent links, bones and selection all still point at them.
            var nodeIds = scene.Entities
                .Where(entry => entry.ModelNode is { } node && node.Root == root.Id)
                .OrderBy(entry => entry.ModelNode!.Index)
                .Select(entry => entry.Id)
                .ToArray();

            var material = new Material(root.Mesh?.Material ?? model.Name,
                ToVector4(root.Mesh?.Color, Vector4.One));

            var placement = ToTransform(root.Transform);
            var scale = placement.Scale;
            var uniform = MathF.Abs(scale.X - scale.Y) < 1e-4f && MathF.Abs(scale.X - scale.Z) < 1e-4f
                ? scale.X
                : 1f;

            ModelSpawner.Spawn(world, model, material, placement.Position, placement.Rotation, uniform,
                index => index < nodeIds.Length ? nodeIds[index] : null);

            imported[root.Id] = model;
        }

        return imported;
    }

    /// <summary>
    /// Finds the model file again: the recorded path first, then any asset with the same file name, so a
    /// scene still loads after the project has been moved.
    /// </summary>
    private static string? ResolveModelPath(string source, AssetLibrary assets, string scenePath)
    {
        if (File.Exists(source))
            return source;

        var fileName = Path.GetFileName(source);

        foreach (var asset in assets.Catalog)
        {
            if (string.Equals(Path.GetFileName(asset.Path), fileName, StringComparison.OrdinalIgnoreCase))
                return asset.Path;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(scenePath)) ?? ".";
        var besideScene = Path.Combine(directory, source);

        return File.Exists(besideScene) ? besideScene : null;
    }

    private static Transform ToTransform(TransformData? data) => new(
        ToVector(data?.Position),
        data?.Rotation is { Length: >= 4 } ? ToQuaternion(data.Rotation) : Quaternion.Identity,
        data?.Scale is { Length: >= 3 } ? ToVector(data.Scale) : Vector3.One);

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

        if (world.TryGetComponent<ModelOrigin>(entity, out var origin))
        {
            saved.ModelNode = new ModelNodeData { Root = origin.Root.Id, Index = origin.NodeIndex };

            if (origin.SourcePath is { Length: > 0 } source)
                saved.Model = new ModelData { SourcePath = source };
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

        if (world.TryGetComponent<XpbdBody>(entity, out var body))
        {
            saved.XpbdBody = new XpbdBodyData
            {
                Shape = body.Shape.ToString(),
                HalfExtents = ToArray(body.HalfExtents),
                Radius = body.Radius,
                SegmentLength = body.SegmentLength,
                ContactRadius = body.ContactRadius,
                InverseMass = body.InverseMass,
                Stiffness = body.Stiffness,
            };
        }

        if (world.TryGetComponent<Name>(entity, out var name))
        {
            saved.Name = new NameData { Value = name.Value };
        }

        if (world.TryGetComponent<AnimationPlayer>(entity, out var animation))
        {
            // Inside an imported model the clip came from the file, so only its name travels.
            var fromFile = world.TryGetComponent<ModelOrigin>(entity, out var playerOrigin) &&
                           playerOrigin.Root == entity &&
                           playerOrigin.SourcePath is { Length: > 0 };

            saved.Animation = CaptureAnimation(animation, fromFile ? animation.Clip?.Name : null);
        }

        return saved;
    }

    private static AnimationPlayerData CaptureAnimation(AnimationPlayer player, string? clipName = null) => new()
    {
        Time = player.Time,
        Speed = player.Speed,
        Loop = player.Loop,
        Playing = player.Playing,
        ClipName = clipName,
        Clip = clipName is null && player.Clip is { } clip
            ? new AnimationClipData
            {
                Name = clip.Name,
                Duration = clip.Duration,
                Tracks = clip.Tracks.Select(track => new AnimationTrackData
                {
                    Path = track.Path,
                    Keys = track.Keys.Select(key => new AnimationKeyData
                    {
                        Time = key.Time,
                        Position = ToArray(key.Pose.Position),
                        Rotation = ToArray(key.Pose.Rotation),
                        Scale = ToArray(key.Pose.Scale),
                    }).ToList(),
                }).ToList(),
            }
            : null,
    };

    /// <summary>Rebuilds a player, preferring a clip from the re-imported file over the inline one.</summary>
    private static AnimationPlayer CaptureAnimation(AnimationPlayerData data, AnimationClip? clip = null) => new()
    {
        Time = data.Time,
        Speed = data.Speed,
        Loop = data.Loop,
        Playing = data.Playing,
        Clip = clip ?? (data.Clip is { } inline ? RebuildClip(inline) : null),
    };

    private static AnimationClip? RebuildClip(AnimationClipData data)
    {
        if (data.Duration <= 0f)
            return null;

        var tracks = new List<AnimationTrack>(data.Tracks.Count);

        foreach (var track in data.Tracks)
        {
            if (string.IsNullOrWhiteSpace(track.Path) || track.Keys.Count == 0)
                continue;

            var keys = track.Keys
                .Select(key => new PoseKey(key.Time, new LocalPose(
                    ToVector(key.Position),
                    ToQuaternion(key.Rotation),
                    key.Scale is { Length: >= 3 } ? ToVector(key.Scale) : Vector3.One)))
                .ToList();

            tracks.Add(new AnimationTrack(track.Path, keys));
        }

        return tracks.Count == 0
            ? null
            : new AnimationClip(string.IsNullOrEmpty(data.Name) ? "Clip" : data.Name, data.Duration, tracks);
    }

    private static Mesh? ResolveMesh(MeshData mesh, AssetLibrary assets)
    {
        if (string.IsNullOrEmpty(mesh.Name))
            return null;

        if (string.Equals(mesh.Name, PrimitiveMeshes.Cube.Name, StringComparison.OrdinalIgnoreCase))
            return PrimitiveMeshes.Cube;

        if (string.Equals(mesh.Name, PrimitiveMeshes.Plane.Name, StringComparison.OrdinalIgnoreCase))
            return PrimitiveMeshes.Plane;

        if (string.Equals(mesh.Name, PrimitiveMeshes.Sphere.Name, StringComparison.OrdinalIgnoreCase))
            return PrimitiveMeshes.Sphere;

        if (string.Equals(mesh.Name, PrimitiveMeshes.Capsule.Name, StringComparison.OrdinalIgnoreCase))
            return PrimitiveMeshes.Capsule;

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

    private static XpbdBody CaptureBody(XpbdBodyData data) => new()
    {
        Shape = Enum.TryParse<XpbdShape>(data.Shape, out var shape) ? shape : XpbdShape.Box,
        HalfExtents = data.HalfExtents is { Length: 3 } ? ToVector(data.HalfExtents) : new Vector3(0.5f),
        Radius = data.Radius,
        SegmentLength = data.SegmentLength,
        ContactRadius = data.ContactRadius,
        InverseMass = data.InverseMass,
        Stiffness = data.Stiffness,
    };

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

        public XpbdBodyData? XpbdBody { get; set; }

        public NameData? Name { get; set; }

        public AnimationPlayerData? Animation { get; set; }

        /// <summary>Set on the entity an imported file was spawned on: which file, not what is in it.</summary>
        public ModelData? Model { get; set; }

        /// <summary>Set on every entity an import created: the instance it belongs to and its node order.</summary>
        public ModelNodeData? ModelNode { get; set; }
    }

    private sealed class ModelData
    {
        public string SourcePath { get; set; } = string.Empty;
    }

    private sealed class ModelNodeData
    {
        public uint Root { get; set; }

        public int Index { get; set; }
    }

    private sealed class NameData
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class AnimationPlayerData
    {
        public float Time { get; set; }

        public float Speed { get; set; } = 1f;

        public bool Loop { get; set; } = true;

        public bool Playing { get; set; } = true;

        /// <summary>Clip to pick again from the model file, for an imported model's player.</summary>
        public string? ClipName { get; set; }

        public AnimationClipData? Clip { get; set; }
    }

    private sealed class AnimationClipData
    {
        public string Name { get; set; } = string.Empty;

        public float Duration { get; set; }

        public List<AnimationTrackData> Tracks { get; set; } = [];
    }

    private sealed class AnimationTrackData
    {
        public string Path { get; set; } = string.Empty;

        public List<AnimationKeyData> Keys { get; set; } = [];
    }

    private sealed class AnimationKeyData
    {
        public float Time { get; set; }

        public float[]? Position { get; set; }

        public float[]? Rotation { get; set; }

        public float[]? Scale { get; set; }
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

    private sealed class XpbdBodyData
    {
        public string Shape { get; set; } = "Box";

        public float[]? HalfExtents { get; set; }

        public float Radius { get; set; } = 0.5f;

        public float SegmentLength { get; set; } = 1f;

        public float ContactRadius { get; set; } = 0.02f;

        public float InverseMass { get; set; } = 1f;

        public float Stiffness { get; set; } = 1f;
    }
}
