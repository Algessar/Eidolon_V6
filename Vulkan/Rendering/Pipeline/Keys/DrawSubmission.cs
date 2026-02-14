using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal enum SubmissionScissorPolicy
{
    PassDefault,
    Explicit,
}

internal enum SubmissionViewportPolicy
{
    PassDefault,
    Explicit,
}
//NOTE: This looks very much like a refactored DrawData to me.
internal struct DrawSubmission
{
    public RenderPassType PassType;

    public PipelineData PipelineData;
    public PipelineKey? PipelineKey;

    public DescriptorSet DescriptorSet;
    public DescriptorKey? DescriptorKey;

    public PrimitiveTopology Topology;

    public GpuBuffer VertexBuffer;
    public ulong VertexOffset;

    public GpuBuffer IndexBuffer;
    public ulong IndexOffset;
    public IndexType IndexType;

    public uint VertexCount;
    public uint IndexCount;
    public uint InstanceCount;
    public uint FirstVertex;
    public uint FirstIndex;
    public int VertexBase;

    public SubmissionScissorPolicy ScissorPolicy;
    public Rect2D Scissor;

    public SubmissionViewportPolicy ViewportPolicy;
    public Viewport Viewport;

    public PushConstantPayload PushConstants;
}

internal struct PushConstantPayload
{
    public ShaderStageFlags StageFlags;
    public uint Offset;
    public byte[] Data;

    public bool HasData => Data is { Length: > 0 };

    public static PushConstantPayload Empty => new()
    {
        StageFlags = 0,
        Offset = 0,
        Data = Array.Empty<byte>()
    };
}