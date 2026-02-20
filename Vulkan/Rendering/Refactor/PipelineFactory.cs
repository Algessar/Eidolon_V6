using Silk.NET.Vulkan;
// ai-ignore
//NOTE:  This is an attempt to:
// - Clarify build flow
// - Test and try to understand the syntactic sugar of `return this;`


namespace Eidolon.Vulkan.Refactor;

internal class PipelineFactory
{
    PipelineBuilder _pipelineBuilder;
    RenderPassBuilder _renderPassBuilder;
    DescriptorSetLayoutBuilder _descriptorSetLayoutBuilder;
    BufferBuilder _bufferBuilder;
    
    public PipelineFactory()
    {
        Debug.Log("Creating PipelineFactory", VALIDATION_LAYERS.WARNING);
        
        Debug.Log("PipelineFactory created!", VALIDATION_LAYERS.SUCCESS);
        var pipelineKey = new PipelineKey
        {
            VertexShaderPath = null,
            FragmentShaderPath = null,
            RenderPass = default,
            Topology = PrimitiveTopology.PointList
        };
        var data = Create(pipelineKey).HasDepth;
    }

    public PipelineData Create(PipelineKey key)
    {
        _pipelineBuilder.Build(key);
        
        return Create(key);
    }
}

internal class PipelineBuilder
{
    private PipelineFactory Factory;
    
    internal PipelineBuilder(PipelineFactory factory)
    {
        Factory = factory;
    }

    public PipelineBuilder Build( PipelineKey key)
    {
        Factory.Create(key);
        return this;
    }
}

internal class RenderPassBuilder
{
    
}

internal class DescriptorSetLayoutBuilder
{
    
}

internal class BufferBuilder
{
    
}