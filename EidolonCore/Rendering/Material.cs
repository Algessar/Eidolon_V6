using System.Numerics;

namespace EidolonCore.Rendering;

/// <summary>
/// Surface appearance for a drawable entity. Only a base colour for now; textures, roughness and
/// metallic parameters arrive with the real material system.
/// </summary>
public sealed class Material
{
    public string Name { get; set; }

    public Vector4 BaseColor { get; set; }

    public Material(string name = "Material", Vector4? baseColor = null)
    {
        Name = name;
        BaseColor = baseColor ?? Vector4.One;
    }

    public override string ToString() => $"Material({Name})";
}
