using System.Numerics;
using System.Runtime.InteropServices;

namespace Eidolon.Vulkan;

/// <summary>
/// Per-object push constant block for the scene shaders.
/// </summary>
/// <remarks>
/// Layout must match the <c>PushConstants</c> block declared in the vertex and fragment shaders:
/// <c>mat4 model;</c> followed by <c>vec4 baseColor;</c>. 80 bytes, well inside the 128-byte
/// minimum every Vulkan implementation guarantees.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct ObjectPushConstants
{
    public Matrix4x4 Model;
    public Vector4 BaseColor;

    public static readonly int SizeInBytes = Marshal.SizeOf<ObjectPushConstants>();
}
