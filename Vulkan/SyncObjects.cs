using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Eidolon.Vulkan;

internal unsafe class SyncObjects
{
    Fence[]       _imagesInFlight { get; set; }
    Fence[]       _framesInFlight { get; set; }
    Semaphore[]   _waitSemaphore { get; set; }
    Semaphore[]   _signalSemaphore { get; set; }
}