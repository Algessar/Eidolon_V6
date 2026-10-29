using System.Numerics;
using Eidolon.Vulkan;
using EidolonCore.Assets;
using EidolonCore.ECS;
using EidolonCore.Nexus;
using EidolonCore.Nexus.Animation;
using EidolonCore.Rendering;
using Eidolon.Assimp;
using EidolonCore.Scene;
using EidolonCore.Simulation;
using ImGuiNET;
using Silk.NET.Windowing;

namespace Eidolon.Editor;

internal unsafe class EditorUI : IEditorOverlay
{
    /// <summary>Inspector rotation fields are in degrees; the engine keeps radians.</summary>
    private const float DegreesToRadians = MathF.PI / 180f;

    private const float RadiansToDegrees = 180f / MathF.PI;

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

    /// <summary>ImGui cannot be touched before the backend has created its context.</summary>
    private bool _initialized;
    private readonly TransformGizmo _transformGizmo = new();
    private readonly TimelinePanel _timeline = new();

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
        _browser = new AssetBrowserPanel(assets, SpawnAsset, SpawnImportedMesh, ImportModel);
    }

    /// <summary>
    /// Configures ImGui for the editor. Must run after the ImGui context exists, which is the
    /// renderer's job, so the host can build the overlay before the backend starts.
    /// </summary>
    public void Initialize()
    {
        if (_initialized)
            return;

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
        _initialized = true;
    }
    
    public void Update()
    {
        if (!_initialized)
        {
            // Fail with a readable message instead of an ImGui assertion in native code.
            throw new InvalidOperationException(
                "EditorUI.Update ran before Initialize. The backend creates the ImGui context and then " +
                "calls Initialize; drawing earlier would use ImGui without a context.");
        }

        DrawDockSpace();
        DrawMainMenuBar();
        DrawGameWindow();
        DrawSceneViewport();
        DrawSceneWindow();
        DrawInspectorWindow();
        _browser.Draw();
        _timeline.Draw(_world, _session, _selected);
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

        ImGui.Text(_session.InPlayMode
            ? $"{_world.EntityCount} entities (runtime copy)"
            : $"{_world.EntityCount} entities");

        if (_session.LastError is { } playError)
            ImGui.TextColored(ErrorColor, $"System failed, play stopped: {playError.Message}");

        if (ImGui.BeginPopupContextWindow("##hierarchyCreate", ImGuiPopupFlags.MouseButtonRight))
        {
            if (ImGui.MenuItem("Add Game Camera"))
                SpawnGameCamera();

            if (ImGui.MenuItem("Add Cube"))
                SpawnPrimitive(PrimitiveMeshes.Cube, "Cube");

            if (ImGui.MenuItem("Add Plane"))
                SpawnPrimitive(PrimitiveMeshes.Plane, "Plane");

            if (ImGui.MenuItem("Add Sphere"))
                SpawnPrimitive(PrimitiveMeshes.Sphere, "Sphere");

            if (ImGui.MenuItem("Add Capsule"))
                SpawnPrimitive(PrimitiveMeshes.Capsule, "Capsule");

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
        // A name is what a clip binds to, so it is also the most useful label here.
        if (_world.TryGetComponent<Name>(entity, out var name))
            return $"#{entity.Id}  {name.Value}";

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

        if (_world.TryGetComponent<Name>(entity, out var entityName))
            ImGui.TextDisabled($"Name: {entityName.Value}");

        if (_world.HasComponent<Transform>(entity))
            DrawTransformInspector(entity);

        if (_world.TryGetComponent<MeshRenderer>(entity, out var meshRenderer))
            DrawMeshRendererInspector(meshRenderer);

        DrawOptionalComponents(entity);

        ImGui.Separator();

        ImGui.BeginDisabled(_session.InPlayMode);
        if (ImGui.Button("Delete entity"))
        {
            _world.DestroyEntity(entity);
            _selected = null;
        }

        ImGui.EndDisabled();

        ImGui.End();
    }

    /// <summary>Optional components on the selected entity, with an add menu for the ones it lacks.</summary>
    private void DrawOptionalComponents(Entity entity)
    {
        if (_world.TryGetComponent<Spin>(entity, out var spin))
        {
            if (ImGui.CollapsingHeader("Spin", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var axis = spin.Axis;
                var speed = spin.RadiansPerSecond;

                if (ImGui.DragFloat3("Axis", ref axis, 0.02f))
                    spin.Axis = axis;

                if (ImGui.DragFloat("Radians / second", ref speed, 0.05f))
                    spin.RadiansPerSecond = speed;

                if (ImGui.Button("Remove Spin"))
                    _world.RemoveComponent<Spin>(entity);
                else
                    _world.SetComponent(entity, spin);
            }
        }

        if (_world.TryGetComponent<XpbdBody>(entity, out var body))
        {
            if (ImGui.CollapsingHeader("Rigid Body", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginCombo("Shape", body.Shape.ToString()))
                {
                    foreach (var candidate in Enum.GetValues<XpbdShape>())
                    {
                        if (ImGui.Selectable(candidate.ToString(), candidate == body.Shape))
                            body.Shape = candidate;
                    }

                    ImGui.EndCombo();
                }

                switch (body.Shape)
                {
                    case XpbdShape.Sphere:
                    {
                        var radius = body.Radius;

                        if (ImGui.DragFloat("Radius", ref radius, 0.01f, 0.01f, 20f))
                            body.Radius = radius;

                        break;
                    }

                    case XpbdShape.Capsule:
                    {
                        var radius = body.Radius;
                        var segment = body.SegmentLength;

                        if (ImGui.DragFloat("Radius", ref radius, 0.01f, 0.01f, 20f))
                            body.Radius = radius;

                        if (ImGui.DragFloat("Segment length", ref segment, 0.01f, 0f, 20f))
                            body.SegmentLength = segment;

                        break;
                    }

                    default:
                    {
                        var halfExtents = body.HalfExtents;
                        var contactRadius = body.ContactRadius;

                        if (ImGui.DragFloat3("Half extents", ref halfExtents, 0.02f))
                            body.HalfExtents = halfExtents;

                        if (ImGui.DragFloat("Contact radius", ref contactRadius, 0.005f, 0.001f, 0.25f))
                            body.ContactRadius = contactRadius;

                        break;
                    }
                }

                // A body with no inverse mass is pinned: it collides but nothing can move it.
                var isStatic = body.InverseMass <= 0f;

                if (ImGui.Checkbox("Static", ref isStatic))
                    body.InverseMass = isStatic ? 0f : 1f;

                var inverseMass = body.InverseMass;

                if (ImGui.DragFloat("Inverse mass", ref inverseMass, 0.02f, 0f, 10f))
                    body.InverseMass = inverseMass;

                var stiffness = body.Stiffness;

                if (ImGui.DragFloat("Stiffness", ref stiffness, 0.01f, 0.05f, 1f))
                    body.Stiffness = stiffness;

                if (ImGui.Button("Remove Rigid Body"))
                    _world.RemoveComponent<XpbdBody>(entity);
                else
                    _world.SetComponent(entity, body);
            }
        }

        if (_world.TryGetComponent<AnimationPlayer>(entity, out var player))
        {
            if (ImGui.CollapsingHeader("Animation", ImGuiTreeNodeFlags.DefaultOpen))
                DrawAnimationInspector(player);
        }

        if (ImGui.Button("Add Component"))
            ImGui.OpenPopup("##addComponent");

        if (ImGui.BeginPopup("##addComponent"))
        {
            if (!_world.HasComponent<Spin>(entity) && ImGui.MenuItem("Spin"))
                _world.SetComponent(entity, new Spin());

            if (!_world.HasComponent<XpbdBody>(entity) && ImGui.MenuItem("Rigid Body"))
                _world.SetComponent(entity, BodyFromMesh(entity));

            ImGui.EndPopup();
        }
    }

    /// <summary>Playback controls for a clip. The timeline proper is the next slice.</summary>
    /// <remarks>
    /// Scrubbing shows the sampled time but does not preview the pose: clips are evaluated by the
    /// simulation, which only runs in play mode.
    /// </remarks>
    private static void DrawAnimationInspector(AnimationPlayer player)
    {
        if (player.Clip is not { } clip)
        {
            ImGui.TextDisabled("No clip assigned.");
            return;
        }

        ImGui.Text($"{clip.Name}: {clip.Duration:0.00}s, {clip.Tracks.Count} track(s)");

        var time = player.Time;

        if (ImGui.SliderFloat("Time", ref time, 0f, clip.Duration))
            player.Time = time;

        var playing = player.Playing;

        if (ImGui.Checkbox("Playing", ref playing))
            player.Playing = playing;

        var loop = player.Loop;

        if (ImGui.Checkbox("Loop", ref loop))
            player.Loop = loop;

        var speed = player.Speed;

        if (ImGui.DragFloat("Speed", ref speed, 0.01f, -4f, 4f))
            player.Speed = speed;
    }

    /// <summary>
    /// Guesses a body from what the entity already is: a cube becomes a box of the same size, a sphere
    /// or capsule the matching primitive, and anything imported gets the box default.
    /// </summary>
    /// <remarks>
    /// This only seeds the component; every value stays editable, because a mesh is a presentation of
    /// the shape rather than its definition.
    /// </remarks>
    private XpbdBody BodyFromMesh(Entity entity)
    {
        var body = new XpbdBody();

        if (!_world.TryGetComponent<Transform>(entity, out var transform) ||
            !_world.TryGetComponent<MeshRenderer>(entity, out var renderer) ||
            renderer.Mesh is not { } mesh)
        {
            return body;
        }

        var scale = Matrix4x4.Decompose(transform.GetWorldMatrix(_world), out var worldScale, out _, out _)
            ? worldScale
            : transform.Scale;

        var size = mesh.LocalBounds.Size;
        var worldSize = new Vector3(
            MathF.Abs(scale.X) * size.X,
            MathF.Abs(scale.Y) * size.Y,
            MathF.Abs(scale.Z) * size.Z);

        if (ReferenceEquals(mesh, PrimitiveMeshes.Sphere))
        {
            body.Shape = XpbdShape.Sphere;
            body.Radius = MathF.Max(worldSize.X, MathF.Max(worldSize.Y, worldSize.Z)) * 0.5f;
            return body;
        }

        if (ReferenceEquals(mesh, PrimitiveMeshes.Capsule))
        {
            body.Shape = XpbdShape.Capsule;
            body.Radius = MathF.Max(worldSize.X, worldSize.Z) * 0.5f;
            body.SegmentLength = MathF.Max(0f, worldSize.Y - worldSize.X);
            return body;
        }

        body.HalfExtents = worldSize * 0.5f;
        return body;
    }

    private void DrawTransformInspector(Entity entity)
    {
        if (!ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ref var transform = ref _world.GetComponent<Transform>(entity);

        ImGui.DragFloat3("Position", ref transform.Position, 0.05f);

        // Rotation is a quaternion in the engine but three numbers in the inspector. The axes are
        // named for what they are: camera terms would be wrong on an entity that is not a camera.
        var (x, y, z) = transform.Rotation.ToEulerAngles();
        var xDegrees = x * RadiansToDegrees;
        var yDegrees = y * RadiansToDegrees;
        var zDegrees = z * RadiansToDegrees;

        var rotated = ImGui.DragFloat("Rotation X", ref xDegrees, 0.5f);
        rotated |= ImGui.DragFloat("Rotation Y", ref yDegrees, 0.5f);
        rotated |= ImGui.DragFloat("Rotation Z", ref zDegrees, 0.5f);

        if (rotated)
        {
            transform.Rotation = QuaternionExtensions.FromEulerAngles(
                xDegrees * DegreesToRadians,
                yDegrees * DegreesToRadians,
                zDegrees * DegreesToRadians);
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
    public Vector2 EditorViewSize { get; private set; }

    /// <summary>Size of the game view panel.</summary>
    public Vector2 GameViewSize { get; private set; }

    private void DrawSceneViewport()
    {
        // Give the scene viewport a usable size the first time the editor runs.
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

            HandleViewportPicking(imageMin, available, allowPicking: !modalBefore && !_transformGizmo.IsModal);
            HandleViewportDropTarget();
            UpdateDragPreview(imageMin, available);
            DrawViewOrientationWidget(imageMin, available);
        }
        else
        {
            ImGui.Text("Scene texture unavailable");
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

        if (ImGui.IsKeyPressed(ImGuiKey.F))
        {
            FrameSelected();
            return;
        }

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
                var pivot = _viewport.OrbitAroundSelected && TryGetSelectedCenter(out var selectedCenter)
                    ? selectedCenter
                    : camera.Target;

                camera.Position = ViewportCamera.Orbit(camera.Position, pivot, delta.X * 0.01f, delta.Y * 0.01f);

                // Orbit the view target by the same amount. Replacing it with the selection
                // would snap the view to the selection as soon as middle-mouse is pressed.
                // Moving the complete camera rig keeps the selected item under the same screen
                // position while the camera path still rotates around it.
                camera.Target = ViewportCamera.Orbit(camera.Target, pivot, delta.X * 0.01f, delta.Y * 0.01f);
            }
        }

        if (io.MouseWheel != 0f)
            camera.Move((ViewportCamera.Dolly(camera.Position, camera.Target, io.MouseWheel) - camera.Position));

        // Keep the entity transform in step so the camera sync cannot undo this move.
        SyncCameraTransform(camera);
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

        World runtime;

        try
        {
            runtime = SceneCloner.Clone(_session.Authored);
        }
        catch (Exception exception)
        {
            SetStatus($"Could not start play mode: {exception.Message}", isError: true);
            return;
        }

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
        var orbitAroundSelected = _viewport.OrbitAroundSelected;
        if (ImGui.Checkbox("Orbit Selected", ref orbitAroundSelected))
            _viewport.OrbitAroundSelected = orbitAroundSelected;

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

    /// <summary>Selects the entity under the cursor when the scene viewport is clicked.</summary>
    private void HandleViewportPicking(Vector2 imageMin, Vector2 imageSize, bool allowPicking)
    {
        if (!allowPicking || !ImGui.IsItemHovered() || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            return;

        // Clicks that belong to the gizmo or the orientation widget must not re-select.
        if (_transformGizmo.ActiveAxis != GizmoAxis.None || _transformGizmo.HoveredAxis != GizmoAxis.None)
            return;

        if (IsMouseOverViewWidget(imageMin, imageSize))
            return;

        if (TryRayFromViewport(imageMin, imageSize, out var ray))
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

    /// <summary>
    /// Frames the selection: aims the view at it and pulls back far enough to see it whole. This is
    /// the explicit operator that replaces the old "frame whatever is selected on every rotate"
    /// behaviour.
    /// </summary>
    private void FrameSelected()
    {
        if (FindCamera() is not { } camera || _selected is not { } entity || !_world.IsAlive(entity))
            return;

        if (!TryGetSelectedCenter(out var origin))
            origin = new Vector3(0f, 0.5f, 0f);

        var radius = 1f;

        if (_world.TryGetComponent<Transform>(entity, out var transform) &&
            _world.TryGetComponent<MeshRenderer>(entity, out var renderer) && renderer.Mesh is { } mesh)
        {
            var matrix = transform.GetWorldMatrix(_world);
            if (Matrix4x4.Decompose(matrix, out var worldScale, out _, out _))
                radius = MathF.Max(0.5f,
                    mesh.LocalBounds.MaxDimension * 0.5f * worldScale.Length() / MathF.Sqrt(3f));
        }

        var direction = camera.Target - camera.Position;
        direction = direction.LengthSquared() < 1e-6f ? new Vector3(0f, 0f, -1f) : Vector3.Normalize(direction);

        // Never frame from inside the object: selections without geometry still get a usable view.
        var distance = MathF.Max(2.5f, radius * 4f);
        camera.Position = origin - direction * distance;
        camera.Target = origin;

        SyncCameraTransform(camera);
    }

    /// <summary>Gets the selected entity's visible centre in world space.</summary>
    private bool TryGetSelectedCenter(out Vector3 center)
    {
        center = default;

        if (_selected is not { } entity || !_world.IsAlive(entity))
            return false;

        if (_world.TryGetComponent<Transform>(entity, out var transform))
        {
            var worldMatrix = transform.GetWorldMatrix(_world);
            var localCenter = _world.TryGetComponent<MeshRenderer>(entity, out var renderer) && renderer.Mesh is { } mesh
                ? mesh.LocalBounds.Center
                : Vector3.Zero;
            center = Vector3.Transform(localCenter, worldMatrix);
            return true;
        }

        if (_world.TryGetComponent<Camera>(entity, out var selectedCamera))
        {
            center = selectedCamera.Position;
            return true;
        }

        return false;
    }

    /// <summary>Writes the camera position back onto the entity transform that owns it.</summary>
    private void SyncCameraTransform(Camera camera)
    {
        foreach (var entity in _world.GetEntitiesWith<Transform, Camera>())
        {
            if (!ReferenceEquals(_world.GetComponent<Camera>(entity), camera))
                continue;

            ref var transform = ref _world.GetComponent<Transform>(entity);
            var worldRotation = CameraOrientation.RotationFrom(camera.Target - camera.Position);

            // The camera moves in world space; the entity that owns it may be parented.
            transform.TrySetWorldPose(_world, camera.Position, worldRotation);

            // SceneCameras tracks the decomposed world rotation, not the local rotation.
            camera.TransformRotation = worldRotation;
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

    /// <summary>Turns a point in the scene viewport image into a world-space ray.</summary>
    /// <remarks>
    /// The ray must come from the camera the viewport is rendered through. The game camera is a
    /// different view of the same scene, and casting from it selects whatever happens to sit under the
    /// cursor in <em>that</em> view rather than in the one being clicked.
    /// </remarks>
    private bool TryRayFromViewport(Vector2 imageMin, Vector2 imageSize, out Ray ray)
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

        if (!inside || !TryRayFromViewport(imageMin, imageSize, out var ray))
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
    private void HandleViewportDropTarget()
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

    /// <summary>
    /// The camera the viewport renders through: the editor camera, or any camera when a scene has none,
    /// because the viewport breaks without one.
    /// </summary>
    /// <remarks>
    /// A scene may also carry a game camera, and that one must not win just because it was found first.
    /// </remarks>
    private Camera? FindCamera() => FindCamera(CameraKind.Editor) ?? FindAnyCamera();

    private Camera? FindCamera(CameraKind kind)
    {
        foreach (var entity in _world.GetEntitiesWith<Camera>())
        {
            var camera = _world.GetComponent<Camera>(entity);

            if (camera.Kind == kind)
                return camera;
        }

        return null;
    }

    private Camera? FindAnyCamera()
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

        if (ImGui.BeginMenu("File"))
        {
            if (ImGui.MenuItem("Save Scene"))
                SaveScene();

            if (ImGui.MenuItem("Load Scene"))
                LoadScene();

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Create"))
        {
            // Creating entities mid-play would only exist in the runtime copy, so it is disabled.
            ImGui.BeginDisabled(_session.InPlayMode);

            if (ImGui.MenuItem("Game Camera"))
                SpawnGameCamera();

            ImGui.Separator();

            if (ImGui.MenuItem("Cube"))
                SpawnPrimitive(PrimitiveMeshes.Cube, "Cube");

            if (ImGui.MenuItem("Plane"))
                SpawnPrimitive(PrimitiveMeshes.Plane, "Plane");

            if (ImGui.MenuItem("Sphere"))
                SpawnPrimitive(PrimitiveMeshes.Sphere, "Sphere");

            if (ImGui.MenuItem("Capsule"))
                SpawnPrimitive(PrimitiveMeshes.Capsule, "Capsule");

            ImGui.Separator();

            if (ImGui.MenuItem("Import model..."))
                ImportModel();

            ImGui.EndDisabled();
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("View"))
        {
            if (ImGui.MenuItem("Grid", null, _viewport.ShowGrid))
                _viewport.ShowGrid = !_viewport.ShowGrid;

            if (ImGui.MenuItem("Frame Selected", "F"))
                FrameSelected();

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
        if (!_session.InPlayMode || !gameViewHovered || ImGui.GetIO().AppFocusLost)
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
            SampleGameKeys()));
    }

    /// <summary>Packs the gameplay keys out of ImGui's current keyboard state.</summary>
    private static GameKey SampleGameKeys()
    {
        var keys = GameKey.None;

        if (ImGui.IsKeyDown(ImGuiKey.W)) keys |= GameKey.W;
        if (ImGui.IsKeyDown(ImGuiKey.A)) keys |= GameKey.A;
        if (ImGui.IsKeyDown(ImGuiKey.S)) keys |= GameKey.S;
        if (ImGui.IsKeyDown(ImGuiKey.D)) keys |= GameKey.D;
        if (ImGui.IsKeyDown(ImGuiKey.Q)) keys |= GameKey.Q;
        if (ImGui.IsKeyDown(ImGuiKey.E)) keys |= GameKey.E;
        if (ImGui.IsKeyDown(ImGuiKey.Space)) keys |= GameKey.Space;
        if (ImGui.IsKeyDown(ImGuiKey.ModShift)) keys |= GameKey.Shift;
        if (ImGui.IsKeyDown(ImGuiKey.ModCtrl)) keys |= GameKey.Control;
        if (ImGui.IsKeyDown(ImGuiKey.LeftArrow)) keys |= GameKey.Left;
        if (ImGui.IsKeyDown(ImGuiKey.RightArrow)) keys |= GameKey.Right;
        if (ImGui.IsKeyDown(ImGuiKey.UpArrow)) keys |= GameKey.Up;
        if (ImGui.IsKeyDown(ImGuiKey.DownArrow)) keys |= GameKey.Down;

        return keys;
    }

    private static string ScenesDirectory => Path.Combine(AppContext.BaseDirectory, "Scenes");

    private const string SceneFilter = "Eidolon scene (*.json)\0*.json\0All files (*.*)\0*.*\0\0";

    private const string ModelFilter =
        "Models (*.fbx;*.obj;*.gltf;*.glb;*.dae;*.3ds;*.blend)\0*.fbx;*.obj;*.gltf;*.glb;*.dae;*.3ds;*.blend\0All files (*.*)\0*.*\0\0";

    private void SaveScene()
    {
        try
        {
            var chosen = FileDialog.TrySaveFile("Save scene", SceneFilter, "json", ScenesDirectory);

            if (chosen is null)
                return;

            SceneSerializer.Save(_session.Authored, chosen, _viewport);
            SetStatus($"Saved {_session.Authored.EntityCount} entities to {chosen}", isError: false);
        }
        catch (Exception exception)
        {
            SetStatus($"Save failed: {exception.Message}", isError: true);
        }
    }

    private void LoadScene()
    {
        if (_session.InPlayMode)
        {
            SetStatus("Stop play mode before loading a scene.", isError: true);
            return;
        }

        var chosen = FileDialog.TryPickFile("Load scene", SceneFilter, ScenesDirectory);

        if (chosen is null)
            return;

        try
        {
            SceneSerializer.Load(chosen, _session.Authored, _assets, _viewport);
            _selected = null;
            _dragSource = null;
            SetStatus($"Loaded {_session.Authored.EntityCount} entities from {chosen}", isError: false);
        }
        catch (Exception exception)
        {
            SetStatus($"Load failed: {exception.Message}", isError: true);
        }
    }

    /// <summary>
    /// Imports a model through Assimp and instantiates it as named, parented entities.
    /// </summary>
    /// <remarks>
    /// The import lands at the viewport's target so it appears where you are looking. Clips the file
    /// carries become the root's <see cref="AnimationPlayer"/>, and their paths already match the node
    /// names, so the timeline can key them immediately.
    /// </remarks>
    private void ImportModel()
    {
        if (_session.InPlayMode)
            return;

        var models = Path.Combine(AppContext.BaseDirectory, "Assets", "Models");
        var modelsFolder = Directory.Exists(models) ? models : null;
        var chosen = FileDialog.TryPickFile("Import model", ModelFilter, modelsFolder);

        if (chosen is not null)
            ImportModel(chosen);
    }

    /// <summary>Imports a model picked in the browser, which hands over the asset rather than a path.</summary>
    private void ImportModel(MeshAsset asset) => ImportModel(asset.Path);

    /// <summary>Imports one path: shared by the file dialog and the browser.</summary>
    private void ImportModel(string path)
    {
        if (_session.InPlayMode)
            return;

        try
        {
            var model = AssimpModelImporter.Import(path);
            var material = new Material(Path.GetFileNameWithoutExtension(path), NextSpawnColor());
            var position = FindCamera()?.Target ?? Vector3.Zero;

            _selected = ModelSpawner.Spawn(_world, model, material, position);
            SetStatus($"Imported {model.Name}: {model.Nodes.Count} node(s), {model.Clips.Count} clip(s).",
                isError: false);
        }
        catch (Exception exception)
        {
            SetStatus($"Import failed: {exception.Message}", isError: true);
        }
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
