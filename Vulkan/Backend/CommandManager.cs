using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal unsafe class CommandManager : IDisposable
{
    // Central handling for all buffers: the only place where buffers of any kind are disposed?

    [Group("References")] private VulkanMaster _master;

    [Group("Resources")]
    public CommandPool CommandPool { get; set; }
    private CommandPool _graphicsCommandPool;
    private CommandPool _transientCommandPool;


    public CommandManager(VulkanMaster master)
    {
        Debug.Log("Creating CommandManager" , VALIDATION_LAYERS.INFO);
        _master = master;
        Debug.Log("CommandManager Created.", VALIDATION_LAYERS.SUCCESS);
    }
    
    
    public CommandBuffer[] AllocateCommandBuffers(uint count, CommandBufferLevel level = CommandBufferLevel.Primary)
    {
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _graphicsCommandPool,
            Level = level,
            CommandBufferCount = count
        };

        var buffers = new CommandBuffer[count];
        fixed (CommandBuffer* buffersPtr = buffers)
        {
            if (_master.Vk.AllocateCommandBuffers(_master.VulkanDevice.Device, in allocInfo, buffersPtr) != Result.Success)
                throw new Exception("Failed to allocate command buffers.");
        }

        return buffers;
    }

    
    public CommandBuffer AllocateTransientCommandBuffer()
    {
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _transientCommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };

        CommandBuffer cmd;
        _master.Vk.AllocateCommandBuffers(
            _master.VulkanDevice.Device,
            in allocInfo,
            &cmd
        );

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };

        _master.Vk.BeginCommandBuffer(cmd, &beginInfo);

        return cmd;
    }

    private CommandPool CreateTransientCommandPool()
    {
        var indices = _master.VulkanDevice.FindQueueFamilies(_master.VulkanDevice.PhysicalDevice);

        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = indices.GraphicsFamily!.Value,
            Flags =
                CommandPoolCreateFlags.TransientBit |
                CommandPoolCreateFlags.ResetCommandBufferBit
        };

        if (_master.Vk.CreateCommandPool(
                _master.VulkanDevice.Device,
                in poolInfo,
                null,
                out var pool) != Result.Success)
        {
            throw new Exception("Failed to create transient command pool!");
        }

        return pool;
    }
    public void Dispose()
    {
        
    }
}