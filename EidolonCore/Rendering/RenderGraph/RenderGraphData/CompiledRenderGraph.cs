namespace EidolonCore.Rendering;

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


public sealed record CompiledPass(
    int ExecutionIndex,
    int OriginalIndex, 
    string Name,
    RenderPassType Type, 
    IReadOnlyList<ResourceHandle> Reads,
    IReadOnlyList<ResourceHandle> Writes, 
    IReadOnlyList<int> Dependencies);
    
public sealed record CompiledResource(
    ResourceHandle Handle,
    string Name,
    GraphImageDescription Description,
    bool Imported,
    int FirstUsePass,
    int LastUsePass
);