

namespace Eidolon.Vulkan;

internal struct VertexFormat : IEquatable<VertexFormat>
{
    public uint Stride;                      // Size of one vertex in bytes
    public VertexAttribute[] Attributes;     // Vertex attributes
    
    public bool Equals(VertexFormat other)
    {
        if (Stride != other.Stride) return false;
        if (Attributes.Length != other.Attributes.Length) return false;
        
        for (int i = 0; i < Attributes.Length; i++)
        {
            if (!Attributes[i].Equals(other.Attributes[i]))
                return false;
        }
        
        return true;
    }
    
    public override bool Equals(object? obj)
    {
        return obj is VertexFormat other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Stride);
        foreach (var attr in Attributes)
            hash.Add(attr);
        return hash.ToHashCode();
    }
}

