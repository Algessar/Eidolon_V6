using System.Numerics;
using EidolonCore.Rendering;
using ImGuiNET;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Eidolon.Vulkan.Rendering;

internal unsafe class FrameHandler
{
    public VulkanMaster _master { get; }
    private SwapchainHandler _swapchainHandler;
    
    [Header("Resources")]
    private CommandBuffer[] _commandBuffer;
    private Framebuffer[] _framebuffers;

    private CompiledRenderGraph _compiledGraph;
    private readonly Dictionary<uint, GraphImageRuntime> _graphImages = new();

    
    private struct GraphImageRuntime
    {
        public Image Image;
        public ImageView View;
        public DeviceMemory Memory;
        public bool Imported;
    }


    private uint _maxFramesInFlight => Constants.MAX_FRAMES_IN_FLIGHT;

    [Header("Sync Objects")]
    private uint _imageCount;
    private Semaphore[] _waitSemaphore;
    private Semaphore[] _signalSemaphore;
    private Fence[] _inFlightFences;
    private Fence[] _imagesInFlight;
    
    private Dictionary<ulong, string> _semaphoreNames = new();
    private Dictionary<ulong, string> _fenceNames = new();

    
    private uint _currentFrame;
    
    public bool _framebufferResized { get; set; }

    bool LOG_RENDER_GRAPH = true;
    
    public FrameHandler(VulkanMaster master, SwapchainHandler swapchainHandler)
    {
        Debug.Log("Creating FrameHandler", VALIDATION_LAYERS.INFO);
        _master = master;
        _swapchainHandler = swapchainHandler;
        
        _commandBuffer = _master.CommandManager.AllocateCommandBuffers(_imageCount);
        _imageCount = _swapchainHandler.ImageCount;
        CreateSyncObjects();
        
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
        // TODO: create per-frame command buffers (_commandBuffer)
        
        // TODO: create sync objects (_waitSemaphore, _signalSemaphore, fences if needed)
        CreateSyncObjects();
        // TODO: cache image count / framebuffers from swapchain path
        // TODO: hook resize events if needed (_framebufferResized)

        if (LOG_RENDER_GRAPH)
        {
            Debug.Log("FrameHandler.Initialize completed (scaffold).");
        }
    }

    public void SetCompiledGraph(CompiledRenderGraph graph)
    {
        _compiledGraph = graph ?? throw new ArgumentNullException(nameof(graph));
    }
    public void Draw(in DrawData data)
    {
        if (_compiledGraph is null)
        {
            throw new Exception("Draw called without compiled graph.");
        }
        
        var cmd = _commandBuffer[_currentFrame];

        foreach (var pass in _compiledGraph.Passes)
        {
            if (LOG_RENDER_GRAPH)
            {
                Debug.Log($"[RG] Pass {pass.ExecutionIndex}: {pass.Name} ({pass.Type})");
                Debug.Log($"[RG]   Reads:  {string.Join(", ", pass.Reads.Select(r => r.Handle))}");
                Debug.Log($"[RG]   Writes: {string.Join(", ", pass.Writes.Select(w => w.Handle))}");
                Debug.Log($"[RG]   Deps:   {string.Join(", ", pass.Dependencies)}");
            }
        }
        
        

        // TODO: transition pass.Reads to shader-read / attachment-read layout
        // TODO: transition pass.Writes to color/depth attachment layout
        // TODO: begin render pass scope for this graph pass
        // TODO: bind pipeline (you already do CmdBindPipeline today)
        // _master.Vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, );
        
        // TODO: bind descriptors, vertex/index buffers, push constants
        // TODO: issue draw calls for this pass
        // TODO: end render pass scope
        throw new NotImplementedException();
    }

    public void BeginFrame(in DrawData data)
    {

    }

    public void EndFrame(in DrawData data)
    {
        // TODO: end command buffer recording
        // TODO: queue submit with wait/signal semaphores
        // present swapchain image
        // TODO: handle out-of-date/suboptimage + resize path
        
        _currentFrame = (_currentFrame + 1) % _maxFramesInFlight;
        
        if(LOG_RENDER_GRAPH)
        {
            Debug.Log($"EndFrame complete.");
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
                // TODO: map imported resource to swapchain/depth/external target
                _graphImages[resource.Handle.Handle] = new GraphImageRuntime
                {
                    Imported = true
                };
                continue;
            }
            
            //TODO: allocate vk image + memory + image view based on resource.Description + frame size
            _graphImages[resource.Handle.Handle] = new GraphImageRuntime
            {
                Imported = false
            };
        }
    }

    private void CreateSyncObjects()
    {
        _inFlightFences = new Fence[_maxFramesInFlight];
        _imagesInFlight = new Fence[_imageCount];

        _waitSemaphore = new Semaphore[_imageCount]; //NOTE: waitSemaphore
        _signalSemaphore = new Semaphore[_imageCount]; //NOTE: SignalSemaphore

        for (int i = 0; i < _imageCount; i++)
        {
            _waitSemaphore[i] = CreateSemaphore($"WaitSemaphore {i}");
            _signalSemaphore[i] = CreateSemaphore($"SignalSemaphore {i}");
            
            Debug.Log($"Semaphore handles : {_waitSemaphore[i].Handle}", VALIDATION_LAYERS.INFO);
            Debug.Log($"Semaphore handles : {_signalSemaphore[i].Handle}", VALIDATION_LAYERS.INFO);
        }

        for (int i = 0; i < _maxFramesInFlight; i++)
        {
            _inFlightFences[i] = CreateFence($"InFlightFence {i}");
            _imagesInFlight[i] = CreateFence($"ImagesInFlightFence {i}");

            Debug.Log($"In Flight Fences handles : {_inFlightFences[i].Handle}", VALIDATION_LAYERS.INFO);
            Debug.Log($"Images In Flight Fences handles : {_imagesInFlight[i].Handle}", VALIDATION_LAYERS.INFO);
            
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
            
            //TODO: destroy view/image/memory
        }
        
        _graphImages.Clear();
    }
}