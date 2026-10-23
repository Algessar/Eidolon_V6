using System.Numerics;
using EidolonCore.Rendering;

namespace Eidolon.Tests;

public class LineGeometryTests
{
    [Fact]
    public void GridHasOneLinePerCellEdgeInBothDirections()
    {
        var grid = LineGeometry.BuildGrid(halfExtent: 10f, spacing: 1f);

        // 21 lines along X plus 21 lines along Z.
        Assert.Equal(42, grid.LineCount);
        Assert.All(grid.Vertices, vertex => Assert.Equal(0f, vertex.Position.Y));
    }

    [Fact]
    public void GridLinesSpanTheWholeExtent()
    {
        var grid = LineGeometry.BuildGrid(halfExtent: 4f, spacing: 2f);

        Assert.Equal(10, grid.LineCount);
        Assert.Contains(grid.Vertices, vertex => vertex.Position == new Vector3(-4f, 0f, -4f));
        Assert.Contains(grid.Vertices, vertex => vertex.Position == new Vector3(4f, 0f, 4f));
    }

    [Fact]
    public void CentreLinesUseAxisColours()
    {
        var grid = LineGeometry.BuildGrid(halfExtent: 3f, spacing: 1f);

        var xAxisLine = grid.Vertices
            .Where(vertex => vertex.Color == LineGeometry.XAxisColor)
            .ToArray();

        // The X axis is the line at z = 0: both endpoints carry the red colour.
        Assert.Equal(2, xAxisLine.Length);
        Assert.All(xAxisLine, vertex => Assert.Equal(0f, vertex.Position.Z));

        var zAxisLine = grid.Vertices
            .Where(vertex => vertex.Color == LineGeometry.ZAxisColor)
            .ToArray();

        Assert.Equal(2, zAxisLine.Length);
        Assert.All(zAxisLine, vertex => Assert.Equal(0f, vertex.Position.X));
    }

    [Fact]
    public void EveryFifthLineIsMajor()
    {
        var grid = LineGeometry.BuildGrid(halfExtent: 10f, spacing: 1f, majorEvery: 5);

        var major = grid.Vertices.Count(vertex => vertex.Color == LineGeometry.GridMajorColor);
        var minor = grid.Vertices.Count(vertex => vertex.Color == LineGeometry.GridMinorColor);

        // Steps 15, 10, -5, -10 and 5 (skipping 0, which is an axis) are major in each direction.
        Assert.Equal(16, major);
        Assert.Equal(2 * (2 * 21 - 2) - 16, minor);
    }

    [Fact]
    public void ArrowRunsFromOriginToItsAxis()
    {
        var arrow = LineGeometry.BuildArrow(Vector3.UnitY, LineGeometry.YAxisColor);

        Assert.Equal(5, arrow.LineCount);
        Assert.Equal(Vector3.Zero, arrow.Vertices[0].Position);
        VectorAssert.Near(Vector3.UnitY * 0.74f, arrow.Vertices[1].Position);

        // Every arrowhead edge ends at the tip.
        var tips = arrow.Vertices.Skip(2).Where((_, index) => index % 2 == 1).ToArray();
        Assert.Equal(4, tips.Length);
        Assert.All(tips, vertex => VectorAssert.Near(Vector3.UnitY, vertex.Position));
    }

    [Fact]
    public void ArrowNormalisesItsAxis()
    {
        var arrow = LineGeometry.BuildArrow(new Vector3(0f, 0f, 10f), Vector3.One);

        var tips = arrow.Vertices.Skip(2).Where((_, index) => index % 2 == 1).ToArray();
        Assert.All(tips, vertex => VectorAssert.Near(Vector3.UnitZ, vertex.Position));
    }

    [Fact]
    public void ArrowRejectsZeroAxis()
    {
        Assert.Throws<ArgumentException>(() => LineGeometry.BuildArrow(Vector3.Zero, Vector3.One));
    }

    [Fact]
    public void LineMeshRejectsUnpairedVertices()
    {
        Assert.Throws<ArgumentException>(() =>
            new LineMesh("bad", [new LineVertex(Vector3.Zero, Vector3.One)]));
    }
}
