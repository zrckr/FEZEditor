using System.Runtime.InteropServices;
using FezEditor.Actors;
using FezEditor.Components.Chris;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class ChrisEditor : EditorComponent, IChrisEditor
{
    public override object Asset => _context.Dematerialize(Obj);

    public bool IsViewportHovered { get; private set; }

    public TrixelObject Obj { get; }

    public TrixelFace? Hit { get; private set; }

    public CursorMesh Cursor => _cursorActor.GetComponent<CursorMesh>();

    public TrixelsMesh Trixels => _meshActor.GetComponent<TrixelsMesh>();

    public Gizmo Gizmo => _gizmoActor.GetComponent<Gizmo>();

    public Color PaintColor { get; set; } = new(255, 255, 255, 0);

    public ChrisTool CurrentTool { get; set; } = ChrisTool.Select;

    public HashSet<TrixelFace> SelectedFaces { get; } = new();

    public FaceOrientation? SelectionOrientation { get; set; }

    public PaintMode CurrentPaintMode { get; set; } = PaintMode.Color;

    private readonly IContext _context;

    private readonly ConfirmWindow _confirm;

    private readonly HashSet<int> _selectedTriles = new();

    private int _currentTrile = -1;

    private string _filterTriles = "";

    private Scene _scene = null!;

    private Actor _cameraActor = null!;

    private Actor _meshActor = null!;

    private Actor _cursorActor = null!;

    private Actor _boundsActor = null!;

    private Actor _gizmoActor = null!;

    private Actor _collisionActor = null!;

    private bool _showProperties;

    private bool _showTexture;

    private bool _showTrileSetList = true;

    private readonly List<BaseTool> _tools = new();

    public ChrisEditor(Game game, string title, ArtObject ao) : this(game, title, new ArtObjectContext(ao))
    {
    }

    public ChrisEditor(Game game, string title, TrileSet set) : this(game, title, new TrileSetContext(set, game))
    {
    }

    private ChrisEditor(Game game, string title, IContext context) : base(game, title)
    {
        _context = context;
        Obj = context.Materialize();
        History.Track(Obj);
        History.StateChanged += _ => RevisualizeSubject();
        Game.AddComponent(_confirm = new ConfirmWindow(game));
    }

    public override void Dispose()
    {
        Game.RemoveComponent(_confirm);
        _scene.Dispose();
        _context.Dispose();
        base.Dispose();
    }

    public override void LoadContent()
    {
        _scene = new Scene(Game, ContentManager);
        _scene.Viewport.SetClearColor(new Color(0.04f, 0.04f, 0.045f));
        _scene.Lighting.Ambient = Color.LightGray;
        {
            _cameraActor = _scene.CreateActor();
            _cameraActor.Name = "Camera";

            var camera = _cameraActor.AddComponent<Camera>();
            var zoom = _cameraActor.AddComponent<ZoomControl>();
            var orbit = _cameraActor.AddComponent<OrbitControl>();
            _cameraActor.AddComponent<OrientationGizmo>();

            camera.Projection = Camera.ProjectionType.Perspective;
            camera.FieldOfView = 90f;
            camera.Near = 0.01f;
            camera.Far = 50f;
            zoom.MinDistance = 10f / 16f;
            zoom.MaxDistance = 16f;
            orbit.UseRightMouseButton = true;
        }
        {
            _meshActor = _scene.CreateActor();
            _meshActor.AddComponent<TrixelsMesh>();
        }
        {
            _collisionActor = _scene.CreateActor();
            _collisionActor.AddComponent<TrileCollisionMesh>();
        }
        {
            _boundsActor = _scene.CreateActor();
            _boundsActor.AddComponent<BoundsMesh>();
        }
        {
            _cursorActor = _scene.CreateActor();
            _cursorActor.AddComponent<CursorMesh>();
        }
        {
            _gizmoActor = _scene.CreateActor();
            var gizmo = _gizmoActor.AddComponent<Gizmo>();
            gizmo.Camera = _cameraActor.GetComponent<Camera>();
        }
        {
            _tools.Add(new SelectTool(Game, this));
            _tools.Add(new ExtrudeTool(Game, this));
            _tools.Add(new PaintTool(Game, this));
            _tools.Add(new BucketTool(Game, this));
            _tools.Add(new PickTool(Game, this));
        }

        RevisualizeSubject();
        var zoom1 = _cameraActor.GetComponent<ZoomControl>();
        zoom1.Distance = Obj.Size.X * 1.1f;
    }

    public override void Update(GameTime gameTime)
    {
        Gizmo.Hide();
        Cursor.ClearHover();
        Cursor.ClearSelection();
        StatusService.ClearHints();

        foreach (var tool in _tools)
        {
            tool.Update();
        }

        _scene.Update(gameTime);
    }

    public override void Draw()
    {
        DrawToolbar();

        var size = ImGuiX.GetContentRegionAvail();
        var w = (int)size.X;
        var h = (int)size.Y;

        if (w > 0 && h > 0)
        {
            var texture = _scene.Viewport.GetTexture();
            if (texture == null || texture.Width != w || texture.Height != h)
            {
                _scene.Viewport.SetSize(w, h);
            }

            if (texture is { IsDisposed: false })
            {
                ImGuiX.Image(texture, size);
                const ImGuiHoveredFlags hoverFlags = ImGuiHoveredFlags.AllowWhenBlockedByActiveItem |
                                                     ImGuiHoveredFlags.AllowWhenBlockedByPopup;
                InputService.IsViewportHovered = ImGui.IsItemHovered(hoverFlags);

                var viewportMin = ImGuiX.GetItemRectMin();
                _gizmoActor.GetComponent<Gizmo>().Viewport = viewportMin;

                Hit = null;
                IsViewportHovered = ImGui.IsItemHovered(hoverFlags) && !ImGui.IsMouseDragging(ImGuiMouseButton.Right);
                if (IsViewportHovered)
                {
                    var ray = _scene.Viewport.Unproject(ImGuiX.GetMousePos(), viewportMin);
                    Hit = RaycastTrixelFace(ray);
                }

                foreach (var tool in _tools)
                {
                    tool.DrawOverlay();
                }

                var orientation = _cameraActor.GetComponent<OrientationGizmo>();
                orientation.UseFaceLabels = true;
                orientation.Draw(viewportMin + new Vector2(size.X - 8f, 8f));
                ImGuiX.DrawStats(viewportMin + new Vector2(8, 8), RenderingService.GetStats());
            }
        }

        #region Trile Set List

        if (_context is TrileSetContext subject && _showTrileSetList)
        {
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoCollapse;
            ImGuiX.SetNextWindowSize(new Vector2(320, 480), ImGuiCond.Appearing);
            if (!ImGui.Begin($"Trile Set##{Title}", ref _showTrileSetList, flags))
            {
                ImGui.End();
            }
            else
            {
                ImGui.Text("Name:");
                ImGui.SameLine();

                var name = subject.Name;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##TrileSetName", ref name, 255))
                {
                    using (History.BeginScope("Rename Trile Set"))
                    {
                        subject.Name = name;
                    }
                }

                // Toolbar
                {
                    if (ImGui.Button($"{Lucide.Plus}"))
                    {
                        using (History.BeginScope("Add Trile"))
                        {
                            var newId = subject.AddDefaultTrile();
                            _selectedTriles.Clear();
                            _selectedTriles.Add(newId);
                            _currentTrile = newId;
                            subject.Id = newId;
                            SwitchTrileSubject();
                        }
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Add Trile");
                    }
                }

                ImGui.SameLine();
                {
                    ImGui.BeginDisabled(_selectedTriles.Count == 0);
                    if (ImGui.Button($"{Lucide.Minus}"))
                    {
                        using (History.BeginScope("Remove Trile"))
                        {
                            var nextId = subject.RemoveTriles(_selectedTriles);
                            _selectedTriles.Clear();
                            _selectedTriles.Add(nextId);
                            _currentTrile = nextId;
                            subject.Id = nextId;
                            SwitchTrileSubject();
                        }
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Remove Selected");
                    }

                    ImGui.EndDisabled();
                }

                ImGui.SameLine();
                {
                    ImGui.BeginDisabled(_selectedTriles.Count == 0);
                    if (ImGui.Button($"{Lucide.Copy}"))
                    {
                        using (History.BeginScope("Copy Triles"))
                        {
                            var newId = subject.CopyTriles(_selectedTriles);
                            _selectedTriles.Clear();
                            _selectedTriles.Add(newId);
                            _currentTrile = newId;
                            subject.Id = newId;
                            SwitchTrileSubject();
                        }
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Copy Selected");
                    }

                    ImGui.EndDisabled();
                }

                ImGui.SameLine();
                {
                    ImGui.BeginDisabled(_selectedTriles.Count == 0);
                    if (ImGui.Button($"{Lucide.ListX}"))
                    {
                        _selectedTriles.Clear();
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Clear Selection");
                    }

                    ImGui.EndDisabled();
                }

                ImGui.SameLine();
                {
                    ImGui.BeginDisabled(_selectedTriles.Count == 0);
                    if (ImGui.Button($"{Lucide.ArrowRightFromLine}"))
                    {
                        var options = new FileDialog.Options
                        {
                            Title = "Choose trile set file...",
                            Filters = new FileDialog.Filter[]
                            {
                                new("FEZTS files", "fezts.glb")
                            }
                        };

                        FileDialog.Show(FileDialog.Type.OpenFile, files =>
                        {
                            var relativePath = ResourceService.GetRelativePath(files[0]).Replace(".fezts.glb", "");
                            var targetSet = (TrileSet)ResourceService.Load(relativePath);
                            subject.AppendTriles(_selectedTriles, targetSet);
                            ResourceService.Save(relativePath, targetSet);
                        }, options);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Export Selected to File");
                    }

                    ImGui.EndDisabled();
                }

                ImGui.SameLine();
                {
                    var filterWidth = ImGui.GetContentRegionAvail().X;
                    if (!string.IsNullOrEmpty(_filterTriles))
                    {
                        filterWidth -= ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X;
                    }

                    ImGui.SetNextItemWidth(filterWidth);
                    ImGui.InputTextWithHint("##Filter", "Filter", ref _filterTriles, 255);
                    if (!string.IsNullOrEmpty(_filterTriles))
                    {
                        ImGui.SameLine();
                        if (ImGui.Button(Lucide.X))
                        {
                            _filterTriles = "";
                        }
                    }
                }

                ImGui.Separator();
                if (ImGuiX.BeginChild("##TrileSetList", Vector2.Zero))
                {
                    foreach (var entry in subject.EnumerateTriles(_filterTriles))
                    {
                        var toggled = _selectedTriles.Contains(entry.Id);
                        if (ImGui.Checkbox($"##chk_{entry.Id}", ref toggled))
                        {
                            if (toggled)
                            {
                                _selectedTriles.Add(entry.Id);
                            }
                            else
                            {
                                _selectedTriles.Remove(entry.Id);
                            }
                        }

                        ImGui.SameLine();

                        var sel = _currentTrile == entry.Id;
                        var size1 = new Vector2(32f);
                        var text = $"{entry.Id}: {entry.Name}";

                        if (ImGuiX.SelectableWithImage(entry.Texture, size1, entry.Uv0, entry.Uv1, text, sel))
                        {
                            _currentTrile = entry.Id;
                            subject.Id = _currentTrile;
                            SwitchTrileSubject();
                        }
                    }

                    ImGui.EndChild();
                }

                ImGui.End();
            }
        }

        #endregion

        #region Properties Window

        if (_showProperties)
        {
            const ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize |
                                           ImGuiWindowFlags.NoCollapse;
            if (ImGui.Begin($"Properties##{Title}", ref _showProperties, flags))
            {
                if (_context.DrawProperties(History))
                {
                    RevisualizeSubject();
                }

                ImGui.End();
            }
        }

        #endregion

        #region Texture Window

        if (_showTexture)
        {
            var trixelMesh = _meshActor.GetComponent<TrixelsMesh>();
            var texture = CurrentPaintMode is PaintMode.Emission
                ? trixelMesh.EmissionTexture!
                : trixelMesh.ColorTexture!;
            const ImGuiWindowFlags flags1 = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar;

            ImGuiX.SetNextWindowSize(new Vector2(640, 160), ImGuiCond.Appearing);
            if (ImGui.Begin($"Texture Viewer##{Title}", ref _showTexture, flags1))
            {
                var sizeText = $"Texture Size: {texture.Width}x{texture.Height}px";
                var textWidth = ImGui.CalcTextSize(sizeText).X;
                var availWidth = ImGui.GetContentRegionAvail().X;
                ImGui.SameLine(ImGui.GetCursorPosX() + availWidth - textWidth);
                ImGui.TextDisabled(sizeText);

                var availW = ImGuiX.GetContentRegionAvail().X;
                var scale = availW / texture.Width;
                var displaySize = new Vector2(texture.Width, texture.Height) * scale;
                ImGuiX.Image(texture, displaySize);

                var drawList = ImGui.GetWindowDrawList();
                var imageMin = ImGuiX.GetItemRectMin();
                var imageMax = ImGuiX.GetItemRectMax();
                var colW = displaySize.X / 6f;

                var faces = FaceExtensions.NaturalOrder;
                for (var i = 0; i <= 6; i++)
                {
                    var x = imageMin.X + (i * colW);
                    drawList.AddLine(
                        new NVector2(x, imageMin.Y),
                        new NVector2(x, imageMax.Y),
                        new Color(0, 0.5f, 1, 0.5f).PackedValue
                    );

                    if (i < 6)
                    {
                        drawList.AddText(
                            new NVector2(x + 2, imageMin.Y + 2),
                            new Color(0, 0.5f, 1, 1f).PackedValue,
                            faces[i].ToString()
                        );
                    }
                }

                ImGui.End();
            }
        }

        #endregion
    }

    private void DrawModeButton(string icon, ChrisTool tool)
    {
        ImGui.BeginDisabled(CurrentTool == tool);
        if (ImGui.Button(icon))
        {
            CurrentTool = tool;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tool.GetLabel());
        }

        ImGui.EndDisabled();
    }

    private static void DrawToggleButton(ref bool flag, string icon, string tooltip)
    {
        ImGui.BeginDisabled(flag);
        if (ImGui.Button(icon))
        {
            flag = true;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }

        ImGui.EndDisabled();
    }

    private void DrawToolbar()
    {
        DrawModeButton(Lucide.MousePointer2, ChrisTool.Select);

        ImGui.SameLine();
        DrawModeButton(Lucide.ArrowUpFromLine, ChrisTool.Extrude);

        ImGui.SameLine();
        ImGui.TextDisabled("|");

        ImGui.SameLine();
        DrawModeButton(Lucide.Paintbrush, ChrisTool.Paint);

        ImGui.SameLine();
        DrawModeButton(Lucide.PaintBucket, ChrisTool.Bucket);

        ImGui.SameLine();
        var colorButtonFlags = CurrentPaintMode is PaintMode.Emission
            ? ImGuiColorEditFlags.NoTooltip
            : ImGuiColorEditFlags.NoAlpha;
        var colorButtonColor = CurrentPaintMode is PaintMode.Emission
            ? new Color(PaintColor.A, PaintColor.A, PaintColor.A, PaintColor.A)
            : PaintColor;
        if (ImGuiX.ColorButton("##PaintButton", colorButtonColor, colorButtonFlags))
        {
            ImGui.OpenPopup("##PaintPicker");
            CurrentTool = ChrisTool.Paint;
        }

        if (CurrentPaintMode is PaintMode.Emission && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text($"Emission: {PaintColor.A}");
            ImGui.EndTooltip();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        var paintMode = (int)CurrentPaintMode;
        var paintModesList = Enum.GetNames<PaintMode>();
        if (ImGui.Combo("##PaintMode", ref paintMode, paintModesList, paintModesList.Length))
        {
            CurrentPaintMode = (PaintMode)paintMode;
            Trixels.ShowEmission = CurrentPaintMode is PaintMode.Emission;
            Trixels.Visualize(Obj);
        }

        ImGui.SameLine();
        DrawModeButton(Lucide.Pipette, ChrisTool.Pick);

        ImGui.SameLine();
        ImGui.TextDisabled("|");

        ImGui.SameLine();
        DrawToggleButton(ref _showProperties, Lucide.Wrench, "Properties");

        ImGui.SameLine();
        DrawToggleButton(ref _showTexture, Lucide.Image, "Texture");

        var mesh = _meshActor.GetComponent<TrixelsMesh>();
        var wireFrame = mesh.Wireframe ? Lucide.BugOff : Lucide.Bug;
        ImGui.SameLine();
        if (ImGui.Button(wireFrame))
        {
            mesh.Wireframe = !mesh.Wireframe;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Wireframe");
        }

        if (_context is TrileSetContext)
        {
            var collision = _collisionActor.GetComponent<TrileCollisionMesh>();
            var icon = collision.Visible ? Lucide.EyeOff : Lucide.Eye;
            ImGui.SameLine();
            if (ImGui.Button(icon))
            {
                collision.Visible = !collision.Visible;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Collision");
            }

            ImGui.SameLine();
            DrawToggleButton(ref _showTrileSetList, Lucide.List, "Trile Set");
        }

        ImGui.Separator();
    }

    private void SwitchTrileSubject()
    {
        Obj.CopyFrom(_context.Materialize());
        History.Clear();
        RevisualizeSubject();
    }

    private void RevisualizeSubject()
    {
        _context.SyncProperties(Obj);
        var mesh = _meshActor.GetComponent<TrixelsMesh>();
        mesh.SetTexture(Obj.Texture);
        mesh.Visualize(Obj);
        if (_context is TrileSetContext subject)
        {
            var collision = _collisionActor.GetComponent<TrileCollisionMesh>();
            collision.ClearInstanceData();
            collision.AddInstanceData(Vector3.Zero, subject.GetTrileCollision(), Obj.Size);
            _collisionActor.Transform.Position = -Obj.Offset;
            subject.FlushThumbnail(Obj);
        }

        var bounds = _boundsActor.GetComponent<BoundsMesh>();
        bounds.Size = Obj.Size;
        _boundsActor.Transform.Position = -Obj.Offset;
    }

    private TrixelFace? RaycastTrixelFace(Ray ray)
    {
        var best = default(TrixelFace?);
        var bestT = float.MaxValue;

        foreach (var tf in Obj.VisibleFaces)
        {
            var normal = tf.Face.AsVector();
            var denom = Vector3.Dot(normal, ray.Direction);
            if (MathF.Abs(denom) < float.Epsilon)
            {
                continue;
            }

            var faceCenter = ((tf.Emplacement.ToVector3() +
                               ((Vector3.One + normal) * 0.5f)) * Mathz.TrixelSize) - Obj.Offset;

            var t = (Vector3.Dot(normal, faceCenter) - Vector3.Dot(normal, ray.Position)) / denom;
            if (t < 0f || t >= bestT)
            {
                continue;
            }

            var hit = ray.Position + (ray.Direction * t);
            var local = hit - faceCenter;
            var tan = tf.Face.GetTangent().AsVector();
            var bitan = tf.Face.GetBitangent().AsVector();

            const float h = Mathz.TrixelSize / 2f;
            if (MathF.Abs(Vector3.Dot(local, tan)) <= h && MathF.Abs(Vector3.Dot(local, bitan)) <= h)
            {
                best = tf;
                bestT = t;
            }
        }

        return best;
    }

    public static object CreateAo(string name)
    {
        const int trileWidth = (int)(1 / Mathz.TrixelSize);
        const int trileHeight = (int)(1 / Mathz.TrixelSize);

        var ao = new ArtObject
        {
            Name = name,
            Size = new RVector3(1, 1, 1),
            Cubemap = new RTexture2D
            {
                Width = trileWidth,
                Height = trileHeight
            }
        };

        var colors = new Color[trileWidth * trileHeight];
        Array.Fill(colors, Color.White);
        ao.Cubemap.TextureData = MemoryMarshal.AsBytes(colors.AsSpan()).ToArray();

        var obj = new TrixelObject { Size = Vector3.One, Offset = Vector3.One / 2f };
        (ao.Geometry.Vertices, ao.Geometry.Indices) = TrixelMaterializer.Dematerialize(obj);

        return ao;
    }

    public static object CreateTs(string name)
    {
        var trileSet = new TrileSet
        {
            Name = name,
            Triles = new Dictionary<int, Trile>(),
            TextureAtlas = new RTexture2D()
        };
        TrileSetContext.AddDefaultTrile(trileSet, "Trile");

        return trileSet;
    }
}