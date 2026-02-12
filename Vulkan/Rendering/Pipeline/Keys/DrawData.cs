using System.Numerics;
using EidolonCore.Resources;
using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal struct DrawData
{
    public PipelineData PipelineData;

    public IRenderTarget? RenderTarget;

    // I think this will be handled completely by DescriptorManager now. I think.
    // public DescriptorSet DescriptorSet;

    //NOTE: Do I want to keep this?
    // public DescriptorBinding DescriptorBinding;

    public GpuBuffer VertexBuffer;
    public GpuBuffer IndexBuffer;
    public uint VertexCount;
    public uint IndexCount;
    public bool HasIndices;
    public IndexType IndexType;
    public Matrix4x4? ModelMatrix;
    public int[] VertexOffsets { get; set; }
    public int[] IndexOffsets { get; set; }
}


internal interface IRenderTarget
{
}
