using System.Numerics;

namespace EidolonCore.Rendering;

/// <summary>
/// Built-in meshes, used to populate a scene before an asset pipeline exists.
/// </summary>
/// <remarks>
/// Each property returns the same <see cref="Mesh"/> instance every time, so entities sharing a
/// primitive share one GPU buffer.
/// </remarks>
public static class PrimitiveMeshes
{
    /// <summary>A unit cube centred on the origin, with flat per-face normals.</summary>
    public static Mesh Cube { get; } = CreateCube();

    /// <summary>A unit quad on the XZ plane, centred on the origin and facing +Y.</summary>
    public static Mesh Plane { get; } = CreatePlane();

    /// <summary>A unit-diameter sphere centred on the origin, so its radius matches a 0.5 body radius.</summary>
    public static Mesh Sphere { get; } = CreateSphere();

    /// <summary>A capsule on the Y axis: radius 0.5 and a one unit segment between its cap centres.</summary>
    public static Mesh Capsule { get; } = CreateCapsule();

    private const int SphereRings = 16;
    private const int SphereSegments = 24;
    private const int CapRings = 8;
    private const int CapSegments = 24;
    private const float SphereRadius = 0.5f;
    private const float CapsuleRadius = 0.5f;
    private const float CapsuleSegment = 1f;

    private static Mesh CreateCube()
    {
        // Per face: outward normal and its four corners in counter-clockwise order seen from
        // outside, so the geometric winding matches the declared normal.
        (Vector3 Normal, Vector3[] Corners)[] faces =
        [
            (new(0, 0, 1),
                [new(-.5f, -.5f, .5f), new(.5f, -.5f, .5f), new(.5f, .5f, .5f), new(-.5f, .5f, .5f)]),
            (new(0, 0, -1),
                [new(.5f, -.5f, -.5f), new(-.5f, -.5f, -.5f), new(-.5f, .5f, -.5f), new(.5f, .5f, -.5f)]),
            (new(-1, 0, 0),
                [new(-.5f, -.5f, -.5f), new(-.5f, -.5f, .5f), new(-.5f, .5f, .5f), new(-.5f, .5f, -.5f)]),
            (new(1, 0, 0),
                [new(.5f, -.5f, .5f), new(.5f, -.5f, -.5f), new(.5f, .5f, -.5f), new(.5f, .5f, .5f)]),
            (new(0, 1, 0),
                [new(-.5f, .5f, .5f), new(.5f, .5f, .5f), new(.5f, .5f, -.5f), new(-.5f, .5f, -.5f)]),
            (new(0, -1, 0),
                [new(-.5f, -.5f, -.5f), new(.5f, -.5f, -.5f), new(.5f, -.5f, .5f), new(-.5f, -.5f, .5f)]),
        ];

        Vector2[] uvs = [new(0, 1), new(1, 1), new(1, 0), new(0, 0)];

        var vertices = new List<Vertex>(24);
        var indices = new List<uint>(36);

        foreach (var (normal, corners) in faces)
        {
            var baseIndex = (uint)vertices.Count;

            for (var i = 0; i < corners.Length; i++)
                vertices.Add(new Vertex(corners[i], normal, uvs[i]));

            indices.AddRange([baseIndex, baseIndex + 1, baseIndex + 2, baseIndex, baseIndex + 2, baseIndex + 3]);
        }

        return new Mesh("Cube", vertices.ToArray(), indices.ToArray());
    }

    private static Mesh CreatePlane()
    {
        Vertex[] vertices =
        [
            new(new Vector3(-0.5f, 0, -0.5f), Vector3.UnitY, new Vector2(0, 1)),
            new(new Vector3(0.5f, 0, -0.5f), Vector3.UnitY, new Vector2(1, 1)),
            new(new Vector3(0.5f, 0, 0.5f), Vector3.UnitY, new Vector2(1, 0)),
            new(new Vector3(-0.5f, 0, 0.5f), Vector3.UnitY, new Vector2(0, 0)),
        ];

        // Wound so the geometric normal is +Y.
        return new Mesh("Plane", vertices, [0, 2, 1, 0, 3, 2]);
    }

    private static Mesh CreateSphere()
    {
        var vertices = new List<Vertex>((SphereRings + 1) * (SphereSegments + 1));

        for (var ring = 0; ring <= SphereRings; ring++)
        {
            var theta = MathF.PI * ring / SphereRings;
            var height = MathF.Cos(theta);
            var radial = MathF.Sin(theta);

            for (var segment = 0; segment <= SphereSegments; segment++)
            {
                var phi = MathF.Tau * segment / SphereSegments;
                var normal = new Vector3(radial * MathF.Cos(phi), height, radial * MathF.Sin(phi));

                vertices.Add(new Vertex(normal * SphereRadius, normal, Uv(ring, SphereRings, segment, SphereSegments)));
            }
        }

        var indices = new List<uint>(SphereRings * SphereSegments * 6);
        AppendSurface(indices, SphereRings, SphereSegments);

        return new Mesh("Sphere", vertices.ToArray(), indices.ToArray());
    }

    private static Mesh CreateCapsule()
    {
        // Revolved profile: a top hemisphere, a straight cylinder, then a bottom hemisphere. Carrying
        // the normal along in the profile avoids having to derive it from neighbouring rings.
        var profile = new List<(float Y, float Radial, float NormalY, float NormalRadial)>();
        var half = CapsuleSegment * 0.5f;

        for (var step = 0; step <= CapRings; step++)
        {
            var theta = MathF.PI * 0.5f * step / CapRings;
            profile.Add((half + CapsuleRadius * MathF.Cos(theta), CapsuleRadius * MathF.Sin(theta),
                MathF.Cos(theta), MathF.Sin(theta)));
        }

        profile.Add((-half, CapsuleRadius, 0f, 1f));

        for (var step = 1; step <= CapRings; step++)
        {
            var theta = MathF.PI * 0.5f * (1f + (float)step / CapRings);
            profile.Add((-half + CapsuleRadius * MathF.Cos(theta), CapsuleRadius * MathF.Sin(theta),
                MathF.Cos(theta), MathF.Sin(theta)));
        }

        var rings = profile.Count - 1;
        var vertices = new List<Vertex>((rings + 1) * (CapSegments + 1));

        for (var ring = 0; ring <= rings; ring++)
        {
            var (y, radial, normalY, normalRadial) = profile[ring];

            for (var segment = 0; segment <= CapSegments; segment++)
            {
                var phi = MathF.Tau * segment / CapSegments;
                var direction = new Vector3(MathF.Cos(phi), 0f, MathF.Sin(phi));

                var position = direction * radial + new Vector3(0f, y, 0f);
                var normal = direction * normalRadial + new Vector3(0f, normalY, 0f);

                vertices.Add(new Vertex(position, Vector3.Normalize(normal), Uv(ring, rings, segment, CapSegments)));
            }
        }

        var indices = new List<uint>(rings * CapSegments * 6);
        AppendSurface(indices, rings, CapSegments);

        return new Mesh("Capsule", vertices.ToArray(), indices.ToArray());
    }

    /// <summary>Triangles for a grid of quads, wound counter-clockwise seen from outside.</summary>
    /// <remarks>The first and last rings are the poles of a revolved surface, so half of their quads collapse.</remarks>
    private static void AppendSurface(List<uint> indices, int rings, int segments)
    {
        for (var ring = 0; ring < rings; ring++)
        {
            for (var segment = 0; segment < segments; segment++)
            {
                var top = (uint)(ring * (segments + 1) + segment);
                var bottom = top + (uint)(segments + 1);

                if (ring != 0)
                    indices.AddRange([top, top + 1, bottom]);

                if (ring != rings - 1)
                    indices.AddRange([bottom, top + 1, bottom + 1]);
            }
        }
    }

    private static Vector2 Uv(int ring, int rings, int segment, int segments) =>
        new((float)segment / segments, (float)ring / rings);
}
