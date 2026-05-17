using System.Text.Json.Serialization;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.TrileSet;

namespace FezEditor.Structure;

[JsonDerivedType(typeof(ArtObjectProperties), "ArtObject")]
[JsonDerivedType(typeof(TrileProperties), "Trile")]
public abstract class TrixelProperties;

public class ArtObjectProperties : TrixelProperties
{
    public string Name { get; set; } = "";
    public RVector3 Size { get; set; }
    public ActorType ActorType { get; set; }
    public bool NoSihouette { get; set; }

    [JsonConstructor]
    public ArtObjectProperties()
    {
    }

    public ArtObjectProperties(ArtObject ao)
    {
        Name = ao.Name;
        Size = ao.Size;
        ActorType = ao.ActorType;
        NoSihouette = ao.NoSihouette;
    }

    public void CopyTo(ArtObject ao)
    {
        ao.Name = Name;
        ao.Size = Size;
        ao.ActorType = ActorType;
        ao.NoSihouette = NoSihouette;
    }
}

public class TrileProperties : TrixelProperties
{
    public string Name { get; set; } = "";
    public RVector3 Size { get; set; }
    public RVector3 Offset { get; set; }
    public bool Immaterial { get; set; }
    public bool SeeThrough { get; set; }
    public bool Thin { get; set; }
    public bool ForceHugging { get; set; }
    public ActorType Type { get; set; }
    public FaceOrientation Face { get; set; }
    public SurfaceType SurfaceType { get; set; }
    public Dictionary<FaceOrientation, CollisionType> Faces { get; set; } = new();

    [JsonConstructor]
    public TrileProperties()
    {
    }

    public TrileProperties(Trile trile)
    {
        Name = trile.Name;
        Size = trile.Size;
        Offset = trile.Offset;
        Immaterial = trile.Immaterial;
        SeeThrough = trile.SeeThrough;
        Thin = trile.Thin;
        ForceHugging = trile.ForceHugging;
        Type = trile.Type;
        Face = trile.Face;
        SurfaceType = trile.SurfaceType;
        Faces = new Dictionary<FaceOrientation, CollisionType>(trile.Faces);
    }

    public void CopyTo(Trile trile)
    {
        trile.Name = Name;
        trile.Size = Size;
        trile.Offset = Offset;
        trile.Immaterial = Immaterial;
        trile.SeeThrough = SeeThrough;
        trile.Thin = Thin;
        trile.ForceHugging = ForceHugging;
        trile.Type = Type;
        trile.Face = Face;
        trile.SurfaceType = SurfaceType;
        trile.Faces = new Dictionary<FaceOrientation, CollisionType>(Faces);
    }
}
