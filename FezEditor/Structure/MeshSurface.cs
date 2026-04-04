using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;

namespace FezEditor.Structure;

public class MeshSurface
{
    public required Vector3[] Vertices;
    public required int[] Indices;
    public Vector3[]? Normals;
    public Color[]? Colors;
    public Vector2[]? TexCoords;

    public static MeshSurface CreateTestTriangle()
    {
        return new MeshSurface
        {
            Vertices = new[]
            {
                new Vector3(0.0f, 0.5f, 0f), // top
                new Vector3(0.5f, -0.5f, 0f), // bottom-right
                new Vector3(-0.5f, -0.5f, 0f) // bottom-left
            },
            Indices = new[] { 0, 1, 2 },
            Colors = new[]
            {
                Color.Red,
                Color.Green,
                Color.Blue
            }
        };
    }

    public static MeshSurface CreateBox(Vector3 size)
    {
        size /= 2f;
        return new MeshSurface
        {
            Vertices = new[]
            {
                new Vector3(-1f, -1f, -1f) * size,
                new Vector3(-1f, 1f, -1f) * size,
                new Vector3(1f, 1f, -1f) * size,
                new Vector3(1f, -1f, -1f) * size,
                new Vector3(1f, -1f, -1f) * size,
                new Vector3(1f, 1f, -1f) * size,
                new Vector3(1f, 1f, 1f) * size,
                new Vector3(1f, -1f, 1f) * size,
                new Vector3(1f, -1f, 1f) * size,
                new Vector3(1f, 1f, 1f) * size,
                new Vector3(-1f, 1f, 1f) * size,
                new Vector3(-1f, -1f, 1f) * size,
                new Vector3(-1f, -1f, 1f) * size,
                new Vector3(-1f, 1f, 1f) * size,
                new Vector3(-1f, 1f, -1f) * size,
                new Vector3(-1f, -1f, -1f) * size,
                new Vector3(-1f, -1f, -1f) * size,
                new Vector3(-1f, -1f, 1f) * size,
                new Vector3(1f, -1f, 1f) * size,
                new Vector3(1f, -1f, -1f) * size,
                new Vector3(-1f, 1f, -1f) * size,
                new Vector3(-1f, 1f, 1f) * size,
                new Vector3(1f, 1f, 1f) * size,
                new Vector3(1f, 1f, -1f) * size
            },
            Normals = new[]
            {
                -Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitZ,
                Vector3.UnitX, Vector3.UnitX, Vector3.UnitX, Vector3.UnitX,
                Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ,
                -Vector3.UnitX, -Vector3.UnitX, -Vector3.UnitX, -Vector3.UnitX,
                -Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY,
                Vector3.UnitY, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY
            },
            Indices = new[]
            {
                0, 2, 1, 0, 3, 2, 4, 6, 5,
                4, 7, 6, 8, 10, 9, 8, 11, 10,
                12, 14, 13, 12, 15, 14, 16, 17, 18,
                16, 18, 19, 20, 22, 21, 20, 23, 22
            }
        };
    }

    public static MeshSurface CreateTexturedBox(Vector3 size)
    {
        var half = size / 2f;
        return new MeshSurface
        {
            Vertices = new[]
            {
                new Vector3(-1f, -1f, -1f) * half,
                new Vector3(-1f, 1f, -1f) * half,
                new Vector3(1f, 1f, -1f) * half,
                new Vector3(1f, -1f, -1f) * half,
                new Vector3(1f, -1f, -1f) * half,
                new Vector3(1f, 1f, -1f) * half,
                new Vector3(1f, 1f, 1f) * half,
                new Vector3(1f, -1f, 1f) * half,
                new Vector3(1f, -1f, 1f) * half,
                new Vector3(1f, 1f, 1f) * half,
                new Vector3(-1f, 1f, 1f) * half,
                new Vector3(-1f, -1f, 1f) * half,
                new Vector3(-1f, -1f, 1f) * half,
                new Vector3(-1f, 1f, 1f) * half,
                new Vector3(-1f, 1f, -1f) * half,
                new Vector3(-1f, -1f, -1f) * half,
                new Vector3(-1f, -1f, -1f) * half,
                new Vector3(-1f, -1f, 1f) * half,
                new Vector3(1f, -1f, 1f) * half,
                new Vector3(1f, -1f, -1f) * half,
                new Vector3(-1f, 1f, -1f) * half,
                new Vector3(-1f, 1f, 1f) * half,
                new Vector3(1f, 1f, 1f) * half,
                new Vector3(1f, 1f, -1f) * half
            },
            Normals = new[]
            {
                -Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitZ,
                Vector3.UnitX, Vector3.UnitX, Vector3.UnitX, Vector3.UnitX,
                Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ,
                -Vector3.UnitX, -Vector3.UnitX, -Vector3.UnitX, -Vector3.UnitX,
                -Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY, -Vector3.UnitY,
                Vector3.UnitY, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY
            },
            TexCoords = new[]
            {
                new Vector2(0, size.Y), new Vector2(0, 0), new Vector2(size.X, 0), new Vector2(size.X, size.Y),
                new Vector2(0, size.Y), new Vector2(0, 0), new Vector2(size.Z, 0), new Vector2(size.Z, size.Y),
                new Vector2(0, size.Y), new Vector2(0, 0), new Vector2(size.X, 0), new Vector2(size.X, size.Y),
                new Vector2(0, size.Y), new Vector2(0, 0), new Vector2(size.Z, 0), new Vector2(size.Z, size.Y),
                new Vector2(0, size.Z), new Vector2(0, 0), new Vector2(size.X, 0), new Vector2(size.X, size.Z),
                new Vector2(0, size.Z), new Vector2(0, 0), new Vector2(size.X, 0), new Vector2(size.X, size.Z)
            },
            Indices = new[]
            {
                0, 2, 1, 0, 3, 2, 4, 6, 5,
                4, 7, 6, 8, 10, 9, 8, 11, 10,
                12, 14, 13, 12, 15, 14, 16, 17, 18,
                16, 18, 19, 20, 22, 21, 20, 23, 22
            }
        };
    }

    public static MeshSurface CreateColoredBox(Vector3 size, Color color)
    {
        size /= 2f;
        return new MeshSurface
        {
            Vertices = new[]
            {
                (new Vector3(-1f, -1f, -1f) * size),
                (new Vector3(1f, -1f, -1f) * size),
                (new Vector3(1f, 1f, -1f) * size),
                (new Vector3(-1f, 1f, -1f) * size),
                (new Vector3(-1f, -1f, 1f) * size),
                (new Vector3(1f, -1f, 1f) * size),
                (new Vector3(1f, 1f, 1f) * size),
                (new Vector3(-1f, 1f, 1f) * size)
            },
            Colors = new[]
            {
                color, color, color, color,
                color, color, color, color
            },
            Indices = new[]
            {
                0, 1, 2, 0, 2, 3, 1, 5, 6,
                1, 6, 2, 0, 7, 4, 0, 3, 7,
                3, 2, 6, 3, 6, 7, 4, 6, 5,
                4, 7, 6, 0, 5, 1, 0, 4, 5
            }
        };
    }

    public static MeshSurface CreateQuad(Vector3 size, Vector3? origin = null)
    {
        size /= 2f;
        origin ??= Vector3.Zero;
        return new MeshSurface
        {
            Vertices = new[]
            {
                new Vector3(-size.X, -size.Y, 0) + origin.Value,
                new Vector3(size.X, -size.Y, 0) + origin.Value,
                new Vector3(-size.X, size.Y, 0) + origin.Value,
                new Vector3(size.X, size.Y, 0) + origin.Value
            },
            Normals = new[]
            {
                Vector3.Forward,
                Vector3.Forward,
                Vector3.Forward,
                Vector3.Forward
            },
            TexCoords = new[]
            {
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(0, 0),
                new Vector2(1, 0)
            },
            Indices = new[]
            {
                0, 1, 2,
                2, 1, 3
            }
        };
    }

    public static MeshSurface CreateFaceQuad(Vector3 size, Vector3 origin, FaceOrientation face)
    {
        var normal = face.AsVector();
        var right = face.RightVector();
        var up = face.UpVector();

        // Half-extents along each axis
        var hr = right * size / 2f;
        var hu = up * size / 2f;

        return new MeshSurface
        {
            Vertices = new[]
            {
                origin - hr - hu, // bottom-left
                origin + hr - hu, // bottom-right
                origin - hr + hu, // top-left
                origin + hr + hu // top-right
            },
            Normals = new[] { normal, normal, normal, normal },
            TexCoords = new[]
            {
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(0, 0),
                new Vector2(1, 0)
            },
            Indices = new[]
            {
                0, 1, 2,
                2, 1, 3
            }
        };
    }

    public static MeshSurface CreateFaceQuad(Vector3 size, FaceOrientation face)
    {
        var normal = face.AsVector();
        var right = face.RightVector();
        var up = face.UpVector();
        var center = normal * size / 2f;

        // Half-extents along each axis
        var hr = right * size / 2f;
        var hu = up * size / 2f;

        return new MeshSurface
        {
            Vertices = new[]
            {
                center - hr - hu, // bottom-left
                center + hr - hu, // bottom-right
                center - hr + hu, // top-left
                center + hr + hu // top-right
            },
            Normals = new[] { normal, normal, normal, normal },
            TexCoords = new[]
            {
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(0, 0),
                new Vector2(1, 0)
            },
            Indices = new[]
            {
                0, 1, 2,
                2, 1, 3
            }
        };
    }

    // Arrow: shaft (cylinder) + cone tip along +Y, rotated to point along axis.
    // sides = number of lathe segments (use 8 for translate, 4 for scale).
    public static MeshSurface CreateArrow(Vector3 axis, int sides, float shaftLength, float shaftRadius, float tipLength, float tipRadius)
    {
        axis = Vector3.Normalize(axis);

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<int>();

        // Build rotation from +Y to axis
        var up = Vector3.UnitY;
        Quaternion rotation;
        var dot = Vector3.Dot(up, axis);
        if (dot > 0.9999f)
        {
            rotation = Quaternion.Identity;
        }
        else if (dot < -0.9999f)
        {
            rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI);
        }
        else
        {
            var cross = Vector3.Cross(up, axis);
            rotation = Quaternion.CreateFromAxisAngle(Vector3.Normalize(cross), MathF.Acos(dot));
        }

        // Shaft (cylinder) - no caps, open at both ends
        for (var i = 0; i < sides; i++)
        {
            var angle = 2f * MathF.PI * i / sides;
            var cos = MathF.Cos(angle);
            var sin = MathF.Sin(angle);
            var normal = Vector3.Transform(new Vector3(cos, 0, sin), rotation);
            vertices.Add(Vector3.Transform(new Vector3(cos * shaftRadius, 0f, sin * shaftRadius), rotation));
            normals.Add(normal);
            vertices.Add(Vector3.Transform(new Vector3(cos * shaftRadius, shaftLength, sin * shaftRadius), rotation));
            normals.Add(normal);
        }

        for (var i = 0; i < sides; i++)
        {
            var a = i * 2;
            var b = i * 2 + 1;
            var c = ((i + 1) % sides) * 2;
            var d = ((i + 1) % sides) * 2 + 1;
            indices.Add(a); indices.Add(b); indices.Add(c);
            indices.Add(b); indices.Add(d); indices.Add(c);
        }

        // Cone base disc (fill the opening between shaft and cone)
        var coneBaseStart = vertices.Count;
        var coneBaseCenter = Vector3.Transform(new Vector3(0, shaftLength, 0), rotation);
        var downNormal = Vector3.Transform(-Vector3.UnitY, rotation);
        vertices.Add(coneBaseCenter);
        normals.Add(downNormal);
        for (var i = 0; i < sides; i++)
        {
            var angle = 2f * MathF.PI * i / sides;
            vertices.Add(Vector3.Transform(new Vector3(MathF.Cos(angle) * tipRadius, shaftLength, MathF.Sin(angle) * tipRadius), rotation));
            normals.Add(downNormal);
        }
        for (var i = 0; i < sides; i++)
        {
            var a = coneBaseStart + 1 + i;
            var b = coneBaseStart + 1 + (i + 1) % sides;
            indices.Add(coneBaseStart); indices.Add(b); indices.Add(a);
        }

        // Cone surface
        var coneTip = Vector3.Transform(new Vector3(0, shaftLength + tipLength, 0), rotation);
        for (var i = 0; i < sides; i++)
        {
            var a0 = 2f * MathF.PI * i / sides;
            var a1 = 2f * MathF.PI * (i + 1) / sides;
            var v0 = Vector3.Transform(new Vector3(MathF.Cos(a0) * tipRadius, shaftLength, MathF.Sin(a0) * tipRadius), rotation);
            var v1 = Vector3.Transform(new Vector3(MathF.Cos(a1) * tipRadius, shaftLength, MathF.Sin(a1) * tipRadius), rotation);

            // Cone normal: perpendicular to slant edge
            var slant0 = Vector3.Normalize(Vector3.Transform(new Vector3(MathF.Cos(a0), tipRadius / tipLength, MathF.Sin(a0)), rotation));
            var slant1 = Vector3.Normalize(Vector3.Transform(new Vector3(MathF.Cos(a1), tipRadius / tipLength, MathF.Sin(a1)), rotation));
            var tipNormal = Vector3.Normalize((slant0 + slant1) / 2f);

            var baseIdx = vertices.Count;
            vertices.Add(v0); normals.Add(slant0);
            vertices.Add(v1); normals.Add(slant1);
            vertices.Add(coneTip); normals.Add(tipNormal);
            indices.Add(baseIdx); indices.Add(baseIdx + 1); indices.Add(baseIdx + 2);
        }

        return new MeshSurface
        {
            Vertices = vertices.ToArray(),
            Normals = normals.ToArray(),
            Indices = indices.ToArray()
        };
    }

    // Ring (torus) around axis. segments = number of circle steps, crossSections = tube cross-section verts.
    public static MeshSurface CreateRing(Vector3 axis, float radius, int segments, int crossSections, float tubeRadius)
    {
        axis = Vector3.Normalize(axis);

        // Build two perpendicular vectors to axis for the ring plane
        var tangent = MathF.Abs(Vector3.Dot(axis, Vector3.UnitX)) < 0.9f ? Vector3.UnitX : Vector3.UnitZ;
        var right = Vector3.Normalize(Vector3.Cross(tangent, axis));
        var forward = Vector3.Cross(axis, right);

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<int>();

        for (var i = 0; i < segments; i++)
        {
            var angle = 2f * MathF.PI * i / segments;
            var ringCenter = right * MathF.Cos(angle) * radius + forward * MathF.Sin(angle) * radius;
            var outward = Vector3.Normalize(ringCenter);

            for (var j = 0; j < crossSections; j++)
            {
                var tubeAngle = 2f * MathF.PI * j / crossSections;
                var tubeNormal = outward * MathF.Cos(tubeAngle) + axis * MathF.Sin(tubeAngle);
                vertices.Add(ringCenter + tubeNormal * tubeRadius);
                normals.Add(tubeNormal);
            }
        }

        for (var i = 0; i < segments; i++)
        {
            var nextI = (i + 1) % segments;
            for (var j = 0; j < crossSections; j++)
            {
                var nextJ = (j + 1) % crossSections;
                var a = i * crossSections + j;
                var b = i * crossSections + nextJ;
                var c = nextI * crossSections + j;
                var d = nextI * crossSections + nextJ;
                indices.Add(a); indices.Add(b); indices.Add(c);
                indices.Add(b); indices.Add(d); indices.Add(c);
            }
        }

        return new MeshSurface
        {
            Vertices = vertices.ToArray(),
            Normals = normals.ToArray(),
            Indices = indices.ToArray()
        };
    }

    public static MeshSurface CreateWireframeBox(Vector3 size, Color color)
    {
        size /= 2f;
        var corners = new[]
        {
            new Vector3(-size.X, -size.Y, -size.Z),
            new Vector3(size.X, -size.Y, -size.Z),
            new Vector3(size.X, size.Y, -size.Z),
            new Vector3(-size.X, size.Y, -size.Z),
            new Vector3(-size.X, -size.Y, size.Z),
            new Vector3(size.X, -size.Y, size.Z),
            new Vector3(size.X, size.Y, size.Z),
            new Vector3(-size.X, size.Y, size.Z)
        };

        return new MeshSurface
        {
            Vertices = corners,
            Colors = Enumerable.Repeat(color, corners.Length).ToArray(),
            Indices = new[]
            {
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7
            }
        };
    }
}