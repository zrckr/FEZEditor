using FezEditor.Structure;

namespace FezEditor.Components.Chris;

internal interface IContext : IDisposable
{
    TrixelObject Materialize();

    object GetAsset(TrixelObject obj);

    bool DrawProperties(History history);
}
