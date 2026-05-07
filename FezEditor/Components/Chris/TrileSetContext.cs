using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Graphics;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PrimitiveType = FEZRepacker.Core.Definitions.Game.XNA.PrimitiveType;

namespace FezEditor.Components.Chris;

internal class TrileSetContext : IContext
{
    private const int FaceCount = 6; // FaceOrientation

    private const int BytesPerPixel = 4;

    private const int FaceSize = 16;

    private const int TrileWidth = FaceSize * FaceCount;

    private const int TrileHeight = FaceSize;

    private const int AtlasFaceSize = 18;

    public const int AtlasTrileWidth = AtlasFaceSize * FaceCount;

    public const int AtlasTrileHeight = AtlasFaceSize;

    public const int AtlasWidth = 1024;

    public const int AtlasStartingHeight = 32;

    private const int AtlasColumns = AtlasWidth / AtlasTrileWidth;

    public int Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                if (!_set.Triles.ContainsKey(value))
                {
                    throw new KeyNotFoundException($"Trile with ID {value} not found");
                }

                _id = value;
            }
        }
    }

    public string Name
    {
        get => _set.Name;
        set => _set.Name = value;
    }

    private Trile Trile => _set.Triles[_id];

    private readonly TrileSet _set;

    private readonly Game _game;

    private readonly Texture2D _missing;

    private Texture2D? _atlasTexture;

    private int _id;

    private Action<Vector3>? _resized;

    public TrileSetContext(TrileSet set, Game game)
    {
        _set = set;
        _game = game;
        _missing = game.Content.Load<Texture2D>("Textures/Missing");
        _id = set.Triles
            .OrderBy(kv => kv.Key)
            .First(kv => kv.Value.Geometry.Vertices.Length > 0)
            .Key;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _atlasTexture?.Dispose();
        _atlasTexture = null;
    }

    public TrixelObject Materialize()
    {
        var obj = TrixelMaterializer.ReconstructGeometry(
            Trile.Size.ToXna(), Trile.Geometry.Vertices, Trile.Geometry.Indices);
        _resized = obj.Resize;

        var atlas = _set.TextureAtlas;
        var px = (int)MathF.Round(Trile.AtlasOffset.X * atlas.Width);
        var py = (int)MathF.Round(Trile.AtlasOffset.Y * atlas.Height);
        obj.Texture = new RTexture2D
        {
            Width = TrileWidth,
            Height = TrileHeight,
            TextureData = ReadTrileFromAtlas(atlas.TextureData, atlas.Width, px, py)
        };

        return obj;
    }

    public object GetAsset(TrixelObject obj)
    {
        #region Apply New Properties

        var trile = new Trile
        {
            Size = obj.Size.ToRepacker(),
            Name = Trile.Name,
            Offset = Trile.Offset,
            AtlasOffset = Trile.AtlasOffset,
            Immaterial = Trile.Immaterial,
            SeeThrough = Trile.SeeThrough,
            Thin = Trile.Thin,
            ForceHugging = Trile.ForceHugging,
            Type = Trile.Type,
            Face = Trile.Face,
            SurfaceType = Trile.SurfaceType,
            Faces = new Dictionary<FaceOrientation, CollisionType>(Trile.Faces)
        };

        (trile.Geometry.Vertices, trile.Geometry.Indices) = TrixelMaterializer.Dematerialize(obj);
        _set.Triles[Id] = trile;

        #endregion

        #region Rebuild Atlas Texture

        var overrides = new Dictionary<int, byte[]> { [Id] = obj.Texture.TextureData };
        RebuildAtlas(_set, overrides);

        _atlasTexture?.Dispose();
        _atlasTexture = new Texture2D(_game.GraphicsDevice, _set.TextureAtlas.Width, _set.TextureAtlas.Height,
            false, SurfaceFormat.Color);
        _atlasTexture.SetData(_set.TextureAtlas.TextureData);
        RepackerExtensions.SetAlpha(_atlasTexture, 1f);

        #endregion

        ApplyAtlasOffsets(_set);

        return _set;
    }

    // Reads the 96x16 border-stripped pixels from a 108x18 atlas slot at (px, py).
    private static byte[] ReadTrileFromAtlas(byte[] atlasData, int atlasWidth, int px, int py)
    {
        var dst = new byte[TrileWidth * TrileHeight * BytesPerPixel];
        for (var face = 0; face < FaceCount; face++)
        {
            var srcFace = face * AtlasFaceSize;
            var dstFace = face * FaceSize;
            for (var row = 0; row < AtlasFaceSize; row++)
            {
                if (row is not (0 or AtlasFaceSize - 1))
                {
                    for (var col = 0; col < AtlasFaceSize; col++)
                    {
                        if (col is not (0 or AtlasFaceSize - 1))
                        {
                            var dstRow = row - 1;
                            var dstCol = col - 1;
                            var srcPixel = (((py + row) * atlasWidth) + px + srcFace + col) * BytesPerPixel;
                            var dstPixel = ((dstRow * TrileWidth) + dstFace + dstCol) * BytesPerPixel;
                            atlasData.AsSpan(srcPixel, BytesPerPixel).CopyTo(dst.AsSpan(dstPixel, BytesPerPixel));
                        }
                    }
                }
            }
        }

        return dst;
    }

    // Writes the 96x16 border-stripped pixels into a 108x18 atlas slot at (px, py).
    // The 1px border around each face is filled by clamping to the nearest edge pixel.
    private static void WriteTrileToAtlas(byte[] src, byte[] atlasData, int atlasWidth, int px, int py)
    {
        for (var face = 0; face < FaceCount; face++)
        {
            var srcFace = face * FaceSize;
            var dstFace = face * AtlasFaceSize;
            for (var row = 0; row < AtlasFaceSize; row++)
            {
                for (var col = 0; col < AtlasFaceSize; col++)
                {
                    var srcRow = Math.Clamp(row - 1, 0, FaceSize - 1);
                    var srcCol = Math.Clamp(col - 1, 0, FaceSize - 1);
                    var srcPixel = ((srcRow * TrileWidth) + srcFace + srcCol) * BytesPerPixel;
                    var dstPixel = (((py + row) * atlasWidth) + px + dstFace + col) * BytesPerPixel;
                    src.AsSpan(srcPixel, BytesPerPixel).CopyTo(atlasData.AsSpan(dstPixel, BytesPerPixel));
                }
            }
        }
    }

    public bool DrawProperties(History history)
    {
        var revisualize = false;

        var name = Trile.Name;
        if (ImGui.InputText("Name", ref name, 255))
        {
            using (history.BeginScope("Edit Name"))
            {
                Trile.Name = name;
            }
        }

        var size = Trile.Size.ToXna();
        if (ImGuiX.DragFloat3("Size", ref size))
        {
            using (history.BeginScope("Edit Size"))
            {
                Trile.Size = size.ToRepacker();
                _resized?.Invoke(size);
                revisualize = true;
            }
        }

        var offset = Trile.Offset.ToXna();
        if (ImGuiX.DragFloat3("Offset", ref offset))
        {
            using (history.BeginScope("Edit Offset"))
            {
                Trile.Offset = offset.ToRepacker();
            }
        }

        var immaterial = Trile.Immaterial;
        if (ImGui.Checkbox("Immaterial", ref immaterial))
        {
            using (history.BeginScope("Edit Immaterial"))
            {
                Trile.Immaterial = immaterial;
            }
        }

        var seeThrough = Trile.SeeThrough;
        if (ImGui.Checkbox("See Through", ref seeThrough))
        {
            using (history.BeginScope("Edit See Through"))
            {
                Trile.SeeThrough = seeThrough;
            }
        }

        var thin = Trile.Thin;
        if (ImGui.Checkbox("Thin", ref thin))
        {
            using (history.BeginScope("Edit Thin"))
            {
                Trile.Thin = thin;
            }
        }

        var forceHugging = Trile.ForceHugging;
        if (ImGui.Checkbox("Force Hugging", ref forceHugging))
        {
            using (history.BeginScope("Edit Force Hugging"))
            {
                Trile.ForceHugging = forceHugging;
            }
        }

        var actorType = (int)Trile.Type;
        var actorTypes = Enum.GetNames<ActorType>();
        if (ImGui.Combo("Actor Type", ref actorType, actorTypes, actorTypes.Length))
        {
            using (history.BeginScope("Edit Actor Type"))
            {
                Trile.Type = (ActorType)actorType;
            }
        }

        var actorFace = (int)Trile.Face;
        var actorFaces = Enum.GetNames<FaceOrientation>();
        if (ImGui.Combo("Initial Face", ref actorFace, actorFaces, actorFaces.Length))
        {
            using (history.BeginScope("Edit Initial Face"))
            {
                Trile.Face = (FaceOrientation)actorFace;
            }
        }

        var surfaceType = (int)Trile.SurfaceType;
        var surfaceTypes = Enum.GetNames<SurfaceType>();
        if (ImGui.Combo("Surface Type", ref surfaceType, surfaceTypes, surfaceTypes.Length))
        {
            using (history.BeginScope("Edit Surface Type"))
            {
                Trile.SurfaceType = (SurfaceType)surfaceType;
            }
        }

        // FaceOrientation is not IEquatable, so string key is being used
        var collisionFaces = Trile.Faces.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
        if (ImGuiX.EditableDict("Collision Faces", ref collisionFaces, RenderFace, AddCollisionType,
                () => CollisionType.None))
        {
            using (history.BeginScope("Edit Collision Faces"))
            {
                Trile.Faces =
                    collisionFaces.ToDictionary(kv => Enum.Parse<FaceOrientation>(kv.Key), kv => kv.Value);
                revisualize = true;
            }
        }

        return revisualize;
    }

    private static bool RenderFace(string key, ref CollisionType value)
    {
        ImGui.Text(key + ":");
        ImGui.SameLine();
        var collisionType = (int)value;
        var collisionTypes = Enum.GetNames<CollisionType>();
        var changed = ImGui.Combo($"##{key}_value", ref collisionType, collisionTypes, collisionTypes.Length);
        value = (CollisionType)collisionType;
        return changed;
    }

    private static bool AddCollisionType(ref string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            key = nameof(FaceOrientation.Left);
        }

        var face = (int)Enum.Parse<FaceOrientation>(key);
        var faces = Enum.GetNames<FaceOrientation>();
        return ImGui.Combo("##item", ref face, faces, faces.Length);
    }

    public Dictionary<FaceOrientation, CollisionType> GetTrileCollision()
    {
        return Trile.Faces;
    }

    public IEnumerable<Entry> EnumerateTriles(string filter = "")
    {
        var ids = _set.Triles.Keys.ToArray();
        foreach (var id in ids)
        {
            var trile = _set.Triles[id];
            if (!trile.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (trile.Geometry.Vertices.Length < 1)
            {
                yield return new Entry(id, trile.Name, _missing, Vector2.Zero, Vector2.One);
                continue;
            }

            if (_atlasTexture == null)
            {
                var atlas = _set.TextureAtlas;
                _atlasTexture = new Texture2D(_game.GraphicsDevice, atlas.Width, atlas.Height, false, SurfaceFormat.Color);
                _atlasTexture.SetData(atlas.TextureData);
                RepackerExtensions.SetAlpha(_atlasTexture, 1f);
            }

            // Thumbnail shows the front face (face 0) usable area, skipping the 1px border.
            var uv0 = new Vector2(
                trile.AtlasOffset.X + (1f / AtlasWidth),
                trile.AtlasOffset.Y + (1f / _atlasTexture.Height)
            );
            var uv1 = new Vector2(
                uv0.X + ((float)FaceSize / AtlasWidth),
                uv0.Y + ((float)FaceSize / _atlasTexture.Height)
            );

            yield return new Entry(id, trile.Name, _atlasTexture, uv0, uv1);
        }
    }

    public static void ApplyAtlasOffsets(TrileSet set)
    {
        var atlasW = set.TextureAtlas.Width;
        var atlasH = set.TextureAtlas.Height;

        foreach (var trile in set.Triles.Values)
        {
            foreach (var vertex in trile.Geometry.Vertices)
            {
                // Vertex U is in [0,1] spanning all 6 faces packed without borders.
                // Map each face's [f/6, (f+1)/6] range into atlas space, inserting per-face borders.
                var u = vertex.TextureCoordinate.X;
                var faceIndex = Math.Clamp((int)(u * FaceCount), 0, FaceCount - 1);
                var uWithinFace = (u * FaceCount) - faceIndex;

                var faceAtlasX = trile.AtlasOffset.X + (((faceIndex * AtlasFaceSize) + 1f) / atlasW);
                var mappedU = faceAtlasX + (uWithinFace * FaceSize / atlasW);
                var mappedV = trile.AtlasOffset.Y + (1f / atlasH) +
                              (vertex.TextureCoordinate.Y * FaceSize / atlasH);

                vertex.TextureCoordinate = new RVector2(mappedU, mappedV);
            }
        }
    }

    private static void RebuildAtlas(TrileSet set, Dictionary<int, byte[]> overrides)
    {
        var rows = (int)MathF.Ceiling((float)set.Triles.Count / AtlasColumns);
        var requiredHeight = rows * AtlasTrileHeight;

        var atlasHeight = AtlasStartingHeight;
        while (atlasHeight < requiredHeight)
        {
            atlasHeight <<= 1;
        }

        var atlasData = new byte[AtlasWidth * atlasHeight * 4];
        var i = 0;

        foreach (var (id, trile) in set.Triles)
        {
            var col = i % AtlasColumns;
            var row = i / AtlasColumns;
            var destX = col * AtlasTrileWidth;
            var destY = row * AtlasTrileHeight;

            byte[] src;
            if (overrides.TryGetValue(id, out var pending))
            {
                src = pending;
            }
            else if (set.TextureAtlas.TextureData.Length > 0)
            {
                var atlas = set.TextureAtlas;
                var px = (int)MathF.Round(trile.AtlasOffset.X * atlas.Width);
                var py = (int)MathF.Round(trile.AtlasOffset.Y * atlas.Height);
                src = ReadTrileFromAtlas(atlas.TextureData, atlas.Width, px, py);
            }
            else
            {
                src = new byte[TrileWidth * TrileHeight * 4];
            }

            WriteTrileToAtlas(src, atlasData, AtlasWidth, destX, destY);

            trile.AtlasOffset = new RVector2((float)destX / AtlasWidth, (float)destY / atlasHeight);
            i++;
        }

        set.TextureAtlas = new RTexture2D
        {
            Width = AtlasWidth,
            Height = atlasHeight,
            TextureData = atlasData
        };
    }

    public int AddTrile()
    {
        var newId = _set.Triles.Count > 0
            ? _set.Triles.Keys.Max() + 1
            : 0;

        var newTrile = new Trile
        {
            Name = "UNTITLED",
            Size = Vector3.One.ToRepacker(),
            Faces = new Dictionary<FaceOrientation, CollisionType>
            {
                [FaceOrientation.Front] = CollisionType.None,
                [FaceOrientation.Right] = CollisionType.None,
                [FaceOrientation.Back] = CollisionType.None,
                [FaceOrientation.Left] = CollisionType.None
            }
        };

        _set.Triles[newId] = newTrile;
        return newId;
    }

    public int RemoveTriles(HashSet<int> ids)
    {
        foreach (var id in ids)
        {
            _set.Triles.Remove(id);
        }

        if (_set.Triles.Count == 0)
        {
            return AddTrile();
        }

        // Return last remaining id before the removed range, or first available
        var remaining = _set.Triles.Keys.OrderBy(k => k).ToList();
        return remaining.LastOrDefault(k => k < ids.Min(), remaining[0]);
    }

    public int CopyTriles(IEnumerable<int> sourceIds)
    {
        var lastId = -1;
        foreach (var sourceId in sourceIds)
        {
            var source = _set.Triles[sourceId];
            var newId = _set.Triles.Keys.Max() + 1;

            _set.Triles[newId] = new Trile
            {
                Name = source.Name,
                Size = source.Size,
                Offset = source.Offset,
                AtlasOffset = source.AtlasOffset,
                Immaterial = source.Immaterial,
                SeeThrough = source.SeeThrough,
                Thin = source.Thin,
                ForceHugging = source.ForceHugging,
                Type = source.Type,
                Face = source.Face,
                SurfaceType = source.SurfaceType,
                Faces = new Dictionary<FaceOrientation, CollisionType>(source.Faces),
                Geometry = new IndexedPrimitives<VertexInstance, RVector4>
                {
                    PrimitiveType = PrimitiveType.TriangleList,
                    Vertices = source.Geometry.Vertices.ToArray(),
                    Indices = source.Geometry.Indices.ToArray()
                }
            };


            lastId = newId;
        }

        return lastId;
    }

    public void AppendTriles(IEnumerable<int> ids, TrileSet targetSet)
    {
        // Collect pixel data for selected triles before modifying anything
        var slices = new List<(Trile Trile, byte[] Pixels)>();
        foreach (var id in ids)
        {
            if (!_set.Triles.TryGetValue(id, out var trile))
            {
                continue;
            }

            var atlas = _set.TextureAtlas;
            var px = (int)MathF.Round(trile.AtlasOffset.X * atlas.Width);
            var py = (int)MathF.Round(trile.AtlasOffset.Y * atlas.Height);
            slices.Add((trile, ReadTrileFromAtlas(atlas.TextureData, atlas.Width, px, py)));
        }

        // Assign new non-conflicting IDs in the target set
        var nextId = targetSet.Triles.Count > 0 ? targetSet.Triles.Keys.Max() + 1 : 0;
        foreach (var (trile, _) in slices)
        {
            var copy = new Trile
            {
                Name = trile.Name,
                Size = trile.Size,
                Offset = trile.Offset,
                Immaterial = trile.Immaterial,
                SeeThrough = trile.SeeThrough,
                Thin = trile.Thin,
                ForceHugging = trile.ForceHugging,
                Type = trile.Type,
                Face = trile.Face,
                SurfaceType = trile.SurfaceType,
                Faces = new Dictionary<FaceOrientation, CollisionType>(trile.Faces)
            };
            (copy.Geometry.Vertices, copy.Geometry.Indices) =
                (trile.Geometry.Vertices.ToArray(), trile.Geometry.Indices.ToArray());
            targetSet.Triles[nextId++] = copy;
        }

        // Build pixel overrides: newly appended triles keyed by their new IDs
        var appendStart = nextId - slices.Count;
        var overrides = new Dictionary<int, byte[]>();
        for (var j = 0; j < slices.Count; j++)
        {
            overrides[appendStart + j] = slices[j].Pixels;
        }

        RebuildAtlas(targetSet, overrides);
    }

    public readonly record struct Entry(int Id, string Name, Texture2D Texture, Vector2 Uv0, Vector2 Uv1);
}