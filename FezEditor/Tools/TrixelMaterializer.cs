using FezEditor.Structure;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using Microsoft.Xna.Framework;

namespace FezEditor.Tools;

public static class TrixelMaterializer
{
    #region Mesh -> Trixels

    public static TrixelObject Materialize(ArtObject ao)
    {
        return ReconstructGeometry(ao.Size.ToXna(), ao.Geometry.Vertices, ao.Geometry.Indices);
    }

    public static TrixelObject Materialize(Trile trile)
    {
        return ReconstructGeometry(trile.Size.ToXna(), trile.Geometry.Vertices, trile.Geometry.Indices);
    }

    /// <summary>
    /// Reconstructs a TrixelObject from an optimized GPU mesh produced by Trile or ArtObject Materializer.
    /// Inverts the forward materialization path: GPU quads -> RectangularTrixelSurfaceParts -> trixel emplacements.
    /// Any trixel not covered by a surface face is marked as missing (subtractive storage).
    /// </summary>
    private static TrixelObject ReconstructGeometry(Vector3 size, VertexInstance[] vertices, ushort[] indices)
    {
        // Step 1: Extract surface trixels from mesh quads
        var surface = new HashSet<Vector3I>();
        var offset = size / 2f;

        // Each rectangle emits 6 indices (2 triangles). Process one quad at a time.
        for (var i = 0; i < indices.Length; i += 6)
        {
            // Deduplicate the 6 indexed vertices down to 4 unique corners
            var unique = new List<Vector3>();
            var normal = Vector3.Zero;

            for (var j = 0; j < 6; j++)
            {
                var v = vertices[indices[i + j]];
                var pos = v.Position.ToXna();
                normal = v.Normal.ToXna();
                if (!unique.Any(u => Vector3.DistanceSquared(u, pos) < 0.0001f))
                {
                    unique.Add(pos);
                }
            }

            var orientation1 = FaceExtensions.OrientationFromDirection(normal);
            var tangentVec1 = orientation1.GetTangent().AsVector();
            var bitangentVec1 = orientation1.GetBitangent().AsVector();

            var v1 = unique.OrderBy(v => Vector3.Dot(v, tangentVec1))
                .ThenBy(v => Vector3.Dot(v, bitangentVec1))
                .First();

            var v3 = unique.OrderByDescending(v => Vector3.Dot(v, tangentVec1))
                .ThenByDescending(v => Vector3.Dot(v, bitangentVec1))
                .First();

            var (v0, v2, orientation) = (v0: v1, v2: v3, orientation: orientation1);
            var tangentVec = orientation.GetTangent().AsVector();
            var bitangentVec = orientation.GetBitangent().AsVector();
            var normalVec = orientation.AsVector();
            var isPositive = orientation.IsPositive();

            var startF = (v0 + offset - (isPositive ? 1 : 0) * normalVec / 16f) * 16f;
            var start = new Vector3I(
                (int)MathF.Round(startF.X),
                (int)MathF.Round(startF.Y),
                (int)MathF.Round(startF.Z)
            );

            var tw = (int)MathF.Round(MathF.Abs(Vector3.Dot(v2 - v0, tangentVec)) * 16f);
            var bh = (int)MathF.Round(MathF.Abs(Vector3.Dot(v2 - v0, bitangentVec)) * 16f);

            for (var t = 0; t < tw; t++)
            for (var b = 0; b < bh; b++)
            {
                var pos = start.ToVector3() + tangentVec * t + bitangentVec * b;
                surface.Add(new Vector3I(
                    (int)MathF.Round(pos.X),
                    (int)MathF.Round(pos.Y),
                    (int)MathF.Round(pos.Z)
                ));
            }
        }

        // Step 2: Flood-fill empty space from boundary inward
        // Any trixel NOT reached by the flood = present (either on surface or enclosed interior)
        var obj = new TrixelObject(size);
        var w = obj.Width;
        var h = obj.Height;
        var d = obj.Depth;
        var empty = new bool[w * h * d];
        var queue = new Queue<int>();

        void TrySeed(int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 || x >= w || y >= h || z >= d)
            {
                return;
            }

            var idx = x + y * w + z * w * h;
            if (empty[idx] || surface.Contains(new Vector3I(x, y, z)))
            {
                return;
            }

            empty[idx] = true;
            queue.Enqueue(idx);
        }

        // Seed from all 6 boundary faces
        for (var x = 0; x < w; x++)
        {
            for (var y = 0; y < h; y++)
            {
                TrySeed(x, y, 0);
                TrySeed(x, y, d - 1);
            }
        }

        for (var y = 0; y < h; y++)
        {
            for (var z = 0; z < d; z++)
            {
                TrySeed(0, y, z);
                TrySeed(w - 1, y, z);
            }
        }

        for (var x = 0; x < w; x++)
        {
            for (var z = 0; z < d; z++)
            {
                TrySeed(x, 0, z);
                TrySeed(x, h - 1, z);
            }
        }

        while (queue.Count > 0)
        {
            var idx = queue.Dequeue();
            var z = idx / (w * h);
            var y = (idx / w) % h;
            var x = idx % w;
            TrySeed(x + 1, y, z);
            TrySeed(x - 1, y, z);
            TrySeed(x, y + 1, z);
            TrySeed(x, y - 1, z);
            TrySeed(x, y, z + 1);
            TrySeed(x, y, z - 1);
        }

        // Step 3: Mark empty trixels as missing
        for (var x = 0; x < w; x++)
        {
            for (var y = 0; y < h; y++)
            {
                for (var z = 0; z < d; z++)
                {
                    obj.SetMissing(new Vector3I(x, y, z), empty[x + y * w + z * w * h]);
                }
            }
        }

        return obj;
    }

    #endregion
    
    #region Trixels -> Mesh

    public static ArtObject DematerializeToArtObject(TrixelObject obj)
    {
        var ao = new ArtObject { Size = obj.Size.ToRepacker() };
        (ao.Geometry.Vertices, ao.Geometry.Indices) = Dematerialize(obj);
        return ao;
    }

    public static Trile DematerializeToTrile(TrixelObject obj)
    {
        var trile = new Trile { Size = obj.Size.ToRepacker() };
        (trile.Geometry.Vertices, trile.Geometry.Indices) = Dematerialize(obj);
        return trile;
    }

    private static (VertexInstance[] Vertices, ushort[] Indices) Dematerialize(TrixelObject obj)
    {
        var rects = GreedyMesh(BuildVisibleFaces(obj));
        var offset = obj.Size / 2f;
        var texSize = obj.Size;
        var texScale = new Vector2(1.3333334f, 1f);

        // Vertex deduplication: Position+Normal pair shares same index
        var vertexMap = new Dictionary<(Vector3 vertex, Vector3 normal), ushort>();
        var vertices = new List<VertexInstance>();
        var indices = new List<ushort>();

        ushort AddVertexInstance(Vector3 vertex, Vector3 normal, FaceOrientation face)
        {
            vertex = vertex.Round(4);
            var key = (vertex, normal);
            if (vertexMap.TryGetValue(key, out var idx))
            {
                return idx;
            }
            
            idx = (ushort)vertices.Count;
            vertexMap[key] = idx;

            var texCoord = ComputeTexCoord(vertex, normal, texSize, face) * texScale;
            vertices.Add(new VertexInstance
            {
                Position = vertex.ToRepacker(),
                Normal = normal.ToRepacker(),
                TextureCoordinate = texCoord.ToRepacker()
            });

            return idx;
        }

        foreach (var rect in rects)
        {
            var normalVec = rect.Orientation.AsVector();
            var tangentVec = rect.Orientation.GetTangent().AsVector();
            var bitangentVec = rect.Orientation.GetBitangent().AsVector();
            var isPositive = rect.Orientation >= FaceOrientation.Right;

            // Reconstruct Start.Position from grid coordinates
            var startPos = tangentVec * rect.StartTangent
                           + bitangentVec * rect.StartBitangent
                           + normalVec * rect.Depth;

            // Vertex formula — mirrors TrileMaterializer/ArtObjectMaterializer
            var v0 = startPos / 16f + (isPositive ? 1 : 0) * normalVec / 16f - offset;
            var v1 = v0 + tangentVec * rect.TangentSize / 16f;
            var v2 = v1 + bitangentVec * rect.BitangentSize / 16f;
            var v3 = v0 + bitangentVec * rect.BitangentSize / 16f;

            var i0 = AddVertexInstance(v0, normalVec, rect.Orientation);
            var i1 = AddVertexInstance(v1, normalVec, rect.Orientation);
            var i2 = AddVertexInstance(v2, normalVec, rect.Orientation);
            var i3 = AddVertexInstance(v3, normalVec, rect.Orientation);

            indices.Add(i0);
            if (isPositive)
            {
                indices.Add(i1);
                indices.Add(i2);
                indices.Add(i0);
                indices.Add(i2);
                indices.Add(i3);
            }
            else
            {
                indices.Add(i2);
                indices.Add(i1);
                indices.Add(i0);
                indices.Add(i3);
                indices.Add(i2);
            }
        }

        return (vertices.ToArray(), indices.ToArray());
    }
    
    private static List<MeshRect> GreedyMesh(IEnumerable<TrixelFace> faces)
    {
        var groups = new Dictionary<(FaceOrientation, int), List<(int t, int b)>>();
        foreach (var face in faces)
        {
            var orient = face.Face;
            var normal = orient.AsVector();
            var tangent = orient.GetTangent().AsVector();
            var bitangent = orient.GetBitangent().AsVector();
            var pos = face.Emplacement.ToVector3();
            var key = (orient, (int)Vector3.Dot(pos, normal));

            if (!groups.TryGetValue(key, out var list))
            {
                groups[key] = list = new List<(int, int)>();
            }

            list.Add(((int)Vector3.Dot(pos, tangent), (int)Vector3.Dot(pos, bitangent)));
        }

        var rects = new List<MeshRect>();
        foreach (var ((orient, depth), cells) in groups)
        {
            var minT = int.MaxValue;
            var maxT = int.MinValue;
            var minB = int.MaxValue;
            var maxB = int.MinValue;
            foreach (var (t, b) in cells)
            {
                if (t < minT)
                {
                    minT = t;
                }

                if (t > maxT)
                {
                    maxT = t;
                }

                if (b < minB)
                {
                    minB = b;
                }

                if (b > maxB)
                {
                    maxB = b;
                }
            }

            var w = maxT - minT + 1;
            var h = maxB - minB + 1;
            var grid = new bool[w * h];
            foreach (var (t, b) in cells)
            {
                grid[(t - minT) + (b - minB) * w] = true;
            }

            for (var bIdx = 0; bIdx < h; bIdx++)
            for (var tIdx = 0; tIdx < w; tIdx++)
            {
                if (!grid[tIdx + bIdx * w])
                {
                    continue;
                }

                // Extend in tangent direction
                var tw = 1;
                while (tIdx + tw < w && grid[tIdx + tw + bIdx * w])
                {
                    tw++;
                }

                // Extend in bitangent direction (full width must match)
                var bh = 1;
                while (bIdx + bh < h)
                {
                    var ok = true;
                    for (var tt = 0; tt < tw; tt++)
                    {
                        if (!grid[(tIdx + tt) + (bIdx + bh) * w])
                        {
                            ok = false;
                            break;
                        }
                    }

                    if (!ok)
                    {
                        break;
                    }

                    bh++;
                }

                // Clear claimed cells
                for (var bb = 0; bb < bh; bb++)
                {
                    for (var tt = 0; tt < tw; tt++)
                    {
                        grid[(tIdx + tt) + (bIdx + bb) * w] = false;
                    }
                }

                rects.Add(new MeshRect(
                    Orientation: orient,
                    StartTangent: tIdx + minT,
                    StartBitangent: bIdx + minB, depth,
                    TangentSize: tw,
                    BitangentSize: bh));
            }
        }

        return rects;
    }
    
    private static Vector2 ComputeTexCoord(
        Vector3 position, 
        Vector3 normal, 
        Vector3 trileSize,
        FaceOrientation orientation)
    {
        var faceOffset = orientation switch
        {
            FaceOrientation.Front => new Vector2(0f, 0f),
            FaceOrientation.Right => new Vector2(0.25f, 0f),
            FaceOrientation.Back or FaceOrientation.Left => new Vector2(0.375f, 0f),
            FaceOrientation.Top => new Vector2(0.5f, 0f),
            _ => new Vector2(0.625f, 0f)
        };

        var projected = ((Vector3.One - normal.Abs()) * (position / trileSize) * 2f) + normal;
        projected = (projected / 2f) + new Vector3(0.5f);

        var u = Vector3.Dot(orientation.RightVector(), projected);
        var v = Vector3.Dot(orientation.UpVector(), projected);
        if (orientation != FaceOrientation.Top) v = 1f - v;

        return new Vector2(faceOffset.X + u / 8f, faceOffset.Y + v);
    }
    
    private record struct MeshRect(
        FaceOrientation Orientation,
        int StartTangent,
        int StartBitangent,
        int Depth,
        int TangentSize,
        int BitangentSize
    );

    #endregion

    #region Visualization

    /// <summary>
    /// Enumerates all visible trixel faces for rendering. A face is visible if the trixel exists
    /// (not missing) and is either on the object boundary or adjacent to a missing trixel.
    /// </summary>
    public static IEnumerable<TrixelFace> BuildVisibleFaces(TrixelObject obj)
    {
        var w = obj.Width;
        var h = obj.Height;
        var d = obj.Depth;

        // Boundary faces: trixels on the 6 outer surfaces of the bounding box.
        // These are always visible (no neighbor on the outside), so only check if trixel exists.

        // Front (z = d-1, normal = -Z) / Back (z = 0, normal = +Z)
        for (var x = 0; x < w; x++)
        {
            for (var y = 0; y < h; y++)
            {
                if (!obj.IsMissing(new Vector3I(x, y, d - 1)))
                {
                    yield return new TrixelFace(new Vector3I(x, y, d - 1), FaceOrientation.Front);
                }

                if (!obj.IsMissing(new Vector3I(x, y, 0)))
                {
                    yield return new TrixelFace(new Vector3I(x, y, 0), FaceOrientation.Back);
                }
            }
        }

        // Right (x = w-1, normal = +X) / Left (x = 0, normal = -X)
        for (var y = 0; y < h; y++)
        {
            for (var z = 0; z < d; z++)
            {
                if (!obj.IsMissing(new Vector3I(w - 1, y, z)))
                {
                    yield return new TrixelFace(new Vector3I(w - 1, y, z), FaceOrientation.Right);
                }

                if (!obj.IsMissing(new Vector3I(0, y, z)))
                {
                    yield return new TrixelFace(new Vector3I(0, y, z), FaceOrientation.Left);
                }
            }
        }

        // Top (y = h-1, normal = +Y) / Down (y = 0, normal = -Y)
        for (var x = 0; x < w; x++)
        {
            for (var z = 0; z < d; z++)
            {
                if (!obj.IsMissing(new Vector3I(x, h - 1, z)))
                {
                    yield return new TrixelFace(new Vector3I(x, h - 1, z), FaceOrientation.Top);
                }

                if (!obj.IsMissing(new Vector3I(x, 0, z)))
                {
                    yield return new TrixelFace(new Vector3I(x, 0, z), FaceOrientation.Down);
                }
            }
        }

        // Inner faces: exposed when a missing trixel is adjacent to an existing one.
        // For each missing trixel, check all 6 neighbors — if the neighbor exists and
        // is within bounds, emit a face on that neighbor pointing toward the void.
        // The face orientation is the direction FROM the neighbor TOWARD the missing trixel
        for (var x = 0; x < w; x++)
        {
            for (var y = 0; y < h; y++)
            {
                for (var z = 0; z < d; z++)
                {
                    var emplacement = new Vector3I(x, y, z);
                    if (!obj.IsMissing(emplacement))
                    {
                        continue;
                    }

                    if (z + 1 < d && !obj.IsMissing(new Vector3I(x, y, z + 1)))
                    {
                        yield return new TrixelFace(new Vector3I(x, y, z + 1), FaceOrientation.Back);
                    }

                    if (z - 1 >= 0 && !obj.IsMissing(new Vector3I(x, y, z - 1)))
                    {
                        yield return new TrixelFace(new Vector3I(x, y, z - 1), FaceOrientation.Front);
                    }

                    if (x + 1 < w && !obj.IsMissing(new Vector3I(x + 1, y, z)))
                    {
                        yield return new TrixelFace(new Vector3I(x + 1, y, z), FaceOrientation.Left);
                    }

                    if (x - 1 >= 0 && !obj.IsMissing(new Vector3I(x - 1, y, z)))
                    {
                        yield return new TrixelFace(new Vector3I(x - 1, y, z), FaceOrientation.Right);
                    }

                    if (y + 1 < h && !obj.IsMissing(new Vector3I(x, y + 1, z)))
                    {
                        yield return new TrixelFace(new Vector3I(x, y + 1, z), FaceOrientation.Down);
                    }

                    if (y - 1 >= 0 && !obj.IsMissing(new Vector3I(x, y - 1, z)))
                    {
                        yield return new TrixelFace(new Vector3I(x, y - 1, z), FaceOrientation.Top);
                    }
                }
            }
        }
    }

    #endregion
}