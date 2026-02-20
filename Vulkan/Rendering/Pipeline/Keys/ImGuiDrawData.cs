using System.Numerics;

namespace Eidolon.Vulkan;

internal sealed class ImGuiDrawData //This is a fucking key isn't it?
{
    public static readonly ImGuiDrawData Empty = new();

    public ImGuiVertex[] Vertices { get; init; } = Array.Empty<ImGuiVertex>();
    public ushort[] Indices { get; init; } = Array.Empty<ushort>();
    public ImGuiDrawCommand[] Commands { get; init; } = Array.Empty<ImGuiDrawCommand>();
    public Vector2 DisplaySize { get; init; }

    public int TotalVertexCount => Vertices.Length;
    public int TotalIndexCount => Indices.Length;
    public bool HasData => Vertices.Length > 0 && Indices.Length > 0 && Commands.Length > 0;
}

internal readonly record struct ImGuiVertex(Vector2 Position, Vector2 UV, uint Color);

internal readonly record struct ImGuiDrawCommand(
    uint ElementCount,
    uint FirstIndex,
    int VertexOffset,
    Vector4 ClipRect,
    nint TextureId);