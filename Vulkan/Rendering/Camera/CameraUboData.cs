using System.Numerics;
using System.Runtime.InteropServices;

namespace Eidolon.Vulkan;

[StructLayout(LayoutKind.Sequential)]
internal struct CameraUboData
{
    public Matrix4x4 ViewProjection;
}