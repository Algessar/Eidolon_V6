using System.Numerics;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Eidolon.Vulkan;

//TODO: Resizing the window creates black bands. Put on hold for now, but should be fixed eventually.

internal unsafe class FrameHandler : IDisposable
{
    public event Action OnSwapchainRecreated;
    
    public VulkanMaster _master { get; }
    private SwapchainHandler _swapchainHandler;

    private readonly PassExecutionHandler _passExecutionHandler;
    private readonly GraphResourceRuntimeManager _graphResourceRuntimeManager;
    private readonly GraphBarrierHandler _graphBarrierHandler;

    [Header("Resources")] private CommandBuffer[] _commandBuffer;

    private CompiledRenderGraph? _compiledGraph;
    private readonly GraphResourceImportMap _importMap = new();

    private uint _maxFramesInFlight => Constants.MAX_FRAMES_IN_FLIGHT;

    [Header("Sync Objects")] private uint _imageCount;
    private Semaphore[] _waitSemaphore = Array.Empty<Semaphore>();
    private Semaphore[] _signalSemaphore = Array.Empty<Semaphore>();
    private Fence[] _inFlightFences = Array.Empty<Fence>();
    private Fence[] _imagesInFlight;

    private uint _currentFrame;

    private bool _frameActive;

    public bool FrameActive => _frameActive;

    private uint _currentImageIndex;
    public uint CurrentFrame => _currentFrame;

    //NOTE: Only used for debug logging rn?
    private Dictionary<ulong, string> _semaphoreNames = new();
    private Dictionary<ulong, string> _fenceNames = new();

    public bool _framebufferResized { get; set; }
    private const bool LOG_RENDER_GRAPH = false;
    private const bool DEBUG = false;

    public FrameHandler(VulkanMaster master)
    {
        Debug.Log("Creating FrameHandler", VALIDATION_LAYERS.INFO);
        _master = master;
        _swapchainHandler = master.SwapchainHandler;
        _imageCount = _swapchainHandler.ImageCount;
        _passExecutionHandler = new PassExecutionHandler(master, ResolvePassAttachment);
        _graphResourceRuntimeManager = new GraphResourceRuntimeManager(master);
        _graphBarrierHandler = new GraphBarrierHandler(master, _graphResourceRuntimeManager, LOG_RENDER_GRAPH);

        Initialize();
        _commandBuffer = _master.CommandHandler.AllocateCommandBuffers(_maxFramesInFlight);

        _master.GetWindow.FramebufferResize += OnWindowResize;
        
        
        Debug.Log("FrameHandler created.", VALIDATION_LAYERS.SUCCESS);
    }

    private void OnWindowResize(Vector2D<int> newSize)
    {
        _framebufferResized = true;
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
        _graphResourceRuntimeManager.DestroyGraphResources();
        _passExecutionHandler.Reset();

        _compiledGraph = graph ?? throw new ArgumentNullException(nameof(graph));
        _graphResourceRuntimeManager.ClearResourceLookup();

        ConfigureImportedResourceMappings(graph);
    }

    private void ConfigureImportedResourceMappings(CompiledRenderGraph graph)
    {
        foreach (var resource in graph.Resources)
        {
            _graphResourceRuntimeManager.RegisterResource(resource);

            if (!resource.Imported)
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

    #region Drawing

    public void Draw(in DrawData data)
    {
        if (DEBUG)
        {
            // Debug.Log("Running Draw()", VALIDATION_LAYERS.INFO);
        }
        if (_compiledGraph is null)
            throw new Exception("Draw called without compiled graph.");

        BeginFrame();
        if (!_frameActive)
            return;

        ExecutePasses(data);
        EndFrame();
    }
    
    private void ExecutePasses(in DrawData data)
    {
        if(DEBUG)
        {
            Debug.Log("ExecutePasses entered", VALIDATION_LAYERS.INFO);
            Debug.Log($"Swapchain Extent: {_swapchainHandler.Extent.Width}x{_swapchainHandler.Extent.Height}");
        }        
        
        if (_compiledGraph is null)
            throw new Exception("Draw called without compiled graph.");

        _graphResourceRuntimeManager.EnsureGraphResources(_compiledGraph, _swapchainHandler, LOG_RENDER_GRAPH);

        var cmd = _commandBuffer[_currentFrame];

        // var passViewport = new Viewport(0, 0, _swapchainHandler.Extent.Width, _swapchainHandler.Extent.Height, 0f, 1f);
        // var passScissor = new Rect2D(new Offset2D(0, 0), _swapchainHandler.Extent);
        //
        // _master.Vk.CmdSetViewport(cmd, 0, 1, &passViewport);
        // _master.Vk.CmdSetScissor(cmd, 0, 1, &passScissor);

        var submissions = data.Submissions; // ?? Array.Empty<DrawSubmission>(); // NOTE: Rider warns that left operand is never null.

        
        foreach (var pass in _compiledGraph.Passes)
        {
            if (_currentFrame == 0 && DEBUG)
            {
                Debug.Log($"[RG] Pass {pass.ExecutionIndex}: {pass.Name} ({pass.Type})", VALIDATION_LAYERS.INFO,
                    LOG_RENDER_GRAPH);
                Debug.Log($"[RG]   Reads:  {string.Join(", ", pass.Reads.Select(r => r.Handle))}",
                    VALIDATION_LAYERS.INFO, LOG_RENDER_GRAPH);
                Debug.Log($"[RG]   Writes: {string.Join(", ", pass.Writes.Select(w => w.Handle))}",
                    VALIDATION_LAYERS.INFO, LOG_RENDER_GRAPH);
                Debug.Log($"[RG]   Deps:   {string.Join(", ", pass.Dependencies)}", VALIDATION_LAYERS.INFO,
                    LOG_RENDER_GRAPH);
            }



            _graphBarrierHandler.TransitionLayouts(cmd, pass);
            if (pass.Type == RenderPassType.Present)
            {
                // Note: this looks unnecessary tbh
                // Transition to present layout (already done in TransitionLayouts)
                // Do NOT begin a render pass.
                continue; // skip the render pass block
            }

            var execution = _passExecutionHandler.GetOrCreate(pass, _compiledGraph, _currentImageIndex);

            // Set viewport and scissor to match this pass's framebuffer
            var passViewport = new Viewport(0, 0, execution.Extent.Width, execution.Extent.Height, 0f, 1f);
            var passScissor = new Rect2D(new Offset2D(0, 0), execution.Extent);
            _master.Vk.CmdSetViewport(cmd, 0, 1, &passViewport);
            _master.Vk.CmdSetScissor(cmd, 0, 1, &passScissor);
            if (LOG_RENDER_GRAPH)
            {
                Debug.Log($"Pass {pass.Name} render area: {execution.Extent.Width}x{execution.Extent.Height}");
            }

            var clearValues = stackalloc ClearValue[2]; //NOTE: CA2014: Potential stack overflow. Move the stackalloc out of the loop.

            clearValues[0] = execution.ClearColor
                ? new ClearValue { Color = new ClearColorValue(1f, 0f, 0f, 0f) }
                : new ClearValue();

            uint clearValueCount = 1;

            if (execution.HasDepth)
            {
                clearValues[1] = execution.ClearDepth
                    ? new ClearValue { DepthStencil = new ClearDepthStencilValue(1.0f, 0) }
                    : new ClearValue();
                clearValueCount = 2;
            }

            var beginInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = execution.RenderPass,
                Framebuffer = execution.Framebuffer,
                RenderArea = new Rect2D(new Offset2D(0, 0), execution.Extent),
                ClearValueCount = clearValueCount,
                PClearValues = clearValues
            };
  
            _master.Vk.CmdBeginRenderPass(cmd, in beginInfo, SubpassContents.Inline);

            // Inside ExecutePasses, after CmdBeginRenderPass for the UI pass:
            if (pass.Type == RenderPassType.UI)
            {
                var clearRect = new ClearRect
                {
                    Rect = new Rect2D(new Offset2D(0, 0), execution.Extent),
                    BaseArrayLayer = 0,
                    LayerCount = 1
                };
                var clearAttachment = new ClearAttachment
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    ColorAttachment = 0,
                    ClearValue = new ClearValue { Color = new ClearColorValue(0.1f, 0.1f, 0.1f, 1f) } // dark gray
                };
                _master.Vk.CmdClearAttachments(cmd, 1, &clearAttachment, 1, &clearRect);
            }

            // Debug.Log($"pass.Type = {pass.Type}", VALIDATION_LAYERS.INFO);
            if (pass.Type is not RenderPassType.Present)
            {
                
                foreach (var submission in submissions)
                {
                    if (submission.PassType != pass.Type)
                        continue;

                    RecordSubmission(cmd, in submission, passViewport, passScissor);
                }
            }

            _master.Vk.CmdEndRenderPass(cmd);
        }
    }

    private void RecordSubmission(CommandBuffer cmd, in DrawSubmission submission, in Viewport passViewport,
        in Rect2D passScissor)
    {
        if (!submission.PipelineData.IsValid)
            return;

        Debug.Log($"Execution push constant range: {submission.PushConstants.Data.Length}");


        _master.Vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, submission.PipelineData.VkPipeline);

        var descriptorSet = submission.DescriptorSet;
        if (descriptorSet.Handle != 0)
        {
            _master.Vk.CmdBindDescriptorSets(
                cmd,
                PipelineBindPoint.Graphics,
                submission.PipelineData.VkLayout,
                0,
                1,
                &descriptorSet,
                0,
                null);
        }

        var viewport = submission.ViewportPolicy == SubmissionViewportPolicy.Explicit
            ? submission.Viewport
            : passViewport;
        _master.Vk.CmdSetViewport(cmd, 0, 1, &viewport);

        var scissor = submission.ScissorPolicy == SubmissionScissorPolicy.Explicit ? submission.Scissor : passScissor;
        _master.Vk.CmdSetScissor(cmd, 0, 1, &scissor);

        if (_currentFrame == 0 && LOG_RENDER_GRAPH)
        {
            Debug.Log(
                $"Submission vertex count: {submission.VertexCount}, index count: {submission.IndexCount}, instance count: {submission.InstanceCount}",
                VALIDATION_LAYERS.INFO, LOG_RENDER_GRAPH);
        }

        if (submission.PushConstants.HasData)
        {
            fixed (byte* pushData = submission.PushConstants.Data)
            {
                _master.Vk.CmdPushConstants(
                    cmd,
                    submission.PipelineData.VkLayout,
                    submission.PushConstants.StageFlags,
                    submission.PushConstants.Offset,
                    (uint)submission.PushConstants.Data.Length,
                    pushData
                );
            }
        }
        else
        {
            var identity = submission.ModelMatrix;
            _master.Vk.CmdPushConstants(
                cmd,
                submission.PipelineData.VkLayout,
                ShaderStageFlags.VertexBit,
                0,
                (uint)sizeof(Matrix4x4),
                &identity);
        }

        if (submission.VertexBuffer.IsValid)
        {
            var vb = submission.VertexBuffer.Buffer;
            var vbOffset = submission.VertexOffset;
            _master.Vk.CmdBindVertexBuffers(cmd, 0, 1, &vb, &vbOffset);
        }

        if (submission.IndexBuffer.IsValid && submission.IndexCount > 0)
        {
            if(DEBUG) Debug.Log(
                $"Binding indexBuffer with handle: {submission.IndexBuffer.Buffer.Handle}, indexCount : {submission.IndexCount}", VALIDATION_LAYERS.INFO);
            
            _master.Vk.CmdBindIndexBuffer(cmd, submission.IndexBuffer.Buffer, submission.IndexOffset,
                submission.IndexType);
            _master.Vk.CmdDrawIndexed(cmd, submission.IndexCount, Math.Max(submission.InstanceCount, 1),
                submission.FirstIndex, submission.VertexBase, 0);
            return;
        }

        if (submission.VertexCount > 0)
        {
            _master.Vk.CmdDraw(cmd, submission.VertexCount, Math.Max(submission.InstanceCount, 1),
                submission.FirstVertex, 0);
        }
    }


    private void BeginFrame()
    {
        if (DEBUG)
        {
            Debug.Log("BeginFrame called", VALIDATION_LAYERS.INFO);
        }
        if (_frameActive)
            throw new Exception("BeginFrame called while frame active.");

        var framebufferSize = _master.GetWindow.FramebufferSize;
        if (framebufferSize is { X: > 0, Y: > 0 } &&
            ((uint)framebufferSize.X != _swapchainHandler.Extent.Width ||
             (uint)framebufferSize.Y != _swapchainHandler.Extent.Height))
        {
            _framebufferResized = true;
        }
        
        if (!HasValidFramebufferSize())
        {
            _frameActive = false;
            return;
        }
        
        if (IsWindowMinimized())
        {
            _frameActive = false;
            Debug.Log($"IsWindowMinized check: {IsWindowMinimized()}, FrameActive: {_frameActive}", VALIDATION_LAYERS.INFO);
            return;
        }
        
        var device = _master.VulkanDevice.Device;
        var vk = _master.Vk;

        var cmd = _commandBuffer[_currentFrame];

        // Wait for this frame to finish
        fixed (Fence* frameFence = &_inFlightFences[_currentFrame])
        {
            vk.WaitForFences(device, 1, frameFence, true, ulong.MaxValue);
        }

        if (_framebufferResized)
        {
            RecreateSwapchain();
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
                if (!IsWindowMinimized())
                    RecreateSwapchain();
                _frameActive = false;
                Debug.Log("Recreated swapchain, OutOfDateKhr", VALIDATION_LAYERS.INFO);
                return;
            case Result.SuboptimalKhr:
                _framebufferResized = true;
                Debug.Log($"Framebuffer resize: {_framebufferResized}", VALIDATION_LAYERS.INFO);
                break;
            default:
            {
                if(DEBUG)
                {
                    Debug.Log("acquireResult default triggered", VALIDATION_LAYERS.INFO);
                }
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

        _graphResourceRuntimeManager.ResolveImportedGraphResources(_compiledGraph, _importMap, _swapchainHandler,
            _currentImageIndex);

        fixed (Fence* frameFence = &_inFlightFences[_currentFrame])
        {
            vk.ResetFences(device, 1, frameFence);
        }

        // var clearColor = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
        vk.ResetCommandBuffer(cmd, 0);
        
        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            // Flags = CommandBufferUsageFlags.SimultaneousUseBit
        };

        vk.BeginCommandBuffer(cmd, in beginInfo);

        _frameActive = true;
    }

    private void EndFrame()
    {
        
        if (DEBUG)
        {
            Debug.Log("Running EndFrame() before frame active check");
        }
        if (!_frameActive)
        {
            return;
        }
        
        if (DEBUG)
        {
            Debug.Log("Running EndFrame() after frame active check");
        }

        var cmd = _commandBuffer[_currentFrame];

        // _master.Vk.CmdEndRenderPass(cmd);

        if (_master.Vk.EndCommandBuffer(cmd) != Result.Success)
            throw new Exception("Failed to end command buffer!");

        Semaphore waitSemaphore = _waitSemaphore[_currentFrame];
        Semaphore signalSemaphore = _signalSemaphore[_currentImageIndex];
        Fence frameFence = _inFlightFences[_currentFrame];

        _swapchainHandler.QueueSubmit(cmd, waitSemaphore, signalSemaphore, frameFence);


        var presentResult = _swapchainHandler.Present(signalSemaphore, _currentImageIndex);
        if (presentResult == Result.ErrorOutOfDateKhr || presentResult == Result.SuboptimalKhr || _framebufferResized)
        {
            RecreateSwapchain();
        }

        _currentFrame = (uint)((_currentFrame + 1) % _inFlightFences.Length);

        _frameActive = false;
    }

    #endregion

    #region Creation

    private void CreateResources()
    {
        if (_compiledGraph is null)
            return;

        _graphResourceRuntimeManager.EnsureGraphResources(_compiledGraph, _swapchainHandler, LOG_RENDER_GRAPH);

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

            if (_master.Vk.AllocateCommandBuffers(_master.VulkanDevice.Device, in allocInfo, commandBufferPtr) !=
                Result.Success)
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

        Debug.Log(
            $"Created {_signalSemaphore.Length} signal semaphores, {_waitSemaphore.Length} wait semaphores and  {_inFlightFences.Length} in flight fences",
            VALIDATION_LAYERS.SUCCESS);
    }

    private Semaphore CreateSemaphore(string name)
    {
        var semaphoreInfo = new SemaphoreCreateInfo
        {
            SType = StructureType.SemaphoreCreateInfo
        };
        if (_master.Vk.CreateSemaphore(_master.VulkanDevice.Device, in semaphoreInfo, null, out var semaphore) !=
            Result.Success)
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

        if (_master.Vk.CreateFence(_master.VulkanDevice.Device, in fenceInfo, null, out var fence) != Result.Success)
            throw new Exception("Failed to create fence!");

        _fenceNames[fence.Handle] = name;

        return fence;
    }

    #endregion


    #region Resolve

    private PassAttachmentRuntime? ResolvePassAttachment(uint handle)
    {
        if (!_graphResourceRuntimeManager.TryGetRuntime(handle, out var runtime))
            return null;

        return new PassAttachmentRuntime(runtime.View, runtime.Format, runtime.Extent, runtime.Usage);
    }

    #endregion Resolve
    
    public bool TryGetResourceView(string resourceName, out ImageView view)
    {
        view = default;

        if (_compiledGraph is null || string.IsNullOrWhiteSpace(resourceName))
            return false;

        foreach (var resource in _compiledGraph.Resources)
        {
            if (!string.Equals(resource.Name, resourceName, StringComparison.Ordinal))
                continue;

            if (!_graphResourceRuntimeManager.TryGetRuntime(resource.Handle.Handle, out var runtime))
                return false;

            if (runtime.View.Handle == 0)
                return false;

            view = runtime.View;
            return true;
        }

        return false;
    }
    
    public bool TryGetResourceExtent(string resourceName, out Extent2D extent)
    {
        extent = default;

        if (_compiledGraph is null || string.IsNullOrWhiteSpace(resourceName))
            return false;

        foreach (var resource in _compiledGraph.Resources)
        {
            if (!string.Equals(resource.Name, resourceName, StringComparison.Ordinal))
                continue;

            if (!_graphResourceRuntimeManager.TryGetRuntime(resource.Handle.Handle, out var runtime))
                return false;

            if (runtime.Extent.Width == 0 || runtime.Extent.Height == 0)
                return false;

            extent = runtime.Extent;
            return true;
        }

        return false;
    }


    #region Cleanup
    
    private bool HasValidFramebufferSize()
    {
        var framebufferSize = _master.GetWindow.FramebufferSize;
        return framebufferSize.X > 0 && framebufferSize.Y > 0;
    }

    private bool IsWindowMinimized() => _master.GetWindow.WindowState == WindowState.Minimized;

    private bool RecreateSwapchain()
    {
        Debug.Log("Recreating swapchain", VALIDATION_LAYERS.INFO);
        if (_swapchainHandler.Extent.Width <= 0 || _swapchainHandler.Extent.Height <= 0)
        {
            _framebufferResized = true;
            return false;
        }

        // Wait for the device to be completely idle
        // _master.Vk.DeviceWaitIdle(_master.VulkanDevice.Device);

        if (!HasValidFramebufferSize())
        {
            _framebufferResized = true;
            return false;
        }
        
        if (!_swapchainHandler.RecreateSwapchain())
        {
            _framebufferResized = true;
            return false;
        }

        // Recreate per‑frame wait semaphores
        RecreateWaitSemaphores();
        
        // Reset command buffers (they are no longer in use)
        for(int i = 0; i < _commandBuffer.Length; i++)
        {
            _master.Vk.ResetCommandBuffer(_commandBuffer[i], 0);
        }

        // Update image count and per‑image structures
        _imageCount = _swapchainHandler.ImageCount;
        _imagesInFlight = new Fence[_imageCount];
        RecreateSignalSemaphores();

        _currentImageIndex = 0;
        _graphResourceRuntimeManager.DestroyGraphResources();
        _passExecutionHandler.Reset();

        _framebufferResized = false;
        OnSwapchainRecreated?.Invoke();
        return true;
    }
    
    private void RecreateWaitSemaphores()
    {
        // Destroy existing wait semaphores
        foreach (var semaphore in _waitSemaphore)
        {
            if (semaphore.Handle != 0)
            {
                _master.Vk.DestroySemaphore(_master.VulkanDevice.Device, semaphore, null);
            }
        }

        // Recreate with the same per‑frame count
        _waitSemaphore = new Semaphore[Constants.MAX_FRAMES_IN_FLIGHT];
        for (int i = 0; i < Constants.MAX_FRAMES_IN_FLIGHT; i++)
        {
            _waitSemaphore[i] = CreateSemaphore($"WaitSemaphore {i}");
        }
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

    public void Dispose()
    {
        _master.Vk.DeviceWaitIdle(_master.VulkanDevice.Device);


        _graphResourceRuntimeManager.DestroyGraphResources();
        _passExecutionHandler.Reset();
    }

    #endregion Cleanup
}