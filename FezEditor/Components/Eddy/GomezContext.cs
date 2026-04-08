using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Eddy;

internal class GomezContext : BaseContext
{
    private Actor? _gomezActor;

    private bool _hovered;

    private bool _selected;

    private IDisposable? _translateScope;

    public GomezContext(Game game, Level level, IEddyEditor eddy) : base(game, level, eddy)
    {
    }

    protected override void TestConditions()
    {
        _hovered = false;
        if (Eddy.Hit.HasValue && Eddy.Hit.Value.Actor.HasComponent<NpcMesh>())
        {
            var actor = Eddy.Hit.Value.Actor;
            if (_gomezActor == actor && Eddy.Tool is EddyTool.Select or EddyTool.Pick)
            {
                _hovered = true;
                Eddy.HoveredContext = EddyContext.Gomez;
                return;
            }
        }

        if (!_hovered && !Eddy.Gizmo.IsActive && Eddy.Tool != EddyTool.Paint &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Eddy.IsViewportHovered)
        {
            _selected = false;
            Eddy.SelectedContext = EddyContext.Default;
            Eddy.Tool = EddyTool.Select;
        }

        if (_selected)
        {
            Eddy.SelectedContext = EddyContext.Gomez;
        }
    }

    protected override void Act()
    {
        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            _selected = false;
            Eddy.SelectedContext = EddyContext.Default;
            Eddy.Tool = EddyTool.Select;
        }

        switch (Eddy.Tool)
        {
            case EddyTool.Select: UpdateSelect(); break;
            case EddyTool.Translate: UpdateTranslate(); break;
            case EddyTool.Rotate: UpdateRotate(); break;
            case EddyTool.Scale:
            case EddyTool.Paint:
            case EddyTool.Pick: break;
            default: throw new ArgumentOutOfRangeException();
        }

        if (_hovered)
        {
            var hoverSurfaces = BuildWireframeForGomez(HoverColor);
            if (hoverSurfaces.HasValue)
            {
                Eddy.Cursor.SetHoverSurfaces([hoverSurfaces.Value], HoverColor);
            }
        }

        if (_selected)
        {
            var selectionSurfaces = BuildWireframeForGomez(SelectionColor);
            if (selectionSurfaces.HasValue)
            {
                Eddy.Cursor.SetSelectionSurfaces([selectionSurfaces.Value], SelectionColor);
            }
        }
    }

    private void UpdateSelect()
    {
        StatusService.AddHints(
            ("LMB", "Select"),
            ("Shift+LMB", "Add to Selection")
        );

        if (_selected)
        {
            StatusService.AddHints(
                ("Delete", "Erase"),
                ("Ctrl+C", "Copy"),
                ("Ctrl+X", "Cut")
            );
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _hovered)
        {
            _selected = true;
        }
    }

    private void UpdateTranslate()
    {
        if (!_selected)
        {
            return;
        }

        var origin = Level.StartingFace.Id.ToXna().ToVector3() + Vector3.Up;
        if (Eddy.Gizmo.Translate(ref origin))
        {
            var empl = origin - Vector3.Up;
            Level.StartingFace.Id = new TrileEmplacement((int)empl.X, (int)empl.Y, (int)empl.Z);
            _gomezActor!.Transform.Position = origin;
        }

        if (Eddy.Gizmo.DragStarted)
        {
            _translateScope?.Dispose();
            _translateScope = Eddy.History.BeginScope("Translate Gomez");
        }

        if (Eddy.Gizmo.DragEnded)
        {
            _translateScope?.Dispose();
            _translateScope = null;
        }
    }

    private void UpdateRotate()
    {
        if (!_selected)
        {
            return;
        }

        var origin = Level.StartingFace.Id.ToXna().ToVector3() + Vector3.Up;
        if (Eddy.Gizmo.Rotate(origin))
        {
            using (Eddy.History.BeginScope("Rotate Gomez"))
            {
                var index = Array.IndexOf(FaceExtensions.NaturalOrder, Level.StartingFace.Face);
                Level.StartingFace.Face = FaceExtensions.NaturalOrder[(index + 1) % 4];
                _gomezActor!.Transform.Rotation = Level.StartingFace.Face.AsQuaternion();
            }
        }
    }

    private (MeshSurface, PrimitiveType)? BuildWireframeForGomez(Color color)
    {
        if (_gomezActor == null || !_gomezActor.TryGetComponent<NpcMesh>(out var mesh))
        {
            return null;
        }

        var box = mesh!.GetBounds().First();
        var size = box.Max - box.Min;
        var center = (box.Min + box.Max) * 0.5f;

        var surface = MeshSurface.CreateWireframeBox(size, color);
        for (var i = 0; i < surface.Vertices.Length; i++)
        {
            surface.Vertices[i] += center;
        }

        return (surface, PrimitiveType.LineList);
    }

    public override void DrawProperties()
    {
        if (Eddy.SelectedContext != EddyContext.Gomez || !_selected)
        {
            return;
        }

        ImGui.Text("Gomez (Starting Position)");

        var emplacement = Level.StartingFace.Id;
        var empValues = new[] { emplacement.X, emplacement.Y, emplacement.Z };
        if (ImGui.InputInt3("Emplacement", ref empValues[0]))
        {
            using (Eddy.History.BeginScope("Edit Gomez Position"))
            {
                Level.StartingFace.Id = new TrileEmplacement(empValues[0], empValues[1], empValues[2]);
            }
        }

        var face = Array.IndexOf(FaceExtensions.NaturalOrder, Level.StartingFace.Face);
        var faces = FaceExtensions.NaturalOrder.Select(fo => fo.ToString()).ToArray();
        if (ImGui.Combo("Face", ref face, faces, faces.Length))
        {
            using (Eddy.History.BeginScope("Edit Gomez Rotation"))
            {
                Level.StartingFace.Face = FaceExtensions.NaturalOrder[face];
            }
        }
    }

    public override void Revisualize(bool partial = false)
    {
        if (partial)
        {
            if (Eddy.SelectedContext != EddyContext.Gomez)
            {
                return;
            }

            if (_gomezActor != null)
            {
                _gomezActor.Transform.Position = Level.StartingFace.Id.ToXna().ToVector3() + Vector3.Up;
                _gomezActor.Transform.Rotation = Level.StartingFace.Face.AsQuaternion();
            }

            return;
        }

        TeardownVisualization();

        #region Gomez

        {
            _gomezActor = CreateSubActor();
            _gomezActor.Name = "Gomez";
            _gomezActor.Transform.Position = Level.StartingFace.Id.ToXna().ToVector3() + Vector3.Up;
            _gomezActor.Transform.Rotation = Level.StartingFace.Face.AsQuaternion();

            var mesh = _gomezActor.AddComponent<NpcMesh>();
            var animations = ResourceService.LoadAnimations("Character Animations/Gomez");
            mesh.Visualize(animations, "IdleWink");
        }

        #endregion
    }

    protected override bool IsContextAllowed(EddyContext context)
    {
        return context == EddyContext.Gomez;
    }

    public override void Dispose()
    {
        TeardownVisualization();
        base.Dispose();
    }

    private void TeardownVisualization()
    {
        if (_gomezActor != null)
        {
            Eddy.Scene.DestroyActor(_gomezActor);
            _gomezActor = null;
        }
    }
}