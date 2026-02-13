
using System.Numerics;
using EidolonCore.Rendering;
using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal sealed class ImGuiRenderer : IDisposable
{
    // Lifetime (created once)
    private PipelineData _pipeline;
    private DescriptorSet _fontSet;
    private GpuImage _fontImage; // From ImageData?

    // Per-frame CPU state
    private DrawData _drawData; // Holds Vertex/IndexBuffers


    public void NewFrame(float delta, Vector2 size)
    {
        
    }

    public void BuildUI()
    {
        
    }

    public void Upload()
    {
        
    }

    public void AddToGraph(RenderGraphBuilder builder, ResourceHandle target)
    {
        
    }

    public void Dispose()
    {
        
    }
}

internal struct ImGuiDrawData
{
    public GpuBuffer VertexBuffer;
    public GpuBuffer IndexBuffer;
    
    public uint VertexCount;
    public uint IndexCount;
    
    public bool HasIndices;
    
    
}

