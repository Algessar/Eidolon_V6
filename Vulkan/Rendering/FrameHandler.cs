using System.Numerics;
using System.Reflection.Metadata;
using EidolonCore.Rendering;
using ImGuiNET;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Eidolon.Vulkan.Rendering;

internal unsafe class FrameHandler : IFrameContext, IDisposable
{
    public VulkanMaster _master { get; }
    private SwapchainHandler _swapchainHandler;
    [Header("Resources")]
    private CommandBuffer[] _commandBuffer;
    private Framebuffer[] _framebuffers;

    private CompiledRenderGraph? _compiledGraph;
    private readonly GraphResourceImportMap _importMap = new();    
    private readonly Dictionary<uint, GraphImageRuntime> _graphImages = new();
    private readonly Dictionary<uint, CompiledResource> _resourceLookup  = new();
    
    private struct GraphImageRuntime
    {
        public Image Image;
        public ImageView View;
        public DeviceMemory Memory;
        public bool Imported;
        public Format Format;
        public Extent2D Extent;
        public FlagImageUsage Usage;
        public ImageLayout CurrentLayout;
    }


    private uint _maxFramesInFlight => Constants.MAX_FRAMES_IN_FLIGHT;

    [Header("Sync Objects")]
    private uint _imageCount;
    private Semaphore[] _waitSemaphore = Array.Empty<Semaphore>();
    private Semaphore[] _signalSemaphore = Array.Empty<Semaphore>();
    private Semaphore[] _imageAvailableSemaphores = Array.Empty<Semaphore>();
    private Semaphore[] _renderFinishedSemaphores = Array.Empty<Semaphore>();
    private Fence[] _inFlightFences = Array.Empty<Fence>();
    private Fence[] _imagesInFlight;
    
    private uint _currentFrame;
    private bool _frameActive;
    private uint _currentImageIndex;
    private bool _useSwapchainForFrame = true;
    
    private Dictionary<ulong, string> _semaphoreNames = new();
    private Dictionary<ulong, string> _fenceNames = new();

    
    
    public bool _framebufferResized { get; set; }

    bool LOG_RENDER_GRAPH = true;
    
    public FrameHandler(VulkanMaster master)
    {
        Debug.Log("Creating FrameHandler", VALIDATION_LAYERS.INFO);
        _master = master;
        _swapchainHandler = master.SwapchainHandler;
        _imageCount = _swapchainHandler.ImageCount;
        
        _commandBuffer = _master.CommandManager.AllocateCommandBuffers(_maxFramesInFlight);
        Initialize();

        _importMap = new GraphResourceImportMap();

        
        _master.GetWindow.FramebufferResize += OnWindowResize;
        Debug.Log("FrameHandler created.", VALIDATION_LAYERS.SUCCESS);
        
    }

    private void OnWindowResize(Vector2D<int> newSize)
    {
        // var id = ImGui.CreateContext();
        if (newSize.X == 0 || newSize.Y == 0)
            return;

        _framebufferResized = true;
	    
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(newSize.X, newSize.Y);
    }


    public void Initialize()
    {
        CreateSyncObjects();
        // CreateResources();
        _currentFrame = 0;
        _currentImageIndex = 0;

    }

    public void SetCompiledGraph(CompiledRenderGraph graph)
    {
        DestroyGraphResources();
        
        _compiledGraph = graph ?? throw new ArgumentNullException(nameof(graph));
        _resourceLookup.Clear();

        ConfigureImportedResourceMappings(graph);
    }

    private void ConfigureImportedResourceMappings(CompiledRenderGraph graph)
    {
        foreach (var resource in graph.Resources)
        {
            if (!resource.Imported)
            {
                continue;
            }

            if ((resource.Description.Usage & FlagImageUsage.Present) != 0)
            {
                _importMap.Register(resource.Handle, ImportedResourceKind.SwapchainColor);
                continue;
            }

            if ((resource.Description.Usage & FlagImageUsage.DepthStencilAttachment) != 0)
            {
                _importMap.Register(resource.Handle, ImportedResourceKind.SceneDepth);
            }
        }
    }

    public void Draw(in DrawData data)
    {
        if (_compiledGraph is null)
            throw new Exception("Draw called without compiled graph.");

        BeginFrame(data);
        if (!_frameActive)
            return;

        EnsureGraphResources(_compiledGraph);
        var cmd = _commandBuffer[_currentFrame];


        // Dynamic state must be set before drawing
        var viewport = new Viewport(
            0,
            0,
            _swapchainHandler.Extent.Width,
            _swapchainHandler.Extent.Height,
            0f,
            1f);

        _master.Vk.CmdSetViewport(cmd, 0, 1, &viewport);

        var scissor = new Rect2D(new Offset2D(0, 0), _swapchainHandler.Extent);
        _master.Vk.CmdSetScissor(cmd, 0, 1, &scissor);

        // Execute passes (already in execution order)
        foreach (var pass in _compiledGraph.Passes)
        {
            if (LOG_RENDER_GRAPH)
            {
                Debug.Log($"[RG] Pass {pass.ExecutionIndex}: {pass.Name} ({pass.Type})", VALIDATION_LAYERS.INFO, false);
                Debug.Log($"[RG]   Reads:  {string.Join(", ", pass.Reads.Select(r => r.Handle))}", VALIDATION_LAYERS.INFO, false);
                Debug.Log($"[RG]   Writes: {string.Join(", ", pass.Writes.Select(w => w.Handle))}", VALIDATION_LAYERS.INFO, false);
                Debug.Log($"[RG]   Deps:   {string.Join(", ", pass.Dependencies)}", VALIDATION_LAYERS.INFO, false);
            }

            var descriptorSet = _master.DescriptorFactory.GetDescriptorSet(_currentFrame);

            _master.Vk.CmdBindDescriptorSets(
                cmd,
                PipelineBindPoint.Graphics,
                data.PipelineData.VkLayout,
                0,
                1,
                &descriptorSet,
                0,
                null);

            // Your current system has no per-pass pipeline yet,
            // so we keep the existing behavior.
            if (data.PipelineData.IsValid)
            {
                _master.Vk.CmdBindPipeline(
                    cmd,
                    PipelineBindPoint.Graphics,
                    data.PipelineData.VkPipeline);

                // _master.Vk.CmdBindDescriptorSets(
                //     cmd,
                //     PipelineBindPoint.Graphics,
                //     data.PipelineData.VkLayout,
                //     0,
                //     1,
                //     in data.DescriptorSet,
                //     0,
                //     null);
                
                
                var modelMatrix = data.ModelMatrix ?? Matrix4x4.Identity;
                _master.Vk.CmdPushConstants(
                    cmd,
                    data.PipelineData.VkLayout,
                    ShaderStageFlags.VertexBit,
                    0,
                    (uint)sizeof(Matrix4x4),
                    &modelMatrix
                );

                _master.Vk.CmdDraw(cmd, 3, 1, 0, 0);
            }
        }

        EndFrame(data);
    }

    public void BeginFrame(in DrawData data)
    {
        if (_frameActive)
            throw new Exception("BeginFrame called while frame active.");

        var device = _master.VulkanDevice.Device;
        var vk = _master.Vk;
       
        
        // Wait for this frame to finish
        fixed (Fence* frameFence = &_inFlightFences[_currentFrame])
        {
            vk.WaitForFences(device, 1, frameFence, true, ulong.MaxValue);
        }
        
        if (_framebufferResized)
        {
            RecreateSwapchain(data.PipelineData);
            _frameActive = false;
            return;
        }

        // Acquire next image
        var acquireResult = _swapchainHandler.AcquireNextImage(
            _waitSemaphore[_currentFrame], 
            default,
            out uint imageIndex);

        if (acquireResult == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapchain(data.PipelineData);
            _frameActive = false;
            return;
        }

        if (acquireResult == Result.SuboptimalKhr)
        {
            _framebufferResized = true;
        }
        else if (acquireResult != Result.Success)
        {
            _frameActive = false;
            return;
        }
        
        // if (imageIndex >= _imagesInFlight.Length)
        // {
        //     throw new IndexOutOfRangeException(
        //         $"AcquireNextImage returned image index {imageIndex}, but images-in-flight size is {_imagesInFlight.Length}.");
        // }
        
        var imageFence = _imagesInFlight[imageIndex];
        if (imageFence.Handle != 0)
        {
            _master.Vk.WaitForFences(
                _master.VulkanDevice.Device,
                1,
                in imageFence,
                true,
                ulong.MaxValue);
        }
        
        _imagesInFlight[imageIndex] = _inFlightFences[_currentFrame];
   
        _currentImageIndex = imageIndex;
        
        
        fixed (Fence* frameFence = &_inFlightFences[_currentFrame])
        {
            vk.ResetFences(device, 1, frameFence);
        }

        var cmd = _commandBuffer[_currentFrame];
        var clearColor = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
        vk.ResetCommandBuffer(cmd, 0);

        //NOTE: RecordCommandBuffer calls Vk.BeginCommandBuffer
        _master.CommandManager.RecordCommandBuffer(
            cmd,
            _swapchainHandler.Framebuffers[_currentImageIndex],
            data.PipelineData.RenderPass,
            _swapchainHandler.Extent,
            clearColor,
            data.PipelineData.HasDepth
        );
        _frameActive = true;
    }

    public void EndFrame(in DrawData data)
    {
        
        if (!_frameActive)
        {
            return;
        }
        
        var cmd = _commandBuffer[_currentFrame];
        
        _master.Vk.CmdEndRenderPass(cmd);
       
        if (_master.Vk.EndCommandBuffer(cmd) != Result.Success)
            throw new Exception("Failed to end command buffer!");

        Semaphore waitSemaphore = _waitSemaphore[_currentFrame];
        Semaphore signalSemaphore = _signalSemaphore[_currentFrame];
        Fence frameFence = _inFlightFences[_currentFrame];
        
        _swapchainHandler.QueueSubmit(cmd, waitSemaphore, signalSemaphore, frameFence);
 
        // _swapchainHandler.Present(signalSemaphore, _currentImageIndex);

        var presentResult = _swapchainHandler.Present(signalSemaphore, _currentImageIndex);
        if (presentResult is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr || _framebufferResized)
        {
            RecreateSwapchain(data.PipelineData);
        }

        
        _currentFrame = (uint)((_currentFrame + 1) % _inFlightFences.Length);

        _frameActive = false;
    }

    public void CreateResources()
    {
        
        if (_compiledGraph is null)
            return;

        EnsureGraphResources(_compiledGraph);
        
        _commandBuffer = new CommandBuffer[_maxFramesInFlight];
        _waitSemaphore = new Semaphore[_maxFramesInFlight];
        _signalSemaphore = new Semaphore[_maxFramesInFlight];
        _inFlightFences = new Fence[_maxFramesInFlight];

        fixed (CommandBuffer* commandBufferPtr = _commandBuffer)
        {
            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _master.VulkanDevice.TransientCommandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = _maxFramesInFlight,
            };

            if (_master.Vk.AllocateCommandBuffers(_master.VulkanDevice.Device, in allocInfo, commandBufferPtr) != Result.Success)
            {
                throw new Exception("Failed to allocate frame command buffers.");
            }
        }
        
        for (var i = 0; i < _maxFramesInFlight; i++)
        {
            var semaphoreInfo = new SemaphoreCreateInfo
            {
                SType = StructureType.SemaphoreCreateInfo,
            };

            var fenceInfo = new FenceCreateInfo
            {
                SType = StructureType.FenceCreateInfo,
                Flags = FenceCreateFlags.SignaledBit,
            };

            if (_master.Vk.CreateSemaphore(_master.VulkanDevice.Device, in semaphoreInfo, null, out _waitSemaphore[i]) != Result.Success)
            {
                throw new Exception($"Failed to create wait semaphore {i}.");
            }

            if (_master.Vk.CreateSemaphore(_master.VulkanDevice.Device, in semaphoreInfo, null, out _signalSemaphore[i]) != Result.Success)
            {
                throw new Exception($"Failed to create signal semaphore {i}.");
            }

            if (_master.Vk.CreateFence(_master.VulkanDevice.Device, in fenceInfo, null, out _inFlightFences[i]) != Result.Success)
            {
                throw new Exception($"Failed to create in-flight fence {i}.");
            }
        }
    }

    private void EnsureGraphResources(CompiledRenderGraph graph)
    {
        foreach (var resource in graph.Resources)
        {
            if(_graphImages.ContainsKey(resource.Handle.Handle))
                continue;

            if (LOG_RENDER_GRAPH)
            {
                Debug.Log($"Creating runtime resource {resource.Name}"
                    + $"H:{resource.Handle.Handle}, Imported:{resource.Imported}" +
                    $"Use:{resource.FirstUsePass} -> {resource.LastUsePass}");
            }

            if (resource.Imported)
            {
                // Imported resources are owned externally (swapchain/depth targets).
                // We keep metadata for debugging/inspection but do not allocate/destroy Vulkan objects here.
                _graphImages[resource.Handle.Handle] = new GraphImageRuntime
                {
                    Imported = true,
                    Usage = resource.Description.Usage,
                    Format = ResolveVkFormat(resource.Description.Format),
                    Extent = ResolveGraphExtent(resource.Description),
                    CurrentLayout = ImageLayout.Undefined,
                };
                continue;
            }
            var runtime = CreateGraphImage(resource);
            _graphImages[resource.Handle.Handle] = runtime;
        }
    }

    private void CreateSyncObjects()
    {
        _inFlightFences = new Fence[_maxFramesInFlight];
        _imagesInFlight = new Fence[_imageCount];

        _waitSemaphore = new Semaphore[_maxFramesInFlight]; //NOTE: waitSemaphore
        _signalSemaphore = new Semaphore[_imageCount]; //NOTE: SignalSemaphore

        for (int i = 0; i < _maxFramesInFlight; i++)
        {
            _waitSemaphore[i] = CreateSemaphore($"WaitSemaphore {i}");
            _signalSemaphore[i] = CreateSemaphore($"SignalSemaphore {i}");
            
            Debug.Log($"Semaphore handles : {_waitSemaphore[i].Handle}", VALIDATION_LAYERS.INFO);
            Debug.Log($"Semaphore handles : {_signalSemaphore[i].Handle}", VALIDATION_LAYERS.INFO);
        }

        for (int i = 0; i < _maxFramesInFlight; i++)
        {
            _inFlightFences[i] = CreateFence($"InFlightFence {i}");

            Debug.Log($"In Flight Fences handles : {_inFlightFences[i].Handle}", VALIDATION_LAYERS.INFO);
        }
        
        Debug.Log($"Created {_imageCount} semaphores and {_maxFramesInFlight} fences", VALIDATION_LAYERS.SUCCESS);
    }

    private Semaphore CreateSemaphore(string name)
    {
        var semaphoreInfo = new SemaphoreCreateInfo
        {
            SType = StructureType.SemaphoreCreateInfo
        };
        if (_master.Vk.CreateSemaphore(_master.VulkanDevice.Device, in semaphoreInfo, null, out var semaphore) != Result.Success)
            throw new Exception("Failed to create semaphore!");
    
        _semaphoreNames[semaphore.Handle] = name;
		
        return semaphore;
    }

    private Fence CreateFence(string name)
    {
        var fenceInfo = new FenceCreateInfo
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit
        };
        
        if(_master.Vk.CreateFence(_master.VulkanDevice.Device, in fenceInfo, null, out var fence) != Result.Success)
            throw new Exception("Failed to create fence!");
        
        _fenceNames[fence.Handle] = name;

        return fence;
    }

    private void DestroyGraphResources()
    {
        foreach (var kv in _graphImages)
        {
            var rt = kv.Value;
            if (rt.Imported)
                continue;
            
            if (rt.View.Handle != 0)
            {
                _master.Vk.DestroyImageView(_master.VulkanDevice.Device, rt.View, null);
            }

            if (rt.Image.Handle != 0)
            {
                _master.Vk.DestroyImage(_master.VulkanDevice.Device, rt.Image, null);
            }

            if (rt.Memory.Handle != 0)
            {
                _master.Vk.FreeMemory(_master.VulkanDevice.Device, rt.Memory, null);
            }
        }
        
        _graphImages.Clear();
    }

    private GraphImageRuntime CreateGraphImage(in CompiledResource resource)
    {
        var format = ResolveVkFormat(resource.Description.Format);
        var usage = ResolveImageUsage(resource.Description.Usage);
        var extent = ResolveGraphExtent(resource.Description);

        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(extent.Width, extent.Height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = format,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = usage,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = SharingMode.Exclusive
        };

        if (_master.Vk.CreateImage(_master.VulkanDevice.Device, in imageInfo, null, out var image) != Result.Success)
        {
            throw new Exception($"Failed to create graph image for resource '{resource.Name}'.");
        }

        _master.Vk.GetImageMemoryRequirements(_master.VulkanDevice.Device, image, out var memRequirements);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = _master.VulkanDevice.FindMemoryType(memRequirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
        };

        if (_master.Vk.AllocateMemory(_master.VulkanDevice.Device, in allocInfo, null, out var memory) != Result.Success)
        {
            _master.Vk.DestroyImage(_master.VulkanDevice.Device, image, null);
            throw new Exception($"Failed to allocate graph image memory for resource '{resource.Name}'.");
        }

        if (_master.Vk.BindImageMemory(_master.VulkanDevice.Device, image, memory, 0) != Result.Success)
        {
            _master.Vk.FreeMemory(_master.VulkanDevice.Device, memory, null);
            _master.Vk.DestroyImage(_master.VulkanDevice.Device, image, null);
            throw new Exception($"Failed to bind graph image memory for resource '{resource.Name}'.");
        }

        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ResolveAspectFlags(resource.Description.Usage, resource.Description.Format),
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        if (_master.Vk.CreateImageView(_master.VulkanDevice.Device, in viewInfo, null, out var view) != Result.Success)
        {
            _master.Vk.FreeMemory(_master.VulkanDevice.Device, memory, null);
            _master.Vk.DestroyImage(_master.VulkanDevice.Device, image, null);
            throw new Exception($"Failed to create graph image view for resource '{resource.Name}'.");
        }

        Debug.Log($"[RG] Allocated runtime image '{resource.Name}' ({extent.Width}x{extent.Height}) format={format} usage={usage}");

        return new GraphImageRuntime
        {
            Image = image,
            View = view,
            Memory = memory,
            Imported = false,
            Format = format,
            Extent = extent,
            Usage = resource.Description.Usage,
            CurrentLayout = ImageLayout.Undefined,
        };
    
    }
    private Format ResolveVkFormat(ImageFormat format)
    {
        return format switch
        {
            ImageFormat.Bgra8Unorm => Format.B8G8R8A8Unorm,
            ImageFormat.Rgba16Float => Format.R16G16B16A16Sfloat,
            ImageFormat.D24UnormS8Uint => Format.D24UnormS8Uint,
            ImageFormat.D32Float => Format.D32Sfloat,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported render-graph image format."),
        };
        
    }
    private static ImageAspectFlags ResolveAspectFlags(FlagImageUsage usage, ImageFormat format)
    {
        if ((usage & FlagImageUsage.DepthStencilAttachment) != 0)
        {
            return format == ImageFormat.D24UnormS8Uint ? 
                ImageAspectFlags.DepthBit 
                | ImageAspectFlags.StencilBit
                : ImageAspectFlags.DepthBit;
        }
        return ImageAspectFlags.ColorBit;
    }

    private Extent2D ResolveGraphExtent(GraphImageDescription description)
    {
        var width = Math.Max(1u, (uint)MathF.Round(_swapchainHandler.Extent.Width * description.ScaleX));
        var height = Math.Max(1u, (uint)MathF.Round(_swapchainHandler.Extent.Height * description.ScaleY));
        return new Extent2D(width, height);
    }

    private static ImageUsageFlags ResolveImageUsage(FlagImageUsage usage)
    {
        ImageUsageFlags result = 0;

        if ((usage & FlagImageUsage.ColorAttachment) != 0)
            result |= ImageUsageFlags.ColorAttachmentBit;
        if ((usage & FlagImageUsage.DepthStencilAttachment) != 0)
            result |= ImageUsageFlags.DepthStencilAttachmentBit;
        if ((usage & FlagImageUsage.Sampled) != 0)
            result |= ImageUsageFlags.SampledBit;
        if ((usage & FlagImageUsage.Storage) != 0)
            result |= ImageUsageFlags.StorageBit;
        if ((usage & FlagImageUsage.TransferSource) != 0)
            result |= ImageUsageFlags.TransferSrcBit;
        if ((usage & FlagImageUsage.TransferDestination) != 0)
            result |= ImageUsageFlags.TransferDstBit;

        if (result == 0)
            result = ImageUsageFlags.SampledBit;

        return result;
    }

    private void RecreateSwapchain(in PipelineData pipelineData)
    {
        _framebufferResized = false;

        _swapchainHandler.RecreateSwapchain(pipelineData.RenderPass, pipelineData.HasDepth);
        _swapchainHandler = _master.SwapchainHandler;

        _imageCount = _swapchainHandler.ImageCount;
        _imagesInFlight = new Fence[_imageCount];

        RecreateRenderFinishedSemaphores();

        _currentImageIndex = 0;

        DestroyGraphResources();
    }
    
    private void RecreateRenderFinishedSemaphores()
    {
        foreach (var semaphore in _renderFinishedSemaphores)
        {
            if (semaphore.Handle != 0)
            {
                _master.Vk.DestroySemaphore(_master.VulkanDevice.Device, semaphore, null);
            }
        }

        _renderFinishedSemaphores = new Semaphore[_imageCount];
        for (var i = 0; i < _imageCount; i++)
        {
            _renderFinishedSemaphores[i] = CreateSemaphore($"RenderFinishedSemaphore {i}");
        }
    }

    public void Dispose()
    {
        DestroyGraphResources();
    }
}