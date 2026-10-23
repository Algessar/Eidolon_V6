using System.Numerics;
using System.Runtime.InteropServices;

namespace Eidolon.Vulkan;

/// <summary>
/// Per-draw push constant block for the overlay line shaders.
/// </summary>
/// <remarks>
/// Layout must match the <c>PushConstants</c> block in <c>line.vert</c>:
/// <c>mat4 model;</c> followed by <c>vec4 tint;</c>. Same 80-byte layout as
/// <see cref="ObjectPushConstants"/>, so the two can never disagree about alignment.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct LinePushConstants
{
    public Matrix4x4 Model;

    /// <summary>Multiplied with the per-vertex colour; use values above 1 to highlight.</summary>
    public Vector4 Tint;

    public static readonly int SizeInBytes = Marshal.SizeOf<LinePushConstants>();
}
