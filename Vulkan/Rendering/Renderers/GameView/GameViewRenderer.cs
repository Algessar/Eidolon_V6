using System.Numerics;
using System.Runtime.InteropServices;
using Eidolon.Engine;
using Eidolon.Vulkan;
using ImGuiNET;
using Silk.NET.Vulkan;

namespace EidolonEngine;

/// <summary>
/// Produces game-view draw submissions. Render-pass/framebuffer ownership stays in render-graph execution.
/// </summary>
internal sealed class GameViewRenderer
{
    private VulkanMaster _master;
    private readonly Camera _camera;

    private CameraController _cameraController;
    

    private readonly DescriptorSet _descriptorSet;
    private PipelineData _gameViewPipeline;
    
    private GpuBuffer _gridVertexBuffer;
    private GpuBufferKey _gridVertexBufferKey;
    private uint _gridVertexCount;


    public GameViewRenderer(VulkanMaster master, CameraController cameraController)
    {
        _master = master;
        _cameraController = cameraController;
        _camera = _cameraController.Camera;
        _descriptorSet = _master.DescriptorFactory.GetDescriptorSet(0);
    }
    
    
    public DrawSubmission[] CurrentSubmissions { get; private set; } = Array.Empty<DrawSubmission>();
    public void NewFrame(double delta)
    {
        _cameraController.Update(delta);
        BuildDrawSubmissions();
        // Debug.Log("Running NewFrame in GameViewRenderer", VALIDATION_LAYERS.INFO);
    }

    public void BuildDrawSubmissions()
    {
        EnsurePipeline();
        EnsureGridGeometry();
        CurrentSubmissions = BuildGameViewSubmissions(_gameViewPipeline);
        // Debug.Log($"[GameView] Submissions count: {CurrentSubmissions.Length}, VertexCount: {_gridVertexCount}, Pipeline valid: {_gameViewPipeline.IsValid}");
    }

    private void EnsurePipeline()
    {
        if (_gameViewPipeline.IsValid)
            return;

        // Keep this pipeline compatible with the render-graph GameView target format
        // (EidolonEditor builds GameView as Rgba16Float).
        var renderPassKey = new RenderPassKey
        {
            ColorFormat = Format.R16G16B16A16Sfloat,
            DepthFormat = Format.D32Sfloat,
            HasDepth = true,
            HasAlpha = true,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.ColorAttachmentOptimal,
            InitialDepthLayout = ImageLayout.Undefined,
            FinalDepthLayout = ImageLayout.DepthStencilAttachmentOptimal,
        };

        var renderPass = _master.RenderPassFactory.CreateRenderPass(renderPassKey);

        var pipelineKey = new PipelineKey
        {
            VertexShaderPath = "default_shader.vert.spv",
            FragmentShaderPath = "default_shader.frag.spv",
            RenderPass = renderPass,
            Layout = _master.DescriptorFactory.Layout,
            VertexFormat = new VertexFormat
            {
                Stride = (uint)Marshal.SizeOf<Vertex>(),
                Attributes =
                [
                    new VertexAttribute(0, Format.R32G32B32Sfloat, (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.Position))),
                    new VertexAttribute(1, Format.R32G32B32Sfloat, (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.Color))),
                ]
            },
            Topology = PrimitiveTopology.LineList,
            CullMode = CullModeBits.None,
            FrontFace = FrontFace.CounterClockwise,
            HasDepth = true,
            DepthTestEnable = true,
            DepthWriteEnable = true,
            EnableBlending = false,
            BlendState = BlendState.NoBlending,
        };

        _gameViewPipeline = _master.PipelineFactory.GetOrCreate(pipelineKey);
    }

    private DrawSubmission[] BuildGameViewSubmissions(PipelineData pipelineData)
    {
        if (!pipelineData.IsValid || !_gridVertexBuffer.IsValid || _gridVertexCount == 0)
        {
            Debug.Log("[GameView] Submission skipped: invalid pipeline or buffer.", VALIDATION_LAYERS.WARNING);
            return Array.Empty<DrawSubmission>();
        }

        var aspectRatio = 1f;
        if (_master.FrameHandler is not null &&
            _master.FrameHandler.TryGetResourceExtent("GameView", out var gameViewExtent) &&
            gameViewExtent.Height > 0)
        {
            aspectRatio = ResolveGameViewAspectRatio();
        }
        else
        {
            var framebufferSize = _master.GetWindow.FramebufferSize;
            aspectRatio = framebufferSize.Y > 0 ? (float)framebufferSize.X / framebufferSize.Y : 1f;
        }
        var shaderMatrix = Matrix4x4.Transpose(_camera.BuildViewProjection(aspectRatio));
        var pushConstantData = MemoryMarshal
            .AsBytes(MemoryMarshal.CreateReadOnlySpan(ref shaderMatrix, 1))
            .ToArray();

        return
        [
            new DrawSubmission
            {
                PassType = RenderPassType.GameView,
                PipelineData = pipelineData,
                DescriptorSet = _descriptorSet,
                VertexBuffer = _gridVertexBuffer,
                VertexOffset = 0,
                IndexBuffer = default,
                IndexOffset = 0,
                IndexType = IndexType.Uint16,
                VertexCount = _gridVertexCount,
                IndexCount = 0,
                InstanceCount = 1,
                FirstVertex = 0,
                FirstIndex = 0,
                VertexBase = 0,
                ScissorPolicy = SubmissionScissorPolicy.PassDefault,
                Scissor = default,
                ViewportPolicy = SubmissionViewportPolicy.PassDefault,
                Viewport = default,
                PushConstants = new PushConstantPayload
                {
                    StageFlags = ShaderStageFlags.VertexBit,
                    Offset = 0,
                    Data = pushConstantData,
                },
                ModelMatrix = Matrix4x4.Identity,
            }
        ];
    }
    
    private float ResolveGameViewAspectRatio()
    {
        if (_master.FrameHandler is not null &&
            _master.FrameHandler.TryGetResourceExtent("GameView", out var gameViewExtent) &&
            gameViewExtent.Height > 0)
        {
            return (float)gameViewExtent.Width / gameViewExtent.Height;
        }

        var framebufferSize = _master.GetWindow.FramebufferSize;
        return framebufferSize.Y > 0 ? (float)framebufferSize.X / framebufferSize.Y : 1f;
    }
    
    private unsafe void EnsureGridGeometry()
    {
        if (_gridVertexBuffer.IsValid)
        {
            return;
        }

        var gridVertices = BuildGridVertices(gridHalfExtent: 20, spacing: 1f);
        _gridVertexCount = (uint)gridVertices.Length;

        _gridVertexBufferKey = new GpuBufferKey
        {
            UsageClass = GpuBufferUsageClass.Vertex,
            Size = (ulong)(Marshal.SizeOf<Vertex>() * gridVertices.Length),
            Usage = BufferUsageFlags.VertexBufferBit,
            MemoryProperties = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            Count = 1,
            AllocationStrategy = GpuBufferAllocationStrategy.Static,
        };

        _gridVertexBuffer = _master.GpuBufferFactory.GetOrCreate(_gridVertexBufferKey)[0];

        void* mapped;
        if (_master.Vk.MapMemory(_master.VulkanDevice.Device, _gridVertexBuffer.Memory, 0, _gridVertexBuffer.Size, 0, &mapped) != Result.Success)
        {
            throw new InvalidOperationException("Failed to map game-view grid vertex buffer.");
        }

        fixed (Vertex* verticesPtr = gridVertices)
        {
            System.Buffer.MemoryCopy(verticesPtr, mapped, (long)_gridVertexBuffer.Size, (long)_gridVertexBuffer.Size);
        }
        Debug.Log($"[GameView] Grid buffer handle: {_gridVertexBuffer.Buffer.Handle}, size: {_gridVertexBuffer.Size}, vertex count: {_gridVertexCount}");
        _master.Vk.UnmapMemory(_master.VulkanDevice.Device, _gridVertexBuffer.Memory);
    }

    private static Vertex[] BuildGridVertices(int gridHalfExtent, float spacing)
    {
        var lineCountPerAxis = gridHalfExtent * 2 + 1;
        var vertices = new Vertex[lineCountPerAxis * 4];
        var index = 0;

        var axisColorX = new Vector3(0.95f, 0.25f, 0.25f);
        var axisColorZ = new Vector3(0.25f, 0.55f, 0.95f);
        var majorColor = new Vector3(0.35f, 0.35f, 0.35f);
        var minorColor = new Vector3(0.2f, 0.2f, 0.2f);

        for (var i = -gridHalfExtent; i <= gridHalfExtent; i++)
        {
            var offset = i * spacing;
            var color = i == 0
                ? axisColorX
                : (i % 5 == 0 ? majorColor : minorColor);

            vertices[index++] = new Vertex(new Vector3(-gridHalfExtent * spacing, 0f, offset), color);
            vertices[index++] = new Vertex(new Vector3(gridHalfExtent * spacing, 0f, offset), color);

            color = i == 0
                ? axisColorZ
                : (i % 5 == 0 ? majorColor : minorColor);

            vertices[index++] = new Vertex(new Vector3(offset, 0f, -gridHalfExtent * spacing), color);
            vertices[index++] = new Vertex(new Vector3(offset, 0f, gridHalfExtent * spacing), color);
        }

        return vertices;
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }
}