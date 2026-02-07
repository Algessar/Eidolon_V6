using System.Numerics;
using EidolonCore.Resources;

namespace EidolonCore.Rendering;

public struct RenderBatch
{
    public ResourceHandle MeshHandle;
    public ResourceHandle MaterialHandle;
    public Matrix4x4 Transform;
    public BoundingBox Bounds;
}