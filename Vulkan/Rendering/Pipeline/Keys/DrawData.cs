using System.Numerics;
using EidolonCore.Resources;
using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal struct DrawData
{
    //NOTE: This definitely stays. 
    
    public PipelineData PipelineData;
    public IRenderTarget? RenderTarget;
    
    public DescriptorSet DescriptorSet;
    
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

    
    public void Dispose()
    {
        VertexBuffer.Dispose();
        IndexBuffer.Dispose();
    }
}

internal interface IRenderTarget
{
}

public struct DrawCommand
{
    public ResourceHandle MeshHandle;
    public ResourceHandle MaterialHandle;
    public Matrix4x4 ModelMatrix;
}
