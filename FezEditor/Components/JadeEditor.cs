using FezEditor.Actors;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.MapTree;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class JadeEditor : EditorComponent
{
    private const float LinkThickness = 0.05375f;

    public override object Asset => _mapTree;

    private readonly MapTree _mapTree;

    private readonly Dictionary<MapNode, NodeActors> _nodeMapping = new();

    private readonly ConfirmWindow _confirm;

    private Scene _scene = null!;

    private Actor _cameraActor = null!;

    private State _nextState = State.MapView;

    private MapNode? _selectedNode;

    private bool _showProperties;

    private Vector2 _viewportCenter;

    public JadeEditor(Game game, string title, MapTree mapTree) : base(game, title)
    {
        _mapTree = mapTree;
        History.Track(mapTree);
        History.StateChanged += () => RebuildSceneSubTree(_mapTree, _mapTree.Root);
        Game.AddComponent(_confirm = new ConfirmWindow(game));
    }

    public override void LoadContent()
    {
        _scene = new Scene(Game, ContentManager);
        _scene.Lighting.Ambient = new Color(new Vector3(1f / 3f));
        {
            _cameraActor = _scene.CreateActor();
            _cameraActor.Name = "Camera";

            var camera = _cameraActor.AddComponent<Camera>();
            var orbit = _cameraActor.AddComponent<OrbitControl>();
            _cameraActor.AddComponent<MapPanControl>();
            _cameraActor.AddComponent<MapZoomControl>();
            _cameraActor.AddComponent<OrientationGizmo>();

            camera.Offset = new Vector3(0, 0, 250f);
            orbit.Yaw = MathF.PI / 4f;
            orbit.Pitch = -MathF.PI / 8f;
            orbit.PitchClamp = new Vector2(-MathF.PI / 8f, MathF.PI / 8f * 3f);
        }
        {
            var actor = _scene.CreateActor();
            var stars = actor.AddComponent<StarsMesh>();
            stars.Camera = _cameraActor.GetComponent<Camera>();
        }

        RebuildSceneSubTree(_mapTree, _mapTree.Root);
    }

    public override void Update(GameTime gameTime)
    {
        StatusService.AddHints(("LMB", "Select Node"));
        _scene.Update(gameTime);
    }

    public override void Draw()
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
                var gizmo = _cameraActor.GetComponent<OrientationGizmo>();
                gizmo.UseFaceLabels = true;
                gizmo.Draw(imageMin + new Vector2(size.X - 8f, 8f));
                ImGuiX.DrawStats(imageMin + new Vector2(8, 8), RenderingService.GetStats());

                if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    var viewportMin = ImGuiX.GetItemRectMin();
                    _viewportCenter = viewportMin + (size / 2);
                    var ray = _scene.Viewport.Unproject(ImGuiX.GetMousePos(), viewportMin);
                    var actor = _scene.Raycast(ray)?.Actor;
                    if (actor != null)
                    {
                        _nextState = State.MenuPopup;
                        _selectedNode = HighlightNode(actor);
                    }
                    else
                    {
                        _nextState = State.MapView;
                        _selectedNode = null;
                    }
                }
            }
        }

        DrawMenuPopup();
        DrawEditMapNodeWindow();
        DrawRemoveMapNodeModal();
    }

    private void DrawMenuPopup()
    {
        var panControl = _cameraActor.GetComponent<MapPanControl>();
        if (_nextState == State.MenuPopup && panControl.Focused)
        {
            ImGuiX.SetNextWindowPos(_viewportCenter + new Vector2(48f, 32f), ImGuiCond.Always, new Vector2(0.5f));
            ImGui.OpenPopup("##MenuPopup");
            _showProperties = false;
            _nextState = State.MapView;
        }

        if (ImGui.BeginPopup("##MenuPopup"))
        {
            if (ImGui.MenuItem("Edit..."))
            {
                _showProperties = true;
            }

            if (ImGui.BeginMenu("Add"))
            {
                var (_, parentConnection) = FindParentWithConnection(_mapTree, _selectedNode!);
                var parentFace = parentConnection?.Face.GetOpposite();
                foreach (var face in FaceExtensions.NaturalOrder)
                {
                    ImGui.BeginDisabled(face == parentFace);
                    if (ImGui.MenuItem(face.ToString()))
                    {
                        AppMapNode(face);
                    }

                    ImGui.EndDisabled();
                }

                ImGui.EndMenu();
            }

            if (ImGui.MenuItem("Remove"))
            {
                _nextState = State.RemoveMapNode;
            }

            ImGui.EndPopup();
        }
    }

    private void DrawEditMapNodeWindow()
    {
        if (_selectedNode == null || !_showProperties)
        {
            _showProperties = false;
            return;
        }

        var updateMesh = false;
        var updateIcons = false;

        const ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize |
                                       ImGuiWindowFlags.NoCollapse;
        if (ImGui.Begin($"Properties##{Title}", ref _showProperties, flags))
        {
            var levelName = _selectedNode.LevelName;
            if (ImGui.InputText("Level Name", ref levelName, 255))
            {
                using (History.BeginScope("Edit Level Name"))
                {
                    _selectedNode.LevelName = levelName;
                    updateMesh = true;
                }
            }

            var nodeType = (int)_selectedNode.NodeType;
            var nodeTypes = Enum.GetNames<LevelNodeType>();
            if (ImGui.Combo("Node Type", ref nodeType, nodeTypes, nodeTypes.Length))
            {
                using (History.BeginScope("Edit Node Type"))
                {
                    _selectedNode.NodeType = (LevelNodeType)nodeType;
                    updateMesh = true;
                }
            }

            var hasLesserGate = _selectedNode.HasLesserGate;
            if (ImGui.Checkbox("Has Lesser Gate", ref hasLesserGate))
            {
                using (History.BeginScope("Has Lesser Gate"))
                {
                    _selectedNode.HasLesserGate = hasLesserGate;
                    updateIcons = true;
                }
            }

            var hasWarpGate = _selectedNode.HasWarpGate;
            if (ImGui.Checkbox("Has Warp Gate", ref hasWarpGate))
            {
                using (History.BeginScope("Has Warp Gate"))
                {
                    _selectedNode.HasWarpGate = hasWarpGate;
                    updateIcons = true;
                }
            }

            var chestCount = _selectedNode.Conditions.ChestCount;
            if (ImGui.InputInt("Chest Count", ref chestCount))
            {
                using (History.BeginScope("Edit Chest Count"))
                {
                    _selectedNode.Conditions.ChestCount = chestCount;
                    updateIcons = true;
                }
            }

            var lockedDoorCount = _selectedNode.Conditions.LockedDoorCount;
            if (ImGui.InputInt("Locked Door Count", ref lockedDoorCount))
            {
                using (History.BeginScope("Edit Locked Door Count"))
                {
                    _selectedNode.Conditions.LockedDoorCount = lockedDoorCount;
                    updateIcons = true;
                }
            }

            var unlockedDoorCount = _selectedNode.Conditions.UnlockedDoorCount;
            if (ImGui.InputInt("Unlocked Door Count", ref unlockedDoorCount))
            {
                using (History.BeginScope("Edit Unlocked Door Count"))
                {
                    _selectedNode.Conditions.UnlockedDoorCount = unlockedDoorCount;
                }
            }

            var cubeShardCount = _selectedNode.Conditions.CubeShardCount;
            if (ImGui.InputInt("Cube Shard Count", ref cubeShardCount))
            {
                using (History.BeginScope("Edit Cube Shard Count"))
                {
                    _selectedNode.Conditions.CubeShardCount = cubeShardCount;
                    updateIcons = true;
                }
            }

            var otherCollectibleCount = _selectedNode.Conditions.OtherCollectibleCount;
            if (ImGui.InputInt("Other Collectible Count", ref otherCollectibleCount))
            {
                using (History.BeginScope("Edit Other Collectible Count"))
                {
                    _selectedNode.Conditions.OtherCollectibleCount = otherCollectibleCount;
                }
            }

            var splitUpCount = _selectedNode.Conditions.SplitUpCount;
            if (ImGui.InputInt("Split Up Count", ref splitUpCount))
            {
                using (History.BeginScope("Edit Split Up Count"))
                {
                    _selectedNode.Conditions.SplitUpCount = splitUpCount;
                    updateIcons = true;
                }
            }

            var secretCount = _selectedNode.Conditions.SecretCount;
            if (ImGui.InputInt("Secret Count", ref secretCount))
            {
                using (History.BeginScope("Edit Secret Count"))
                {
                    _selectedNode.Conditions.SecretCount = secretCount;
                    updateIcons = true;
                }
            }

            var scriptIds = _selectedNode.Conditions.ScriptIds;
            if (ImGuiX.EditableList("Script Ids", ref scriptIds, RenderInt, () => 0))
            {
                using (History.BeginScope("Edit Script Ids"))
                {
                    _selectedNode.Conditions.ScriptIds = scriptIds;
                }
            }

            if (_selectedNode.Connections.Count > 0)
            {
                ImGui.SeparatorText("Connection Branch Oversizes");
            }

            foreach (var connection in _selectedNode.Connections)
            {
                var branchOversize = connection.BranchOversize;
                if (ImGui.InputFloat(connection.Face.ToString(), ref branchOversize))
                {
                    using (History.BeginScope("Edit Branch Oversize"))
                    {
                        connection.BranchOversize = branchOversize;
                        RebuildSceneSubTree(_mapTree, _selectedNode);
                        break;
                    }
                }
            }

            ImGui.End();
        }

        if (_nodeMapping.TryGetValue(_selectedNode, out var actors))
        {
            if (updateMesh)
            {
                var mesh = actors.Mesh.GetComponent<MapNodeMesh>();
                mesh.Visualize(_selectedNode);
            }

            if (updateIcons)
            {
                var icons = actors.Icons.GetComponent<MapIconsMesh>();
                icons.Visualize(_selectedNode);
            }
        }
    }

    private void DrawRemoveMapNodeModal()
    {
        if (_nextState == State.RemoveMapNode)
        {
            _confirm.Text = $"Delete \"{_selectedNode!.LevelName}\" map node?";
            _confirm.Confirmed = RemoveMapNode;
            _confirm.Closed = () => { _selectedNode = null; };
            _nextState = State.MapView;
        }
    }

    public override void Dispose()
    {
        _scene.Dispose();
        Game.RemoveComponent(_confirm);
        base.Dispose();
    }

    private void RebuildSceneSubTree(MapTree tree, MapNode node)
    {
        var (_, parentConnection) = FindParentWithConnection(tree, node);
        var actors = _nodeMapping.GetValueOrDefault(node);
        var offset = actors?.Mesh.Transform.Position ?? Vector3.Zero;

        var multiBranchIds = new Dictionary<MapNodeConnection, int>();
        var multiBranchCounts = new Dictionary<MapNodeConnection, int>();

        var stack = new Stack<NodeProcessingState>();
        stack.Push(new NodeProcessingState(node, parentConnection, offset));

        RemoveNodeMapping(node);
        while (stack.Count > 0)
        {
            (node, parentConnection, offset) = stack.Pop();

            // Pre-register node so CreateLinkBranch can attach links to it.
            // Links are created before the node mesh so they draw first in BFS order,
            // allowing the node texture to overdraw them via depth testing.
            _nodeMapping[node] = new NodeActors(null!, null!, new List<Actor>());

            foreach (var c in node.Connections)
            {
                if (c.Node.NodeType == LevelNodeType.Lesser &&
                    node.Connections.Any(x => x.Face == c.Face && c.Node.NodeType != LevelNodeType.Lesser))
                {
                    if (node.Connections.All(x => x.Face != FaceOrientation.Top))
                    {
                        c.Face = FaceOrientation.Top;
                    }
                    else if (node.Connections.All(x => x.Face != FaceOrientation.Down))
                    {
                        c.Face = FaceOrientation.Down;
                    }
                }
            }

            foreach (var c in node.Connections)
            {
                multiBranchIds.TryAdd(c, 0);
            }

            foreach (var c in node.Connections)
            {
                multiBranchIds[c] = node.Connections
                    .Where(x => x.Face == c.Face)
                    .Max(x => multiBranchIds[x]) + 1;
                multiBranchCounts[c] = node.Connections.Count(x => x.Face == c.Face);
            }

            var num = 0f;
            var orderedConnections = node.Connections.OrderByDescending(x => x.Node.NodeType.GetSizeFactor());
            foreach (var item in orderedConnections)
            {
                if (parentConnection != null && item.Face == parentConnection.Face.GetOpposite())
                {
                    item.Face = item.Face.GetOpposite();
                }

                // Calculate size factor for this connection
                var sizeFactor = 3f + ((node.NodeType.GetSizeFactor() + item.Node.NodeType.GetSizeFactor()) / 2f);
                if ((node.NodeType == LevelNodeType.Hub || item.Node.NodeType == LevelNodeType.Hub) &&
                    node.NodeType != LevelNodeType.Lesser && item.Node.NodeType != LevelNodeType.Lesser)
                {
                    sizeFactor += 1f;
                }

                // Adjust for lesser nodes
                if ((node.NodeType == LevelNodeType.Lesser || item.Node.NodeType == LevelNodeType.Lesser) &&
                    multiBranchCounts[item] == 1)
                {
                    sizeFactor -= item.Face.IsSide() ? 1 : 2;
                }

                // Apply branch oversize
                sizeFactor *= 1.25f + item.BranchOversize;
                var num4 = sizeFactor * 0.375f;
                if (item.Node.NodeType == LevelNodeType.Node && node.NodeType == LevelNodeType.Node)
                {
                    num4 *= 1.5f;
                }

                // Calculate branch offset for multi-branch connections
                var faceVector = item.Face.AsVector();
                var vector2 = Vector3.Zero;
                if (multiBranchCounts[item] > 1)
                {
                    vector2 = (multiBranchIds[item] - 1 - ((multiBranchCounts[item] - 1) / 2f)) *
                              (Mathz.XzMask - item.Face.AsVector().Abs()) * num4;
                }

                var childOffset = offset + (faceVector * sizeFactor) + vector2;
                stack.Push(new NodeProcessingState(item.Node, item, childOffset));

                if (multiBranchCounts[item] > 1)
                {
                    // Create multi-branch link segments
                    num = Math.Max(num, sizeFactor / 2f);
                    var scale = (faceVector * num) + (Vector3.One * LinkThickness);
                    var position = (faceVector * num / 2f) + offset;
                    CreateLinkBranch(node, position, scale);

                    scale = vector2 + (Vector3.One * LinkThickness);
                    position = (vector2 / 2f) + offset + (faceVector * num);
                    CreateLinkBranch(node, position, scale);

                    var num5 = sizeFactor - num;
                    scale = (faceVector * num5) + (Vector3.One * LinkThickness);
                    position = (faceVector * num5 / 2f) + offset + (faceVector * num) + vector2;
                    CreateLinkBranch(node, position, scale);
                }
                else
                {
                    // Create single branch link
                    var scale = (faceVector * sizeFactor) + (Vector3.One * LinkThickness);
                    var position = (faceVector * sizeFactor / 2f) + offset;
                    CreateLinkBranch(node, position, scale);
                }

                // Handle special cases
                switch (item.Node.LevelName)
                {
                    case "LIGHTHOUSE_SPIN":
                        {
                            const float num6 = 3.425f;
                            var scale = (Vector3.Backward * num6) + (Vector3.One * LinkThickness);
                            var position = (Vector3.Backward * num6 / 2f) + offset + (faceVector * sizeFactor);
                            CreateLinkBranch(node, position, scale);
                            break;
                        }

                    case "LIGHTHOUSE_HOUSE_A":
                        {
                            const float num7 = 5f;
                            var scale = (Vector3.Right * num7) + (Vector3.One * LinkThickness);
                            var position = (Vector3.Right * num7 / 2f) + offset + (faceVector * sizeFactor);
                            CreateLinkBranch(node, position, scale);
                            break;
                        }
                }
            }

            // Node mesh and icons are created last so they draw on top of links.
            CreateNodeMesh(node, offset);
        }
    }

    private void RemoveNodeMapping(MapNode node)
    {
        var stack = new Stack<MapNode>();
        stack.Push(node);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (_nodeMapping.Remove(current, out var actors))
            {
                foreach (var c in current.Connections)
                {
                    stack.Push(c.Node);
                }

                foreach (var actor in actors.Links)
                {
                    _scene.DestroyActor(actor);
                }

                // Icons is a child of Mesh actor, it will be deleted too
                _scene.DestroyActor(actors.Mesh);
            }
        }
    }

    private void CreateNodeMesh(MapNode node, Vector3 offset)
    {
        var mesh = _scene.CreateActor();
        mesh.Transform.Position = offset;
        mesh.Name = node.LevelName;

        var visual = mesh.AddComponent<MapNodeMesh>();
        visual.Camera = _cameraActor.GetComponent<Camera>();
        visual.Visualize(node);

        var icons = _scene.CreateActor(mesh);
        icons.Name = $"{node.LevelName} ^ Icons";
        icons.AddComponent<MapIconsMesh>().Visualize(node);

        _nodeMapping[node] = new NodeActors(mesh, icons, _nodeMapping[node].Links);
    }

    private void CreateLinkBranch(MapNode node, Vector3 position, Vector3 scale)
    {
        if (_nodeMapping.TryGetValue(node, out var mapActor))
        {
            var actor = _scene.CreateActor();
            actor.Transform.Position = position;
            actor.Transform.Scale = scale;
            actor.Name = $"{node.LevelName} ^ Link";
            actor.AddComponent<MapLinkMesh>();
            mapActor.Links.Add(actor);
        }
    }

    private MapNode HighlightNode(Actor actor)
    {
        foreach (var (node, actors) in _nodeMapping)
        {
            if (actors.Mesh == actor)
            {
                var panControl = _cameraActor.GetComponent<MapPanControl>();
                panControl.FocusOn(actor.Transform.Position);
                return node;
            }
        }

        throw new ArgumentException("Mapping for actor not found");
    }

    private void AppMapNode(FaceOrientation face)
    {
        using (History.BeginScope("Add Map Node"))
        {
            var newNode = new MapNode { LevelName = "UNTITLED" };
            _selectedNode!.Connections.Add(new MapNodeConnection { Node = newNode, Face = face });
            RebuildSceneSubTree(_mapTree, _selectedNode);
        }
    }

    private void RemoveMapNode()
    {
        var (parent, _) = FindParentWithConnection(_mapTree, _selectedNode!);
        var connection = parent?.Connections.FirstOrDefault(c => c.Node == _selectedNode);
        if (connection != null)
        {
            using (History.BeginScope("Remove Map Node"))
            {
                parent!.Connections.Remove(connection);
                RemoveNodeMapping(_selectedNode!);
                RebuildSceneSubTree(_mapTree, parent);
            }
        }
    }

    private static (MapNode?, MapNodeConnection?) FindParentWithConnection(MapTree tree, MapNode node)
    {
        var stack = new Stack<MapNode>();
        stack.Push(tree.Root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var currentConnection in current.Connections)
            {
                if (currentConnection.Node == node)
                {
                    return (current, currentConnection);
                }
            }

            foreach (var connection in current.Connections)
            {
                stack.Push(connection.Node);
            }
        }

        return (null, null);
    }

    private static bool RenderInt(int index, ref int item)
    {
        return ImGui.InputInt("##item", ref item);
    }

    private record struct NodeProcessingState(
        MapNode Node,
        MapNodeConnection? ParentConnection,
        Vector3 Offset
    );

    private record NodeActors(Actor Mesh, Actor Icons, List<Actor> Links);

    private enum State
    {
        MapView,
        MenuPopup,
        RemoveMapNode
    }

    public static object Create(string name)
    {
        return new MapTree
        {
            Root = new MapNode
            {
                LevelName = name
            }
        };
    }
}