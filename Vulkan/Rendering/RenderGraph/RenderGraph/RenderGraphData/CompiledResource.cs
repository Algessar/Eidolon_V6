namespace Eidolon.Vulkan;

public sealed record CompiledResource(
    ResourceHandle Handle,
    GraphImageDescription Description,
    string Name,
    bool Imported,
    int FirstUsePass,
    int LastUsePass
);