using System.Numerics;
using System.Runtime.InteropServices;
using EidolonCore.Assets;
using EidolonCore.Nexus.Animation;
using EidolonCore.Rendering;
using Silk.NET.Assimp;

using AssimpApi = Silk.NET.Assimp.Assimp;
using CoreMesh = EidolonCore.Rendering.Mesh;

namespace Eidolon.Assimp;

/// <summary>
/// Reads any format Assimp supports - FBX, DAE, glTF, OBJ, 3DS and the rest - into a format-neutral
/// <see cref="ModelAsset"/>.
/// </summary>
/// <remarks>
/// The mapping is deliberately thin: nodes become named model nodes (so their names survive as animation
/// paths), meshes are merged per node, and Assimp's tick-based animation channels are converted to seconds
/// and to one key per sampled time. Skinning is not read yet, so skinned meshes import as static geometry
/// attached to whatever node they belong to.
/// </remarks>
public static unsafe class AssimpModelImporter
{
    /// <summary>Assimp reports zero ticks per second for files that do not say.</summary>
    private const double DefaultTicksPerSecond = 25d;

    private const PostProcessSteps ReadFlags =
        PostProcessSteps.Triangulate | PostProcessSteps.GenerateSmoothNormals | PostProcessSteps.JoinIdenticalVertices;

    /// <summary>Imports a file, throwing with Assimp's own message when it cannot be read.</summary>
    public static ModelAsset Import(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!System.IO.File.Exists(path))
            throw new FileNotFoundException("No model at that path.", path);

        using var api = AssimpApi.GetApi();

        // The C entry point takes a UTF-8 path, so marshal it rather than relying on a string overload.
        var nativePath = Marshal.StringToCoTaskMemUTF8(path);
        Scene* scene;

        try
        {
            scene = api.ImportFile((byte*)nativePath, (uint)ReadFlags);
        }
        finally
        {
            Marshal.FreeCoTaskMem(nativePath);
        }

        if (scene is null)
            throw new InvalidDataException($"Assimp could not read '{path}': {api.GetErrorStringS()}");

        try
        {
            return Read(scene, Path.GetFileNameWithoutExtension(path));
        }
        finally
        {
            api.ReleaseImport(scene);
        }
    }

    private static ModelAsset Read(Scene* scene, string name)
    {
        var model = new ModelAsset { Name = name };

        // Node name to clip path, filled while walking the tree; clip channels are keyed by node name.
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);

        ReadNode(model, scene, scene->MRootNode, isRoot: true, parentPath: string.Empty, paths);
        ReadAnimations(model, scene, paths);

        return model;
    }

    private static int ReadNode(ModelAsset model, Scene* scene, Node* node, bool isRoot, string parentPath,
        Dictionary<string, string> paths)
    {
        var index = model.Nodes.Count;
        var nodeName = NameOf(node->MName);
        var modelNode = new ModelNode { Name = string.IsNullOrEmpty(nodeName) ? $"Node{index}" : nodeName };

        model.Nodes.Add(modelNode);

        var path = isRoot
            ? string.Empty
            : string.IsNullOrEmpty(parentPath) ? modelNode.Name : $"{parentPath}/{modelNode.Name}";

        // The root maps to the empty path, which is how an animation drives the entity itself. Missing it
        // would silently drop root motion, the one channel most rigs care about.
        paths.TryAdd(modelNode.Name, path);

        var matrix = ToMatrix(node->MTransformation);

        if (Matrix4x4.Decompose(matrix, out var scale, out var rotation, out var position))
        {
            modelNode.Position = position;
            modelNode.Rotation = Quaternion.Normalize(rotation);
            modelNode.Scale = scale;
        }

        (modelNode.Mesh, modelNode.Skin) = BuildGeometry(scene, node, modelNode.Name);

        for (var child = 0; child < node->MNumChildren; child++)
            modelNode.Children.Add(ReadNode(model, scene, node->MChildren[child], isRoot: false, path, paths));

        return index;
    }

    /// <summary>
    /// Merges the node's meshes into one buffer and reads the bone influences that deform them.
    /// </summary>
    /// <remarks>
    /// Bone indices are remapped into a single bone list for the merged mesh, and each vertex's weights
    /// are normalised, because exporters are not obliged to emit weights that sum to one.
    /// </remarks>
    private static (CoreMesh? Mesh, ModelSkin? Skin) BuildGeometry(Scene* scene, Node* node, string name)
    {
        if (node->MNumMeshes == 0 || scene->MMeshes is null)
            return (null, null);

        var vertices = new List<Vertex>();
        var indices = new List<uint>();
        var influences = new List<SkinInfluence>();
        var boneNames = new List<string>();
        var boneLookup = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var slot = 0; slot < node->MNumMeshes; slot++)
        {
            var mesh = scene->MMeshes[node->MMeshes[slot]];

            if (mesh is null)
                continue;

            var baseVertex = vertices.Count;

            for (var vertex = 0; vertex < mesh->MNumVertices; vertex++)
            {
                var position = mesh->MVertices[vertex];
                var normal = mesh->MNormals is null ? Vector3.UnitY : ToVector3(mesh->MNormals[vertex]);
                var uv = ReadUv(mesh, vertex);

                vertices.Add(new Vertex(ToVector3(position), normal, uv));
                influences.Add(SkinInfluence.None);
            }

            for (var faceIndex = 0; faceIndex < mesh->MNumFaces; faceIndex++)
            {
                var face = mesh->MFaces[faceIndex];

                // Triangulate runs first, so faces are triangles; a fan keeps stray polygons usable.
                for (var corner = 0; corner + 2 < face.MNumIndices; corner++)
                {
                    indices.Add((uint)baseVertex + face.MIndices[0]);
                    indices.Add((uint)baseVertex + face.MIndices[corner + 1]);
                    indices.Add((uint)baseVertex + face.MIndices[corner + 2]);
                }
            }

            for (var boneIndex = 0; boneIndex < mesh->MNumBones; boneIndex++)
            {
                var bone = mesh->MBones[boneIndex];

                if (bone is null)
                    continue;

                var boneName = NameOf(bone->MName);

                if (string.IsNullOrEmpty(boneName))
                    continue;

                if (!boneLookup.TryGetValue(boneName, out var skinIndex))
                {
                    skinIndex = boneNames.Count;
                    boneNames.Add(boneName);
                    boneLookup[boneName] = skinIndex;
                }

                for (var weightIndex = 0; weightIndex < bone->MNumWeights; weightIndex++)
                {
                    var weight = bone->MWeights[weightIndex];
                    var vertex = baseVertex + (int)weight.MVertexId;

                    if (vertex < 0 || vertex >= influences.Count)
                        continue;

                    var influence = influences[vertex];
                    influence.Add(skinIndex, weight.MWeight);
                    influences[vertex] = influence;
                }
            }

            // Normalise this mesh's vertices now that all of its bones have been read.
            for (var vertex = baseVertex; vertex < vertices.Count; vertex++)
            {
                var influence = influences[vertex];
                influence.Normalize();
                influences[vertex] = influence;
            }
        }

        if (vertices.Count == 0 || indices.Count == 0)
            return (null, null);

        var coreMesh = new CoreMesh(name, vertices.ToArray(), indices.ToArray());

        var skin = boneNames.Count > 0 && influences.Count == vertices.Count
            ? new ModelSkin { BoneNames = boneNames, Influences = influences }
            : null;

        return (coreMesh, skin);
    }
    private static Vector2 ReadUv(Silk.NET.Assimp.Mesh* mesh, int vertex)
    {
        if (mesh->MTextureCoords[0] is null)
            return Vector2.Zero;

        var uv = mesh->MTextureCoords[0][vertex];
        return new Vector2(uv.X, uv.Y);
    }

    private static void ReadAnimations(ModelAsset model, Scene* scene, Dictionary<string, string> paths)
    {
        for (var index = 0; index < scene->MNumAnimations; index++)
        {
            var animation = scene->MAnimations[index];

            if (animation is null)
                continue;

            var ticks = animation->MTicksPerSecond > 0 ? animation->MTicksPerSecond : DefaultTicksPerSecond;
            var duration = animation->MDuration / ticks;

            if (duration <= 0d)
                continue;

            var tracks = new List<AnimationTrack>();

            for (var channelIndex = 0; channelIndex < animation->MNumChannels; channelIndex++)
            {
                var channel = animation->MChannels[channelIndex];

                if (channel is null || !paths.TryGetValue(NameOf(channel->MNodeName), out var path))
                    continue;

                var keys = ReadKeys(channel, ticks);

                if (keys.Count > 0)
                    tracks.Add(new AnimationTrack(path, keys));
            }

            if (tracks.Count == 0)
                continue;

            var clipName = NameOf(animation->MName);
            model.Clips.Add(new AnimationClip(
                string.IsNullOrEmpty(clipName) ? $"Clip{index + 1}" : clipName,
                (float)duration,
                tracks));
        }
    }

    /// <summary>
    /// Merges the channel's separate position, rotation and scale key arrays into one key per time.
    /// </summary>
    /// <remarks>
    /// Assimp keeps three arrays that may be keyed at different times, while a pose here holds all three
    /// channels at one instant. Every distinct time becomes a key, and each channel holds the value of its
    /// last key at or before that time, which matches how exporters usually key all three together.
    /// </remarks>
    private static List<PoseKey> ReadKeys(NodeAnim* channel, double ticks)
    {
        var times = new SortedSet<double>();

        for (var key = 0; key < channel->MNumPositionKeys; key++)
            times.Add(channel->MPositionKeys[key].MTime);

        for (var key = 0; key < channel->MNumRotationKeys; key++)
            times.Add(channel->MRotationKeys[key].MTime);

        for (var key = 0; key < channel->MNumScalingKeys; key++)
            times.Add(channel->MScalingKeys[key].MTime);

        var keys = new List<PoseKey>(times.Count);

        foreach (var time in times)
        {
            var position = LastVector(channel->MPositionKeys, channel->MNumPositionKeys, time, Vector3.Zero);
            var rotation = LastRotation(channel->MRotationKeys, channel->MNumRotationKeys, time);
            var scale = LastVector(channel->MScalingKeys, channel->MNumScalingKeys, time, Vector3.One);

            keys.Add(new PoseKey((float)(time / ticks), new LocalPose(position, rotation, scale)));
        }

        return keys;
    }

    private static Vector3 LastVector(VectorKey* keys, uint count, double time, Vector3 fallback)
    {
        if (keys is null || count == 0)
            return fallback;

        var chosen = keys[0];

        for (var index = 1; index < count; index++)
        {
            if (keys[index].MTime > time)
                break;

            chosen = keys[index];
        }

        return ToVector3(chosen.MValue);
    }

    private static Quaternion LastRotation(QuatKey* keys, uint count, double time)
    {
        if (keys is null || count == 0)
            return Quaternion.Identity;

        var chosen = keys[0];

        for (var index = 1; index < count; index++)
        {
            if (keys[index].MTime > time)
                break;

            chosen = keys[index];
        }

        var value = chosen.MValue;
        var rotation = new Quaternion(value.X, value.Y, value.Z, value.W);
        return rotation.LengthSquared() < 1e-8f ? Quaternion.Identity : Quaternion.Normalize(rotation);
    }

    private static Vector3 ToVector3(Vector3 value) => value;

    private static Matrix4x4 ToMatrix(Matrix4x4 value) => value;

    private static string NameOf(AssimpString value) =>
        value.Length == 0 ? string.Empty : Marshal.PtrToStringUTF8((nint)value.Data, (int)value.Length) ?? string.Empty;
}
