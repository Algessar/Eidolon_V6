using System.Numerics;
using EidolonCore.Rendering;

namespace Eidolon.Tests;

public class PrimitiveMeshTests
{
    public static TheoryData<string, Mesh> ClosedMeshes => new()
    {
        { "Cube", PrimitiveMeshes.Cube },
        { "Sphere", PrimitiveMeshes.Sphere },
        { "Capsule", PrimitiveMeshes.Capsule },
    };

    [Theory]
    [MemberData(nameof(ClosedMeshes))]
    public void TrianglesFaceOutwards(string name, Mesh mesh)
    {
        var centre = mesh.LocalBounds.Center;
        var facingIn = 0;

        for (var i = 0; i + 2 < mesh.Indices.Length; i += 3)
        {
            var a = mesh.Vertices[mesh.Indices[i]].Position;
            var b = mesh.Vertices[mesh.Indices[i + 1]].Position;
            var c = mesh.Vertices[mesh.Indices[i + 2]].Position;

            var normal = Vector3.Cross(b - a, c - a);

            // Pole quads collapse onto a point; a zero-area triangle faces nowhere.
            if (normal.LengthSquared() < 1e-12f)
                continue;

            if (Vector3.Dot(normal, (a + b + c) / 3f - centre) <= 0f)
                facingIn++;
        }

        Assert.True(facingIn == 0, $"{name}: {facingIn} triangles are wound inwards and would be culled.");
    }

    [Theory]
    [MemberData(nameof(ClosedMeshes))]
    public void IndicesAreInRange(string name, Mesh mesh)
    {
        foreach (var index in mesh.Indices)
            Assert.True(index < mesh.Vertices.Length, $"{name}: index {index} is outside {mesh.Vertices.Length} vertices.");
    }

    [Fact]
    public void SphereIsTheUnitDiameterBall()
    {
        var bounds = PrimitiveMeshes.Sphere.LocalBounds;

        VectorAssert.Near(new Vector3(-0.5f), bounds.Min, 1e-3f);
        VectorAssert.Near(new Vector3(0.5f), bounds.Max, 1e-3f);
    }

    [Fact]
    public void CapsuleMatchesTheBodyItStandsFor()
    {
        // Radius 0.5 around a one unit segment: two units tall in Y.
        var bounds = PrimitiveMeshes.Capsule.LocalBounds;

        VectorAssert.Near(new Vector3(-0.5f, -1f, -0.5f), bounds.Min, 1e-3f);
        VectorAssert.Near(new Vector3(0.5f, 1f, 0.5f), bounds.Max, 1e-3f);
    }
}
