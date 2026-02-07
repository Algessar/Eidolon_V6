using Silk.NET.Vulkan;
using System.Runtime.InteropServices;

namespace Eidolon.Vulkan;

internal struct VertexAttribute : IEquatable<VertexAttribute>
{
    public uint Location;      // Shader location (layout(location = X))
    public Format Format;      // Data format (R32G32B32Sfloat, etc.)
    public uint Offset;        // Byte offset from start of vertex
    
    public VertexAttribute(uint location, Format format, uint offset)
    {
        Location = location;
        Format = format;
        Offset = offset;
    }

    // For use with generic Vertex structs
    public static VertexAttribute Create<T>(uint location, Format format, string fieldName)
        where T : struct
    {
        return new VertexAttribute(location, format, (uint)Marshal.OffsetOf<T>(fieldName));
    }
    
    public bool Equals(VertexAttribute other)
    {
        return Location == other.Location &&
               Format == other.Format &&
               Offset == other.Offset;
    }
    
    public override bool Equals(object? obj)
    {
        return obj is VertexAttribute other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(Location, Format, Offset);
    }
    
    public static bool operator ==(VertexAttribute left, VertexAttribute right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(VertexAttribute left, VertexAttribute right)
    {
        return !(left == right);
    }
}

