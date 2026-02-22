using System.Numerics;
using ImGuiNET;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Eidolon.Vulkan;

internal unsafe class FrameHandler : IFrameContext, IDisposable
{
    public VulkanMaster _master { get; }
    private SwapchainHandler _swapchainHandler;

    private readonly PassExecutionFactory _passExecutionFactory;
    private readonly GraphResourceRuntimeManager _graphResourceRuntimeManager;
    private readonly GraphBarrierPlanner _graphBarrierPlanner;

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
    private uint _currentImageIndex;
    public uint CurrentFrame => _currentFrame;

    //NOTE: Only used for debug logging rn?
    private Dictionary<ulong, string> _semaphoreNames = new();
    private Dictionary<ulong, string> _fenceNames = new();

    public bool _framebufferResized { get; set; }

    private readonly bool LOG_RENDER_GRAPH = false;

    public FrameHandler(VulkanMaster master)
    {
        Debug.Log("Creating FrameHandler", VALIDATION_LAYERS.INFO);
        _master = master;
        _swapchainHandler = master.SwapchainHandler;
        _imageCount = _swapchainHandler.ImageCount;
        _passExecutionFactory = new PassExecutionFactory(master, ResolvePassAttachment);
        _graphResourceRuntimeManager = new GraphResourceRuntimeManager(master);
        _graphBarrierPlanner = new GraphBarrierPlanner(master, _graphResourceRuntimeManager, LOG_RENDER_GRAPH);

        Initialize();
        _commandBuffer = _master.CommandHandler.AllocateCommandBuffers(_maxFramesInFlight);

        _master.GetWindow.FramebufferResize += OnWindowResize;
        Debug.Log("FrameHandler created.", VALIDATION_LAYERS.SUCCESS);
    }



    private void OnWindowResize(Vector2D<int> newSize)
    {
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
        _graphResourceRuntimeManager.DestroyGraphResources();
        _passExecutionFactory.Reset();

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

    private void ExecutePasses(in DrawData data)
    {
        if (_compiledGraph is null)
            throw new Exception("Draw called without compiled graph.");

        _graphResourceRuntimeManager.EnsureGraphResources(_compiledGraph, _swapchainHandler, LOG_RENDER_GRAPH);

        var cmd = _commandBuffer[_currentFrame];

        var passViewport = new Viewport(0, 0, _swapchainHandler.Extent.Width, _swapchainHandler.Extent.Height, 0f, 1f);
        var passScissor = new Rect2D(new Offset2D(0, 0), _swapchainHandler.Extent);

        _master.Vk.CmdSetViewport(cmd, 0, 1, &passViewport);
        _master.Vk.CmdSetScissor(cmd, 0, 1, &passScissor);

        var submissions =
            data.Submissions ?? Array.Empty<DrawSubmission>(); // NOTE: Rider warns that left operand is never null.

        foreach (var pass in _compiledGraph.Passes)
        {
            if (_currentFrame == 0)
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

            if (_currentFrame == 0 && LOG_RENDER_GRAPH)
            {
                Debug.Log($"RenderPass used in FrameHandler: {data.PipelineData.RenderPass.Handle}");
            }

            _graphBarrierPlanner.TransitionLayouts(cmd, pass);
            if (pass.Type == RenderPassType.Present)
            {
                // Note: this looks unnecessary tbh
                // Transition to present layout (already done in TransitionLayouts)
                // Do NOT begin a render pass.
                continue; // skip the render pass block
            }

            var execution = _passExecutionFactory.GetOrCreate(pass, _compiledGraph, _currentImageIndex);

            var clearValues =
                stackalloc ClearValue[2]; //NOTE: CA2014: Potential stack overflow. Move the stackalloc out of the loop.

            clearValues[0] = execution.ClearColor
                ? new ClearValue { Color = new ClearColorValue(1f, 1f, 1f, 1f) }
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

        if (_currentFrame == 0)
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
            var identity = Matrix4x4.Identity;
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


    public void Draw(in DrawData data)
    {
        if (_compiledGraph is null)
            throw new Exception("Draw called without compiled graph.");

        BeginFrame(data);
        if (!_frameActive)
            return;

        ExecutePasses(data);
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
            SType = StructureType.CommandBufferBeginInfo
        };

        vk.BeginCommandBuffer(cmd, in beginInfo);

        _frameActive = true;
    }

    public void EndFrame(in DrawData data)
    {
        if (!_frameActive)
        {
            return;
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
            RecreateSwapchain(data.PipelineData);
        }

        _currentFrame = (uint)((_currentFrame + 1) % _inFlightFences.Length);

        _frameActive = false;
    }

    #endregion

    #region Creation

    public void CreateResources()
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

    #region Cleanup

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

        _graphResourceRuntimeManager.DestroyGraphResources();
        _passExecutionFactory.Reset();
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


    public void Dispose()
    {
        _master.Vk.DeviceWaitIdle(_master.VulkanDevice.Device);


        _graphResourceRuntimeManager.DestroyGraphResources();
        _passExecutionFactory.Reset();
    }

    #endregion Cleanup
}