using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Eidolon.Vulkan.Rendering;

//NOTE: This class seems to have been abandoned for unclear reasons.
internal unsafe class BufferFactory
{
    private VulkanMaster _master;
    
    private CommandPool _transientCommandPool;
    private GpuBuffer[] _uniformBuffers = Array.Empty<GpuBuffer>();


    public BufferFactory(VulkanMaster master)
    {
        _master = master;
        
        _uniformBuffers = CreateUniformGpuBuffers(Constants.MAX_FRAMES_IN_FLIGHT);
    }

    public GpuBuffer[] CreateUniformGpuBuffers(uint count)
    {
        var uniformGpuBuffers = new GpuBuffer[count];

        ulong bufferSize = (ulong)sizeof(VertexAttribute);

        for (int i = 0; i < count; i++)
        {
            uniformGpuBuffers[i] = CreateBuffer(
                bufferSize,
                BufferUsageFlags.UniformBufferBit, 
                MemoryPropertyFlags.HostVisibleBit 
                | MemoryPropertyFlags.HostCoherentBit
            );
            
            uniformGpuBuffers[i].HostVisible = true;
            
        }
        return uniformGpuBuffers;
    }
    
    private GpuBuffer CreateBuffer(ulong size, BufferUsageFlags usage, MemoryPropertyFlags memoryProperties)
    {
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };

        if (_master.Vk.CreateBuffer(_master.VulkanDevice.Device, &bufferInfo, null, out Buffer buffer) != Result.Success)
            throw new Exception("Failed to create buffer!");

        _master.Vk.GetBufferMemoryRequirements(_master.VulkanDevice.Device, buffer, out var memRequirements);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = _master.VulkanDevice.FindMemoryType(memRequirements.MemoryTypeBits, memoryProperties)
        };

        if (_master.Vk.AllocateMemory(_master.VulkanDevice.Device, &allocInfo, null, out DeviceMemory memory) !=
            Result.Success)
            throw new Exception("Failed to allocate buffer memory!");

        _master.Vk.BindBufferMemory(_master.VulkanDevice.Device, buffer, memory, 0);

        Debug.Log($"Created buffer :: {buffer.Handle} :: Memory {memory.Handle}");
        return new GpuBuffer
        {
            Buffer = buffer,
            Memory = memory,
            Size = size,
            Usage = usage,
            HostVisible = memoryProperties.HasFlag(MemoryPropertyFlags.HostVisibleBit)
        };
    }

    //SET FLAGS ETC
    public Framebuffer CreateFramebuffer()
    {
        return new Framebuffer();
    }
    
    
    public CommandBuffer BeginSingleTimeCommands()
    {
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
            CommandPool = _transientCommandPool
        };

        // Use the overload that returns a single buffer
        if (_master.Vk.AllocateCommandBuffers(_master.VulkanDevice.Device, in allocInfo, out CommandBuffer commandBuffer) != Result.Success)
            throw new Exception("Failed to allocate single-time command buffer!");

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };

        if (_master.Vk.BeginCommandBuffer(commandBuffer, ref beginInfo) != Result.Success)
            throw new Exception("Failed to begin single-time command buffer!");

        return commandBuffer;
    }
	
    public void EndSingleTimeCommands(CommandBuffer commandBuffer)
    {
        // End recording
        if (_master.Vk.EndCommandBuffer(commandBuffer) != Result.Success)
            throw new Exception("Failed to end single-time command buffer!");

        // Create array of 1 command buffer for submission
        CommandBuffer[] buffers = { commandBuffer };
        fixed (CommandBuffer* buffersPtr = buffers)
        {
            var submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = buffersPtr
            };

            if (_master.Vk.QueueSubmit(_master.VulkanDevice.GraphicsQueue, 1, in submitInfo, default) != Result.Success)
                throw new Exception("Failed to submit single-time command buffer!");
        }

        // Wait for completion
        _master.Vk.QueueWaitIdle(_master.VulkanDevice.GraphicsQueue);

        // Free the command buffer
        _master.Vk.FreeCommandBuffers(_master.VulkanDevice.Device, _transientCommandPool, 1, in commandBuffer);
    }

     
}