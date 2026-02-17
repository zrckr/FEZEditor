using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;

namespace FezEditor.Structure;

public static class TrixelMesher
{
    public readonly struct TrixelRect
    {
        public readonly FaceOrientation Face;
        public readonly int Depth; // trixel units along normal axis
        public readonly int U, V; // origin in tangent/bitangent space
        public readonly int W, H; // tangent and bitangent size

        public TrixelRect(FaceOrientation face, int depth, int u, int v, int w, int h)
        {
            Face = face;
            Depth = depth;
            U = u;
            V = v;
            W = w;
            H = h;
        }
    }

    public static (VertexInstance[] vertices, ushort[] indices) ToGeometry(TrixelObject obj)
    {
        // VertexGroup equivalent using VertexInstance equality (Position + NormalByte + TexCoord)
        var vertexMap = new Dictionary<VertexInstance, int>();
        var vertexList = new List<VertexInstance>();
        var indexList = new List<int>();

        int AddVertex(VertexInstance v)
        {
            if (!vertexMap.TryGetValue(v, out var idx))
            {
                idx = vertexList.Count;
                vertexMap[v] = idx;
                vertexList.Add(v);
            }

            return idx;
        }

        var rects = Build(obj);
        foreach (var rect in rects)
        {
            var normal = rect.Face.AsVector();
            var tangent = rect.Face.GetTangent().AsVector();
            var bitangent = rect.Face.GetBitangent().AsVector();

            var origin = (rect.U * tangent + rect.V * bitangent + rect.Depth * normal) / 16f;
            if (rect.Face.IsPositive())
                origin += normal / 16f;
            origin -= obj.Size / 2f;

            var t = tangent * rect.W / 16f;
            var b = bitangent * rect.H / 16f;

            Vector3[] pos = { origin, origin + t, origin + t + b, origin + b };

            var verts = new int[4];
            for (var i = 0; i < 4; i++)
            {
                var v = new VertexInstance
                {
                    Position = pos[i].ToRepacker(),
                    Normal = normal.ToRepacker(),
                    TextureCoordinate = ComputeTexCoord(pos[i], rect.Face, obj.Size).ToRepacker()
                };
                verts[i] = AddVertex(v);
            }

            // Winding: Front/Top/Right = CW, others = CCW (matches FaceMaterialization.SetupIndices)
            if (rect.Face == FaceOrientation.Front ||
                rect.Face == FaceOrientation.Top ||
                rect.Face == FaceOrientation.Right)
            {
                indexList.AddRange(new[] { verts[0], verts[1], verts[2], verts[0], verts[2], verts[3] });
            }
            else
            {
                indexList.AddRange(new[] { verts[0], verts[2], verts[1], verts[0], verts[3], verts[2] });
            }
        }

        return (vertexList.ToArray(), indexList.Select(i => (ushort)i).ToArray());
    }

    private static Vector2 ComputeTexCoord(Vector3 position, FaceOrientation face, Vector3 size)
    {
        var normal = face.AsVector();
        var p = ((Vector3.One - normal.Abs()) * (position / size) * 2f + normal) / 2f + new Vector3(0.5f);

        var u = Vector3.Dot(face.RightVector(), p);
        var v = Vector3.Dot(face.UpVector(), p);

        if (face != FaceOrientation.Top)
            v = 1f - v;

        // Original atlas: each column is 1/8 wide, offsets chosen so negative-U faces land correctly.
        // For 6-column [0,1] atlas: each column is 1/6 wide, u must be in [0,1].
        // Faces where RightVector is negative (Back, Right) produce u in [-1, 0] — shift to [0, 1].
        if (u < 0f) u += 1f;

        float colStart = face switch
        {
            FaceOrientation.Front => 0f,
            FaceOrientation.Right => 1f / 6f,
            FaceOrientation.Back  => 2f / 6f,
            FaceOrientation.Left  => 3f / 6f,
            FaceOrientation.Top   => 4f / 6f,
            FaceOrientation.Down  => 5f / 6f,
            _ => 0f
        };

        return new Vector2(colStart + u / 6f, v);
    }

    private static List<TrixelRect> Build(TrixelObject obj)
    {
        var result = new List<TrixelRect>();
        var w = obj.Width;
        var h = obj.Height;
        var d = obj.Depth;

        foreach (FaceOrientation face in Enum.GetValues<FaceOrientation>())
        {
            // Collect visible trixels grouped by depth
            var byDepth = new Dictionary<int, HashSet<(int u, int v)>>();

            void Add(int depth, int u, int v)
            {
                if (!byDepth.TryGetValue(depth, out var set))
                    byDepth[depth] = set = new HashSet<(int, int)>();
                set.Add((u, v));
            }

            var tangent = face.GetTangent().AsVector();
            var bitangent = face.GetBitangent().AsVector();
            var normal = face.AsVector();

            if (face.IsPositive())
            {
                // Outer shell: positive face, fixed depth = max-1
                var (fixedDepth, _, _) = Project(
                    face == FaceOrientation.Right ? w - 1 : 0,
                    face == FaceOrientation.Top ? h - 1 : 0,
                    face == FaceOrientation.Front ? d - 1 : 0);

                for (var x = 0; x < w; x++)
                for (var y = 0; y < h; y++)
                for (var z = 0; z < d; z++)
                {
                    var (depth, u, v) = Project(x, y, z);
                    if (depth == fixedDepth && !obj.IsMissing(new Vector3I(x, y, z)))
                        Add(fixedDepth, u, v);
                }

                // Interior faces exposed by missing trixels (neighbor toward negative)
                var step = -normal;
                for (var x = 0; x < w; x++)
                for (var y = 0; y < h; y++)
                for (var z = 0; z < d; z++)
                {
                    if (!obj.IsMissing(new Vector3I(x, y, z))) continue;
                    var nx = x + (int)step.X;
                    var ny = y + (int)step.Y;
                    var nz = z + (int)step.Z;
                    if (nx < 0 || ny < 0 || nz < 0 || nx >= w || ny >= h || nz >= d) continue;
                    if (obj.IsMissing(new Vector3I(nx, ny, nz))) continue;
                    var (depth, u, v) = Project(nx, ny, nz);
                    if (depth != fixedDepth) // interior only
                        Add(depth, u, v);
                }
            }
            else
            {
                // Outer shell: negative face, fixed depth = 0
                var (fixedDepth, _, _) = Project(0, 0, 0);

                for (var x = 0; x < w; x++)
                for (var y = 0; y < h; y++)
                for (var z = 0; z < d; z++)
                {
                    var (depth, u, v) = Project(x, y, z);
                    if (depth == fixedDepth && !obj.IsMissing(new Vector3I(x, y, z)))
                        Add(fixedDepth, u, v);
                }

                var step = normal; // toward positive = away from this face
                for (var x = 0; x < w; x++)
                for (var y = 0; y < h; y++)
                for (var z = 0; z < d; z++)
                {
                    if (!obj.IsMissing(new Vector3I(x, y, z))) continue;
                    var nx = x + (int)step.X;
                    var ny = y + (int)step.Y;
                    var nz = z + (int)step.Z;
                    if (nx < 0 || ny < 0 || nz < 0 || nx >= w || ny >= h || nz >= d) continue;
                    if (obj.IsMissing(new Vector3I(nx, ny, nz))) continue;
                    var (depth, u, v) = Project(nx, ny, nz);
                    if (depth != fixedDepth)
                        Add(depth, u, v);
                }
            }

            foreach (var (depth, trixels) in byDepth)
                GreedyMesh(face, depth, trixels, result);

            // Helper: convert (x,y,z) to (depth, u, v) for this face
            (int depth, int u, int v) Project(int x, int y, int z)
            {
                var p = new Vector3(x, y, z);
                return (
                    (int)Vector3.Dot(p, normal),
                    (int)Vector3.Dot(p, tangent),
                    (int)Vector3.Dot(p, bitangent)
                );
            }
        }

        return result;
    }

    private static void GreedyMesh(FaceOrientation face, int depth,
        HashSet<(int u, int v)> trixels, List<TrixelRect> result)
    {
        var queue = new Queue<HashSet<(int u, int v)>>();
        queue.Enqueue(new HashSet<(int, int)>(trixels));

        while (queue.Count > 0)
        {
            var component = queue.Dequeue();
            if (component.Count == 0) continue;

            // Centroid seed
            var sumU = 0;
            var sumV = 0;
            foreach (var (u, v) in component)
            {
                sumU += u;
                sumV += v;
            }

            var centerU = sumU / component.Count;
            var centerV = sumV / component.Count;
            var seed = FindNearest(centerU, centerV, component);

            // Spiral walk from seed until miss
            var spiralCells = SpiralWalk(seed.u, seed.v, component);

            // Clamp to n² or n²+n
            var clamped = ClampToRectangleSpiral(spiralCells.Count);
            if (clamped < spiralCells.Count)
                spiralCells.RemoveRange(clamped, spiralCells.Count - clamped);

            // Derive initial rectangle from spiral count
            var rect = GetRectangleSpiralLimits(clamped);

            // Expand in 4 directions
            if (spiralCells.Count < component.Count)
            {
                ExpandSide(ref rect, seed.u, seed.v, component, spiralCells, tangent: true, sign: +1);
                ExpandSide(ref rect, seed.u, seed.v, component, spiralCells, tangent: true, sign: -1);
                ExpandSide(ref rect, seed.u, seed.v, component, spiralCells, tangent: false, sign: +1);
                ExpandSide(ref rect, seed.u, seed.v, component, spiralCells, tangent: false, sign: -1);
            }

            result.Add(new TrixelRect(face, depth,
                seed.u + rect.X, seed.v + rect.Y,
                rect.Width, rect.Height));

            // Remove covered
            foreach (var cell in spiralCells)
                component.Remove(cell);

            // Flood-fill remaining into connected components
            while (component.Count > 0)
            {
                var origin = GetFirst(component);
                var sub = FloodFill(origin, component);
                queue.Enqueue(sub);
                if (sub.Count == component.Count)
                    component.Clear();
                else
                    component.ExceptWith(sub);
            }
        }
    }

    private static (int u, int v) FindNearest(int cu, int cv, HashSet<(int, int)> set)
    {
        if (set.Contains((cu, cv))) return (cu, cv);

        // Spiral outward until found
        int u = cu, v = cv;
        int n = 1, n2 = 0, n3 = 1, n4 = 0, n5 = 1, n6 = -1;
        while (true)
        {
            if (n3 > 0)
            {
                u += n5;
                if (--n3 == 0)
                {
                    n6 *= -1;
                    n4 = ++n2;
                }
            }
            else if (n4 > 0)
            {
                v += n6;
                if (--n4 == 0)
                {
                    n5 *= -1;
                    n3 = ++n;
                }
            }

            if (set.Contains((u, v))) return (u, v);
        }
    }

    private static List<(int u, int v)> SpiralWalk(int startU, int startV, HashSet<(int, int)> set)
    {
        var list = new List<(int, int)>();
        int u = startU, v = startV;
        int n = 1, n2 = 0, n3 = 1, n4 = 0, n5 = 1, n6 = -1;
        do
        {
            list.Add((u, v));
            if (n3 > 0)
            {
                u += n5;
                if (--n3 == 0)
                {
                    n6 *= -1;
                    n4 = ++n2;
                }
            }
            else if (n4 > 0)
            {
                v += n6;
                if (--n4 == 0)
                {
                    n5 *= -1;
                    n3 = ++n;
                }
            }
        } while (set.Contains((u, v)));

        return list;
    }

    private static int ClampToRectangleSpiral(int count)
    {
        var n = (int)Math.Floor(Math.Sqrt(count));
        var n2 = n * n;
        var n3 = n2 + n;
        return n3 >= count ? n2 : n3;
    }

    private static Rectangle GetRectangleSpiralLimits(int count)
    {
        var sq = Math.Sqrt(count);
        var n = (int)Math.Floor(sq);
        var x2 = (int)Math.Floor(sq / 2.0) + 1;
        var x1 = (int)Math.Ceiling(-(sq - 1.0) / 2.0);
        var y2 = x2;
        var y1 = x1;
        if (n != sq)
        {
            if (sq % 2.0 == 0.0) x1--;
            else x2++;
        }

        return new Rectangle(x1, y1, x2 - x1, y2 - y1);
    }

    private static void ExpandSide(ref Rectangle rect, int cu, int cv,
        HashSet<(int, int)> set, List<(int u, int v)> covered,
        bool tangent, int sign)
    {
        // Starting edge corner in component-relative space
        int eu = cu + rect.X + (tangent && sign > 0 ? rect.Width - 1 : 0);
        int ev = cv + rect.Y + (!tangent && sign > 0 ? rect.Height - 1 : 0);
        int lineLen = tangent ? rect.Height : rect.Width;

        bool expanded;
        do
        {
            if (tangent) eu += sign;
            else ev += sign;

            int su = eu, sv = ev;
            var added = new List<(int, int)>();
            bool full = true;
            for (var i = 0; i < lineLen; i++)
            {
                if (!set.Contains((su, sv)))
                {
                    full = false;
                    break;
                }

                added.Add((su, sv));
                if (tangent) sv++;
                else su++;
            }

            expanded = full;
            if (full)
            {
                covered.AddRange(added);
                if (tangent)
                {
                    if (sign < 0) rect.X--;
                    rect.Width++;
                }
                else
                {
                    if (sign < 0) rect.Y--;
                    rect.Height++;
                }
            }
        } while (expanded);
    }

    private static HashSet<(int u, int v)> FloodFill((int u, int v) origin, HashSet<(int, int)> set)
    {
        var visited = new HashSet<(int, int)> { origin };
        var queue = new Queue<(int, int)>();
        queue.Enqueue(origin);
        while (queue.Count > 0)
        {
            var (u, v) = queue.Dequeue();
            foreach (var nb in new[] { (u + 1, v), (u - 1, v), (u, v + 1), (u, v - 1) })
            {
                if (!visited.Contains(nb) && set.Contains(nb))
                {
                    visited.Add(nb);
                    queue.Enqueue(nb);
                }
            }
        }

        return visited;
    }

    private static (int u, int v) GetFirst(HashSet<(int, int)> set)
    {
        foreach (var item in set) return item;
        throw new InvalidOperationException();
    }
}