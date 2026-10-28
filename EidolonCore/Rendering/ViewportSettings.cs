using System.Numerics;

namespace EidolonCore.Rendering;

/// <summary>Viewport overlay settings shared by the editor and the grid renderer.</summary>
public sealed class ViewportSettings
{
    /// <summary>Draw the world grid.</summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>Draw the transform gizmo for the current selection.</summary>
    public bool ShowGizmo { get; set; } = true;

    /// <summary>Use the selected entity's world-space centre as the viewport orbit pivot.</summary>
    public bool OrbitAroundSelected { get; set; } = true;

    /// <summary>Height of the grid plane. Sits just above the demo scene's ground.</summary>
    public float GridHeight { get; set; } = -0.498f;

    /// <summary>Half-size of the grid in world units.</summary>
    public float GridExtent { get; set; } = 20f;

    /// <summary>Distance between grid lines.</summary>
    public float GridSpacing { get; set; } = 1f;
}
