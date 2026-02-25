using System.Collections;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Eidolon.Vulkan;

internal struct GpuBuffer
{
    public Buffer Buffer;
    public DeviceMemory Memory;
    public ulong Size;
    public BufferUsageFlags Usage;
    public MemoryPropertyFlags MemoryFlags;
    public bool HostVisible;  // Can CPU write to it?
    

    public bool IsValid => Buffer.Handle != 0 && Memory.Handle != 0;
}