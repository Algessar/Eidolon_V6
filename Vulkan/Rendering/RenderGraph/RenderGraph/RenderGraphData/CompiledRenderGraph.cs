namespace Eidolon.Vulkan;

public sealed class CompiledRenderGraph
{
    public FrameDescription Frame;
    public IReadOnlyList<CompiledResource> Resources;
    public IReadOnlyList<CompiledPass> Passes;

    internal CompiledRenderGraph(FrameDescription frame, IReadOnlyList<CompiledResource> resources,
        IReadOnlyList<CompiledPass> passes)
    {
        Frame = frame;
        Resources = resources;
        Passes = passes;
    }
}