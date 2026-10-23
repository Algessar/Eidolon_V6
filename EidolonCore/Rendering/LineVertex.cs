using System.Numerics;
using System.Runtime.InteropServices;

namespace EidolonCore.Rendering;

/// <summary>
/// One end of a line segment: world-space position plus a colour.
/// </summary>
/// <remarks>
/// A plain struct with public fields, not a record: the GPU layout is described by field name
/// through <c>Marshal.OffsetOf</c>, which cannot see compiler-generated backing fields.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LineVertex
{
    public Vector3 Position;
    public Vector3 Color;

    public LineVertex(Vector3 position, Vector3 color)
    {
        Position = position;
        Color = color;
    }

    public override string ToString() => $"LineVertex({Position}, {Color})";
}
