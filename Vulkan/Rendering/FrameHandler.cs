using System.Diagnostics.SymbolStore;
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
        
        Initialize();
        _commandBuffer = _master.CommandManager.AllocateCommandBuffers(_maxFramesInFlight);

        _importMap = new GraphResourceImportMap();

        
        _master.GetWindow.FramebufferResize += OnWindowResize;
        Debug.Log("FrameHandler created.", VALIDATION_LAYERS.SUCCESS);
        
    }

    #region Setup

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
        CreateResources();
        CreateSyncObjects();
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
            _resourceLookup[resource.Handle.Handle] = resource;
            
            if(!resource.Imported)
                continue;

            if ((resource.Description.Usage & FlagImageUsage.Present) != 0)
            {
                _importMap.Register(resource.Handle, ImportedResourceKind.SwapchainColor);
            }
            else if ((resource.Description.Usage & FlagImageUsage.DepthStencilAttachment) != 0)
            {            
                _importMap.Register(resource.Handle, ImportedResourceKind.SceneDepth);
            }
        }
    }

    #endregion Setup

    #region Drawing

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

            TransitionPassResources(pass, cmd);
            
            //NOTE: Codex wants BeginPassRenderPass here ... whatever that is ^^ 
            
            PreparePassResourceLayouts(pass);

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

            if (data.PipelineData.IsValid)
            {
                _master.Vk.CmdBindPipeline(
                    cmd,
                    PipelineBindPoint.Graphics,
                    data.PipelineData.VkPipeline);
                
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
       
        var cmd = _commandBuffer[_currentFrame];

        // Wait for this frame to finish
        fixed (Fence* frameFence = &_inFlightFences[_currentFrame])
        {
            vk.WaitForFences(device, 1, frameFence, true, ulong.MaxValue);
        }
        
        if (_framebufferResized && !RecreateSwapchain(data.PipelineData))
        {
            _frameActive = false;
            return;
        }

        // Acquire next image
        
        var acquireResult = _swapchainHandler.AcquireNextImage(
            _waitSemaphore[_currentFrame], 
            default,
            out uint imageIndex);

        switch (acquireResult)
        {
            case Result.ErrorOutOfDateKhr:
                RecreateSwapchain(data.PipelineData);
                _frameActive = false;
                return;
            case Result.SuboptimalKhr:
                _framebufferResized = true;
                break;
            default:
            {
                if (acquireResult != Result.Success)
                {
                    _frameActive = false;
                    return;
                }
                break;
            }
        }

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
        Semaphore signalSemaphore = _signalSemaphore[_currentImageIndex];
        Fence frameFence = _inFlightFences[_currentFrame];
        
        _swapchainHandler.QueueSubmit(cmd, waitSemaphore, signalSemaphore, frameFence);
 

        var presentResult = _swapchainHandler.Present(signalSemaphore, _currentImageIndex);
        if (presentResult == Result.ErrorOutOfDateKhr || presentResult == Result.SuboptimalKhr || _framebufferResized)
        {
            RecreateSwapchain(data.PipelineData);
        }
        
        _currentFrame = (uint)((_currentFrame + 1) % _inFlightFences.Length);

        _frameActive = false;
    }


    #endregion

    #region Creation
    private bool RecreateSwapchain(in PipelineData pipelineData)
    {
        if (!_swapchainHandler.RecreateSwapchain(pipelineData.RenderPass, pipelineData.HasDepth))
        {
            _framebufferResized = true;
            return false;
        }

        _framebufferResized = false;

        _swapchainHandler = _master.SwapchainHandler;

        _imageCount = _swapchainHandler.ImageCount;
        _imagesInFlight = new Fence[_imageCount];

        RecreateSignalSemaphores();

        _currentImageIndex = 0;

        DestroyGraphResources();
        return true;
    }
    
    private void RecreateSignalSemaphores()
    {
        foreach (var semaphore in _signalSemaphore)
        {
            if (semaphore.Handle != 0)
            {
                _master.Vk.DestroySemaphore(_master.VulkanDevice.Device, semaphore, null);
            }
        }

        _signalSemaphore = new Semaphore[_imageCount];
        for (var i = 0; i < _imageCount; i++)
        {
            _signalSemaphore[i] = CreateSemaphore($"SignalSemaphore {i}");
        }
    }
    public void CreateResources()
    {
        
        if (_compiledGraph is null)
            return;

        EnsureGraphResources(_compiledGraph);
        
        _commandBuffer = new CommandBuffer[_maxFramesInFlight];

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
    }
    
    private void CreateSyncObjects()
    {
        //INFO: wait semaphores and in flight fences must be the same size as MaxFramesInFlight.
        _inFlightFences = new Fence[_maxFramesInFlight];
        _waitSemaphore = new Semaphore[_maxFramesInFlight];

        //INFO: Signal semaphores and images in flight must be the same size as swapchain images.
        // 
        _imagesInFlight = new Fence[_imageCount];
        _signalSemaphore = new Semaphore[_imageCount];

        for (int i = 0; i < _maxFramesInFlight; i++)
        {
            _waitSemaphore[i] = CreateSemaphore($"WaitSemaphore {i}");
            
            Debug.Log($"Semaphore handles : {_waitSemaphore[i].Handle}", VALIDATION_LAYERS.INFO);
            _inFlightFences[i] = CreateFence($"InFlightFence {i}");
            Debug.Log($"In Flight Fences handles : {_inFlightFences[i].Handle}", VALIDATION_LAYERS.INFO);
        }

        for (int i = 0; i < _imageCount; i++)
        {
            _signalSemaphore[i] = CreateSemaphore($"SignalFinishedSemaphore {i}");

            Debug.Log($"Signal semaphore handles : {_signalSemaphore[i].Handle}", VALIDATION_LAYERS.INFO);

        }

        if (_signalSemaphore.Length < _imageCount)
        {
            throw new Exception("Not enough signal semaphores for swapchain images.");
        }

        if (_inFlightFences.Length < _maxFramesInFlight)
        {
            throw new Exception("Not enough in flight fences for in-flight frames.");
        }
        
        Debug.Log($"Created {_signalSemaphore.Length} signal semaphores, {_waitSemaphore.Length} wait semaphores and  {_inFlightFences.Length} in flight fences", VALIDATION_LAYERS.SUCCESS);
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

    private void PreparePassResourceLayouts(in CompiledPass pass)
    {
        // Reads first, then writes. This keeps intent explicit while we still use a single render pass.
        foreach (var read in pass.Reads)
        {
            TrackResourceLayout(read, isWrite: false);
        }

        foreach (var write in pass.Writes)
        {
            TrackResourceLayout(write, isWrite: true);
        }
    }

    #endregion Creation
    
    #region Transitions

    private void TransitionPassResources(CompiledPass pass, CommandBuffer cmd)
    {
        foreach (var read in pass.Reads)
        {
            TransitionResource(read, isWrite: false, cmd);
        }

        foreach (var write in pass.Writes)
        {
            TransitionResource(write, isWrite: false, cmd);
        }
    }

    private void TransitionResource(in ResourceHandle handle, bool isWrite, CommandBuffer cmd)
    {
        if (!_resourceLookup.TryGetValue(handle.Handle, out var resource))
        {
            Debug.Log($"Failed to find resource '{handle.Handle}' in compiled graph.", VALIDATION_LAYERS.WARNING);
            return;
        }

        if (!_graphImages.TryGetValue(handle.Handle, out var runtime))
        {
            Debug.Log($"Failed to find runtime image for resource '{handle.Handle}' in compiled graph.", VALIDATION_LAYERS.WARNING);
            return;
        }

        if (runtime.Imported)
        {
            return;
        }
        
        var expectedLayout = ResolveExpectedLayout(resource.Description.Usage, isWrite);
        if (runtime.CurrentLayout == expectedLayout)
        {
            return;
        }

        var srcStage = ResolvePipelineStage(runtime.CurrentLayout);
        var dstStage = ResolvePipelineStage(expectedLayout);
        var srcAccess = ResolveAccessMask(expectedLayout);
        var dstAccess = ResolveAccessMask(runtime.CurrentLayout);

        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = srcAccess,
            DstAccessMask = dstAccess,
            OldLayout = runtime.CurrentLayout,
            NewLayout = expectedLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = runtime.Image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ResolveAspectFlags(resource.Description.Usage, resource.Description.Format),
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };
        
        _master.Vk.CmdPipelineBarrier(
            cmd, 
            srcStage,
            dstStage, 
            0, 
            0, 
            null, 
            0, 
            null, 
            1, 
            &barrier
        );

        if (LOG_RENDER_GRAPH)
        {
            Debug.Log($"[RG] Barrier: {resource.Name} {runtime.CurrentLayout} -> {expectedLayout}");
        }
        
        runtime.CurrentLayout = expectedLayout;
        _graphImages[handle.Handle] = runtime;

    }

    #endregion Transitions
 
    #region Resolve
    private static AccessFlags ResolveAccessMask(ImageLayout layout)
    {
        return layout switch
        {
            ImageLayout.ColorAttachmentOptimal => AccessFlags.ColorAttachmentWriteBit | AccessFlags.ColorAttachmentReadBit,
            ImageLayout.DepthStencilAttachmentOptimal => AccessFlags.DepthStencilAttachmentWriteBit | AccessFlags.DepthStencilAttachmentReadBit,
            ImageLayout.ShaderReadOnlyOptimal => AccessFlags.ShaderReadBit,
            ImageLayout.PresentSrcKhr => 0,
            ImageLayout.Undefined => 0,
            _ => AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit,
        };
    }

    private static PipelineStageFlags ResolvePipelineStage(ImageLayout layout)
    {
        return layout switch
        {
            ImageLayout.ColorAttachmentOptimal => PipelineStageFlags.ColorAttachmentOutputBit,
            ImageLayout.DepthStencilAttachmentOptimal => PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit,
            ImageLayout.ShaderReadOnlyOptimal => PipelineStageFlags.FragmentShaderBit,
            ImageLayout.PresentSrcKhr => PipelineStageFlags.BottomOfPipeBit,
            ImageLayout.Undefined => PipelineStageFlags.TopOfPipeBit,
            _ => PipelineStageFlags.AllCommandsBit,
        };
    }
    
    private Extent2D ResolveGraphExtent(GraphImageDescription description)
    {
        var width = Math.Max(1u, (uint)MathF.Round(_swapchainHandler.Extent.Width * description.ScaleX));
        var height = Math.Max(1u, (uint)MathF.Round(_swapchainHandler.Extent.Height * description.ScaleY));
        return new Extent2D(width, height);
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
    
    #endregion Resolve
    
    private void TrackResourceLayout(in ResourceHandle handle, bool isWrite)
    {
        if (!_resourceLookup.TryGetValue(handle.Handle, out var resource))
        {
            return;
        }

        if (!_graphImages.TryGetValue(handle.Handle, out var runtime))
        {
            return;
        }

        var expectedLayout = ResolveExpectedLayout(resource.Description.Usage, isWrite);
        if (runtime.CurrentLayout == expectedLayout)
        {
            return;
        }

        //NOTE: We intentionally don't emit vkCmdPipelineBarrier yet because command recording is
        // still done as one active render pass scope; this first step tracks and validates intended
        // layout flow so we can move barrier emission out of render-pass scope next.
        if (LOG_RENDER_GRAPH)
        {
            Debug.Log($"[RG] Layout transition planned: {resource.Name} {runtime.CurrentLayout} -> {expectedLayout}");
        }

        runtime.CurrentLayout = expectedLayout;
        _graphImages[handle.Handle] = runtime;
    }

    private static ImageLayout ResolveExpectedLayout(FlagImageUsage usage, bool isWrite)
    {
        if ((usage & FlagImageUsage.DepthStencilAttachment) != 0)
        {
            return ImageLayout.DepthStencilAttachmentOptimal;
        }

        if (isWrite)
        {
            if ((usage & FlagImageUsage.ColorAttachment) != 0 || (usage & FlagImageUsage.Present) != 0)
            {
                return ImageLayout.ColorAttachmentOptimal;
            }
        }
        else
        {
            if ((usage & FlagImageUsage.Sampled) != 0)
            {
                return ImageLayout.ShaderReadOnlyOptimal;
            }

            if ((usage & FlagImageUsage.Present) != 0)
            {
                return ImageLayout.PresentSrcKhr;
            }
        }

        return ImageLayout.General;
    }

    #region Disposal

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
    public void Dispose()
    {
        DestroyGraphResources();
    }
    #endregion
}