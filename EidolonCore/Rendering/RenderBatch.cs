using System.Numerics;
using EidolonCore.ECS;

namespace EidolonCore.Rendering;

public struct RenderBatch
{
    public ResourceHandle MeshHandle;
    public ResourceHandle MaterialHandle;
    public Matrix4x4 Transform;
    public BoundingBox Bounds;
}