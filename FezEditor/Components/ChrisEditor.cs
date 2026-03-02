using FezEditor.Actors;
using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

public partial class ChrisEditor : EditorComponent
{
    public override object Asset => _subject.GetAsset(_obj);

    private readonly ISubject _subject;
    
    private readonly ExportService _exportService;

    private readonly ConfirmWindow _confirm;

    private readonly HashSet<int> _selectedTriles = new();
    
    private int _currentTrile = -1;

    private string _filterTriles = "";
    
    private Scene _scene = null!;

    private Actor _cameraActor = null!;

    private Actor _meshActor = null!;

    private TrixelObject _obj = null!;

    private bool _showProperties;

    private bool _showTexture;

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
        _exportService = game.GetService<ExportService>();
        _exportService.TextureReloaded += OnTextureReload;
        History.StateChanged += RevisualizeSubject;
        Game.AddComponent(_confirm = new ConfirmWindow(game));
    }

    public override void LoadContent()
    {
        _scene = new Scene(Game);
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
    }

    public override void Update(GameTime gameTime)
    {
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
        }
        
        DrawPropertiesWindow();
        DrawTextureWindow();
        
        ImGui.PopStyleVar();
    }

    private void DrawToolbar()
    {
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

                var gizmo = _cameraActor.GetComponent<OrientationGizmo>();
                {
                    var imageMin = ImGuiX.GetItemRectMin();
                    gizmo.UseFaceLabels = true;
                    gizmo.Draw(imageMin + new Vector2(size.X - 8f, 8f));
                }
            }
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
                if (ImGui.Button("Edit Externally"))
                {
                    var exportKey = _subject.TextureExportKey;
                    _exportService.ExportTexture(exportKey, texture);
                    {
                        _confirm.Title = "Export";
                        _confirm.Text = $"The texture has been exported to\n'{exportKey}'";
                        _confirm.ConfirmButtonText = "Ok";
                        _confirm.CancelButtonText = "";
                        _confirm.Confirmed = () => _exportService.EditTexture(exportKey);
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
                    var x = imageMin.X + i* colW;
                    drawList.AddLine(
                        p1: new NVector2(x, imageMin.Y),
                        p2: new NVector2(x, imageMax.Y), 
                        col: new Color(0, 0.5f, 1, 0.5f).PackedValue
                    );
                    
                    if (i < 6)
                    {
                        drawList.AddText(
                            pos: new NVector2(x + 2, imageMin.Y + 2),
                            col: new Color(0, 0.5f, 1, 1f).PackedValue,
                            text_begin: faces[i].ToString()
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
        _exportService.TextureReloaded -= OnTextureReload;
        _exportService.UntrackTexture(_subject.TextureExportKey);
        _scene.Dispose();
        _subject.Dispose();
        base.Dispose();
    }

    private void RevisualizeSubject()
    {
        _obj = _subject.Materialize();

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

        var zoom = _cameraActor.GetComponent<ZoomControl>();
        zoom.Distance = _obj.Size.X * 2f;
    }
    
    private void OnTextureReload(string path, Texture2D newTexture)
    {
        if (path != _subject.TextureExportKey) return;

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