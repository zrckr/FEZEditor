using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class Gizmo : ActorComponent
{
    private static readonly Color ColorXNormal = new(220, 60, 60);

    private static readonly Color ColorXBright = new(255, 180, 180);

    private static readonly Color ColorYNormal = new(60, 220, 60);

    private static readonly Color ColorYBright = new(180, 255, 180);

    private static readonly Color ColorZNormal = new(60, 60, 220);

    private static readonly Color ColorZBright = new(180, 180, 255);

    private static readonly Color ColorPlaneXzNormal = new(60, 220, 60, 60);

    private static readonly Color ColorPlaneXyNormal = new(60, 60, 220, 60);

    private static readonly Color ColorPlaneYzNormal = new(220, 60, 60, 60);

    private static readonly Color ColorPlaneBright = new(255, 255, 100, 120);

    private static readonly Color ColorCenterNormal = new(200, 200, 200);

    private static readonly Color ColorCenterBright = new(255, 255, 100);

    private static readonly Color ColorFaceNormal = new(220, 220, 60);

    private static readonly Color ColorFaceBright = new(255, 255, 140);

    private const float GizmoScreenSize = 80f; // desired on-screen size in pixels

    private const float ArrowShaftLength = 0.75f;

    private const float ArrowShaftRadius = 0.04f;

    private const float ArrowTipLength = 0.25f;

    private const float ArrowTipRadius = 0.10f;

    private const int ArrowSidesTranslate = 8;

    private const int ArrowSidesScale = 4;

    private const float ScaleBoxSize = 0.18f;

    private const float PlaneSize = 0.2f;

    private const float PlaneDist = 0.3f;

    private const float RingRadius = 1.1f;

    private const float RingTubeRadius = 0.035f;

    private const int RingSegments = 64;

    private const int RingCrossSections = 6;

    private const float RingHalfWidth = 0.1f;

    private const float PickRadius = 0.12f; // world-space sphere radius for axis/scale/face pick

    public Camera Camera { get; set; } = null!;

    public Vector2 Viewport { get; set; }

    public bool DragStarted { get; private set; }

    public bool DragEnded { get; private set; }

    public bool IsActive => _activeHandle != Handle.None || _hoveredHandle != Handle.None;

    private readonly RenderingService _rendering;

    private readonly StatusService _status;

    private readonly Rid _mesh;

    private readonly GizmoHandle _translateX, _translateY, _translateZ;

    private readonly GizmoHandle _planeXz, _planeXy, _planeYz;

    private readonly GizmoHandle _scaleX, _scaleY, _scaleZ, _scaleCenter;

    private readonly GizmoHandle _ring;

    private readonly GizmoHandle _face;

    private Handle _activeHandle = Handle.None;

    private Handle _hoveredHandle = Handle.None;

    private Vector3 _dragPlaneNormal;

    private Vector3 _dragPlaneOrigin;

    private Vector3 _dragStartHitPoint;

    private Vector3 _dragStartValue; // position or scale snapshot at drag start

    private bool _wasLeftPressed;

    public Gizmo(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _status = game.GetService<StatusService>();

        _mesh = _rendering.MeshCreate();
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);

        var arrowX = MeshSurface.CreateArrow(Vector3.UnitX, ArrowSidesTranslate, ArrowShaftLength, ArrowShaftRadius,
            ArrowTipLength, ArrowTipRadius);
        var arrowY = MeshSurface.CreateArrow(Vector3.UnitY, ArrowSidesTranslate, ArrowShaftLength, ArrowShaftRadius,
            ArrowTipLength, ArrowTipRadius);
        var arrowZ = MeshSurface.CreateArrow(Vector3.UnitZ, ArrowSidesTranslate, ArrowShaftLength, ArrowShaftRadius,
            ArrowTipLength, ArrowTipRadius);

        var scaleArrowX = MeshSurface.CreateArrow(Vector3.UnitX, ArrowSidesScale, ArrowShaftLength, ArrowShaftRadius,
            ArrowTipLength, ArrowTipRadius);
        var scaleArrowY = MeshSurface.CreateArrow(Vector3.UnitY, ArrowSidesScale, ArrowShaftLength, ArrowShaftRadius,
            ArrowTipLength, ArrowTipRadius);
        var scaleArrowZ = MeshSurface.CreateArrow(Vector3.UnitZ, ArrowSidesScale, ArrowShaftLength, ArrowShaftRadius,
            ArrowTipLength, ArrowTipRadius);

        var planeXzMesh = MakePlaneXz(PlaneSize, PlaneDist);
        var planeXyMesh = MakePlaneXy(PlaneSize, PlaneDist);
        var planeYzMesh = MakePlaneYz(PlaneSize, PlaneDist);

        var ringMesh =
            MeshSurface.CreateRing(Vector3.UnitY, RingRadius, RingSegments, RingCrossSections, RingTubeRadius);

        _translateX = new GizmoHandle(_rendering, BlendMode.Opaque, ColorXNormal, ColorXBright, arrowX);
        _translateY = new GizmoHandle(_rendering, BlendMode.Opaque, ColorYNormal, ColorYBright, arrowY);
        _translateZ = new GizmoHandle(_rendering, BlendMode.Opaque, ColorZNormal, ColorZBright, arrowZ);

        _planeXz = new GizmoHandle(_rendering, BlendMode.AlphaBlend, ColorPlaneXzNormal, ColorPlaneBright, planeXzMesh);
        _planeXy = new GizmoHandle(_rendering, BlendMode.AlphaBlend, ColorPlaneXyNormal, ColorPlaneBright, planeXyMesh);
        _planeYz = new GizmoHandle(_rendering, BlendMode.AlphaBlend, ColorPlaneYzNormal, ColorPlaneBright, planeYzMesh);

        _scaleX = new GizmoHandle(_rendering, BlendMode.Opaque, ColorXNormal, ColorXBright, scaleArrowX);
        _scaleY = new GizmoHandle(_rendering, BlendMode.Opaque, ColorYNormal, ColorYBright, scaleArrowY);
        _scaleZ = new GizmoHandle(_rendering, BlendMode.Opaque, ColorZNormal, ColorZBright, scaleArrowZ);
        _scaleCenter = new GizmoHandle(_rendering, BlendMode.Opaque, ColorCenterNormal, ColorCenterBright,
            MeshSurface.CreateBox(new Vector3(ScaleBoxSize)));

        _ring = new GizmoHandle(_rendering, BlendMode.Opaque, ColorYNormal, ColorYBright, ringMesh);
        _face = new GizmoHandle(_rendering, BlendMode.Opaque, ColorFaceNormal, ColorFaceBright);
    }

    public bool Translate(ref Vector3 origin)
    {
        _status.AddHints(
            ("LMB Drag", "Move X or Z"),
            ("Alt+LMB Drag", "Move Y"),
            ("Shift", "Snap to trile")
        );

        var gizmoScale = ComputeGizmoScale(origin);
        var changed = false;

        var leftDown = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var leftClicked = leftDown && !_wasLeftPressed;
        var leftReleased = !leftDown && _wasLeftPressed;

        if (leftClicked && _activeHandle == Handle.None)
        {
            var ray = GetMouseRay();
            var hit = HitTestTranslate(ray, origin, gizmoScale);
            if (hit != Handle.None)
            {
                _activeHandle = hit;
                _dragStartValue = origin;
                InitDragPlane(origin, hit);
                DragStarted = true;
            }
        }
        else
        {
            DragStarted = false;
        }

        DragEnded = false;
        if (_activeHandle is Handle.TranslateX or Handle.TranslateY or Handle.TranslateZ
            or Handle.PlaneXz or Handle.PlaneXy or Handle.PlaneYz)
        {
            if (leftDown)
            {
                var ray = GetMouseRay();
                var hitPoint = RayPlaneIntersect(ray, _dragPlaneOrigin, _dragPlaneNormal);
                if (hitPoint.HasValue)
                {
                    var delta = ConstrainDelta(hitPoint.Value - _dragStartHitPoint, _activeHandle);
                    delta = SnapDelta(delta);
                    var newPos = _dragStartValue + delta;
                    if (newPos != origin)
                    {
                        origin = newPos;
                        changed = true;
                    }
                }
            }

            if (leftReleased)
            {
                _activeHandle = Handle.None;
                DragEnded = true;
            }
        }

        if (_activeHandle == Handle.None)
        {
            _hoveredHandle = HitTestTranslate(GetMouseRay(), origin, gizmoScale);
        }

        RebuildTranslateMesh(origin, gizmoScale);
        _wasLeftPressed = leftDown;
        return changed;
    }

    public bool Rotate(Vector3 origin)
    {
        _status.AddHints(
            ("LMB", "Rotate 90°")
        );

        var gizmoScale = ComputeGizmoScale(origin);
        var clicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        var hitRing = HitTestRing(GetMouseRay(), origin, gizmoScale);

        _hoveredHandle = !clicked && hitRing ? Handle.Ring : Handle.None;
        RebuildRingMesh(origin, gizmoScale);

        return clicked && hitRing;
    }

    public bool Scale(Vector3 origin, ref Vector3 scale)
    {
        _status.AddHints(
            ("LMB Drag", "Scale")
        );

        var gizmoScale = ComputeGizmoScale(origin);
        var changed = false;

        var leftDown = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var leftClicked = leftDown && !_wasLeftPressed;
        var leftReleased = !leftDown && _wasLeftPressed;

        if (leftClicked && _activeHandle == Handle.None)
        {
            var ray = GetMouseRay();
            var hit = HitTestScale(ray, origin, gizmoScale);
            if (hit != Handle.None)
            {
                _activeHandle = hit;
                _dragStartValue = scale;
                _dragStartHitPoint = RayPlaneIntersect(ray, origin, Camera.Position - origin) ?? origin;
                InitScaleDragPlane(origin, hit);
                DragStarted = true;
            }
        }
        else
        {
            DragStarted = false;
        }

        DragEnded = false;
        if (_activeHandle is Handle.ScaleX or Handle.ScaleY or Handle.ScaleZ or Handle.ScaleCenter)
        {
            if (leftDown)
            {
                var hitPoint = RayPlaneIntersect(GetMouseRay(), _dragPlaneOrigin, _dragPlaneNormal);
                if (hitPoint.HasValue)
                {
                    var newScale = ApplyScaleDelta(_dragStartValue,
                        delta: hitPoint.Value - _dragStartHitPoint, _activeHandle, gizmoScale);
                    if (newScale != scale)
                    {
                        scale = newScale;
                        changed = true;
                    }
                }
            }

            if (leftReleased)
            {
                _activeHandle = Handle.None;
                DragEnded = true;
            }
        }

        if (_activeHandle == Handle.None)
        {
            _hoveredHandle = HitTestScale(GetMouseRay(), origin, gizmoScale);
        }

        RebuildScaleMesh(origin, gizmoScale);
        _wasLeftPressed = leftDown;
        return changed;
    }

    public bool ScaleFace(Vector3 origin, FaceOrientation face, out float delta)
    {
        _status.AddHints(
            ("LMB Drag", "Add / Remove Trile")
        );

        delta = 0;
        var gizmoScale = ComputeGizmoScale(origin);
        var changed = false;

        var faceNormal = face.AsVector();
        var tipPos = origin + faceNormal * (ArrowShaftLength + ArrowTipLength * 0.5f) * gizmoScale;

        var leftDown = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var leftClicked = leftDown && !_wasLeftPressed;
        var leftReleased = !leftDown && _wasLeftPressed;

        if (leftClicked && _activeHandle == Handle.None)
        {
            var ray = GetMouseRay();
            if (HitTestSphere(ray, tipPos, PickRadius * gizmoScale))
            {
                _activeHandle = Handle.Face;
                _dragPlaneNormal = Vector3.Normalize(Camera.Position - origin);
                _dragPlaneOrigin = tipPos;
                _dragStartHitPoint = RayPlaneIntersect(ray, _dragPlaneOrigin, _dragPlaneNormal) ?? tipPos;
                DragStarted = true;
            }
        }
        else
        {
            DragStarted = false;
        }

        DragEnded = false;
        if (_activeHandle == Handle.Face)
        {
            if (leftDown)
            {
                var hitPoint = RayPlaneIntersect(GetMouseRay(), _dragPlaneOrigin, _dragPlaneNormal);
                if (hitPoint.HasValue)
                {
                    delta = Vector3.Dot(hitPoint.Value - _dragStartHitPoint, faceNormal);
                    changed = true;
                }
            }

            if (leftReleased)
            {
                _activeHandle = Handle.None;
                DragEnded = true;
            }
        }

        _hoveredHandle = Handle.None;
        if (_activeHandle == Handle.None && HitTestSphere(GetMouseRay(), tipPos, PickRadius * gizmoScale))
        {
            _hoveredHandle = Handle.Face;
        }

        RebuildFaceMesh(origin, faceNormal, gizmoScale);
        _wasLeftPressed = leftDown;
        return changed;
    }

    public void Hide()
    {
        _rendering.MeshClear(_mesh);
        _rendering.InstanceSetVisibility(Actor.InstanceRid, false);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _translateX.Dispose(_rendering);
        _translateY.Dispose(_rendering);
        _translateZ.Dispose(_rendering);
        _planeXz.Dispose(_rendering);
        _planeXy.Dispose(_rendering);
        _planeYz.Dispose(_rendering);
        _scaleX.Dispose(_rendering);
        _scaleY.Dispose(_rendering);
        _scaleZ.Dispose(_rendering);
        _scaleCenter.Dispose(_rendering);
        _ring.Dispose(_rendering);
        _face.Dispose(_rendering);
        _rendering.FreeRid(_mesh);
    }

    private float ComputeGizmoScale(Vector3 origin)
    {
        var p0 = Camera.Project(origin, Viewport);
        var p1 = Camera.Project(origin + Vector3.UnitY, Viewport);
        var pixelsPerUnit = MathF.Abs(p0.Y - p1.Y);
        if (pixelsPerUnit < 0.001f)
        {
            return 1f;
        }

        return GizmoScreenSize / pixelsPerUnit;
    }

    private Ray GetMouseRay() => Camera.Unproject(ImGuiX.GetMousePos(), Viewport);

    private static Handle HitTestTranslate(Ray ray, Vector3 origin, float scale)
    {
        var tipDist = (ArrowShaftLength + ArrowTipLength * 0.5f) * scale;
        var pickR = PickRadius * scale;

        float? dX = HitTestSphere(ray, origin + Vector3.UnitX * tipDist, pickR)
            ? RayDistanceToPoint(ray, origin + Vector3.UnitX * tipDist)
            : null;

        float? dY = HitTestSphere(ray, origin + Vector3.UnitY * tipDist, pickR)
            ? RayDistanceToPoint(ray, origin + Vector3.UnitY * tipDist)
            : null;

        float? dZ = HitTestSphere(ray, origin + Vector3.UnitZ * tipDist, pickR)
            ? RayDistanceToPoint(ray, origin + Vector3.UnitZ * tipDist)
            : null;

        var best = Handle.None;
        var bestDist = float.MaxValue;

        if (dX < bestDist)
        {
            bestDist = dX.Value;
            best = Handle.TranslateX;
        }

        if (dY < bestDist)
        {
            bestDist = dY.Value;
            best = Handle.TranslateY;
        }

        if (dZ < bestDist)
        {
            best = Handle.TranslateZ;
        }

        if (best != Handle.None)
        {
            return best;
        }

        var planeDist = PlaneDist * scale;
        var planeHalfSize = PlaneSize * scale * 1.5f;

        var planeXzCenter = origin + new Vector3(planeDist, 0, planeDist);
        if (RayPlaneIntersect(ray, planeXzCenter, Vector3.UnitY) is { } hXZ)
        {
            var diff = hXZ - planeXzCenter;
            if (MathF.Abs(diff.X) < planeHalfSize && MathF.Abs(diff.Z) < planeHalfSize)
            {
                return Handle.PlaneXz;
            }
        }

        var planeXyCenter = origin + new Vector3(planeDist, planeDist, 0);
        if (RayPlaneIntersect(ray, planeXyCenter, Vector3.UnitZ) is { } hXY)
        {
            var diff = hXY - planeXyCenter;
            if (MathF.Abs(diff.X) < planeHalfSize && MathF.Abs(diff.Y) < planeHalfSize)
            {
                return Handle.PlaneXy;
            }
        }

        var planeYzCenter = origin + new Vector3(0, planeDist, planeDist);
        if (RayPlaneIntersect(ray, planeYzCenter, Vector3.UnitX) is { } hYZ)
        {
            var diff = hYZ - planeYzCenter;
            if (MathF.Abs(diff.Y) < planeHalfSize && MathF.Abs(diff.Z) < planeHalfSize)
            {
                return Handle.PlaneYz;
            }
        }

        return Handle.None;
    }

    private bool HitTestRing(Ray ray, Vector3 origin, float scale)
    {
        var ringR = RingRadius * scale;
        var halfW = RingHalfWidth * scale;

        var toOrigin = ray.Position - origin;
        var b = Vector3.Dot(toOrigin, ray.Direction);
        var c = Vector3.Dot(toOrigin, toOrigin) - (ringR + halfW) * (ringR + halfW);
        var disc = b * b - c;
        if (disc < 0)
        {
            return false;
        }

        var sqrtDisc = MathF.Sqrt(disc);
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var t = -b + sign * sqrtDisc;
            if (t < 0)
            {
                continue;
            }

            var local = ray.Position + ray.Direction * t - origin;

            if (MathF.Abs(local.Y) > halfW)
            {
                continue;
            }

            if (Vector3.Dot(Vector3.UnitY, Vector3.Normalize(Camera.Position - origin)) < 0)
            {
                continue;
            }

            if (MathF.Abs(new Vector3(local.X, 0, local.Z).Length() - ringR) < halfW * 2f)
            {
                return true;
            }
        }

        return false;
    }

    private static Handle HitTestScale(Ray ray, Vector3 origin, float scale)
    {
        var tipDist = (ArrowShaftLength + ArrowTipLength * 0.5f) * scale;
        var pickR = PickRadius * scale;

        float? dX = HitTestSphere(ray, origin + Vector3.UnitX * tipDist, pickR)
            ? RayDistanceToPoint(ray, origin + Vector3.UnitX * tipDist)
            : null;

        float? dY = HitTestSphere(ray, origin + Vector3.UnitY * tipDist, pickR)
            ? RayDistanceToPoint(ray, origin + Vector3.UnitY * tipDist)
            : null;

        float? dZ = HitTestSphere(ray, origin + Vector3.UnitZ * tipDist, pickR)
            ? RayDistanceToPoint(ray, origin + Vector3.UnitZ * tipDist)
            : null;

        float? dC = HitTestSphere(ray, origin, pickR)
            ? RayDistanceToPoint(ray, origin)
            : null;

        var best = Handle.None;
        var bestDist = float.MaxValue;

        if (dX < bestDist)
        {
            bestDist = dX.Value;
            best = Handle.ScaleX;
        }

        if (dY < bestDist)
        {
            bestDist = dY.Value;
            best = Handle.ScaleY;
        }

        if (dZ < bestDist)
        {
            bestDist = dZ.Value;
            best = Handle.ScaleZ;
        }

        if (dC < bestDist)
        {
            best = Handle.ScaleCenter;
        }

        return best;
    }

    private static bool HitTestSphere(Ray ray, Vector3 center, float radius)
    {
        var oc = ray.Position - center;
        var b = Vector3.Dot(oc, ray.Direction);
        var c = Vector3.Dot(oc, oc) - radius * radius;
        return b * b - c >= 0;
    }

    private static float RayDistanceToPoint(Ray ray, Vector3 point)
        => Vector3.Dot(point - ray.Position, ray.Direction);

    private void InitDragPlane(Vector3 origin, Handle handle)
    {
        _dragPlaneOrigin = origin;
        _dragPlaneNormal = handle switch
        {
            Handle.TranslateX => GetCameraFacingPlaneNormal(origin, Vector3.UnitX),
            Handle.TranslateY => GetCameraFacingPlaneNormal(origin, Vector3.UnitY),
            Handle.TranslateZ => GetCameraFacingPlaneNormal(origin, Vector3.UnitZ),
            Handle.PlaneXz => Vector3.UnitY,
            Handle.PlaneXy => Vector3.UnitZ,
            Handle.PlaneYz => Vector3.UnitX,
            _ => Vector3.UnitY
        };
        _dragStartHitPoint = RayPlaneIntersect(GetMouseRay(), _dragPlaneOrigin, _dragPlaneNormal) ?? origin;
    }

    private void InitScaleDragPlane(Vector3 origin, Handle handle)
    {
        _dragPlaneOrigin = origin;
        _dragPlaneNormal = handle switch
        {
            Handle.ScaleX => GetCameraFacingPlaneNormal(origin, Vector3.UnitX),
            Handle.ScaleY => GetCameraFacingPlaneNormal(origin, Vector3.UnitY),
            Handle.ScaleZ => GetCameraFacingPlaneNormal(origin, Vector3.UnitZ),
            _ => Vector3.Normalize(Camera.Position - origin)
        };
    }

    private Vector3 GetCameraFacingPlaneNormal(Vector3 origin, Vector3 axis)
    {
        var camDir = Vector3.Normalize(Camera.Position - origin);
        var perp = camDir - Vector3.Dot(camDir, axis) * axis;
        if (perp.LengthSquared() < 0.001f)
        {
            perp = Vector3.Cross(axis, Vector3.UnitY);
            if (perp.LengthSquared() < 0.001f)
            {
                perp = Vector3.Cross(axis, Vector3.UnitX);
            }
        }

        return Vector3.Normalize(perp);
    }

    private static Vector3? RayPlaneIntersect(Ray ray, Vector3 planeOrigin, Vector3 planeNormal)
    {
        var denom = Vector3.Dot(ray.Direction, planeNormal);
        if (MathF.Abs(denom) < 0.0001f)
        {
            return null;
        }

        var t = Vector3.Dot(planeOrigin - ray.Position, planeNormal) / denom;
        if (t < 0)
        {
            return null;
        }

        return ray.Position + ray.Direction * t;
    }

    private static Vector3 ConstrainDelta(Vector3 delta, Handle handle)
    {
        return handle switch
        {
            Handle.TranslateX => new Vector3(delta.X, 0, 0),
            Handle.TranslateY => new Vector3(0, delta.Y, 0),
            Handle.TranslateZ => new Vector3(0, 0, delta.Z),
            Handle.PlaneXz => new Vector3(delta.X, 0, delta.Z),
            Handle.PlaneXy => new Vector3(delta.X, delta.Y, 0),
            Handle.PlaneYz => new Vector3(0, delta.Y, delta.Z),
            _ => delta
        };
    }

    private static Vector3 SnapDelta(Vector3 delta)
    {
        var snap = ImGui.GetIO().KeyShift ? 1f : Mathz.TrixelSize;
        return new Vector3(
            MathF.Round(delta.X / snap) * snap,
            MathF.Round(delta.Y / snap) * snap,
            MathF.Round(delta.Z / snap) * snap
        );
    }

    private static Vector3 ApplyScaleDelta(Vector3 startScale, Vector3 delta, Handle handle, float gizmoScale)
    {
        var sensitivity = 1f / gizmoScale;
        return handle switch
        {
            Handle.ScaleX => startScale + new Vector3(delta.X * sensitivity, 0, 0),
            Handle.ScaleY => startScale + new Vector3(0, delta.Y * sensitivity, 0),
            Handle.ScaleZ => startScale + new Vector3(0, 0, delta.Z * sensitivity),
            Handle.ScaleCenter => startScale + Vector3.One * (delta.X + delta.Y + delta.Z) * sensitivity / 3f,
            _ => startScale
        };
    }

    private void RebuildTranslateMesh(Vector3 origin, float scale)
    {
        _rendering.MeshClear(_mesh);
        Actor.Transform.Position = origin;
        Actor.Transform.Scale = Vector3.One * scale;

        _translateX.SetColor(_rendering, _hoveredHandle == Handle.TranslateX);
        _translateY.SetColor(_rendering, _hoveredHandle == Handle.TranslateY);
        _translateZ.SetColor(_rendering, _hoveredHandle == Handle.TranslateZ);
        _planeXz.SetColor(_rendering, _hoveredHandle == Handle.PlaneXz);
        _planeXy.SetColor(_rendering, _hoveredHandle == Handle.PlaneXy);
        _planeYz.SetColor(_rendering, _hoveredHandle == Handle.PlaneYz);

        _translateX.AddSurface(_rendering, _mesh, PrimitiveType.TriangleList);
        _translateY.AddSurface(_rendering, _mesh, PrimitiveType.TriangleList);
        _translateZ.AddSurface(_rendering, _mesh, PrimitiveType.TriangleList);
        _planeXz.AddSurface(_rendering, _mesh, PrimitiveType.TriangleList);
        _planeXy.AddSurface(_rendering, _mesh, PrimitiveType.TriangleList);
        _planeYz.AddSurface(_rendering, _mesh, PrimitiveType.TriangleList);

        _rendering.InstanceSetVisibility(Actor.InstanceRid, true);
    }

    private void RebuildRingMesh(Vector3 origin, float scale)
    {
        _rendering.MeshClear(_mesh);
        Actor.Transform.Position = origin;
        Actor.Transform.Scale = Vector3.One * scale;

        _ring.SetColor(_rendering, _hoveredHandle == Handle.Ring);
        _ring.AddSurface(_rendering, _mesh, PrimitiveType.TriangleList);

        _rendering.InstanceSetVisibility(Actor.InstanceRid, true);
    }

    private void RebuildScaleMesh(Vector3 origin, float gizmoScale)
    {
        _rendering.MeshClear(_mesh);
        Actor.Transform.Position = origin;
        Actor.Transform.Scale = Vector3.One * gizmoScale;

        _scaleX.SetColor(_rendering, _hoveredHandle == Handle.ScaleX);
        _scaleY.SetColor(_rendering, _hoveredHandle == Handle.ScaleY);
        _scaleZ.SetColor(_rendering, _hoveredHandle == Handle.ScaleZ);
        _scaleCenter.SetColor(_rendering, _hoveredHandle == Handle.ScaleCenter);

        _scaleX.AddSurface(_rendering, _mesh, PrimitiveType.TriangleList);
        _scaleY.AddSurface(_rendering, _mesh, PrimitiveType.TriangleList);
        _scaleZ.AddSurface(_rendering, _mesh, PrimitiveType.TriangleList);
        _scaleCenter.AddSurface(_rendering, _mesh, PrimitiveType.TriangleList);

        _rendering.InstanceSetVisibility(Actor.InstanceRid, true);
    }

    private void RebuildFaceMesh(Vector3 origin, Vector3 faceNormal, float scale)
    {
        _rendering.MeshClear(_mesh);
        Actor.Transform.Position = origin;
        Actor.Transform.Scale = Vector3.One * scale;

        _face.SetColor(_rendering, _hoveredHandle == Handle.Face);
        var arrow = MeshSurface.CreateArrow(faceNormal,
            ArrowSidesTranslate, ArrowShaftLength, ArrowShaftRadius, ArrowTipLength, ArrowTipRadius);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, arrow, _face.Mat);

        _rendering.InstanceSetVisibility(Actor.InstanceRid, true);
    }

    private static MeshSurface MakePlaneXz(float size, float dist)
    {
        var h = size / 2f;
        return new MeshSurface
        {
            Vertices = new[]
            {
                new Vector3(dist - h, 0, dist - h), new Vector3(dist + h, 0, dist - h),
                new Vector3(dist - h, 0, dist + h), new Vector3(dist + h, 0, dist + h)
            },
            Normals = new[] { Vector3.UnitY, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY },
            Indices = new[] { 0, 1, 2, 2, 1, 3 }
        };
    }

    private static MeshSurface MakePlaneXy(float size, float dist)
    {
        var h = size / 2f;
        return new MeshSurface
        {
            Vertices = new[]
            {
                new Vector3(dist - h, dist - h, 0), new Vector3(dist + h, dist - h, 0),
                new Vector3(dist - h, dist + h, 0), new Vector3(dist + h, dist + h, 0)
            },
            Normals = new[] { -Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitZ },
            Indices = new[] { 0, 1, 2, 2, 1, 3 }
        };
    }

    private static MeshSurface MakePlaneYz(float size, float dist)
    {
        var h = size / 2f;
        return new MeshSurface
        {
            Vertices = new[]
            {
                new Vector3(0, dist - h, dist - h), new Vector3(0, dist + h, dist - h),
                new Vector3(0, dist - h, dist + h), new Vector3(0, dist + h, dist + h)
            },
            Normals = new[] { Vector3.UnitX, Vector3.UnitX, Vector3.UnitX, Vector3.UnitX },
            Indices = new[] { 0, 1, 2, 2, 1, 3 }
        };
    }

    private enum Handle
    {
        None,
        TranslateX,
        TranslateY,
        TranslateZ,
        PlaneXz,
        PlaneXy,
        PlaneYz,
        ScaleX,
        ScaleY,
        ScaleZ,
        ScaleCenter,
        Ring,
        Face
    }

    private readonly struct GizmoHandle
    {
        public readonly Rid Mat;
        public readonly MeshSurface? Mesh;

        private readonly Color _normal;
        private readonly Color _bright;

        public GizmoHandle(RenderingService r, BlendMode blend, Color normal, Color bright, MeshSurface? mesh = null)
        {
            Mat = r.MaterialCreate();
            r.MaterialAssignEffect(Mat, r.BasicEffect);
            r.MaterialSetCullMode(Mat, CullMode.None);
            r.MaterialSetBlendMode(Mat, blend);
            r.MaterialSetDepthTest(Mat, CompareFunction.Always);
            r.MaterialSetDepthWrite(Mat, false);
            Mesh = mesh;
            _normal = normal;
            _bright = bright;
        }

        public void SetColor(RenderingService r, bool hovered)
        {
            r.MaterialSetAlbedo(Mat, hovered ? _bright : _normal);
        }

        public void AddSurface(RenderingService r, Rid mesh, PrimitiveType primitive)
        {
            if (Mesh != null)
            {
                r.MeshAddSurface(mesh, primitive, Mesh, Mat);
            }
        }

        public void Dispose(RenderingService r) => r.FreeRid(Mat);
    }
}