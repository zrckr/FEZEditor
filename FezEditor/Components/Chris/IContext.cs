using FezEditor.Structure;

namespace FezEditor.Components.Chris;

internal interface IContext : IDisposable
{
    TrixelObject Materialize();

    object Dematerialize(TrixelObject obj);

    void SyncProperties(TrixelObject obj);

    bool DrawProperties(History history);
}
