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
        var obj = TrixelMaterializer.ReconstructGeometry(_ao.Size.ToXna(), _ao.Geometry.Vertices,
            _ao.Geometry.Indices);
        _resized = obj.Resize;
        obj.Texture = _ao.Cubemap;
        return obj;
    }

    public object GetAsset(TrixelObject obj)
    {
        var ao = new ArtObject { Size = obj.Size.ToRepacker() };
        (ao.Geometry.Vertices, ao.Geometry.Indices) = TrixelMaterializer.Dematerialize(obj);
        ao.Name = _ao.Name;
        ao.ActorType = _ao.ActorType;
        ao.NoSihouette = _ao.NoSihouette;
        ao.Cubemap = obj.Texture;
        return ao;
    }

    public bool DrawProperties(History history)
    {
        var revisualize = false;

        var name = _ao.Name;
        if (ImGui.InputText("Name", ref name, 255))
        {
            using (history.BeginScope("Edit Name"))
            {
                _ao.Name = name;
            }
        }

        var size = _ao.Size.ToXna();
        if (ImGuiX.DragFloat3("Size", ref size))
        {
            using (history.BeginScope("Edit Size"))
            {
                _ao.Size = size.ToRepacker();
                _resized?.Invoke(size);
                revisualize = true;
            }
        }

        var actor = (int)_ao.ActorType;
        var actors = Enum.GetNames<ActorType>();
        if (ImGui.Combo("Actor Type", ref actor, actors, actors.Length))
        {
            using (history.BeginScope("Edit Actor Type"))
            {
                _ao.ActorType = (ActorType)actor;
            }
        }

        var noSihouette = _ao.NoSihouette;
        if (ImGui.Checkbox("No Sihouette", ref noSihouette))
        {
            using (history.BeginScope("Edit NoSihouette"))
            {
                _ao.NoSihouette = noSihouette;
            }
        }

        return revisualize;
    }
}
