using System.Numerics;
using System.Runtime.InteropServices;

namespace Eidolon.Vulkan;

/// <summary>
/// Push constant block for the selection outline: only the object's model matrix travels. The rim width
/// and the colour are shader constants, because they are the same for every selected object.
/// </summary>
/// <remarks>
/// Layout must match the <c>PushConstants</c> block in <c>outline.vert</c>: one <c>mat4 model;</c>.
/// The skinned variant uses <see cref="SkinnedPushConstants"/> instead, which is the same block with the
/// bone palette offset appended.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct OutlinePushConstants
{
    public Matrix4x4 Model;

    public static readonly int SizeInBytes = Marshal.SizeOf<OutlinePushConstants>();
}
