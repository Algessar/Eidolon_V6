using System.Numerics;
using EidolonCore.Rendering;

namespace Eidolon.Tests;

public class MeshTests
{
    [Fact]
    public void CubeHasFourVerticesPerFaceAndTwoTrianglesEach()
    {
        var cube = PrimitiveMeshes.Cube;

        Assert.Equal(24, cube.Vertices.Length);
        Assert.Equal(36, cube.Indices.Length);
        Assert.Equal(12, cube.TriangleCount);
        Assert.True(cube.IsValid);
    }

    [Fact]
    public void CubeFaceNormalsAreUnitAndSharedPerFace()
    {
        var cube = PrimitiveMeshes.Cube;

        for (var face = 0; face < 6; face++)
        {
            var normal = cube.Vertices[face * 4].Normal;
            Assert.Equal(1f, normal.Length(), 5);

            for (var corner = 0; corner < 4; corner++)
                Assert.Equal(normal, cube.Vertices[face * 4 + corner].Normal);
        }
    }

    [Fact]
    public void CubeBoundsAreTheUnitCube()
    {
        var bounds = PrimitiveMeshes.Cube.LocalBounds;

        Assert.Equal(new Vector3(-0.5f), bounds.Min);
        Assert.Equal(new Vector3(0.5f), bounds.Max);
        Assert.Equal(1f, bounds.MaxDimension);
        Assert.Equal(Vector3.Zero, bounds.Center);
    }

    [Fact]
    public void PlaneFacesUpAndIsFlat()
    {
        var plane = PrimitiveMeshes.Plane;

        Assert.Equal(4, plane.Vertices.Length);
        Assert.Equal(6, plane.Indices.Length);

        foreach (var vertex in plane.Vertices)
            Assert.Equal(Vector3.UnitY, vertex.Normal);

        Assert.Equal(0f, plane.LocalBounds.Size.Y);
        Assert.Equal(1f, plane.LocalBounds.MaxDimension);
    }

    [Fact]
    public void PrimitiveMeshesAreShared()
    {
        Assert.Same(PrimitiveMeshes.Cube, PrimitiveMeshes.Cube);
        Assert.NotSame(PrimitiveMeshes.Cube, PrimitiveMeshes.Plane);
    }

    [Fact]
    public void BoundsFromVerticesComputesMinAndMax()
    {
        Vertex[] vertices =
        [
            new(new Vector3(1, -2, 3), Vector3.UnitY),
            new(new Vector3(-4, 5, 0), Vector3.UnitY),
        ];

        var bounds = Bounds.FromVertices(vertices);

        Assert.Equal(new Vector3(-4, -2, 0), bounds.Min);
        Assert.Equal(new Vector3(1, 5, 3), bounds.Max);
    }

    [Fact]
    public void BoundsFromEmptyVerticesIsZero()
    {
        var bounds = Bounds.FromVertices([]);

        Assert.Equal(Vector3.Zero, bounds.Min);
        Assert.Equal(Vector3.Zero, bounds.Max);
        Assert.Equal(0f, bounds.MaxDimension);
    }
}
