using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Sky;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class LukeEditor : EditorComponent
{
    private const float DefaultInspectorWidth = 480f;

    private const float PreviewTextureWidth = 256f;

    public override object Asset => _sky;

    private readonly Sky _sky;

    private readonly List<TempTextureTracker> _trackers = new();

    private Clock _clock = null!;

    private Scene _scene = null!;

    private Actor _cameraActor = null!;

    private Actor _skyActor = null!;

    private Actor _levelCube = null!;

    private bool _revisualize;

    private bool _showProperties;

    public LukeEditor(Game game, string title, Sky sky) : base(game, title)
    {
        _sky = sky;
        History.Track(sky);
    }

    public override void LoadContent()
    {
        _clock = new Clock();
        _scene = new Scene(Game, ContentManager);
        Camera camera;
        {
            _cameraActor = _scene.CreateActor();
            _cameraActor.Name = "Camera";
            camera = _cameraActor.AddComponent<Camera>();
            camera.Projection = Camera.ProjectionType.Orthographic;
            camera.Offset = Vector3.Backward * 250f;
            camera.Size = 80f / 3f;
        }
        {
            _skyActor = _scene.CreateActor();
            _skyActor.Name = "Sky";
            var visualizer = _skyActor.AddComponent<SkyVisualizer>();
            visualizer.Initialize(_scene, camera, _clock);
        }
        {
            _levelCube = _scene.CreateActor();
            _levelCube.Name = "LevelQuad";
            var mesh = _levelCube.AddComponent<SimpleMesh>();
            mesh.Visualize(MeshSurface.CreateFaceQuad(Vector3.One, FaceOrientation.Back));
        }

        RevisualizeSky();
    }

    private void RevisualizeSky()
    {
        DisposeTrackers();

        var visualizer = _skyActor.GetComponent<SkyVisualizer>();
        visualizer.Visualize(_sky);
        visualizer.VisualizeShadows(_sky.Name, _sky.Shadows);

        _levelCube.Transform.Scale = visualizer.LevelSize;
        _levelCube.Visible = visualizer.Shadows;

        if (!ResourceService.IsReadonly)
        {
            var textureNames = new List<string>();

            if (!string.IsNullOrEmpty(_sky.Background))
            {
                textureNames.Add(_sky.Background);
            }

            if (!string.IsNullOrEmpty(_sky.Stars))
            {
                textureNames.Add(_sky.Stars);
            }

            if (!string.IsNullOrEmpty(_sky.CloudTint))
            {
                textureNames.Add(_sky.CloudTint);
            }

            if (!string.IsNullOrEmpty(_sky.Shadows))
            {
                textureNames.Add(_sky.Shadows);
            }

            foreach (var cloud in _sky.Clouds)
            {
                if (!string.IsNullOrEmpty(cloud))
                {
                    textureNames.Add(cloud);
                }
            }

            foreach (var layer in _sky.Layers)
            {
                if (!string.IsNullOrEmpty(layer.Name))
                {
                    textureNames.Add(layer.Name);
                }
            }

            foreach (var textureName in textureNames)
            {
                var path = ResourceService.GetFullPath($"Skies/{_sky.Name}/{textureName}");
                if (!File.Exists(path))
                {
                    continue;
                }

                var tracker = new TempTextureTracker(Game, path);
                tracker.Changed += _ => RevisualizeSky();
                _trackers.Add(tracker);
            }
        }
    }

    private void DisposeTrackers()
    {
        foreach (var tracker in _trackers)
        {
            tracker.Dispose();
        }

        _trackers.Clear();
    }

    public override void Update(GameTime gameTime)
    {
        _clock.Tick(gameTime);
        _scene.Update(gameTime);
    }

    public override void Draw()
    {
        if (_revisualize)
        {
            RevisualizeSky();
            _revisualize = false;
        }

        DrawToolbar();
        DrawSceneViewport();
        DrawPropertiesWindow();
    }

    private void DrawToolbar()
    {
        ImGui.BeginDisabled(_showProperties);
        if (ImGui.Button($"{Icons.SymbolProperty} Properties"))
        {
            _showProperties = true;
        }
        ImGui.EndDisabled();

        var visualizer = _skyActor.GetComponent<SkyVisualizer>();
        var icon = visualizer.Shadows ? Icons.EyeClosed : Icons.Eye;

        ImGui.SameLine();
        if (ImGui.Button($"{icon} Shadows"))
        {
            visualizer.Shadows = !visualizer.Shadows;
            _levelCube.Visible = visualizer.Shadows;
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
                InputService.IsViewportHovered = ImGui.IsItemHovered();

                var imageMin = ImGuiX.GetItemRectMin();
                ImGuiX.DrawStats(imageMin + new Vector2(8, 8), RenderingService.GetStats());

                var topCenter = imageMin + new Vector2(size.X / 2f, 8f);
                ImGuiX.DrawClock(topCenter, _clock);
            }
        }
    }

    private void DrawPropertiesWindow()
    {
        if (!_showProperties)
        {
            return;
        }

        ImGuiX.SetNextWindowSize(new Vector2(DefaultInspectorWidth, 0), ImGuiCond.Appearing);
        var viewport = ImGui.GetMainViewport();
        var pos = new Vector2(viewport.WorkPos.X + viewport.WorkSize.X - DefaultInspectorWidth, viewport.WorkPos.Y);
        ImGuiX.SetNextWindowPos(new Vector2(pos.X, pos.Y), ImGuiCond.Appearing);
        if (ImGui.Begin($"Properties##{Title}", ref _showProperties))
        {
            _revisualize = false;

            {
                var name = _sky.Name;
                if (ImGui.InputText("Name", ref name, 255))
                {
                    using (History.BeginScope("Edit Name"))
                    {
                        _sky.Name = name;
                    }
                }

                var background = _sky.Background;
                if (ImGui.InputText("Background", ref background, 255))
                {
                    using (History.BeginScope("Edit Background"))
                    {
                        _sky.Background = background;
                        _revisualize = true;
                    }
                }

                DrawTexturePreview(_sky.Background);

                var windSpeed = _sky.WindSpeed;
                if (ImGui.DragFloat("Wind Speed", ref windSpeed, 0.01f))
                {
                    using (History.BeginScope("Edit Wind Speed"))
                    {
                        _sky.WindSpeed = windSpeed;
                        _revisualize = true;
                    }
                }

                var density = _sky.Density;
                if (ImGui.DragFloat("Density", ref density, 0.01f, 0f, 10f))
                {
                    using (History.BeginScope("Edit Density"))
                    {
                        _sky.Density = density;
                        _revisualize = true;
                    }
                }

                var fogDensity = _sky.FogDensity;
                if (ImGui.DragFloat("Fog Density", ref fogDensity, 0.01f))
                {
                    using (History.BeginScope("Edit Fog Density"))
                    {
                        _sky.FogDensity = fogDensity;
                    }
                }

                var cloudsParallax = _sky.CloudsParallax;
                if (ImGui.DragFloat("Clouds Parallax", ref cloudsParallax, 0.01f))
                {
                    using (History.BeginScope("Edit Clouds Parallax"))
                    {
                        _sky.CloudsParallax = cloudsParallax;
                        _revisualize = true;
                    }
                }

                var windParallax = _sky.WindParallax;
                if (ImGui.DragFloat("Wind Parallax", ref windParallax, 0.01f))
                {
                    using (History.BeginScope("Edit Wind Parallax"))
                    {
                        _sky.WindParallax = windParallax;
                        _revisualize = true;
                    }
                }

                var windDistance = _sky.WindDistance;
                if (ImGui.DragFloat("Wind Distance", ref windDistance, 0.01f))
                {
                    using (History.BeginScope("Edit Wind Distance"))
                    {
                        _sky.WindDistance = windDistance;
                        _revisualize = true;
                    }
                }
            }

            if (ImGui.CollapsingHeader("Layers", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var verticalTiling = _sky.VerticalTiling;
                if (ImGui.Checkbox("Vertical Tiling", ref verticalTiling))
                {
                    using (History.BeginScope("Edit Vertical Tiling"))
                    {
                        _sky.VerticalTiling = verticalTiling;
                        _revisualize = true;
                    }
                }

                var horizontalScrolling = _sky.HorizontalScrolling;
                if (ImGui.Checkbox("Horizontal Scrolling", ref horizontalScrolling))
                {
                    using (History.BeginScope("Edit Horizontal Scrolling"))
                    {
                        _sky.HorizontalScrolling = horizontalScrolling;
                        _revisualize = true;
                    }
                }

                var noPerFaceLayerXOffset = _sky.NoPerFaceLayerXOffset;
                if (ImGui.Checkbox("No Per Face Layer X Offset", ref noPerFaceLayerXOffset))
                {
                    using (History.BeginScope("Edit No Per Face Layer X Offset"))
                    {
                        _sky.NoPerFaceLayerXOffset = noPerFaceLayerXOffset;
                        _revisualize = true;
                    }
                }

                var layerBaseHeight = _sky.LayerBaseHeight;
                if (ImGui.DragFloat("Layer Base Height", ref layerBaseHeight, 0.01f))
                {
                    using (History.BeginScope("Edit Layer Base Height"))
                    {
                        _sky.LayerBaseHeight = layerBaseHeight;
                        _revisualize = true;
                    }
                }

                var layerBaseSpacing = _sky.LayerBaseSpacing;
                if (ImGui.DragFloat("Layer Base Spacing", ref layerBaseSpacing, 0.01f))
                {
                    using (History.BeginScope("Edit Layer Base Spacing"))
                    {
                        _sky.LayerBaseSpacing = layerBaseSpacing;
                        _revisualize = true;
                    }
                }

                var layerBaseXOffset = _sky.LayerBaseXOffset;
                if (ImGui.DragFloat("Layer Base X Offset", ref layerBaseXOffset, 0.01f))
                {
                    using (History.BeginScope("Edit Layer Base X Offset"))
                    {
                        _sky.LayerBaseXOffset = layerBaseXOffset;
                        _revisualize = true;
                    }
                }

                var horizontalDistance = _sky.HorizontalDistance;
                if (ImGui.DragFloat("Horizontal Distance", ref horizontalDistance, 0.01f))
                {
                    using (History.BeginScope("Edit Horizontal Distance"))
                    {
                        _sky.HorizontalDistance = horizontalDistance;
                        _revisualize = true;
                    }
                }

                var verticalDistance = _sky.VerticalDistance;
                if (ImGui.DragFloat("Vertical Distance", ref verticalDistance, 0.01f))
                {
                    using (History.BeginScope("Edit Vertical Distance"))
                    {
                        _sky.VerticalDistance = verticalDistance;
                        _revisualize = true;
                    }
                }

                var interLayerHorizontalDistance = _sky.InterLayerHorizontalDistance;
                if (ImGui.DragFloat("Inter Layer H Distance", ref interLayerHorizontalDistance, 0.01f))
                {
                    using (History.BeginScope("Edit Inter Layer Horizontal Distance"))
                    {
                        _sky.InterLayerHorizontalDistance = interLayerHorizontalDistance;
                        _revisualize = true;
                    }
                }

                var interLayerVerticalDistance = _sky.InterLayerVerticalDistance;
                if (ImGui.DragFloat("Inter Layer V Distance", ref interLayerVerticalDistance, 0.01f))
                {
                    using (History.BeginScope("Edit Inter Layer Vertical Distance"))
                    {
                        _sky.InterLayerVerticalDistance = interLayerVerticalDistance;
                        _revisualize = true;
                    }
                }
            }

            if (ImGui.CollapsingHeader("Shadows", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var shadows = _sky.Shadows;
                if (ImGui.InputText("Texture Name##Shadows", ref shadows, 255))
                {
                    using (History.BeginScope("Edit Shadows"))
                    {
                        _sky.Shadows = shadows;
                        _revisualize = true;
                    }
                }

                DrawTexturePreview(_sky.Shadows);

                var shadowOpacity = _sky.ShadowOpacity;
                if (ImGui.DragFloat("Shadow Opacity", ref shadowOpacity, 0.01f, 0f, 1f))
                {
                    using (History.BeginScope("Edit Shadow Opacity"))
                    {
                        _sky.ShadowOpacity = shadowOpacity;
                        _revisualize = true;
                    }
                }

                var foliageShadows = _sky.FoliageShadows;
                if (ImGui.Checkbox("Foliage Shadows", ref foliageShadows))
                {
                    using (History.BeginScope("Edit Foliage Shadows"))
                    {
                        _sky.FoliageShadows = foliageShadows;
                        _revisualize = true;
                    }
                }

            }

            if (ImGui.CollapsingHeader("Stars", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var stars = _sky.Stars;
                if (ImGui.InputText("Texture Name##Stars", ref stars, 255))
                {
                    using (History.BeginScope("Edit Stars"))
                    {
                        _sky.Stars = stars;
                        _revisualize = true;
                    }
                }

                DrawTexturePreview(_sky.Stars);
            }

            if (ImGui.CollapsingHeader("Cloud Tint", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var cloudTint = _sky.CloudTint;
                if (ImGui.InputText("Texture Name##CloudTint", ref cloudTint, 255))
                {
                    using (History.BeginScope("Edit Cloud Tint"))
                    {
                        _sky.CloudTint = cloudTint;
                        _revisualize = true;
                    }
                }

                DrawTexturePreview(_sky.CloudTint);
            }

            if (DrawClouds())
            {
                _revisualize = true;
            }

            if (DrawLayers())
            {
                _revisualize = true;
            }
        }

        ImGui.End();
    }

    private bool DrawClouds()
    {
        var changed = false;

        if (ImGui.CollapsingHeader("Clouds", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (ImGui.Button($"{Icons.Add} Add##AddCloud"))
            {
                using (History.BeginScope("Add Cloud"))
                {
                    _sky.Clouds.Add("");
                    changed = true;
                }
            }

            for (var i = 0; i < _sky.Clouds.Count; i++)
            {
                ImGui.PushID(i);

                var cloud = _sky.Clouds[i];
                if (ImGui.InputText("Texture Name##Cloud", ref cloud, 255))
                {
                    using (History.BeginScope("Edit Cloud"))
                    {
                        _sky.Clouds[i] = cloud;
                        changed = true;
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button(Icons.Trash))
                {
                    using (History.BeginScope("Remove Cloud"))
                    {
                        _sky.Clouds.RemoveAt(i);
                        changed = true;
                    }

                    ImGui.PopID();
                    break;
                }

                DrawTexturePreview(_sky.Clouds[i]);
                ImGui.PopID();
            }
        }

        return changed;
    }

    private bool DrawLayers()
    {
        var changed = false;

        if (ImGui.CollapsingHeader("Layers", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (ImGui.Button($"{Icons.Add} Add##AddLayer"))
            {
                using (History.BeginScope("Add Layer"))
                {
                    _sky.Layers.Add(new SkyLayer());
                    changed = true;
                }
            }

            for (var i = 0; i < _sky.Layers.Count; i++)
            {
                var layer = _sky.Layers[i];
                var label = string.IsNullOrEmpty(layer.Name) ? $"Layer {i}" : layer.Name;

                ImGui.PushID(i);

                if (ImGui.TreeNode($"{label}##Layer"))
                {
                    var name = layer.Name;
                    if (ImGui.InputText("Texture Name##LayerName", ref name, 255))
                    {
                        using (History.BeginScope("Edit Layer Name"))
                        {
                            layer.Name = name;
                            changed = true;
                        }
                    }

                    DrawTexturePreview(layer.Name);

                    var inFront = layer.InFront;
                    if (ImGui.Checkbox("In Front", ref inFront))
                    {
                        using (History.BeginScope("Edit Layer In Front"))
                        {
                            layer.InFront = inFront;
                            changed = true;
                        }
                    }

                    var opacity = layer.Opacity;
                    if (ImGui.DragFloat("Opacity", ref opacity, 0.01f, 0f, 1f))
                    {
                        using (History.BeginScope("Edit Layer Opacity"))
                        {
                            layer.Opacity = opacity;
                            changed = true;
                        }
                    }

                    var fogTint = layer.FogTint;
                    if (ImGui.DragFloat("Fog Tint", ref fogTint, 0.01f, 0f, 1f))
                    {
                        using (History.BeginScope("Edit Layer Fog Tint"))
                        {
                            layer.FogTint = fogTint;
                            changed = true;
                        }
                    }

                    if (ImGui.Button($"{Icons.Trash} Remove"))
                    {
                        using (History.BeginScope("Remove Layer"))
                        {
                            _sky.Layers.RemoveAt(i);
                            changed = true;
                        }

                        ImGui.TreePop();
                        ImGui.PopID();
                        break;
                    }

                    ImGui.TreePop();
                }

                ImGui.PopID();
            }
        }

        return changed;
    }

    private void DrawTexturePreview(string textureName)
    {
        if (string.IsNullOrEmpty(textureName))
        {
            return;
        }

        var visualizer = _skyActor.GetComponent<SkyVisualizer>();
        var texture = visualizer.GetPreviewTexture(textureName);
        if (texture is { IsDisposed: false })
        {
            var availWidth = ImGuiX.GetContentRegionAvail().X;
            var aspect = (float)texture.Height / texture.Width;
            var previewWidth = MathHelper.Min(availWidth, PreviewTextureWidth);
            var previewHeight = previewWidth * aspect;
            ImGuiX.Image(texture,
                new Vector2(previewWidth, previewHeight),
                Vector2.Zero, Vector2.One,
                Color.White, Color.LightGray);

            ImGui.SameLine();
            if (ImGui.Button($"{Icons.Folder}##{textureName}"))
            {
                ResourceService.OpenInFileManager($"Skies/{_sky.Name}/{textureName}");
            }
        }
    }

    public override void Dispose()
    {
        DisposeTrackers();
        _scene.Dispose();
        base.Dispose();
    }

    public static Sky Create(string name)
    {
        return new Sky
        {
            Name = name
        };
    }
}