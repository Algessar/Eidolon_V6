namespace EidolonCore.Rendering;

public sealed record CompiledResource(
    ResourceHandle Handle,
    GraphImageDescription Description,
    string Name,
    bool Imported,
    int FirstUsePass,
    int LastUsePass
);