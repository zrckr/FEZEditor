using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Common;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal class ArtObjectContext : IContext
{
    private readonly ArtObject _ao;

    private ArtObjectProperties _properties = null!;

    private Action<Vector3>? _resized;

    public ArtObjectContext(ArtObject ao)
    {
        _ao = ao;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public TrixelObject Materialize()
    {
        var obj = TrixelMaterializer.ReconstructGeometry(
            _ao.Size.ToXna(), _ao.Geometry.Vertices, _ao.Geometry.Indices, _ao.Size.ToXna() / 2f);
        obj.Texture = _ao.Cubemap;
        obj.Properties = _properties = new ArtObjectProperties(_ao);
        _resized = obj.Resize;
        return obj;
    }

    public void SyncProperties(TrixelObject obj)
    {
        if (obj.Properties is ArtObjectProperties properties)
        {
            _properties = properties;
        }
    }

    public object Dematerialize(TrixelObject obj)
    {
        _ao.Size = obj.Size.ToRepacker();
        (_ao.Geometry.Vertices, _ao.Geometry.Indices) = TrixelMaterializer.Dematerialize(obj);
        _ao.Cubemap = obj.Texture;

        if (obj.Properties is ArtObjectProperties properties)
        {
            properties.CopyTo(_ao);
        }

        return _ao;
    }

    public bool DrawProperties(History history)
    {
        var revisualize = false;

        var name = _properties.Name;
        if (ImGui.InputText("Name", ref name, 255))
        {
            using (history.BeginScope("Edit Name"))
            {
                _properties.Name = name;
            }
        }

        var size = _properties.Size.ToXna();
        if (ImGuiX.DragFloat3("Size", ref size))
        {
            using (history.BeginScope("Edit Size"))
            {
                _properties.Size = size.ToRepacker();
                _resized?.Invoke(size);
                revisualize = true;
            }
        }

        var actor = (int)_properties.ActorType;
        var actors = Enum.GetNames<ActorType>();
        if (ImGui.Combo("Actor Type", ref actor, actors, actors.Length))
        {
            using (history.BeginScope("Edit Actor Type"))
            {
                _properties.ActorType = (ActorType)actor;
            }
        }

        var noSihouette = _properties.NoSihouette;
        if (ImGui.Checkbox("No Sihouette", ref noSihouette))
        {
            using (history.BeginScope("Edit NoSihouette"))
            {
                _properties.NoSihouette = noSihouette;
            }
        }

        return revisualize;
    }
}
