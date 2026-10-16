using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal enum CullModeBits
{
    None,
    Back,
    Front
}

internal record struct PipelineKey
{
    public required string VertexShaderPath { get; init; }
    public required string FragmentShaderPath { get; init; }
    public required RenderPass RenderPass { get; init; }
    public ShaderModule Vert { get; set; }
    public ShaderModule Frag { get; set; }
    public DescriptorSetLayout Layout { get; init; }
    public VertexFormat VertexFormat { get; init; }
    public required PrimitiveTopology Topology { get; init; }
    public CullModeBits CullMode  { get; init; } = CullModeBits.Back;
    
    // Depth test, Pipeline

    public bool DepthTestEnable { get; init; }
    public bool DepthWriteEnable { get; init; }
    public bool DepthCompareOp  { get; init; }
    public FrontFace FrontFace { get; init; }

    public bool EnableBlending;
    public BlendState BlendState;

    public bool HasDepth;
    
    public PipelineKey()
    {
        EnableBlending = false;
        BlendState = BlendState.NoBlending;
        RenderPass = default;
        VertexShaderPath = null;
        FragmentShaderPath = null;
        VertexFormat = default;
        Topology = PrimitiveTopology.PointList;
        DepthTestEnable = false;
        DepthWriteEnable = false;
        DepthCompareOp = false;
    }

}

internal record struct BlendState
{
    // Color blending
    public BlendFactor SrcColorBlendFactor;
    public BlendFactor DstColorBlendFactor;
    public BlendOp ColorBlendOp;
    
    // Alpha blending (can be same or different)
    public BlendFactor SrcAlphaBlendFactor;
    public BlendFactor DstAlphaBlendFactor;
    public BlendOp AlphaBlendOp;
    
    // Optional: color write mask
    public ColorComponentFlags ColorWriteMask;
    
    // Default blend state (standard alpha blending)
    public static BlendState AlphaBlending => new BlendState
    {
        SrcColorBlendFactor = BlendFactor.SrcAlpha,
        DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
        ColorBlendOp = BlendOp.Add,
        SrcAlphaBlendFactor = BlendFactor.One,
        DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
        AlphaBlendOp = BlendOp.Add,
        ColorWriteMask = ColorComponentFlags.RBit | 
                         ColorComponentFlags.GBit | 
                         ColorComponentFlags.BBit | 
                         ColorComponentFlags.ABit
    };
    
    // No blending (opaque)
    public static BlendState NoBlending => new BlendState
    {
        SrcColorBlendFactor = BlendFactor.One,
        DstColorBlendFactor = BlendFactor.Zero,
        ColorBlendOp = BlendOp.Add,
        SrcAlphaBlendFactor = BlendFactor.One,
        DstAlphaBlendFactor = BlendFactor.Zero,
        AlphaBlendOp = BlendOp.Add,
        ColorWriteMask = ColorComponentFlags.RBit | 
                         ColorComponentFlags.GBit | 
                         ColorComponentFlags.BBit | 
                         ColorComponentFlags.ABit
    };
    
    // Additive blending (for particles, glows)
    public static BlendState Additive => new BlendState
    {
        SrcColorBlendFactor = BlendFactor.SrcAlpha,
        DstColorBlendFactor = BlendFactor.One,
        ColorBlendOp = BlendOp.Add,
        SrcAlphaBlendFactor = BlendFactor.One,
        DstAlphaBlendFactor = BlendFactor.One,
        AlphaBlendOp = BlendOp.Add,
        ColorWriteMask = ColorComponentFlags.RBit | 
                         ColorComponentFlags.GBit | 
                         ColorComponentFlags.BBit | 
                         ColorComponentFlags.ABit
    };
}