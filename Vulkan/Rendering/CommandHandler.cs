using System.Numerics;
using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal unsafe class CommandHandler : IDisposable
{
    [Group("References")] private VulkanMaster _master;

    [Group("Resources")]
    private CommandPool _graphicsCommandPool;
    private CommandPool _transientCommandPool;


    public CommandHandler(VulkanMaster master)
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
        
        // Colour clear value
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
        Debug.Log($"Recorded command buffer:"
                  + $"  Framebuffer: {framebuffer.Handle:X}" 
                  + $"  RenderPass: {renderPass.Handle:X}"
                  + $"  Extent: {extent.Width}x{extent.Height}"
                  + $"  HasDepth: {hasDepth}"
                  + $"  Clear values: {clearValuesArray.Length}", VALIDATION_LAYERS.INFO, false);
        return result;

    }

    //NOTE: Remember to check if this is ever used on next major cleanup pass.
    public void BeginRenderPass(CommandBuffer cmd, Framebuffer[] framebuffers, RenderPass renderPass, Extent2D extent, uint currentImageIndex, bool hasDepth)
    {
        var clearValuesArray = hasDepth ? new ClearValue[2] : new ClearValue[1];
        clearValuesArray[0] = new ClearValue
        {
            Color = new ClearColorValue(0.0f, 0.0f, 0.0f, 1.0f)
        };
    
        if (hasDepth)
        {
            clearValuesArray[1] = new ClearValue
            {
                DepthStencil = new ClearDepthStencilValue(1.0f, 0)
            };
        }
    
        fixed (ClearValue* clearValuesPtr = clearValuesArray)
        {
            var renderPassInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = renderPass,
                Framebuffer = framebuffers[currentImageIndex],
                RenderArea = new Rect2D(new Offset2D(0, 0), extent),
                ClearValueCount = (uint)clearValuesArray.Length,
                PClearValues = clearValuesPtr
            };
    
            _master.Vk.CmdBeginRenderPass(cmd, &renderPassInfo, SubpassContents.Inline);
        }
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
    
    
    public void EndSubmitAndFreeTransientCommandBuffer(CommandBuffer cmd)
    {
        if (_master.Vk.EndCommandBuffer(cmd) != Result.Success)
        {
            throw new Exception("Failed to end transient command buffer.");
        }

        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &cmd
        };

        if (_master.Vk.QueueSubmit(_master.VulkanDevice.GraphicsQueue, 1, &submitInfo, default) != Result.Success)
        {
            throw new Exception("Failed to submit transient command buffer.");
        }

        _master.Vk.QueueWaitIdle(_master.VulkanDevice.GraphicsQueue);
        _master.Vk.FreeCommandBuffers(_master.VulkanDevice.Device, _transientCommandPool, 1, &cmd);
    }
    public void TransitionImageLayout(CommandBuffer cmd, Image image, ImageLayout oldLayout, ImageLayout newLayout)
    {
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        PipelineStageFlags srcStage;
        PipelineStageFlags dstStage;

        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
        {
            barrier.SrcAccessMask = 0;
            barrier.DstAccessMask = AccessFlags.TransferWriteBit;
            srcStage = PipelineStageFlags.TopOfPipeBit;
            dstStage = PipelineStageFlags.TransferBit;
        }
        else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;
            srcStage = PipelineStageFlags.TransferBit;
            dstStage = PipelineStageFlags.FragmentShaderBit;
        }
        else
        {
            throw new Exception($"Unsupported image layout transition: {oldLayout} -> {newLayout}");
        }

        _master.Vk.CmdPipelineBarrier(cmd, srcStage, dstStage, 0, 0, null, 0, null, 1, &barrier);
    }
    
    //NOTE: This is a COMMANDBuffer, which means it actually belongs here -.-
    public void CopyBufferToImage(CommandBuffer cmd, Silk.NET.Vulkan.Buffer buffer, Image image, uint width, uint height)
    {
        var region = new BufferImageCopy
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = new Extent3D(width, height, 1)
        };

        _master.Vk.CmdCopyBufferToImage(cmd, buffer, image, ImageLayout.TransferDstOptimal, 1, &region);
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