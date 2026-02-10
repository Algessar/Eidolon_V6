using System.Numerics;
using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal unsafe class CommandManager : IDisposable
{
    [Group("References")] private VulkanMaster _master;

    [Group("Resources")]
    private CommandPool _graphicsCommandPool;
    private CommandPool _transientCommandPool;


    public CommandManager(VulkanMaster master)
    {
        Debug.Log("Creating CommandManager" , VALIDATION_LAYERS.INFO);
        _master = master;
        
       _graphicsCommandPool = CreateGraphicsCommandPool();
       _transientCommandPool = CreateTransientCommandPool();
        Debug.Log("CommandManager Created.", VALIDATION_LAYERS.SUCCESS);
    }
    
    public Result RecordCommandBuffer(
        CommandBuffer cmd,
        Framebuffer framebuffer,
        RenderPass renderPass,
        Extent2D extent,
        Vector4 clearColor,
        bool hasDepth)
    {
        // 1. BEGIN COMMAND BUFFER
        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };
        
        var result = _master.Vk.BeginCommandBuffer(cmd, &beginInfo);
        if (result != Result.Success)
        {
            Debug.Log("Failed to begin command buffer!", VALIDATION_LAYERS.WARNING);
            return result;
        }

        // 2. SETUP CLEAR VALUES
        ClearValue[] clearValuesArray = hasDepth ? new ClearValue[2] : new ClearValue[1];
        
        // Color clear value
        clearValuesArray[0] = new ClearValue
        {
            Color = new ClearColorValue(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W)
        };
        
        // Depth clear value if needed
        if (hasDepth)
        {
            clearValuesArray[1] = new ClearValue
            {
                DepthStencil = new ClearDepthStencilValue(1.0f, 0)
            };
        }

        // 3. BEGIN RENDER PASS
        fixed (ClearValue* clearValuesPtr = clearValuesArray)
        {
            var renderPassInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = renderPass,
                Framebuffer = framebuffer,
                RenderArea = new Rect2D(new Offset2D(0, 0), extent),
                ClearValueCount = (uint)clearValuesArray.Length,
                PClearValues = clearValuesPtr
            };

            _master.Vk.CmdBeginRenderPass(cmd, &renderPassInfo, SubpassContents.Inline);
        }


        // 5. Debug logging
        Debug.Log($"Recorded command buffer:");
        Debug.Log($"  Framebuffer: {framebuffer.Handle:X}");
        Debug.Log($"  RenderPass: {renderPass.Handle:X}");
        Debug.Log($"  Extent: {extent.Width}x{extent.Height}");
        Debug.Log($"  HasDepth: {hasDepth}");
        Debug.Log($"  Clear values: {clearValuesArray.Length}");
        return result;

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
    private CommandPool CreateGraphicsCommandPool()
    {
        var indices = _master.VulkanDevice.FindQueueFamilies(_master.VulkanDevice.PhysicalDevice);

        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = indices.GraphicsFamily!.Value,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
        };
        if (_master.Vk.CreateCommandPool(_master.VulkanDevice.Device, in poolInfo, null, out var commandPool) != Result.Success)
        {
            throw new Exception("Failed to create command pool!");
        }

        return commandPool;
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