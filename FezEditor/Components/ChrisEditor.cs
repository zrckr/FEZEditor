using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

public partial class ChrisEditor : EditorComponent
{
    private static readonly TimeSpan EditStep = TimeSpan.FromMilliseconds(100);

    public override object Asset => _subject.GetAsset(_obj);

    private readonly ISubject _subject;

    private readonly ConfirmWindow _confirm;

    private readonly HashSet<int> _selectedTriles = new();

    private int _currentTrile = -1;

    private string _filterTriles = "";

    private Scene _scene = null!;

    private Actor _cameraActor = null!;

    private Actor _meshActor = null!;

    private TrixelObject _obj = null!;

    private TempTextureTracker? _texture;

    private bool _showProperties;

    private bool _showTexture;

    private EditMode _editMode = EditMode.Select;

    private TrixelFace? _hoveredFace;

    private readonly HashSet<TrixelFace> _selectedFaces = new();

    private FaceOrientation? _selectionOrientation;

    private TrixelFace? _dragStartFace;

    private TimeSpan _nowTime;

    private TimeSpan _lastEditTime;

    public ChrisEditor(Game game, string title, ArtObject ao) : this(game, title, new ArtObjectSubject(ao))
    {
        History.Track(ao);
    }

    public ChrisEditor(Game game, string title, TrileSet set) : this(game, title, new TrileSetSubject(set, game))
    {
        History.Track(set);
    }

    private ChrisEditor(Game game, string title, ISubject subject) : base(game, title)
    {
        _subject = subject;
        History.StateChanged += () => RevisualizeSubject(false);
        Game.AddComponent(_confirm = new ConfirmWindow(game));
    }

    public override void LoadContent()
    {
        _scene = new Scene(Game);
        _scene.Lighting.Ambient = Color.LightGray;
        {
            _cameraActor = _scene.CreateActor();
            _cameraActor.Name = "Camera";

            var camera = _cameraActor.AddComponent<Camera>();
            var zoom = _cameraActor.AddComponent<ZoomControl>();
            _cameraActor.AddComponent<OrbitControl>();
            _cameraActor.AddComponent<OrientationGizmo>();

            camera.Projection = Camera.ProjectionType.Perspective;
            camera.FieldOfView = 90f;
            zoom.MinDistance = 10f / 16f;
            zoom.MaxDistance = 16f;
        }
        {
            _meshActor = _scene.CreateActor();
            _meshActor.AddComponent<TrixelsMesh>();
            _meshActor.AddComponent<TrileCollisionMesh>();
            _meshActor.AddComponent<BoundsMesh>();
        }

        RevisualizeSubject();
        var zoom1 = _cameraActor.GetComponent<ZoomControl>();
        zoom1.Distance = _obj.Size.X * 1.1f;
    }

    public override void Update(GameTime gameTime)
    {
        _nowTime = gameTime.TotalGameTime;
        _scene.Update(gameTime);
    }

    public override void Draw()
    {
        ImGuiX.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8));

        DrawToolbar();

        if (_subject is TrileSetSubject subject)
        {
            var width = ImGui.GetContentRegionAvail().X;
            if (ImGuiX.BeginChild("##SceneViewport", new Vector2(width - 300, 0)))
            {
                DrawSceneViewport();
                EditTrixelObject();
                ImGui.EndChild();
            }

            ImGui.SameLine();

            if (ImGuiX.BeginChild("##TrileSet", Vector2.Zero, ImGuiChildFlags.Border))
            {
                DrawTrileList(subject);
                ImGui.EndChild();
            }
        }
        else if (_subject is ArtObjectSubject)
        {
            DrawSceneViewport();
            EditTrixelObject();
        }

        DrawPropertiesWindow();
        DrawTextureWindow();

        ImGui.PopStyleVar();
    }

    private void DrawToolbar()
    {
        ImGui.BeginDisabled(_editMode == EditMode.Select);
        if (ImGui.Button($"{Icons.Cursor} Select"))
        {
            _editMode = EditMode.Select;
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.BeginDisabled(_editMode == EditMode.Remove);
        if (ImGui.Button($"{Icons.Eraser} Remove"))
        {
            _editMode = EditMode.Remove;
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.BeginDisabled(_editMode == EditMode.Add);
        if (ImGui.Button($"{Icons.Pencil} Add"))
        {
            _editMode = EditMode.Add;
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.TextDisabled("|");

        ImGui.SameLine();
        ImGui.BeginDisabled(_showProperties);
        if (ImGui.Button($"{Icons.SymbolProperty} Properties"))
        {
            _showProperties = true;
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.BeginDisabled(_showTexture);
        if (ImGui.Button($"{Icons.FileMedia} Texture"))
        {
            _showTexture = true;
        }

        ImGui.EndDisabled();

        var mesh = _meshActor.GetComponent<TrixelsMesh>();
        var wireFrame = mesh.Wireframe;
        ImGui.SameLine();
        if (ImGui.Checkbox("Wireframe", ref wireFrame))
        {
            mesh.Wireframe = wireFrame;
        }

        ImGui.EndDisabled();

        if (_subject is TrileSetSubject)
        {
            var collision = _meshActor.GetComponent<TrileCollisionMesh>();
            var icon = collision.Visible ? Icons.EyeClosed : Icons.Eye;
            ImGui.SameLine();
            if (ImGui.Button($"{icon} Collision"))
            {
                collision.Visible = !collision.Visible;
            }
        }

        ImGui.Separator();
    }

    private void DrawTrileList(TrileSetSubject setSubject)
    {
        var name = setSubject.Name;
        ImGui.SetNextItemWidth(150f);
        if (ImGui.InputText("Trile Set Name", ref name, 255))
        {
            using (History.BeginScope("Rename Trile Set"))
            {
                setSubject.Name = name;
            }
        }

        ImGui.Separator();
        if (ImGui.Button($"{Icons.Add} Add"))
        {
            using (History.BeginScope("Add Trile"))
            {
                var newId = setSubject.AddTrile();
                _selectedTriles.Clear();
                _selectedTriles.Add(newId);
                _currentTrile = newId;
                setSubject.Id = newId;
                RevisualizeSubject();
            }
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(_selectedTriles.Count == 0);
        if (ImGui.Button($"{Icons.Remove} Remove"))
        {
            using (History.BeginScope("Remove Trile"))
            {
                var nextId = setSubject.RemoveTriles(_selectedTriles);
                _selectedTriles.Clear();
                _selectedTriles.Add(nextId);
                _currentTrile = nextId;
                setSubject.Id = nextId;
                RevisualizeSubject();
            }
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.BeginDisabled(_selectedTriles.Count == 0);
        if (ImGui.Button($"{Icons.Copy} Copy"))
        {
            using (History.BeginScope("Copy Triles"))
            {
                var newId = setSubject.CopyTriles(_selectedTriles);
                _selectedTriles.Clear();
                _selectedTriles.Add(newId);
                _currentTrile = newId;
                setSubject.Id = newId;
                RevisualizeSubject();
            }
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.BeginDisabled(_selectedTriles.Count == 0);
        if (ImGui.Button($"{Icons.ClearAll} Clear"))
        {
            _selectedTriles.Clear();
        }

        ImGui.EndDisabled();

        ImGui.BeginDisabled(_selectedTriles.Count == 0);
        if (ImGui.Button($"{Icons.Export} Export Selected"))
        {
            var options = new FileDialog.Options
            {
                Title = "Choose trile set file...",
                Filters = new FileDialog.Filter[]
                {
                    new("FEZTS files", "fezts.glb")
                }
            };

            FileDialog.Show(FileDialog.Type.OpenFile, result =>
            {
                if (result.Files.Length > 0)
                {
                    var path = result.Files[0];
                    var targetSet = (TrileSet)ResourceService.Load(path);
                    setSubject.AppendTriles(_selectedTriles, targetSet);
                    ResourceService.Save(path, targetSet);
                }
            }, options);
        }

        ImGui.EndDisabled();

        ImGui.SetNextItemWidth(-40);
        ImGui.InputTextWithHint("", "Filter", ref _filterTriles, 255);

        if (!string.IsNullOrEmpty(_filterTriles))
        {
            ImGui.SameLine();
            if (ImGui.Button(Icons.ClearAll))
            {
                _filterTriles = "";
            }
        }

        ImGui.Separator();

        if (ImGuiX.BeginChild("##TrileSetList", Vector2.Zero))
        {
            foreach (var entry in setSubject.EnumerateTriles(_filterTriles))
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
                var size = new Vector2(32f);
                var text = $"{entry.Id}: {entry.Name}";

                if (ImGuiX.SelectableWithImage(entry.Texture, size, entry.Uv0, entry.Uv1, text, sel))
                {
                    _currentTrile = entry.Id;
                    setSubject.Id = _currentTrile;
                    RevisualizeSubject();
                }
            }

            ImGui.EndChild();
        }
    }

    private void DrawSceneViewport()
    {
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
                InputService.CaptureScroll(ImGui.IsItemHovered());

                var imageMin = ImGuiX.GetItemRectMin();
                var gizmo = _cameraActor.GetComponent<OrientationGizmo>();
                gizmo.UseFaceLabels = true;
                gizmo.Draw(imageMin + new Vector2(size.X - 8f, 8f));
                ImGuiX.DrawStats(imageMin + new Vector2(8, 8), RenderingService.GetStats());
            }
        }
    }

    private void EditTrixelObject()
    {
        var viewportMin = ImGuiX.GetItemRectMin();
        var mesh = _meshActor.GetComponent<TrixelsMesh>();

        if (!ImGui.IsItemHovered())
        {
            if (_hoveredFace.HasValue)
            {
                _hoveredFace = null;
                mesh.SetHoveredFace(null);
            }

            return;
        }

        var ray = _scene.Viewport.Unproject(ImGuiX.GetMousePos(), viewportMin);
        var hit = RaycastTrixelFace(ray);
        if (hit != _hoveredFace)
        {
            _hoveredFace = hit;
            mesh.SetHoveredFace(_hoveredFace);
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _dragStartFace = hit;
            if (!hit.HasValue)
            {
                _selectedFaces.Clear();
                _selectionOrientation = null;
                _editMode = EditMode.Select;
                mesh.SetSelectedFaces(_selectedFaces);
                return;
            }
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _dragStartFace = null;
            return;
        }

        switch (_editMode)
        {
            case EditMode.Select:
                {
                    if (!hit.HasValue || !_dragStartFace.HasValue)
                    {
                        break;
                    }

                    var orientation = _dragStartFace.Value.Face;
                    if (orientation != hit.Value.Face)
                    {
                        break;
                    }

                    var newSelection = BuildRectSelection(orientation, _dragStartFace.Value.Emplacement,
                        hit.Value.Emplacement);
                    if (orientation != _selectionOrientation || !newSelection.SetEquals(_selectedFaces))
                    {
                        _selectionOrientation = orientation;
                        _selectedFaces.Clear();
                        foreach (var f in newSelection)
                        {
                            _selectedFaces.Add(f);
                        }

                        mesh.SetSelectedFaces(_selectedFaces);
                    }

                    break;
                }

            case EditMode.Remove:
            case EditMode.Add:
                {
                    if (!hit.HasValue || _selectedFaces.Count == 0 ||
                        !(ImGui.IsMouseClicked(ImGuiMouseButton.Left) || _nowTime - _lastEditTime >= EditStep))
                    {
                        break;
                    }

                    _lastEditTime = _nowTime;
                    var edit = _editMode == EditMode.Remove;

                    using (History.BeginScope(edit ? "Remove Trixels" : "Add Trixels"))
                    {
                        ApplyChanges(hit.Value.Face, edit);
                    }

                    mesh.Visualize(_obj);
                    RemapSelectionAfterCarve(hit.Value.Face, edit);
                    mesh.SetSelectedFaces(_selectedFaces);

                    break;
                }

            default:
                throw new InvalidOperationException();
        }
    }

    private void DrawPropertiesWindow()
    {
        if (_showProperties)
        {
            const ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize |
                                           ImGuiWindowFlags.NoCollapse;
            if (ImGui.Begin($"Properties##{Title}", ref _showProperties, flags))
            {
                if (_subject.DrawProperties(History))
                {
                    RevisualizeSubject();
                }

                ImGui.End();
            }
        }
    }

    private void DrawTextureWindow()
    {
        if (_showTexture)
        {
            var texture = _meshActor.GetComponent<TrixelsMesh>().Texture!;
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoCollapse;

            ImGuiX.SetNextWindowSize(new Vector2(640, 160), ImGuiCond.Appearing);
            if (ImGui.Begin($"Texture Viewer##{Title}", ref _showTexture, flags))
            {
                if (!ResourceService.IsReadonly)
                {
                    var exportPath = ResourceService.GetFullPath(_subject.TextureExportKey);
                    if (ImGui.Button("Edit Externally"))
                    {
                        _texture = new TempTextureTracker(Game, texture, exportPath);
                        _texture.Changed += OnTextureReload;
                        {
                            _confirm.Title = "Export";
                            _confirm.Text = $"The texture has been exported to\n'{exportPath}'";
                            _confirm.ConfirmButtonText = "Ok";
                            _confirm.CancelButtonText = "";
                            _confirm.Confirmed = () => _texture!.OpenInEditor();
                        }
                    }
                }

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
    }

    public override void Dispose()
    {
        Game.RemoveComponent(_confirm);
        _texture?.Dispose();
        _scene.Dispose();
        _subject.Dispose();
        base.Dispose();
    }

    private void RevisualizeSubject(bool materialize = true)
    {
        if (materialize)
        {
            History.Untrack(_obj);
            _obj = _subject.Materialize();
            History.Track(_obj);
        }

        var mesh = _meshActor.GetComponent<TrixelsMesh>();
        mesh.Texture = _subject.LoadTexture();
        mesh.Visualize(_obj);

        if (_subject is TrileSetSubject subject)
        {
            var collision = _meshActor.GetComponent<TrileCollisionMesh>();
            collision.Visualize(subject.GetTrileCollision(), _obj.Size);
        }

        var bounds = _meshActor.GetComponent<BoundsMesh>();
        bounds.Visualize(_obj.Size);
    }

    private void OnTextureReload(Texture2D newTexture)
    {
        _subject.UpdateTexture(newTexture);

        var mesh = _meshActor.GetComponent<TrixelsMesh>();
        mesh.Texture = newTexture;
        mesh.Visualize(_obj);

        _confirm.Title = "Confirm texture overriding";
        _confirm.Text = $"The texture has been changed externally. Save it to the bundle '{Title}'?";
        _confirm.ConfirmButtonText = "Yes";
        _confirm.CancelButtonText = "No";
        _confirm.Confirmed = () => ResourceService.Save(Title, _subject.GetAsset(_obj));
        _confirm.Canceled = null;
    }

    private TrixelFace? RaycastTrixelFace(Ray ray)
    {
        var mesh = _meshActor.GetComponent<TrixelsMesh>();
        var meshOffset = Vector3.Zero - (_obj.Size / 2f);
        var best = default(TrixelFace?);
        var bestT = float.MaxValue;

        foreach (var tf in mesh.Faces)
        {
            var normal = tf.Face.AsVector();
            var denom = Vector3.Dot(normal, ray.Direction);
            if (MathF.Abs(denom) < 1e-6f)
            {
                continue;
            }

            var faceCenter = ((tf.Emplacement.ToVector3() + ((Vector3.One + normal) * 0.5f))
                              * Mathz.TrixelSize) + meshOffset;

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

    private void RemapSelectionAfterCarve(FaceOrientation orientation, bool inward)
    {
        if (_selectionOrientation != orientation || _selectedFaces.Count == 0)
        {
            return;
        }

        var normal = orientation.AsVector();
        var step = inward ? -normal : normal;
        var ni = new Vector3I((int)step.X, (int)step.Y, (int)step.Z);

        var newFaces = new HashSet<TrixelFace>(_meshActor.GetComponent<TrixelsMesh>().Faces);
        var remapped = new HashSet<TrixelFace>();
        foreach (var tf in _selectedFaces)
        {
            var shifted = new TrixelFace(
                new Vector3I(tf.Emplacement.X + ni.X, tf.Emplacement.Y + ni.Y, tf.Emplacement.Z + ni.Z),
                orientation);
            if (newFaces.Contains(shifted))
            {
                remapped.Add(shifted);
            }
        }

        _selectedFaces.Clear();
        foreach (var f in remapped)
        {
            _selectedFaces.Add(f);
        }
    }

    private HashSet<TrixelFace> BuildRectSelection(FaceOrientation orientation, Vector3I start, Vector3I end)
    {
        var tan = orientation.GetTangent().AsVector();
        var bitan = orientation.GetBitangent().AsVector();

        var startT = (int)((start.X * tan.X) + (start.Y * tan.Y) + (start.Z * tan.Z));
        var startB = (int)((start.X * bitan.X) + (start.Y * bitan.Y) + (start.Z * bitan.Z));
        var endT = (int)((end.X * tan.X) + (end.Y * tan.Y) + (end.Z * tan.Z));
        var endB = (int)((end.X * bitan.X) + (end.Y * bitan.Y) + (end.Z * bitan.Z));

        var minT = Math.Min(startT, endT);
        var maxT = Math.Max(startT, endT);
        var minB = Math.Min(startB, endB);
        var maxB = Math.Max(startB, endB);

        var result = new HashSet<TrixelFace>();
        var mesh = _meshActor.GetComponent<TrixelsMesh>();
        foreach (var tf in mesh.Faces)
        {
            if (tf.Face != orientation)
            {
                continue;
            }

            var t = (int)((tf.Emplacement.X * tan.X) + (tf.Emplacement.Y * tan.Y) + (tf.Emplacement.Z * tan.Z));
            var b = (int)((tf.Emplacement.X * bitan.X) + (tf.Emplacement.Y * bitan.Y) + (tf.Emplacement.Z * bitan.Z));
            if (t >= minT && t <= maxT && b >= minB && b <= maxB)
            {
                result.Add(tf);
            }
        }

        return result;
    }

    private void ApplyChanges(FaceOrientation orientation, bool missing)
    {
        var selected = new HashSet<Vector3I>(_selectedFaces
            .Where(tf => tf.Face == orientation)
            .Select(tf => tf.Emplacement));

        if (missing)
        {
            foreach (var emp in selected)
            {
                _obj.SetMissing(emp, true);
            }

            return;
        }

        var ni = new Vector3I(orientation.AsVector());
        var toAdd = new HashSet<Vector3I>();
        var size = new Vector3I(_obj.Width, _obj.Height, _obj.Depth);

        foreach (var emp in selected)
        {
            var next = emp + ni;
            if (next.LengthSquared() > 0 && next < size)
            {
                toAdd.Add(next);
            }
        }

        foreach (var emp in toAdd)
        {
            _obj.SetMissing(emp, false);
        }
    }

    private enum EditMode
    {
        Select,
        Remove,
        Add
    }

    private interface ISubject : IDisposable
    {
        string TextureExportKey { get; }

        TrixelObject Materialize();

        object GetAsset(TrixelObject obj);

        Texture2D LoadTexture();

        void UpdateTexture(Texture2D texture);

        bool DrawProperties(History history);
    }
}