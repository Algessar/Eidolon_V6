using System.Numerics;
using Eidolon.Vulkan;
using EidolonCore.Assets;
using EidolonCore.ECS;
using EidolonCore.Math;
using EidolonCore.Rendering;
using ImGuiNET;
using Silk.NET.Windowing;

namespace Eidolon.Editor;

internal unsafe class EditorUI
{
    private static readonly Vector4[] SpawnPalette =
    [
        new(0.85f, 0.30f, 0.32f, 1f),
        new(0.30f, 0.55f, 0.90f, 1f),
        new(0.35f, 0.80f, 0.45f, 1f),
        new(0.90f, 0.75f, 0.30f, 1f),
        new(0.65f, 0.45f, 0.90f, 1f),
        new(0.30f, 0.80f, 0.78f, 1f),
    ];

    private static readonly Vector4 ErrorColor = new(0.95f, 0.42f, 0.42f, 1f);
    private static readonly Vector4 SuccessColor = new(0.55f, 0.85f, 0.55f, 1f);

    private readonly IWindow _window;
    private readonly World _world;
    private readonly AssetLibrary _assets;

    private nint _gameViewTextureID;

    private Entity? _selected;
    private Entity? _dragSource;

    private string _assetFilter = string.Empty;
    private const string ObjFilter = "Wavefront OBJ (*.obj)\0*.obj\0All files (*.*)\0*.*\0\0";
    private string? _statusMessage;
    private bool _statusIsError;
    private int _spawnCounter;
    private int _materialCounter;

    public EditorUI(IWindow window, World world, AssetLibrary assets)
    {
        _window = window;
        _world = world;
        _assets = assets;
        InitialSetup();
    }
    
    void InitialSetup()
    {
        var io = ImGui.GetIO();
        
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
        
        float dpiScale = 0.99f;
        io.DisplaySize = new Vector2(_window.Size.X, _window.Size.Y);
        
        // Framebuffer scaling (HiDPI)
        io.DisplayFramebufferScale = new Vector2(
            _window.FramebufferSize.X / (float)_window.Size.X,
            _window.FramebufferSize.Y / (float)_window.Size.Y
        );  
        
        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle();

        if (io.ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
        {
            style.WindowRounding = 0.0f;
            style.Colors[(int)ImGuiCol.WindowBg].W = 1.0f;
        }

        style.ScaleAllSizes(dpiScale);
    }
    
    public void Update()
    {
        DrawDockSpace();
        DrawGameViewWindow();
        DrawSceneWindow();
        DrawInspectorWindow();
        DrawBrowserWindow();
    }

    private const string DragPayload = "EIDOLON_ENTITY";

    /// <summary>
    /// Entity tree. Drag one entity onto another to parent it; drop it on the empty space below
    /// the tree to make it a root again.
    /// </summary>
    private void DrawSceneWindow()
    {
        // First-run placement only: once the window is docked its position comes from the layout.
        ImGui.SetNextWindowPos(new Vector2(1180f, 60f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(340f, 300f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Eidolon / Scene");

        ImGui.Text($"{_world.EntityCount} entities");

        foreach (var root in Hierarchy.GetRoots(_world).ToArray())
            DrawEntityNode(root);

        if (ImGui.BeginDragDropTarget())
        {
            var payload = ImGui.AcceptDragDropPayload(DragPayload);
            if (payload.NativePtr != null && payload.IsDelivery() && _dragSource is { } unparented)
            {
                Hierarchy.TrySetParent(_world, unparented, default);
                _dragSource = null;
            }

            ImGui.EndDragDropTarget();
        }

        ImGui.End();
    }

    private void DrawEntityNode(Entity entity)
    {
        var children = Hierarchy.GetChildren(_world, entity).ToArray();
        var isLeaf = children.Length == 0;

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (isLeaf)
            flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
        if (_selected == entity)
            flags |= ImGuiTreeNodeFlags.Selected;

        var opened = ImGui.TreeNodeEx($"{DescribeEntity(entity)}##{entity.Id}", flags);

        if (ImGui.IsItemClicked())
            _selected = entity;

        if (ImGui.BeginDragDropSource())
        {
            _dragSource = entity;
            ImGui.SetDragDropPayload(DragPayload, IntPtr.Zero, 0);
            ImGui.TextUnformatted(DescribeEntity(entity));
            ImGui.EndDragDropSource();
        }

        if (ImGui.BeginDragDropTarget())
        {
            var payload = ImGui.AcceptDragDropPayload(DragPayload);
            if (payload.NativePtr != null && payload.IsDelivery() && _dragSource is { } dragged)
            {
                Hierarchy.TrySetParent(_world, dragged, entity);
                _dragSource = null;
            }

            ImGui.EndDragDropTarget();
        }

        if (opened && !isLeaf)
        {
            foreach (var child in children)
                DrawEntityNode(child);

            ImGui.TreePop();
        }
    }

    private string DescribeEntity(Entity entity)
    {
        if (_world.TryGetComponent<MeshRenderer>(entity, out var renderer) && renderer.Mesh is not null)
            return $"#{entity.Id}  {renderer.Material.Name}";

        return $"#{entity.Id}  entity";
    }

    /// <summary>Edits the selected entity's transform and material.</summary>
    private void DrawInspectorWindow()
    {
        ImGui.SetNextWindowPos(new Vector2(1180f, 380f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(340f, 360f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Eidolon / Inspector");

        if (_selected is { } stale && !_world.IsAlive(stale))
            _selected = null;

        if (_selected is not { } entity)
        {
            ImGui.TextDisabled("Select an entity in the Scene panel.");
            ImGui.End();
            return;
        }

        ImGui.Text($"Entity #{entity.Id}");

        if (_world.HasComponent<Transform>(entity))
            DrawTransformInspector(entity);

        if (_world.TryGetComponent<MeshRenderer>(entity, out var meshRenderer))
            DrawMeshRendererInspector(meshRenderer);

        ImGui.Separator();

        if (ImGui.Button("Delete entity"))
        {
            _world.DestroyEntity(entity);
            _selected = null;
        }

        ImGui.End();
    }

    private void DrawTransformInspector(Entity entity)
    {
        if (!ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ref var transform = ref _world.GetComponent<Transform>(entity);

        ImGui.DragFloat3("Position", ref transform.Position, 0.05f);

        // Rotation is stored as a quaternion but edited as yaw/pitch/roll, which is the form a
        // person can actually reason about.
        var (yaw, pitch, roll) = transform.Rotation.ToYawPitchRoll();
        var yawDegrees = yaw * Mathf.Rad2Deg;
        var pitchDegrees = pitch * Mathf.Rad2Deg;
        var rollDegrees = roll * Mathf.Rad2Deg;

        var rotated = ImGui.DragFloat("Yaw", ref yawDegrees, 0.5f);
        rotated |= ImGui.DragFloat("Pitch", ref pitchDegrees, 0.5f);
        rotated |= ImGui.DragFloat("Roll", ref rollDegrees, 0.5f);

        if (rotated)
        {
            transform.Rotation = Quaternion.CreateFromYawPitchRoll(
                yawDegrees * Mathf.Deg2Rad,
                pitchDegrees * Mathf.Deg2Rad,
                rollDegrees * Mathf.Deg2Rad);
        }

        ImGui.DragFloat3("Scale", ref transform.Scale, 0.05f);
    }

    private static void DrawMeshRendererInspector(MeshRenderer meshRenderer)
    {
        if (!ImGui.CollapsingHeader("Mesh Renderer", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        if (meshRenderer.Mesh is { } mesh)
        {
            ImGui.Text($"Mesh: {mesh.Name}");
            ImGui.TextDisabled($"{mesh.Vertices.Length} verts, {mesh.TriangleCount} tris");
        }
        else
        {
            ImGui.TextDisabled("No mesh assigned.");
        }

        ImGui.Separator();

        // Name and BaseColor are properties, so edit a copy and write it back.
        var material = meshRenderer.Material;

        var name = material.Name;
        if (ImGui.InputText("Material", ref name, 128))
            material.Name = name;

        var color = material.BaseColor;
        if (ImGui.ColorEdit4("Base colour", ref color))
            material.BaseColor = color;
    }

    public Vector2 GameViewSize = new Vector2();

    private void DrawGameViewWindow()
    {
        // Give the game view a usable size the first time the editor runs.
        ImGui.SetNextWindowSize(new Vector2(1280f, 720f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Eidolon / Game View");
        
        //NOTE: this currently causes the Font Atlas to be rendered in the window.
        var available = ImGui.GetContentRegionAvail();
        GameViewSize = available;
        if (_gameViewTextureID != 0 && available.X > 1f && available.Y > 1f)
        {
            // The scene is rendered with the Vulkan Y flip in the projection, so its
            // framebuffer already has +Y at the top; no extra UV flip is needed here.
            ImGui.Image((nint)_gameViewTextureID, available);
            HandleGameViewPicking();
        }
        else
        {
            ImGui.Text("GameView texture unavailable");
        }
        
        ImGui.End();
    }

    /// <summary>Selects the entity under the cursor when the game view is clicked.</summary>
    private void HandleGameViewPicking()
    {
        if (!ImGui.IsItemHovered() || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            return;

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var width = max.X - min.X;
        var height = max.Y - min.Y;

        if (width <= 0f || height <= 0f)
            return;

        var mouse = ImGui.GetMousePos();

        // Screen top maps to -1 because the projection carries the Vulkan Y flip.
        var ndc = new Vector2(
            (mouse.X - min.X) / width * 2f - 1f,
            (mouse.Y - min.Y) / height * 2f - 1f);

        if (FindCamera() is not { } camera)
            return;

        _selected = ScenePicker.Pick(_world, camera.ScreenPointToRay(ndc));
    }

    private Camera? FindCamera()
    {
        foreach (var entity in _world.GetEntitiesWith<Camera>())
            return _world.GetComponent<Camera>(entity);

        return null;
    }

    public void SetGameViewTexture(nint textureId)
    {
        _gameViewTextureID = textureId;
    }

    /// <summary>
    /// Asset browser: lists the mesh files found under the assets folder and imports them into
    /// the scene.
    /// </summary>
    private void DrawBrowserWindow()
    {
        ImGui.SetNextWindowPos(new Vector2(740f, 60f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(420f, 340f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Eidolon / Browser");

        ImGui.Text($"{_assets.Catalog.Count} mesh asset(s)");

        ImGui.SameLine();
        if (ImGui.SmallButton("Rescan"))
        {
            var found = _assets.Refresh();
            SetStatus($"Rescanned: {found} mesh asset(s).", isError: false);
        }

        // Filter the list so a directory with many models stays usable.
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##assetFilter", "Search assets...", ref _assetFilter, 256);

        ImGui.Separator();

        var filter = _assetFilter.Trim();
        var shown = 0;

        if (_assets.Catalog.Count == 0)
        {
            ImGui.TextDisabled(_assets.RootDirectory is null
                ? "No assets folder has been scanned yet."
                : $"No mesh files found under '{_assets.RootDirectory}'.");
        }
        else
        {
            foreach (var asset in _assets.Catalog)
            {
                if (filter.Length > 0 && !asset.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                shown++;
                ImGui.PushID(asset.Path);

                if (ImGui.SmallButton("Add"))
                    SpawnAsset(asset);

                ImGui.SameLine();
                ImGui.TextUnformatted($"{asset.Name}  ({FormatSize(asset.SizeInBytes)})");

                if (asset.IsLoaded && asset.Mesh is { } loadedMesh)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled($"{loadedMesh.TriangleCount} tris");
                }

                ImGui.PopID();
            }

            if (shown == 0)
                ImGui.TextDisabled($"No assets match '{filter}'.");
        }

        ImGui.Separator();

        ImGui.TextUnformatted("Import a model from disk:");
        if (ImGui.Button("Open .obj..."))
            ImportFromFileDialog();

        if (_statusMessage is not null)
            ImGui.TextColored(_statusIsError ? ErrorColor : SuccessColor, _statusMessage);

        ImGui.End();
    }

    private void SpawnAsset(MeshAsset asset)
    {
        if (!_assets.TryLoad(asset, out var mesh, out var error) || mesh is null)
        {
            SetStatus($"Could not import '{asset.Name}': {error}", isError: true);
            return;
        }

        _selected = Spawn(mesh, asset.Name);
        SetStatus($"Added '{asset.Name}' ({mesh.TriangleCount} tris).", isError: false);
    }

    private void ImportFromFileDialog()
    {
        var path = FileDialog.TryPickFile("Import model", ObjFilter, _assets.RootDirectory);
        if (path is null)
            return;

        try
        {
            var mesh = _assets.LoadObj(path);
            _selected = Spawn(mesh, mesh.Name);
            SetStatus($"Imported '{mesh.Name}' ({mesh.Vertices.Length} verts, {mesh.TriangleCount} tris).",
                isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Import failed: {ex.Message}", isError: true);
        }
    }

    private Entity Spawn(Mesh mesh, string name)
    {
        var color = SpawnPalette[_materialCounter++ % SpawnPalette.Length];
        var material = new Material(name, color);

        return SceneSpawner.SpawnFittedMesh(_world, mesh, material, NextSpawnPosition());
    }

    /// <summary>
    /// Places each import at the scene camera's focus, stepping across a small grid so repeated
    /// imports do not stack on top of each other.
    /// </summary>
    private Vector3 NextSpawnPosition()
    {
        var focus = new Vector3(0f, 0.5f, 0f);

        foreach (var entity in _world.GetEntitiesWith<Camera>())
        {
            focus = _world.GetComponent<Camera>(entity).Target;
            break;
        }

        var index = _spawnCounter++;
        var column = index % 3;
        var row = index / 3;

        return focus + new Vector3((column - 1) * 1.4f, 0f, row * 1.4f);
    }

    private void SetStatus(string message, bool isError)
    {
        _statusMessage = message;
        _statusIsError = isError;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024f:F0} KB",
        _ => $"{bytes / (1024f * 1024f):F1} MB",
    };

    private void DrawDockSpace()
    {
        var viewport = ImGui.GetMainViewport();
        
        ImGui.SetNextWindowPos(viewport.WorkPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(viewport.WorkSize, ImGuiCond.Always);
        ImGui.SetNextWindowViewport(viewport.ID);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0,0,0,0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

        ImGui.Begin(
            "##DockSpaceRoot",
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoBringToFrontOnFocus |
            ImGuiWindowFlags.NoNavFocus |
            ImGuiWindowFlags.AlwaysAutoResize
        );
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor();
        
        uint dockspaceId = ImGui.GetID("MainDockSpace");
        ImGui.DockSpace(dockspaceId, Vector2.Zero, ImGuiDockNodeFlags.PassthruCentralNode);
        ImGui.End();
    }
}
