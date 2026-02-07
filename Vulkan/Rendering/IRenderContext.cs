using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Eidolon.Vulkan;

internal interface IRenderContext: IDisposable
{
    uint MAX_FRAMES_IN_FLIGHT { get; }

    #region References
    VulkanManager VulkanManager { get; }
    

    #endregion

    #region Resources
    CommandBuffer CommandBuffer { get; }
    
    //NOTE: Move to SyncObjects?
    Fence[]       _imagesInFlight { get; set; }
    Fence[]       _framesInFlight { get; set; }
    Semaphore[]   _waitSemaphore { get; set; }
    Semaphore[]   _signalSemaphore { get; set; }

    bool _framebufferResized  { get; set; }

    #endregion

    #region DrawRegion
    
    void Initialize();

    //NOTE: THIS STRUCTURE MAY CHANGE
    void Draw(in DrawData data);
        
    void BeginFrame(in DrawData data);
        
    void EndFrame(in DrawData data);


    #endregion

    #region ResourceCreation

    void CreateResources(); //Calls from managers!
    private void AcquireNextImageDebug(
        out uint imageIndex, 
        Semaphore semaphore, Fence fence, 
        bool shouldLog = true)
    {
        throw new NotImplementedException();
    }

    private void LogSemaphoreState(
        string context, uint frame,
        uint imageIndex, Semaphore wait,
        Semaphore signal, Fence fence)
    {
        
    }
    #endregion
}

internal struct DrawData()
{
    
}