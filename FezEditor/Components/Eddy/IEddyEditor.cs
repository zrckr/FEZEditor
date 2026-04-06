using FezEditor.Actors;
using FezEditor.Structure;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal interface IEddyEditor
{
    History History { get; }

    Scene Scene { get; }

    Clock Clock { get; }

    AssetBrowser AssetBrowser { get; }

    Camera Camera { get; }

    CursorMesh Cursor { get; }

    Gizmo Gizmo { get; }

    bool IsViewportHovered { get; }

    Ray Ray { get; }

    RaycastHit? Hit { get; }

    EddyTool Tool { get; set; }

    EddyContext Context { get; set; }

    Dirty<bool> ShowPickableBounds { get; set; }

    Dirty<bool> ShowCollisionMap { get; set; }
}