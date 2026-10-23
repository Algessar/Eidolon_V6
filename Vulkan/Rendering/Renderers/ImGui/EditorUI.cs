using System.Numerics;
using Eidolon.Vulkan;
using EidolonCore.Assets;
using EidolonCore.ECS;
using EidolonCore.Math;
using EidolonCore.Rendering;
using EidolonCore.Simulation;
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

    /// <summary>Tint of the ghost object shown while an asset is dragged over the viewport.</summary>
    private static readonly Vector4 PreviewColor = new(0.55f, 0.85f, 0.95f, 1f);

    private readonly IWindow _window;
    private readonly SceneSession _session;
    private readonly AssetLibrary _assets;
    private readonly AssetBrowserPanel _browser;
    private readonly ViewportSettings _viewport;
    private readonly TransformGizmo _transformGizmo = new();

    /// <summary>The world being edited: the runtime copy during play, else the authored world.</summary>
    private World _world => _session.Active;

    private nint _editorViewTextureID;
    private nint _gameViewTextureID;

    private Entity? _selected;
    private Entity? _dragSource;

    private Entity? _previewEntity;
    private string? _previewAssetPath;
    private Vector3 _previewOffset;

    private string? _statusMessage;
    private bool _statusIsError;
    private int _spawnCounter;
    private int _materialCounter;

    public EditorUI(IWindow window, SceneSession session, AssetLibrary assets, ViewportSettings viewport)
    {
        _window = window;
        _session = session;
        _assets = assets;
        _viewport = viewport;
        _browser = new AssetBrowserPanel(assets, SpawnAsset, SpawnImportedMesh);
        InitialSetup();
    }
    
    void InitialSetup()
    {
        var io = ImGui.GetIO();
        
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        // This renderer submits draw data for the main Vulkan window only. Detached ImGui
        // platform windows need their own window and Vulkan swapchain/rendering path, which
        // is not implemented yet. Docking within the editor window remains supported.
        io.ConfigFlags &= ~ImGuiConfigFlags.ViewportsEnable;
        
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
        DrawMainMenuBar();
        DrawGameWindow();
        DrawGameViewWindow();
        DrawSceneWindow();
        DrawInspectorWindow();
        _browser.Draw();
    }

    /// <summary>
    /// Entity tree. Drag one entity onto another to parent it; drop it on the empty space below
    /// the tree to make it a root again. Assets dragged in from the Browser are spawned here.
    /// </summary>
    private void DrawSceneWindow()
    {
        // First-run placement only: once the window is docked its position comes from the layout.
        ImGui.SetNextWindowPos(new Vector2(1180f, 60f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(340f, 300f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Hierarchy");

        ImGui.Text($"{_world.EntityCount} entities");

        if (ImGui.BeginPopupContextWindow("##hierarchyCreate", ImGuiPopupFlags.MouseButtonRight))
        {
            if (ImGui.MenuItem("Add Game Camera"))
                SpawnGameCamera();

            if (ImGui.MenuItem("Add Cube"))
                SpawnPrimitive(PrimitiveMeshes.Cube, "Cube");

            if (ImGui.MenuItem("Add Plane"))
                SpawnPrimitive(PrimitiveMeshes.Plane, "Plane");

            ImGui.EndPopup();
        }

        foreach (var root in Hierarchy.GetRoots(_world).ToArray())
            DrawEntityNode(root);

        DrawSceneDropArea();

        if (_statusMessage is not null)
            ImGui.TextColored(_statusIsError ? ErrorColor : SuccessColor, _statusMessage);

        ImGui.End();
    }

    /// <summary>
    /// The empty space under the entity list. Dropping an entity here unparents it again; dropping
    /// an asset spawns it as a new root. It has to be a real item, because a drag-drop target only
    /// ever refers to the last submitted item.
    /// </summary>
    private void DrawSceneDropArea()
    {
        var available = ImGui.GetContentRegionAvail();
        var height = MathF.Max(available.Y, ImGui.GetTextLineHeightWithSpacing() * 2f);

        ImGui.InvisibleButton("##sceneDropArea", new Vector2(MathF.Max(available.X, 1f), height));

        var dragging = EditorDragDrop.IsDragInProgress();

        if (dragging && ImGui.IsItemHovered())
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();

            ImGui.GetWindowDrawList().AddRectFilled(min, max,
                ImGui.GetColorU32(ImGuiCol.DragDropTarget, 0.25f), 3f);
            ImGui.GetWindowDrawList().AddText(min + new Vector2(6f, 4f),
                ImGui.GetColorU32(ImGuiCol.TextDisabled), "Drop here to unparent");
        }

        if (!ImGui.BeginDragDropTarget())
            return;

        var entityPayload = ImGui.AcceptDragDropPayload(EditorDragDrop.EntityPayload);
        if (EditorDragDrop.IsDelivered(entityPayload) && _dragSource is { } unparented)
        {
            Hierarchy.TrySetParent(_world, unparented, default);
            SetStatus($"Unparented #{unparented.Id}.", isError: false);
            _dragSource = null;
        }

        var assetPayload = ImGui.AcceptDragDropPayload(EditorDragDrop.AssetPayload);
        if (EditorDragDrop.IsDelivered(assetPayload) &&
            EditorDragDrop.TryReadAssetPayload(assetPayload, out var assetPath))
        {
            SpawnAssetFromPath(assetPath);
        }

        ImGui.EndDragDropTarget();
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
            ImGui.SetDragDropPayload(EditorDragDrop.EntityPayload, IntPtr.Zero, 0);
            ImGui.TextUnformatted(DescribeEntity(entity));
            ImGui.EndDragDropSource();
        }

        if (ImGui.BeginDragDropTarget())
        {
            var entityPayload = ImGui.AcceptDragDropPayload(EditorDragDrop.EntityPayload);
            if (EditorDragDrop.IsDelivered(entityPayload) && _dragSource is { } dragged)
            {
                Hierarchy.TrySetParent(_world, dragged, entity);
                _dragSource = null;
            }

            var assetPayload = ImGui.AcceptDragDropPayload(EditorDragDrop.AssetPayload);
            if (EditorDragDrop.IsDelivered(assetPayload) &&
                EditorDragDrop.TryReadAssetPayload(assetPayload, out var assetPath))
            {
                SpawnAssetFromPath(assetPath, entity);
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
        ImGui.Begin("Inspector");

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

    /// <summary>Size of the editor viewport panel (drives the editor camera aspect).</summary>
    public Vector2 EditorViewSize = new Vector2();

    /// <summary>Size of the game view panel.</summary>
    public Vector2 GameViewSize = new Vector2();

    private void DrawGameViewWindow()
    {
        // Give the game view a usable size the first time the editor runs.
        ImGui.SetNextWindowSize(new Vector2(1280f, 720f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Scene");

        DrawPlayToolbar();
        DrawViewportToolbar();

        //NOTE: this currently causes the Font Atlas to be rendered in the window.
        var available = ImGui.GetContentRegionAvail();
        EditorViewSize = available;
        if (_editorViewTextureID != 0 && available.X > 1f && available.Y > 1f)
        {
            var imageMin = ImGui.GetCursorScreenPos();

            // The scene is rendered with the Vulkan Y flip in the projection, so its
            // framebuffer already has +Y at the top; no extra UV flip is needed here.
            ImGui.Image((nint)_editorViewTextureID, available);

            var hovering = ImGui.IsMouseHoveringRect(imageMin, imageMin + available);
            var modalBefore = _transformGizmo.IsModal;

            HandleTransformShortcuts(hovering, modalBefore);

            // The Gizmo checkbox hides the handles; G/R/S keep working either way.
            _transformGizmo.ShowHandles = _viewport.ShowGizmo;

            // Gizmo hover/drag is resolved before picking so a click on a handle moves the object
            // instead of re-selecting whatever is behind the gizmo.
            var gizmoSelection = _previewEntity is null ? _selected : null;
            _transformGizmo.Update(_world, gizmoSelection, FindCamera(), imageMin, available);

            // Camera navigation yields to a running transform.
            UpdateViewportCamera(available, hovering);

            HandleGameViewPicking(imageMin, available, allowPicking: !modalBefore && !_transformGizmo.IsModal);
            HandleGameViewDropTarget();
            UpdateDragPreview(imageMin, available);
            DrawViewOrientationWidget(imageMin, available);
        }
        else
        {
            ImGui.Text("GameView texture unavailable");
        }
        
        ImGui.End();
    }

    /// <summary>
    /// Blender-style transform keys: G, R and S start a mouse-driven transform of the selection.
    /// They only work while the viewport has the cursor, so they cannot collide with a text field.
    /// </summary>
    private void HandleTransformShortcuts(bool hoveringViewport, bool modalActive)
    {
        if (modalActive || !hoveringViewport || ImGui.GetIO().WantTextInput)
            return;

        if (_selected is not { } selected || !_world.IsAlive(selected))
            return;

        if (ImGui.IsKeyPressed(ImGuiKey.G))
            _transformGizmo.RequestModal(GizmoMode.Translate);
        else if (ImGui.IsKeyPressed(ImGuiKey.R))
            _transformGizmo.RequestModal(GizmoMode.Rotate);
        else if (ImGui.IsKeyPressed(ImGuiKey.S))
            _transformGizmo.RequestModal(GizmoMode.Scale);
    }

    /// <summary>
    /// Blender navigation: middle-drag orbits around the selection (or the camera target),
    /// Shift+middle-drag pans, and the wheel dollies along the view direction.
    /// </summary>
    private void UpdateViewportCamera(Vector2 imageSize, bool hoveringViewport)
    {
        if (!hoveringViewport || _transformGizmo.IsModal || FindCamera() is not { } camera)
            return;

        var io = ImGui.GetIO();
        var pivot = ViewPivot(camera);

        if (ImGui.IsMouseDown(ImGuiMouseButton.Middle))
        {
            var delta = io.MouseDelta;

            if (io.KeyShift)
            {
                var forward = Vector3.Normalize(camera.Target - camera.Position);
                var right = Vector3.Cross(forward, camera.Up);
                right = right.LengthSquared() > 1e-6f ? Vector3.Normalize(right) : Vector3.UnitX;
                var up = Vector3.Cross(right, forward);

                var worldPerPixel = ViewportCamera.WorldPerPixel(
                    Vector3.Distance(camera.Position, camera.Target), camera.FieldOfView, imageSize.Y);

                var pan = ViewportCamera.Pan(delta.X, delta.Y, right, up, worldPerPixel);
                camera.Position += pan;
                camera.Target += pan;
            }
            else
            {
                camera.Position = ViewportCamera.Orbit(camera.Position, pivot, delta.X * 0.01f, delta.Y * 0.01f);
                camera.Target = pivot;
            }
        }

        if (io.MouseWheel != 0f)
            camera.Move((ViewportCamera.Dolly(camera.Position, camera.Target, io.MouseWheel) - camera.Position));

        // Keep the entity transform in step so the camera sync cannot undo this move.
        SyncCameraTransform(camera);
    }

    /// <summary>The selection's origin if there is one, otherwise the camera's own target.</summary>
    private Vector3 ViewPivot(Camera camera)
    {
        if (_selected is { } entity &&
            _world.IsAlive(entity) &&
            _world.TryGetComponent<Transform>(entity, out var transform))
        {
            var matrix = transform.GetWorldMatrix(_world);
            return new Vector3(matrix.M41, matrix.M42, matrix.M43);
        }

        return camera.Target;
    }

    /// <summary>
    /// Play mode controls. Entering play clones the authored world into the session's runtime
    /// world; stopping drops it again, so anything done while playing is discarded.
    /// </summary>
    private void DrawPlayToolbar()
    {
        var playing = _session.InPlayMode;
        var paused = _session.State == PlayState.Paused;

        if (!playing)
        {
            if (ImGui.Button("Play"))
                StartPlay();
        }
        else if (ImGui.Button(paused ? "Resume" : "Pause"))
        {
            if (paused)
                _session.Resume();
            else
                _session.Pause();
        }

        ImGui.SameLine();

        if (ImGui.Button("Stop") && playing)
        {
            _session.Stop();
            SetStatus("Stopped: authored scene restored.", isError: false);
        }

        ImGui.SameLine();
        ImGui.TextColored(playing ? SuccessColor : new Vector4(0.6f, 0.6f, 0.62f, 1f), _session.State.ToString());

        ImGui.SameLine();
        ImGui.TextDisabled(playing
            ? $"runtime world: {_session.Active.EntityCount} entities"
            : $"authored world: {_session.Active.EntityCount} entities");
    }

    private void StartPlay()
    {
        if (_session.InPlayMode)
            return;

        var runtime = SceneCloner.Clone(_session.Authored);
        _session.EnterPlay(runtime);

        SetStatus($"Playing: {runtime.EntityCount} entities in the runtime world.", isError: false);
    }

    /// <summary>Viewport options: overlays, which gizmo, and snapping.</summary>
    private void DrawViewportToolbar()
    {
        var showGrid = _viewport.ShowGrid;
        if (ImGui.Checkbox("Grid", ref showGrid))
            _viewport.ShowGrid = showGrid;

        ImGui.SameLine();
        var showGizmo = _viewport.ShowGizmo;
        if (ImGui.Checkbox("Gizmo", ref showGizmo))
            _viewport.ShowGizmo = showGizmo;

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();

        // The buttons pick which gizmo is drawn; the G/R/S keys run a modal transform instead.
        if (ModeButton("Move", GizmoMode.Translate))
            _transformGizmo.Mode = GizmoMode.Translate;

        ImGui.SameLine();
        if (ModeButton("Rotate", GizmoMode.Rotate))
            _transformGizmo.Mode = GizmoMode.Rotate;

        ImGui.SameLine();
        if (ModeButton("Scale", GizmoMode.Scale))
            _transformGizmo.Mode = GizmoMode.Scale;

        ImGui.SameLine();
        var snap = _transformGizmo.Snap;
        if (ImGui.Checkbox("Snap", ref snap))
            _transformGizmo.Snap = snap;

        if (_transformGizmo.IsModal)
        {
            ImGui.SameLine();
            var axis = _transformGizmo.ModalAxis;
            var suffix = axis == GizmoAxis.None ? string.Empty : $" {axis}";
            ImGui.TextColored(SuccessColor,
                $"{_transformGizmo.Mode}{suffix}  \u00b7  LMB confirm, RMB/Esc cancel, X/Y/Z axis");
        }
        else if (_transformGizmo.ActiveAxis != GizmoAxis.None)
        {
            ImGui.SameLine();
            ImGui.TextColored(SuccessColor, $"{_transformGizmo.Mode} {_transformGizmo.ActiveAxis}");
        }
    }

    private bool ModeButton(string label, GizmoMode mode)
    {
        var selected = _transformGizmo.Mode == mode;

        if (selected)
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));

        var pressed = ImGui.Button(label);

        if (selected)
            ImGui.PopStyleColor();

        return pressed;
    }

    /// <summary>Selects the entity under the cursor when the game view is clicked.</summary>
    private void HandleGameViewPicking(Vector2 imageMin, Vector2 imageSize, bool allowPicking)
    {
        if (!allowPicking || !ImGui.IsItemHovered() || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            return;

        // Clicks that belong to the gizmo or the orientation widget must not re-select.
        if (_transformGizmo.ActiveAxis != GizmoAxis.None || _transformGizmo.HoveredAxis != GizmoAxis.None)
            return;

        if (IsMouseOverViewWidget(imageMin, imageSize))
            return;

        if (TryRayFromGameView(imageMin, imageSize, out var ray) && FindCamera() is not null)
            _selected = ScenePicker.Pick(_world, ray);
    }

    private const float ViewWidgetRadius = 44f;

    private static readonly GizmoAxis[] ViewWidgetAxes = [GizmoAxis.X, GizmoAxis.Y, GizmoAxis.Z];

    private static Vector2 ViewWidgetCenter(Vector2 imageMin, Vector2 imageSize) =>
        new(imageMin.X + imageSize.X - ViewWidgetRadius - 18f, imageMin.Y + ViewWidgetRadius + 18f);

    private static bool IsMouseOverViewWidget(Vector2 imageMin, Vector2 imageSize) =>
        Vector2.Distance(ImGui.GetMousePos(), ViewWidgetCenter(imageMin, imageSize)) <= ViewWidgetRadius + 8f;

    /// <summary>
    /// Blender-style orientation widget. Balls are placed by rotating the world axes into view
    /// space; clicking one moves the camera to look along that axis.
    /// </summary>
    private void DrawViewOrientationWidget(Vector2 imageMin, Vector2 imageSize)
    {
        if (FindCamera() is not { } camera)
            return;

        var center = ViewWidgetCenter(imageMin, imageSize);
        var drawList = ImGui.GetWindowDrawList();
        var view = camera.ViewMatrix;
        var mouse = ImGui.GetMousePos();

        drawList.AddCircleFilled(center, ViewWidgetRadius + 8f, ImGui.GetColorU32(ImGuiCol.WindowBg, 0.6f), 32);

        var balls = new List<Ball>(6);

        foreach (var axis in ViewWidgetAxes)
        {
            var direction = TransformGizmo.AxisVector(axis);
            var color = TransformGizmo.AxisColor(axis);

            balls.Add(new Ball(direction, axis.ToString(), color, 11f));
            balls.Add(new Ball(-direction, string.Empty, color, 8f));
        }

        foreach (var ball in balls)
        {
            var inView = Vector3.TransformNormal(ball.Direction, view);
            ball.Depth = inView.Z;
            ball.Position = center + new Vector2(inView.X, -inView.Y) * (ViewWidgetRadius * 0.82f);
            ball.Alpha = 0.45f + 0.55f * Math.Clamp(-inView.Z, 0f, 1f);
        }

        // In view space the camera looks down -Z, so larger Z is further away: draw those first.
        balls.Sort(static (left, right) => right.Depth.CompareTo(left.Depth));

        foreach (var ball in balls)
        {
            drawList.AddCircleFilled(ball.Position, ball.Radius,
                ImGui.ColorConvertFloat4ToU32(new Vector4(ball.Color.X, ball.Color.Y, ball.Color.Z, ball.Alpha)), 20);
            drawList.AddCircle(ball.Position, ball.Radius, ImGui.GetColorU32(ImGuiCol.Text, 0.35f), 20, 1f);

            if (ball.Label.Length > 0)
            {
                var size = ImGui.CalcTextSize(ball.Label);
                drawList.AddText(ball.Position - size * 0.5f, ImGui.GetColorU32(ImGuiCol.Text), ball.Label);
            }
        }

        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            return;

        // Opposite axes can project onto the same spot, so pick the ball nearest the camera,
        // which is the one that was drawn last.
        for (var i = balls.Count - 1; i >= 0; i--)
        {
            if (Vector2.Distance(mouse, balls[i].Position) > balls[i].Radius)
                continue;

            SnapCameraToDirection(camera, balls[i].Direction);
            SyncCameraTransform(camera);
            break;
        }
    }

    /// <summary>A clickable axis ball of the orientation widget.</summary>
    private sealed class Ball(Vector3 direction, string label, Vector4 color, float radius)
    {
        public Vector3 Direction { get; } = direction;
        public string Label { get; } = label;
        public Vector4 Color { get; } = color;
        public float Radius { get; } = radius;

        public float Depth { get; set; }
        public float Alpha { get; set; }
        public Vector2 Position { get; set; }
    }

    /// <summary>Writes the camera position back onto the entity transform that owns it.</summary>
    private void SyncCameraTransform(Camera camera)
    {
        foreach (var entity in _world.GetEntitiesWith<Transform, Camera>())
        {
            if (!ReferenceEquals(_world.GetComponent<Camera>(entity), camera))
                continue;

            ref var transform = ref _world.GetComponent<Transform>(entity);
            transform.Position = camera.Position;
            return;
        }
    }

    private static void SnapCameraToDirection(Camera camera, Vector3 direction)
    {
        var distance = Vector3.Distance(camera.Position, camera.Target);
        if (distance < 1e-3f)
            distance = 3f;

        camera.Position = camera.Target + Vector3.Normalize(direction) * distance;
    }

    /// <summary>Turns a point in the game view image into a world-space ray.</summary>
    private bool TryRayFromGameView(Vector2 imageMin, Vector2 imageSize, out Ray ray)
    {
        ray = default;

        if (imageSize.X <= 0f || imageSize.Y <= 0f || FindCamera() is not { } camera)
            return false;

        var mouse = ImGui.GetMousePos();

        // Screen top maps to -1 because the projection carries the Vulkan Y flip.
        var ndc = new Vector2(
            (mouse.X - imageMin.X) / imageSize.X * 2f - 1f,
            (mouse.Y - imageMin.Y) / imageSize.Y * 2f - 1f);

        ray = camera.ScreenPointToRay(ndc);
        return true;
    }

    /// <summary>
    /// Shows a ghost of the dragged asset under the cursor so the drop position is visible before
    /// releasing the mouse. The ghost is a normal entity and is removed if the drag is abandoned.
    /// </summary>
    private void UpdateDragPreview(Vector2 imageMin, Vector2 imageSize)
    {
        if (!EditorDragDrop.TryGetDraggedAssetPath(out var path))
        {
            DestroyDragPreview();
            return;
        }

        var mouse = ImGui.GetMousePos();
        var inside = mouse.X >= imageMin.X && mouse.X <= imageMin.X + imageSize.X &&
                     mouse.Y >= imageMin.Y && mouse.Y <= imageMin.Y + imageSize.Y;

        if (!inside || !TryRayFromGameView(imageMin, imageSize, out var ray))
        {
            DestroyDragPreview();
            return;
        }

        var point = ResolveDropPoint(ray);

        if (_previewEntity is not { } preview || _previewAssetPath != path || !_world.IsAlive(preview))
        {
            DestroyDragPreview();

            if (!_assets.TryGetAsset(path, out var asset) ||
                !_assets.TryLoad(asset, out var mesh, out _) ||
                mesh is null)
            {
                return;
            }

            _previewOffset = mesh.LocalBounds.Center * FittedScale(mesh);
            _previewEntity = SceneSpawner.SpawnFittedMesh(_world, mesh, new Material(asset.Name, PreviewColor), point);
            _previewAssetPath = path;
            return;
        }

        ref var transform = ref _world.GetComponent<Transform>(preview);
        transform.Position = point - _previewOffset;
    }

    /// <summary>
    /// Where a dropped asset lands: on whatever surface is under the cursor, else on the y = 0
    /// plane, else in front of the camera.
    /// </summary>
    private Vector3 ResolveDropPoint(in Ray ray)
    {
        if (ScenePicker.TryPick(_world, ray, out _, out var hitPoint, _previewEntity))
            return hitPoint;

        if (MathF.Abs(ray.Direction.Y) > 1e-4f)
        {
            var toPlane = -ray.Origin.Y / ray.Direction.Y;
            if (toPlane > 0f)
                return ray.Origin + ray.Direction * toPlane;
        }

        return FindCamera()?.Target ?? Vector3.Zero;
    }

    /// <summary>Accepts an asset dropped on the game view and keeps the ghost as a real entity.</summary>
    private void HandleGameViewDropTarget()
    {
        if (!ImGui.BeginDragDropTarget())
            return;

        var payload = ImGui.AcceptDragDropPayload(EditorDragDrop.AssetPayload);
        if (EditorDragDrop.IsDelivered(payload) && EditorDragDrop.TryReadAssetPayload(payload, out var path))
            CommitDragPreview(path);

        ImGui.EndDragDropTarget();
    }

    private void CommitDragPreview(string path)
    {
        if (_previewEntity is { } preview &&
            _world.IsAlive(preview) &&
            string.Equals(_previewAssetPath, path, StringComparison.OrdinalIgnoreCase))
        {
            // Keep the entity that was previewed, but give it a normal material colour.
            ref var renderer = ref _world.GetComponent<MeshRenderer>(preview);
            renderer.Material = new Material(renderer.Material.Name, NextSpawnColor());

            _selected = preview;
            ReleaseDragPreview();

            SetStatus($"Added '{renderer.Material.Name}' to the scene.", isError: false);
            return;
        }

        SpawnAssetFromPath(path);
    }

    private void DestroyDragPreview()
    {
        if (_previewEntity is { } preview && _world.IsAlive(preview))
            _world.DestroyEntity(preview);

        ReleaseDragPreview();
    }

    private void ReleaseDragPreview()
    {
        _previewEntity = null;
        _previewAssetPath = null;
        _previewOffset = Vector3.Zero;
    }

    private static float FittedScale(Mesh mesh) =>
        mesh.LocalBounds.MaxDimension > 1e-6f ? 1f / mesh.LocalBounds.MaxDimension : 1f;

    private Camera? FindCamera()
    {
        foreach (var entity in _world.GetEntitiesWith<Camera>())
            return _world.GetComponent<Camera>(entity);

        return null;
    }

    public void SetViewTextures(nint editorViewTextureId, nint gameViewTextureId)
    {
        _editorViewTextureID = editorViewTextureId;
        _gameViewTextureID = gameViewTextureId;
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

    /// <summary>Spawns an asset that was dragged in from the Browser.</summary>
    private void SpawnAssetFromPath(string path, Entity parent = default)
    {
        if (!_assets.TryGetAsset(path, out var asset))
        {
            SetStatus($"Unknown asset: {path}", isError: true);
            return;
        }

        if (!_assets.TryLoad(asset, out var mesh, out var error) || mesh is null)
        {
            SetStatus($"Could not import '{asset.Name}': {error}", isError: true);
            return;
        }

        var entity = Spawn(mesh, asset.Name);

        if (parent.IsValid)
            Hierarchy.TrySetParent(_world, entity, parent);

        _selected = entity;
        SetStatus($"Added '{asset.Name}' ({mesh.TriangleCount} tris).", isError: false);
    }

    /// <summary>Spawns a mesh imported through the Browser's file dialog.</summary>
    private void SpawnImportedMesh(Mesh mesh)
    {
        _selected = Spawn(mesh, mesh.Name);
        SetStatus($"Imported '{mesh.Name}' ({mesh.Vertices.Length} verts, {mesh.TriangleCount} tris).",
            isError: false);
    }

    private Entity Spawn(Mesh mesh, string name)
    {
        var material = new Material(name, NextSpawnColor());

        return SceneSpawner.SpawnFittedMesh(_world, mesh, material, NextSpawnPosition());
    }

    private Vector4 NextSpawnColor() => SpawnPalette[_materialCounter++ % SpawnPalette.Length];

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

    /// <summary>Top-level menu: create cameras and primitives, toggle overlays.</summary>
    private void DrawMainMenuBar()
    {
        if (!ImGui.BeginMainMenuBar())
            return;

        if (ImGui.BeginMenu("Create"))
        {
            if (ImGui.MenuItem("Game Camera"))
                SpawnGameCamera();

            ImGui.Separator();

            if (ImGui.MenuItem("Cube"))
                SpawnPrimitive(PrimitiveMeshes.Cube, "Cube");

            if (ImGui.MenuItem("Plane"))
                SpawnPrimitive(PrimitiveMeshes.Plane, "Plane");

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("View"))
        {
            if (ImGui.MenuItem("Grid", null, _viewport.ShowGrid))
                _viewport.ShowGrid = !_viewport.ShowGrid;

            if (ImGui.MenuItem("Gizmo", null, _viewport.ShowGizmo))
                _viewport.ShowGizmo = !_viewport.ShowGizmo;

            ImGui.EndMenu();
        }

        ImGui.EndMainMenuBar();
    }

    /// <summary>Read-only view through the game camera; nothing here can be edited.</summary>
    private void DrawGameWindow()
    {
        ImGui.SetNextWindowSize(new Vector2(640f, 400f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Game");

        var available = ImGui.GetContentRegionAvail();
        GameViewSize = available;

        if (HasGameCamera())
        {
            if (_gameViewTextureID != 0 && available.X > 1f && available.Y > 1f)
                ImGui.Image((nint)_gameViewTextureID, available);
            else
                ImGui.TextDisabled("Game view texture unavailable");

            PublishGameInput(ImGui.IsWindowHovered() && !ImGui.IsAnyItemActive());
        }
        else
        {
            ImGui.TextDisabled("No game camera. Add one from Create > Game Camera.");
        }

        ImGui.End();
    }

    /// <summary>
    /// Samples gameplay input from ImGui and publishes it to the session. Only the game view feeds
    /// it, and only while playing; everything else publishes an empty snapshot.
    /// </summary>
    private void PublishGameInput(bool gameViewHovered)
    {
        if (!_session.InPlayMode || !gameViewHovered)
        {
            _session.ClearInput();
            return;
        }

        var io = ImGui.GetIO();

        _session.SetInput(new GameInput(
            io.MousePos,
            io.MouseDelta,
            io.MouseWheel,
            io.MouseDown[0],
            io.MouseDown[1],
            io.MouseDown[2],
            io.KeyShift,
            io.KeyCtrl));
    }

    private bool HasGameCamera()
    {
        foreach (var entity in _world.GetEntitiesWith<Camera>())
        {
            if (_world.GetComponent<Camera>(entity).Kind == CameraKind.Game)
                return true;
        }

        return false;
    }

    private void SpawnGameCamera()
    {
        var camera = FindCamera();
        var position = camera?.Position ?? new Vector3(3f, 2f, 5f);
        var target = camera?.Target ?? Vector3.Zero;

        _selected = SceneSpawner.SpawnCamera(_world, position, target);
        SetStatus($"Added game camera #{_selected?.Id}.", isError: false);
    }

    private void SpawnPrimitive(Mesh mesh, string name)
    {
        _selected = Spawn(mesh, name);
        SetStatus($"Added {name}.", isError: false);
    }

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
            ImGuiWindowFlags.NoDocking |
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoBringToFrontOnFocus |
            ImGuiWindowFlags.NoNavFocus
        );
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor();
        
        uint dockspaceId = ImGui.GetID("MainDockSpace");
        ImGui.DockSpace(dockspaceId, Vector2.Zero, ImGuiDockNodeFlags.PassthruCentralNode);
        ImGui.End();
    }
}
