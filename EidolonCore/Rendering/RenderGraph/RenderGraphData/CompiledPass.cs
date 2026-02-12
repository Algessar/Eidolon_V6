namespace EidolonCore.Rendering;

public sealed record CompiledPass(
    int ExecutionIndex,
    int OriginalIndex, 
    string Name,
    RenderPassType Type, 
    IReadOnlyList<ResourceHandle> Reads,
    IReadOnlyList<ResourceHandle> Writes, 
    IReadOnlyList<int> Dependencies);
        
