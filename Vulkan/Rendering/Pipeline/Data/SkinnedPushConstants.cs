using System.Numerics;
using System.Runtime.InteropServices;

namespace Eidolon.Vulkan;

/// <summary>
/// Per-object push constant block for the skinned pipeline: the object's model matrix and colour plus
/// which slice of the bone palette this skin owns.
/// </summary>
/// <remarks>
/// Layout must match the <c>PushConstants</c> block in <c>skinned.vert</c>: <c>mat4 model;</c>,
/// <c>vec4 baseColor;</c>, <c>uint boneOffset;</c>. 84 bytes, inside the 128-byte guaranteed minimum.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct SkinnedPushConstants
{
    public Matrix4x4 Model;
    public Vector4 BaseColor;
    public uint BoneOffset;

    public static readonly int SizeInBytes = Marshal.SizeOf<SkinnedPushConstants>();
}
