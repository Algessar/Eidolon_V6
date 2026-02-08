using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal unsafe class CommandManager : IDisposable
{
    // Central handling for all buffers: the only place where buffers of any kind are disposed?

    [Group("References")] private VulkanMaster _master;
    
    [Group("Resources")]
    public CommandPool CommandPool { get; set; }


    public CommandManager(VulkanMaster master)
    {
        Debug.Log("Creating CommandManager" , VALIDATION_LAYERS.WARNING);
        _master = master;
        Debug.Log("CommandManager Created.", VALIDATION_LAYERS.SUCCESS);
    }

    
    


    public void Dispose()
    {
        
    }
}