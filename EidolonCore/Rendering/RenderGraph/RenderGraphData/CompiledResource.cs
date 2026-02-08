namespace EidolonCore.Rendering;

public sealed record CompiledResource(
    ResourceHandle Handle,
    string Name,
    GraphImageDescription Description,
    bool Imported,
    int FirstUsePass,
    int LastUsePass
);