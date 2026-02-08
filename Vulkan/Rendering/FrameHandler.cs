using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Eidolon.Vulkan.Rendering;

internal class FrameHandler : IFrameContext
{
    public VulkanMaster _master { get; }
    
    [Header("Resources")]
    private CommandBuffer[] _commandBuffer;
    private Framebuffer[] _framebuffers;


    private uint _maxFramesInFlight => Constants.MAX_FRAMES_IN_FLIGHT;

    private uint _imageCount;
    private Semaphore[] _waitSemaphore;
    private Semaphore[] _signalSemaphore;
    
    private uint _currentFrame;
    
    public bool _framebufferResized { get; set; }

    public FrameHandler(VulkanMaster master)
    {
        _master = master;
    }
    
    public void Initialize()
    {
        throw new NotImplementedException();
    }

    public void Draw(in DrawData data)
    {
        
        //cmd
        var cmd = _commandBuffer[_currentFrame];
        
        //bind pipeline
        
        _master.Vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics,data.PipelineData.VkPipeline);
        
        //bind descriptors
        
        
        
        //bind buffers
        //push constants
        //draw
        
        throw new NotImplementedException();
    }

    public void BeginFrame(in DrawData data)
    {
        throw new NotImplementedException();
    }

    public void EndFrame(in DrawData data)
    {
        throw new NotImplementedException();
    }

    public void CreateResources()
    {
        throw new NotImplementedException();
    }
}