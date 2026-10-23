using System.Numerics;

namespace EidolonCore.Rendering;

/// <summary>
/// Builds the line geometry used by the editor overlays: a world grid and arrow gizmos.
/// Geometry is built in local space so callers can position it with a transform.
/// </summary>
public static class LineGeometry
{
    /// <summary>Colour of the grid lines that are not on an axis or a major division.</summary>
    public static readonly Vector3 GridMinorColor = new(0.29f, 0.29f, 0.31f);

    /// <summary>Colour of every fifth grid line, which makes the grid readable at a distance.</summary>
    public static readonly Vector3 GridMajorColor = new(0.40f, 0.40f, 0.43f);

    public static readonly Vector3 XAxisColor = new(0.85f, 0.27f, 0.30f);
    public static readonly Vector3 YAxisColor = new(0.52f, 0.82f, 0.28f);
    public static readonly Vector3 ZAxisColor = new(0.28f, 0.47f, 0.88f);

    /// <summary>
    /// A square grid on the XZ plane at y = 0, centred on the origin.
    /// </summary>
    /// <param name="halfExtent">Distance from the centre to the edge, in world units.</param>
    /// <param name="spacing">Distance between neighbouring lines.</param>
    /// <param name="majorEvery">Every n-th line is drawn in the major colour; 0 disables it.</param>
    public static LineMesh BuildGrid(float halfExtent = 20f, float spacing = 1f, int majorEvery = 5)
    {
        if (halfExtent <= 0f)
            throw new ArgumentOutOfRangeException(nameof(halfExtent));

        if (spacing <= 0f)
            throw new ArgumentOutOfRangeException(nameof(spacing));

        var steps = (int)MathF.Round(halfExtent / spacing);
        var vertices = new List<LineVertex>((steps * 2 + 1) * 4);

        for (var step = -steps; step <= steps; step++)
        {
            var offset = step * spacing;

            // Lines running along X (varying Z) ...
            var alongX = step == 0
                ? XAxisColor
                : IsMajor(step, majorEvery) ? GridMajorColor : GridMinorColor;

            vertices.Add(new LineVertex(new Vector3(-halfExtent, 0f, offset), alongX));
            vertices.Add(new LineVertex(new Vector3(halfExtent, 0f, offset), alongX));

            // ... and along Z (varying X).
            var alongZ = step == 0
                ? ZAxisColor
                : IsMajor(step, majorEvery) ? GridMajorColor : GridMinorColor;

            vertices.Add(new LineVertex(new Vector3(offset, 0f, -halfExtent), alongZ));
            vertices.Add(new LineVertex(new Vector3(offset, 0f, halfExtent), alongZ));
        }

        return new LineMesh("Grid", vertices.ToArray());
    }

    /// <summary>
    /// A unit-length axis arrow: a shaft from the origin plus a four-sided arrowhead at the tip.
    /// </summary>
    /// <param name="axis">Direction the arrow points in; it is normalised.</param>
    public static LineMesh BuildArrow(Vector3 axis, Vector3 color, float headLength = 0.26f, float headRadius = 0.05f)
    {
        if (axis.LengthSquared() < 1e-8f)
            throw new ArgumentException("Arrow axis must not be zero.", nameof(axis));

        var direction = Vector3.Normalize(axis);
        var side = Vector3.Normalize(Vector3.Cross(direction, MathF.Abs(direction.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY));
        var up = Vector3.Cross(direction, side);

        var shaftEnd = 1f - headLength;
        var tip = direction;
        var headBase = direction * shaftEnd;

        var vertices = new List<LineVertex>(10)
        {
            new(Vector3.Zero, color),
            new(headBase, color),
        };

        // Four edges from the base ring out to the tip.
        for (var i = 0; i < 4; i++)
        {
            var angle = i * MathF.PI * 0.5f;
            var ringPoint = headBase + (MathF.Cos(angle) * side + MathF.Sin(angle) * up) * headRadius;

            vertices.Add(new LineVertex(ringPoint, color));
            vertices.Add(new LineVertex(tip, color));
        }

        return new LineMesh("Arrow", vertices.ToArray());
    }

    private static bool IsMajor(int step, int majorEvery) =>
        majorEvery > 0 && step % majorEvery == 0;
}
