using System.Globalization;
using System.Numerics;
using EidolonCore.Rendering;

namespace EidolonCore.Assets;

/// <summary>
/// Minimal Wavefront OBJ reader: vertex positions, normals, texture coordinates and polygonal
/// faces.
/// </summary>
/// <remarks>
/// <para>
/// Faces may be triangles or convex polygons (fan-triangulated) and may use any of the
/// <c>v</c>, <c>v/vt</c>, <c>v//vn</c> or <c>v/vt/vn</c> corner forms, including negative
/// (relative) indices.
/// </para>
/// <para>
/// Not supported yet: material libraries (<c>mtllib</c>/<c>usemtl</c>), smoothing groups
/// (<c>s</c>), free-form geometry and line/point elements. When a face carries no normal one is
/// derived from its winding and shared per face, which gives flat shading; models that need
/// smooth normals should ship <c>vn</c> data.
/// </para>
/// </remarks>
public static class ObjLoader
{
    public static Mesh Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"OBJ file not found: {path}", path);

        using var reader = new StreamReader(path);
        return Parse(reader, Path.GetFileNameWithoutExtension(path));
    }

    public static Mesh Parse(TextReader reader, string name = "Mesh")
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var texCoords = new List<Vector2>();
        var triangles = new List<Corner[]>();

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            switch (parts[0])
            {
                case "v" when parts.Length >= 4:
                    positions.Add(new Vector3(ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3])));
                    break;

                case "vn" when parts.Length >= 4:
                    normals.Add(new Vector3(ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3])));
                    break;

                case "vt" when parts.Length >= 3:
                    texCoords.Add(new Vector2(ParseFloat(parts[1]), ParseFloat(parts[2])));
                    break;

                case "f" when parts.Length >= 4:
                    AddFace(parts, positions.Count, texCoords.Count, normals.Count, triangles);
                    break;
            }
        }

        if (positions.Count == 0)
            throw new InvalidDataException($"OBJ '{name}' contains no vertex positions.");

        if (triangles.Count == 0)
            throw new InvalidDataException($"OBJ '{name}' contains no faces.");

        return BuildMesh(name, positions, normals, texCoords, triangles);
    }

    private static Mesh BuildMesh(
        string name,
        List<Vector3> positions,
        List<Vector3> normals,
        List<Vector2> texCoords,
        List<Corner[]> triangles)
    {
        var vertices = new List<Vertex>(triangles.Count * 3);
        var indices = new List<uint>(triangles.Count * 3);
        var lookup = new Dictionary<VertexKey, uint>();

        for (var triangleIndex = 0; triangleIndex < triangles.Count; triangleIndex++)
        {
            var triangle = triangles[triangleIndex];

            var needsFaceNormal = triangle[0].Normal < 0 || triangle[1].Normal < 0 || triangle[2].Normal < 0;
            var faceNormal = needsFaceNormal
                ? CalculateFaceNormal(positions, triangle)
                : Vector3.UnitY;

            for (var cornerIndex = 0; cornerIndex < 3; cornerIndex++)
            {
                var corner = triangle[cornerIndex];

                var normal = corner.Normal >= 0 ? normals[corner.Normal] : faceNormal;
                var texCoord = corner.TexCoord >= 0 ? texCoords[corner.TexCoord] : Vector2.Zero;

                // Faces without their own normals get one set of vertices each, which is what
                // keeps their shading flat.
                var key = new VertexKey(
                    corner.Position,
                    corner.TexCoord,
                    corner.Normal >= 0 ? corner.Normal : -1 - triangleIndex);

                if (!lookup.TryGetValue(key, out var index))
                {
                    index = (uint)vertices.Count;
                    lookup[key] = index;
                    vertices.Add(new Vertex(positions[corner.Position], normal, texCoord));
                }

                indices.Add(index);
            }
        }

        return new Mesh(name, vertices.ToArray(), indices.ToArray());
    }

    private static Vector3 CalculateFaceNormal(List<Vector3> positions, Corner[] triangle)
    {
        var a = positions[triangle[0].Position];
        var b = positions[triangle[1].Position];
        var c = positions[triangle[2].Position];

        var normal = Vector3.Cross(b - a, c - a);
        return normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : Vector3.UnitY;
    }

    private static void AddFace(string[] parts, int positionCount, int texCoordCount, int normalCount,
        List<Corner[]> triangles)
    {
        var corners = new Corner[parts.Length - 1];
        for (var i = 1; i < parts.Length; i++)
            corners[i - 1] = ParseCorner(parts[i], positionCount, texCoordCount, normalCount);

        // Fan triangulation; OBJ faces are expected to be convex.
        for (var i = 1; i + 1 < corners.Length; i++)
            triangles.Add([corners[0], corners[i], corners[i + 1]]);
    }

    private static Corner ParseCorner(string token, int positionCount, int texCoordCount, int normalCount)
    {
        var segments = token.Split('/');

        var position = ResolveIndex(segments[0], positionCount, token);
        var texCoord = segments.Length > 1 ? ResolveIndex(segments[1], texCoordCount, token) : -1;
        var normal = segments.Length > 2 ? ResolveIndex(segments[2], normalCount, token) : -1;

        return new Corner(position, texCoord, normal);
    }

    private static int ResolveIndex(string token, int count, string corner)
    {
        if (string.IsNullOrEmpty(token))
            return -1;

        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw))
            throw new FormatException($"Invalid OBJ index '{token}' in face corner '{corner}'.");

        // OBJ indices are 1-based; negative ones are relative to the end of the current list.
        var index = raw > 0 ? raw - 1 : count + raw;

        if (index < 0 || index >= count)
            throw new InvalidDataException($"OBJ index {raw} in face corner '{corner}' is out of range.");

        return index;
    }

    private static float ParseFloat(string token) =>
        float.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);

    private readonly record struct Corner(int Position, int TexCoord, int Normal);

    private readonly record struct VertexKey(int Position, int TexCoord, int Normal);
}
