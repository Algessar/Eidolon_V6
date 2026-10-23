using System.Numerics;
using EidolonCore.Assets;
using EidolonCore.Rendering;

namespace Eidolon.Tests;

public class ObjLoaderTests
{
    private static Mesh Parse(string obj) => ObjLoader.Parse(new StringReader(obj), "test");

    [Fact]
    public void ParsesTriangleAndDerivesNormalFromWinding()
    {
        var mesh = Parse("""
            v 0 0 0
            v 1 0 0
            v 0 1 0
            f 1 2 3
            """);

        Assert.Equal(3, mesh.Vertices.Length);
        Assert.Equal(3, mesh.Indices.Length);
        Assert.Equal(1, mesh.TriangleCount);

        foreach (var vertex in mesh.Vertices)
            VectorAssert.Near(Vector3.UnitZ, vertex.Normal);
    }

    [Fact]
    public void ClockwiseWindingGivesOppositeNormal()
    {
        var mesh = Parse("""
            v 0 0 0
            v 0 1 0
            v 1 0 0
            f 1 2 3
            """);

        VectorAssert.Near(-Vector3.UnitZ, mesh.Vertices[0].Normal);
    }

    [Fact]
    public void TriangulatesPolygonByFanning()
    {
        var mesh = Parse("""
            v 0 0 0
            v 1 0 0
            v 1 1 0
            v 0 1 0
            f 1 2 3 4
            """);

        Assert.Equal(2, mesh.TriangleCount);
        Assert.Equal(6, mesh.Indices.Length);

        // Faces without vn data get their own vertices per triangle so shading stays flat.
        Assert.Equal(6, mesh.Vertices.Length);
    }

    [Fact]
    public void UsesProvidedNormals()
    {
        var mesh = Parse("""
            v 0 0 0
            v 1 0 0
            v 0 1 0
            vn 0 0 -1
            f 1//1 2//1 3//1
            """);

        Assert.Equal(3, mesh.Vertices.Length);
        foreach (var vertex in mesh.Vertices)
            VectorAssert.Near(-Vector3.UnitZ, vertex.Normal);
    }

    [Fact]
    public void UsesProvidedTextureCoordinates()
    {
        var mesh = Parse("""
            v 0 0 0
            v 1 0 0
            v 0 1 0
            vt 0.25 0.5
            vn 0 0 1
            f 1/1/1 2/1/1 3/1/1
            """);

        Assert.Equal(3, mesh.Vertices.Length);
        foreach (var vertex in mesh.Vertices)
            Assert.Equal(new Vector2(0.25f, 0.5f), vertex.TexCoord);
    }

    [Fact]
    public void ResolvesNegativeIndicesRelativeToEnd()
    {
        var mesh = Parse("""
            v 0 0 0
            v 1 0 0
            v 0 1 0
            f -3 -2 -1
            """);

        Assert.Equal(1, mesh.TriangleCount);
        VectorAssert.Near(Vector3.Zero, mesh.Vertices[0].Position);
        VectorAssert.Near(new Vector3(1, 0, 0), mesh.Vertices[1].Position);
    }

    [Fact]
    public void IgnoresStatementsItDoesNotSupport()
    {
        var mesh = Parse("""
            # a comment
            mtllib thing.mtl
            o Thing
            g Group
            s off
            usemtl Red
            v 0 0 0
            v 1 0 0
            v 0 1 0
            f 1 2 3
            """);

        Assert.Equal(1, mesh.TriangleCount);
    }

    [Fact]
    public void RejectsOutOfRangeIndices()
    {
        var ex = Assert.Throws<InvalidDataException>(() => Parse("""
            v 0 0 0
            v 1 0 0
            v 0 1 0
            f 1 2 9
            """));

        Assert.Contains("out of range", ex.Message);
    }

    [Fact]
    public void RejectsFilesWithoutPositions()
    {
        var ex = Assert.Throws<InvalidDataException>(() => Parse("# nothing here"));
        Assert.Contains("no vertex positions", ex.Message);
    }

    [Fact]
    public void RejectsFilesWithoutFaces()
    {
        var ex = Assert.Throws<InvalidDataException>(() => Parse("""
            v 0 0 0
            v 1 0 0
            v 0 1 0
            """));

        Assert.Contains("no faces", ex.Message);
    }

    [Fact]
    public void ComputesBounds()
    {
        var mesh = Parse("""
            v -2 -1 -3
            v 4 2 5
            v 0 0 0
            f 1 2 3
            """);

        Assert.Equal(new Vector3(-2, -1, -3), mesh.LocalBounds.Min);
        Assert.Equal(new Vector3(4, 2, 5), mesh.LocalBounds.Max);
        Assert.Equal(8f, mesh.LocalBounds.MaxDimension);
    }
}
