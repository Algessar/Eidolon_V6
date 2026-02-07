using System.Collections;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Eidolon.Vulkan;

internal struct GpuBuffer : IDisposable
{
    public Buffer Buffer;
    public DeviceMemory Memory;
    public ulong Size;
    public BufferUsageFlags Usage;
    public MemoryPropertyFlags MemoryFlags;
    public bool HostVisible;  // Can CPU write to it?
    
    /// <summary>
    /// Checks if the buffer is valid (has non-zero handle).
    /// </summary>
    public bool IsValid => Buffer.Handle != 0 && Memory.Handle != 0;
    
    /// <summary>
    /// Marks the buffer as disposed. BufferManager should do actual clean-up.
    /// </summary>
    public void Dispose()
    {
        if (Buffer.Handle == 0 && Memory.Handle == 0)
            return;
    
        Debug.Log($"Disposing GPU Buffer {Buffer.Handle}");
    
        // Note: BufferManager should handle actual destruction
        // Just mark as invalid
        Buffer = default;
        Memory = default;
        Size = 0;
        HostVisible = false;

        Debug.Log($"Disposed GPU Buffer :: IsValid -> {IsValid}");
    }


    // public void Dispose()
    // {
    //     // TODO release managed resources here
    // }
}