using System.Numerics;
using System.Runtime.InteropServices;
using Eidolon.Vulkan;
using EidolonCore.Rendering;
using Silk.NET.Vulkan;

namespace Eidolon.Editor;

/// <summary>
/// Draws the editor's world grid as a depth-tested line list inside the GameView pass.
/// </summary>
/// <remarks>
/// The grid is depth tested so scene geometry occludes it, and it does not write depth so it can
/// never hide the objects standing on it.
/// </remarks>
internal unsafe class GridRenderer : IDisposable
{
    private readonly VulkanMaster _master;
    private readonly ViewportSettings _state;
    private readonly List<DrawSubmission> _submissions = [];

    private PipelineData _pipeline;
    private GpuBuffer _gridBuffer;
    private uint _gridVertexCount;
    private DescriptorSet _descriptorSet;

    private float _builtExtent;
    private float _builtSpacing;

    public GridRenderer(VulkanMaster master, ViewportSettings state)
    {
        _master = master;
        _state = state;
    }

    public DrawSubmission[] CurrentSubmissions { get; private set; } = Array.Empty<DrawSubmission>();

    public void NewFrame()
    {
        EnsureResources();

        _submissions.Clear();

        if (!_pipeline.IsValid || !_gridBuffer.IsValid || _gridVertexCount == 0 || !_state.ShowGrid)
        {
            CurrentSubmissions = Array.Empty<DrawSubmission>();
            return;
        }

        var frameIndex = _master.FrameHandler?.CurrentFrame ?? 0;
        _descriptorSet = _master.DescriptorFactory.GetDescriptorSet(frameIndex);

        var model = Matrix4x4.CreateTranslation(0f, _state.GridHeight, 0f);
        var constants = new LinePushConstants { Model = model, Tint = Vector4.One };
        var payload = new byte[LinePushConstants.SizeInBytes];
        MemoryMarshal.Write(payload.AsSpan(), in constants);

        _submissions.Add(new DrawSubmission
        {
            PassType = RenderPassType.EditorView,
            PipelineData = _pipeline,
            DescriptorSet = _descriptorSet,
            VertexBuffer = _gridBuffer,
            VertexOffset = 0,
            IndexBuffer = default,
            IndexType = IndexType.Uint32,
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
                Data = payload,
            },
            ModelMatrix = model,
        });

        CurrentSubmissions = _submissions.ToArray();
    }

    private void EnsureResources()
    {
        EnsurePipeline();

        if (_gridBuffer.IsValid &&
            MathF.Abs(_builtExtent - _state.GridExtent) < 1e-4f &&
            MathF.Abs(_builtSpacing - _state.GridSpacing) < 1e-4f)
        {
            return;
        }

        DestroyBuffer();

        var grid = LineGeometry.BuildGrid(_state.GridExtent, _state.GridSpacing);
        _gridBuffer = Upload(grid, out _gridVertexCount);
        _builtExtent = _state.GridExtent;
        _builtSpacing = _state.GridSpacing;
    }

    private void EnsurePipeline()
    {
        if (_pipeline.IsValid)
            return;

        // Same render pass as the scene geometry, so the submission can share the GameView pass.
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

        var vertexFormat = new VertexFormat
        {
            Stride = (uint)Marshal.SizeOf<LineVertex>(),
            Attributes =
            [
                VertexAttribute.Create<LineVertex>(0, Format.R32G32B32Sfloat, nameof(LineVertex.Position)),
                VertexAttribute.Create<LineVertex>(1, Format.R32G32B32Sfloat, nameof(LineVertex.Color)),
            ]
        };

        _pipeline = _master.PipelineFactory.GetOrCreate(new PipelineKey
        {
            VertexShaderPath = "line.vert.spv",
            FragmentShaderPath = "line.frag.spv",
            RenderPass = renderPass,
            Layout = _master.DescriptorFactory.Layout,
            VertexFormat = vertexFormat,
            Topology = PrimitiveTopology.LineList,
            CullMode = CullModeBits.None,
            FrontFace = FrontFace.CounterClockwise,
            HasDepth = true,
            DepthTestEnable = true,
            DepthWriteEnable = false,
            EnableBlending = false,
            BlendState = BlendState.NoBlending,
            PushConstantSize = (uint)LinePushConstants.SizeInBytes,
            PushConstantStages = ShaderStageFlags.VertexBit,
        });
    }

    private GpuBuffer Upload(LineMesh mesh, out uint vertexCount)
    {
        vertexCount = 0;

        if (!mesh.IsValid)
            return default;

        var bytes = checked((ulong)(mesh.Vertices.Length * Marshal.SizeOf<LineVertex>()));

        var key = new GpuBufferKey
        {
            UsageClass = GpuBufferUsageClass.Vertex,
            Size = bytes,
            Usage = BufferUsageFlags.VertexBufferBit,
            MemoryProperties = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            Count = 1,
            AllocationStrategy = GpuBufferAllocationStrategy.Static,
        };

        var buffer = _master.GpuBufferFactory.Create(key)[0];
        var vertices = mesh.Vertices;

        void* mapped = null;
        var mapResult = _master.Vk.MapMemory(_master.VulkanDevice.Device, buffer.Memory, 0, bytes, 0, &mapped);
        if (mapResult != Result.Success || mapped is null)
            throw new InvalidOperationException($"Failed to map the {mesh.Name} line buffer. Result: {mapResult}");

        fixed (LineVertex* src = vertices)
        {
            global::System.Buffer.MemoryCopy(src, mapped, buffer.Size, bytes);
        }

        _master.Vk.UnmapMemory(_master.VulkanDevice.Device, buffer.Memory);

        vertexCount = (uint)vertices.Length;
        return buffer;
    }

    private void DestroyBuffer()
    {
        if (_gridBuffer.IsValid)
            _master.GpuBufferFactory.Destroy(_gridBuffer);

        _gridBuffer = default;
        _gridVertexCount = 0;
    }

    public void Dispose()
    {
        DestroyBuffer();
        CurrentSubmissions = Array.Empty<DrawSubmission>();
        _submissions.Clear();
    }
}
