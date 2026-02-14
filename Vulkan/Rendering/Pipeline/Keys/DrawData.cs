using System.Numerics;
using EidolonCore.Rendering.Interfaces;
using EidolonCore.Resources;
using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

//INFO: Scene draw submission
internal struct DrawData
{
    public PipelineData PipelineData;

    public IRenderTarget? RenderTarget;

    public GpuBuffer VertexBuffer;
    public GpuBuffer IndexBuffer;
    public uint VertexCount;
    public uint IndexCount;
    public bool HasIndices;
    public IndexType IndexType;
    public Matrix4x4? ModelMatrix;
    public int[] VertexOffsets { get; set; }
    public int[] IndexOffsets { get; set; }
    
    public ImGuiDrawData ImGuiDrawData;
    public PipelineData UiPipelineData; //NOTE: this seems overly specific. There is already PipelineData. Why have a duplicate?
    public DescriptorSet UiDescriptorSet; //NOTE: This could also be generic no?
    //NOTE: I will keep it like this for now and refactor later.
    
}



