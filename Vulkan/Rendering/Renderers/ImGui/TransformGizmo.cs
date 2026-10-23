using System.Numerics;
using Eidolon.Vulkan;
using EidolonCore.ECS;
using EidolonCore.Rendering;
using ImGuiNET;

namespace Eidolon.Editor;

/// <summary>Which transform the gizmo edits.</summary>
internal enum GizmoMode
{
    Translate,
    Rotate,
    Scale,
}

/// <summary>Gizmo handle under the cursor or being dragged.</summary>
internal enum GizmoAxis
{
    None,

    /// <summary>Uniform scale handle in the middle of the scale gizmo.</summary>
    Uniform,
    X,
    Y,
    Z,
}

/// <summary>
/// The viewport transform gizmo: hit testing, dragging and drawing for translate, rotate and scale.
/// </summary>
/// <remarks>
/// Drawn as an ImGui overlay on top of the viewport image rather than as 3D geometry, so the
/// handles keep a constant, clickable thickness and are never hidden by scene geometry. The maths
/// is still fully 3D: handles are projected with the camera matrix and drags are solved by
/// intersecting the mouse ray with the handle's axis or plane.
/// </remarks>
internal sealed class TransformGizmo
{
    private const float AxisPickRadius = 8f;
    private const float RingPickRadius = 8f;
    private const float UniformPickRadius = 10f;
    private const int RingSegments = 64;

    /// <summary>Rotation snapping increment (15 degrees) when snapping is on.</summary>
    private const float RotationSnap = MathF.PI / 12f;

    private static readonly GizmoAxis[] Handles = [GizmoAxis.X, GizmoAxis.Y, GizmoAxis.Z];

    private static readonly Vector4 HighlightColor = new(1f, 0.87f, 0.28f, 1f);
    private static readonly Vector4 UniformColor = new(0.92f, 0.92f, 0.94f, 1f);

    // Frame state, refreshed by Update.
    private Camera? _camera;
    private Vector2 _imageMin;
    private Vector2 _imageSize;

    // Drag state, captured when a handle grab starts.
    private Vector3 _dragOrigin;
    private float _dragWorldLength;
    private float _dragStartParameter;
    private Vector3 _dragStartScale;
    private float _dragStartScreenRadius;
    private Quaternion _dragStartWorldRotation;
    private Quaternion _dragParentRotation;
    private Vector3 _dragStartRingVector;

    // Blender-style modal transform (G / R / S): no button held, mouse follows until confirm/cancel.
    private bool _modalRequested;
    private GizmoMode _requestedMode;
    private bool _modal;
    private Entity _modalEntity;
    private GizmoMode _modalMode;
    private GizmoAxis _modalAxis;
    private Vector2 _modalStartMouse;
    private Vector3 _modalStartWorldPosition;
    private Vector3 _modalStartScale;
    private Quaternion _modalStartWorldRotation;
    private Quaternion _modalParentRotation;
    private Vector3 _modalRight;
    private Vector3 _modalUp;
    private Vector3 _modalForward;
    private float _modalWorldPerPixel;

    public GizmoMode Mode { get; set; } = GizmoMode.Translate;

    /// <summary>Quantise drags to whole steps (0.25 units, 15 degrees, 0.1 scale).</summary>
    public bool Snap { get; set; }

    /// <summary>Whether the handles are drawn and clickable. Modal G/R/S transforms ignore it.</summary>
    public bool ShowHandles { get; set; } = true;

    public GizmoAxis HoveredAxis { get; private set; }

    public GizmoAxis ActiveAxis { get; private set; }

    /// <summary>True while a G/R/S transform is running, i.e. the mouse is driving the transform.</summary>
    public bool IsModal => _modal;

    /// <summary>Axis the running modal transform is constrained to, if any.</summary>
    public GizmoAxis ModalAxis => _modalAxis;

    /// <summary>
    /// Queues a modal transform on the current selection. It starts on the next
    /// <see cref="Update"/> so the frame state (camera, viewport) is current.
    /// </summary>
    public void RequestModal(GizmoMode mode)
    {
        if (_modal)
            return;

        _modalRequested = true;
        _requestedMode = mode;
        Mode = mode;
    }

    /// <summary>
    /// Resolves hover, drags the selected entity and draws the gizmo. Call right after the viewport
    /// image has been submitted; pass a null selection to hide the gizmo.
    /// </summary>
    public void Update(World world, Entity? selection, Camera? camera, Vector2 imageMin, Vector2 imageSize)
    {
        HoveredAxis = GizmoAxis.None;

        _camera = camera;
        _imageMin = imageMin;
        _imageSize = imageSize;

        if (camera is null ||
            selection is not { } entity ||
            !world.IsAlive(entity) ||
            !world.TryGetComponent<Transform>(entity, out var transform))
        {
            ActiveAxis = GizmoAxis.None;
            _modal = false;
            _modalRequested = false;
            return;
        }

        if (_modalRequested)
        {
            _modalRequested = false;

            if (!_modal && world.IsAlive(entity))
                BeginModal(world, entity, transform, camera);
        }

        if (_modal)
        {
            if (!world.IsAlive(_modalEntity) || !world.TryGetComponent<Transform>(_modalEntity, out var modalTransform))
            {
                _modal = false;
                return;
            }

            UpdateModal(world, camera);

            if (world.TryGetComponent<Transform>(_modalEntity, out var movedTransform))
                modalTransform = movedTransform;

            if (ShowHandles)
            {
                var modalOrigin = WorldPosition(modalTransform, world);
                Draw(ImGui.GetWindowDrawList(), modalOrigin, GizmoScale(camera, modalOrigin));
            }

            return;
        }

        var origin = WorldPosition(transform, world);
        var scale = GizmoScale(camera, origin);

        var mouse = ImGui.GetMousePos();
        var overViewport = mouse.X >= imageMin.X && mouse.X <= imageMin.X + imageSize.X &&
                           mouse.Y >= imageMin.Y && mouse.Y <= imageMin.Y + imageSize.Y;

        if (ShowHandles && ActiveAxis == GizmoAxis.None && overViewport)
        {
            HoveredAxis = FindHandleUnderCursor(origin, scale, mouse);

            if (HoveredAxis != GizmoAxis.None)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    BeginDrag(world, entity, transform, origin, scale);
            }
        }

        if (ActiveAxis != GizmoAxis.None)
        {
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                ActiveAxis = GizmoAxis.None;
            }
            else
            {
                ApplyDrag(world, entity);
                HoveredAxis = ActiveAxis;

                if (world.TryGetComponent<Transform>(entity, out var moved))
                    origin = WorldPosition(moved, world);
            }
        }

        if (ShowHandles)
            Draw(ImGui.GetWindowDrawList(), origin, scale);
    }

    /// <summary>Captures everything the modal transform needs to stay stable for its whole run.</summary>
    private void BeginModal(World world, Entity entity, in Transform transform, Camera camera)
    {
        _modal = true;
        _modalEntity = entity;
        _modalMode = Mode;
        _modalAxis = GizmoAxis.None;
        _modalStartMouse = ImGui.GetMousePos();

        _modalStartWorldPosition = WorldPosition(transform, world);
        _modalStartScale = transform.Scale;
        _modalStartWorldRotation = RotationOf(transform.GetWorldMatrix(world));
        _modalParentRotation = ParentRotation(transform, world);

        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var right = Vector3.Cross(forward, camera.Up);

        _modalForward = forward;
        _modalRight = right.LengthSquared() > 1e-6f ? Vector3.Normalize(right) : Vector3.UnitX;
        _modalUp = Vector3.Cross(_modalRight, forward);

        // Screen-to-world scale: depth of the object, or the view distance when the selection is the
        // camera itself (a camera sits exactly at its own origin, which would scale drags to nothing).
        var depth = Vector3.Distance(camera.Position, _modalStartWorldPosition);

        if (depth < 0.1f)
            depth = Vector3.Distance(camera.Position, camera.Target);

        if (depth < 0.1f)
            depth = 1f;

        _modalWorldPerPixel = ViewportCamera.WorldPerPixel(depth, camera.FieldOfView, _imageSize.Y);
    }

    /// <summary>
    /// One frame of the mouse-driven transform: move follows the cursor in the view plane, rotate
    /// and scale follow horizontal mouse movement. X/Y/Z constrain the axis, LMB confirms and RMB
    /// or Escape cancels.
    /// </summary>
    private void UpdateModal(World world, Camera camera)
    {
        if (ImGui.IsKeyPressed(ImGuiKey.Escape) || ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            CancelModal(world);
            return;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _modal = false;
            _modalAxis = GizmoAxis.None;
            ActiveAxis = GizmoAxis.None;
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.X) || ImGui.IsKeyPressed(ImGuiKey.Y) || ImGui.IsKeyPressed(ImGuiKey.Z))
        {
            var pressed = ImGui.IsKeyPressed(ImGuiKey.X)
                ? GizmoAxis.X
                : ImGui.IsKeyPressed(ImGuiKey.Y) ? GizmoAxis.Y : GizmoAxis.Z;

            _modalAxis = _modalAxis == pressed ? GizmoAxis.None : pressed;
        }

        ActiveAxis = _modalAxis;

        var mouse = ImGui.GetMousePos();
        var delta = mouse - _modalStartMouse;

        switch (_modalMode)
        {
            case GizmoMode.Rotate:
                ApplyModalRotate(world, delta);
                break;

            case GizmoMode.Scale:
                ApplyModalScale(world, delta);
                break;

            default:
                ApplyModalTranslate(world, delta);
                break;
        }
    }

    private void ApplyModalTranslate(World world, Vector2 delta)
    {
        var offset = _modalRight * (delta.X * _modalWorldPerPixel) - _modalUp * (delta.Y * _modalWorldPerPixel);

        if (_modalAxis != GizmoAxis.None)
        {
            var axis = AxisVector(_modalAxis);
            offset = axis * Vector3.Dot(offset, axis);
        }

        if (Snap)
            offset = RoundToStep(offset, 0.25f);

        SetWorldPosition(world, _modalEntity, _modalStartWorldPosition + offset);
    }

    private void ApplyModalRotate(World world, Vector2 delta)
    {
        // Blender rotates around the view axis unless an axis is picked.
        var axis = _modalAxis != GizmoAxis.None ? AxisVector(_modalAxis) : _modalForward;

        var angle = delta.X * 0.01f;

        if (Snap)
            angle = MathF.Round(angle / RotationSnap) * RotationSnap;

        SetWorldRotation(world, _modalEntity, _modalStartWorldRotation,
            Matrix4x4.CreateFromAxisAngle(axis, angle));
    }

    private void ApplyModalScale(World world, Vector2 delta)
    {
        var factor = MathF.Max(0.02f, 1f + delta.X * 0.006f);

        if (Snap)
            factor = MathF.Max(0.1f, MathF.Round(factor * 10f) / 10f);

        var scale = _modalStartScale;

        switch (_modalAxis)
        {
            case GizmoAxis.X: scale.X *= factor; break;
            case GizmoAxis.Y: scale.Y *= factor; break;
            case GizmoAxis.Z: scale.Z *= factor; break;
            default: scale *= factor; break;
        }

        ref var transform = ref world.GetComponent<Transform>(_modalEntity);
        transform.Scale = scale;
    }

    private void CancelModal(World world)
    {
        if (world.IsAlive(_modalEntity) && world.HasComponent<Transform>(_modalEntity))
        {
            SetWorldPosition(world, _modalEntity, _modalStartWorldPosition);

            ref var transform = ref world.GetComponent<Transform>(_modalEntity);
            transform.Scale = _modalStartScale;
            transform.Rotation = LocalRotation(_modalStartWorldRotation, _modalParentRotation);
        }

        _modal = false;
        _modalAxis = GizmoAxis.None;
        ActiveAxis = GizmoAxis.None;
    }

    private static Vector3 RoundToStep(Vector3 value, float step) =>
        new(MathF.Round(value.X / step) * step,
            MathF.Round(value.Y / step) * step,
            MathF.Round(value.Z / step) * step);

    /// <summary>Applies a world-space rotation on top of a stored orientation, in the parent's space.</summary>
    private void SetWorldRotation(World world, Entity entity, Quaternion startWorldRotation, Matrix4x4 delta)
    {
        var worldRotation = Matrix4x4.CreateFromQuaternion(startWorldRotation) * delta;

        ref var transform = ref world.GetComponent<Transform>(entity);
        transform.Rotation = LocalRotation(worldRotation, _modalParentRotation);
    }

    private static Quaternion LocalRotation(Matrix4x4 worldRotation, Quaternion parentRotation) =>
        LocalRotation(Quaternion.CreateFromRotationMatrix(worldRotation), parentRotation);

    private static Quaternion LocalRotation(Quaternion worldRotation, Quaternion parentRotation)
    {
        if (!Matrix4x4.Invert(Matrix4x4.CreateFromQuaternion(parentRotation), out var inverseParent))
            return Quaternion.Normalize(worldRotation);

        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(
            Matrix4x4.CreateFromQuaternion(worldRotation) * inverseParent));
    }

    private static Vector3 WorldPosition(in Transform transform, World world)
    {
        var matrix = transform.GetWorldMatrix(world);
        return new Vector3(matrix.M41, matrix.M42, matrix.M43);
    }

    /// <summary>Gizmo size in world units, chosen so it stays roughly constant on screen.</summary>
    private static float GizmoScale(Camera camera, Vector3 origin) =>
        MathF.Max(0.08f, Vector3.Distance(camera.Position, origin) * 0.11f);

    public static Vector3 AxisVector(GizmoAxis axis) => axis switch
    {
        GizmoAxis.X => Vector3.UnitX,
        GizmoAxis.Y => Vector3.UnitY,
        GizmoAxis.Z => Vector3.UnitZ,
        _ => Vector3.Zero,
    };

    public static Vector4 AxisColor(GizmoAxis axis) => axis switch
    {
        GizmoAxis.X => new Vector4(LineGeometry.XAxisColor, 1f),
        GizmoAxis.Y => new Vector4(LineGeometry.YAxisColor, 1f),
        GizmoAxis.Z => new Vector4(LineGeometry.ZAxisColor, 1f),
        _ => UniformColor,
    };

    private void BeginDrag(World world, Entity entity, in Transform transform, Vector3 origin, float scale)
    {
        ActiveAxis = HoveredAxis;
        _dragOrigin = origin;
        _dragWorldLength = scale;

        switch (Mode)
        {
            case GizmoMode.Rotate:
                _dragStartWorldRotation = RotationOf(transform.GetWorldMatrix(world));
                _dragParentRotation = ParentRotation(transform, world);

                if (!TryPlaneVector(AxisVector(ActiveAxis), origin, out var ringVector))
                    ringVector = Vector3.UnitX;

                _dragStartRingVector = ringVector;
                break;

            case GizmoMode.Scale:
                _dragStartScale = transform.Scale;
                _dragStartParameter = ActiveAxis == GizmoAxis.Uniform
                    ? 0f
                    : AxisParameter(origin, AxisVector(ActiveAxis));
                _dragStartScreenRadius = 0f;

                if (TryProjectToScreen(origin, out var centre))
                    _dragStartScreenRadius = MathF.Max(4f, Vector2.Distance(ImGui.GetMousePos(), centre));

                break;

            default:
                _dragStartParameter = AxisParameter(origin, AxisVector(ActiveAxis));
                break;
        }
    }

    private void ApplyDrag(World world, Entity entity)
    {
        switch (Mode)
        {
            case GizmoMode.Rotate:
                ApplyRotate(world, entity);
                break;

            case GizmoMode.Scale:
                ApplyScale(world, entity);
                break;

            default:
                ApplyTranslate(world, entity);
                break;
        }
    }

    private void ApplyTranslate(World world, Entity entity)
    {
        var axis = AxisVector(ActiveAxis);
        var delta = AxisParameter(_dragOrigin, axis) - _dragStartParameter;

        if (Snap)
            delta = MathF.Round(delta * 4f) / 4f;

        SetWorldPosition(world, entity, _dragOrigin + axis * delta);
    }

    private void ApplyRotate(World world, Entity entity)
    {
        var axis = AxisVector(ActiveAxis);

        if (!TryPlaneVector(axis, _dragOrigin, out var current))
            return;

        var angle = MathF.Atan2(
            Vector3.Dot(axis, Vector3.Cross(_dragStartRingVector, current)),
            Vector3.Dot(_dragStartRingVector, current));

        if (Snap)
            angle = MathF.Round(angle / RotationSnap) * RotationSnap;

        var worldRotation = Matrix4x4.CreateFromQuaternion(_dragStartWorldRotation) *
                            Matrix4x4.CreateFromAxisAngle(axis, angle);

        var local = worldRotation;

        if (Matrix4x4.Invert(Matrix4x4.CreateFromQuaternion(_dragParentRotation), out var inverseParent))
            local = worldRotation * inverseParent;

        ref var transform = ref world.GetComponent<Transform>(entity);
        transform.Rotation = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(local));
    }

    private void ApplyScale(World world, Entity entity)
    {
        float factor;

        if (ActiveAxis == GizmoAxis.Uniform)
        {
            factor = 1f;

            if (TryProjectToScreen(_dragOrigin, out var centre))
                factor = Vector2.Distance(ImGui.GetMousePos(), centre) / _dragStartScreenRadius;
        }
        else
        {
            var parameter = AxisParameter(_dragOrigin, AxisVector(ActiveAxis));
            factor = 1f + (parameter - _dragStartParameter) / MathF.Max(1e-3f, _dragWorldLength);
        }

        factor = MathF.Max(0.02f, factor);

        if (Snap)
            factor = MathF.Max(0.1f, MathF.Round(factor * 10f) / 10f);

        var scale = _dragStartScale;

        switch (ActiveAxis)
        {
            case GizmoAxis.X: scale.X *= factor; break;
            case GizmoAxis.Y: scale.Y *= factor; break;
            case GizmoAxis.Z: scale.Z *= factor; break;
            default: scale *= factor; break;
        }

        ref var transform = ref world.GetComponent<Transform>(entity);
        transform.Scale = scale;
    }

    /// <summary>Moves an entity to a world position, converting into its parent's space if needed.</summary>
    private static void SetWorldPosition(World world, Entity entity, Vector3 worldPosition)
    {
        ref var transform = ref world.GetComponent<Transform>(entity);
        var parent = transform.Parent;

        if (parent.IsValid &&
            world.TryGetComponent<Transform>(parent, out var parentTransform) &&
            Matrix4x4.Invert(parentTransform.GetWorldMatrix(world), out var inverseParent))
        {
            transform.Position = Vector3.Transform(worldPosition, inverseParent);
            return;
        }

        transform.Position = worldPosition;
    }

    private static Quaternion RotationOf(in Matrix4x4 matrix) =>
        Matrix4x4.Decompose(matrix, out _, out var rotation, out _) ? rotation : Quaternion.Identity;

    private static Quaternion ParentRotation(in Transform transform, World world)
    {
        if (!transform.Parent.IsValid || !world.TryGetComponent<Transform>(transform.Parent, out var parent))
            return Quaternion.Identity;

        return RotationOf(parent.GetWorldMatrix(world));
    }

    private GizmoAxis FindHandleUnderCursor(Vector3 origin, float scale, Vector2 mouse)
    {
        var best = GizmoAxis.None;
        var bestDistance = float.MaxValue;

        if (Mode == GizmoMode.Scale && TryProjectToScreen(origin, out var centre))
        {
            var distance = Vector2.Distance(mouse, centre);
            if (distance <= UniformPickRadius)
                return GizmoAxis.Uniform;
        }

        foreach (var axis in Handles)
        {
            var distance = Mode == GizmoMode.Rotate
                ? DistanceToRing(axis, origin, scale, mouse)
                : DistanceToAxis(axis, origin, scale, mouse);

            if (distance > (Mode == GizmoMode.Rotate ? RingPickRadius : AxisPickRadius) ||
                distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = axis;
        }

        return best;
    }

    private float DistanceToAxis(GizmoAxis axis, Vector3 origin, float scale, Vector2 mouse)
    {
        if (!TryProjectToScreen(origin, out var start) ||
            !TryProjectToScreen(origin + AxisVector(axis) * scale, out var end))
        {
            return float.MaxValue;
        }

        return DistanceToSegment(mouse, start, end);
    }

    private float DistanceToRing(GizmoAxis axis, Vector3 origin, float scale, Vector2 mouse)
    {
        BuildRing(axis, origin, scale, out var ring);
        var best = float.MaxValue;

        for (var i = 0; i < ring.Length - 1; i++)
        {
            if (!TryProjectToScreen(ring[i], out var a) || !TryProjectToScreen(ring[i + 1], out var b))
                continue;

            best = MathF.Min(best, DistanceToSegment(mouse, a, b));
        }

        return best;
    }

    /// <summary>World-space points around a circle perpendicular to <paramref name="axis"/>.</summary>
    private static void BuildRing(GizmoAxis axis, Vector3 origin, float scale, out Vector3[] points)
    {
        var normal = AxisVector(axis);

        // Pick a stable pair of tangents for the axis.
        var reference = MathF.Abs(normal.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
        var u = Vector3.Normalize(Vector3.Cross(normal, reference));
        var v = Vector3.Cross(normal, u);

        points = new Vector3[RingSegments + 1];

        for (var i = 0; i <= RingSegments; i++)
        {
            var angle = i / (float)RingSegments * MathF.Tau;
            points[i] = origin + (MathF.Cos(angle) * u + MathF.Sin(angle) * v) * scale;
        }
    }

    private void Draw(ImDrawListPtr drawList, Vector3 origin, float scale)
    {
        foreach (var axis in Handles)
        {
            var active = ActiveAxis == axis || HoveredAxis == axis;
            var color = active ? HighlightColor : AxisColor(axis);
            var packed = ImGui.ColorConvertFloat4ToU32(color);

            if (Mode == GizmoMode.Rotate)
            {
                DrawRing(drawList, axis, origin, scale, packed, active ? 3.5f : 2.5f, AxisVector(axis));
                continue;
            }

            if (!TryProjectToScreen(origin, out var start) ||
                !TryProjectToScreen(origin + AxisVector(axis) * scale, out var end))
            {
                continue;
            }

            var segment = end - start;
            var length = segment.Length();

            if (length < 8f)
                continue;

            var direction = segment / length;
            var perpendicular = new Vector2(-direction.Y, direction.X);
            var thickness = active ? 4f : 3f;

            if (Mode == GizmoMode.Scale)
            {
                // Square handle at the tip.
                drawList.AddLine(start, end, packed, thickness);
                var half = active ? 6f : 5f;
                drawList.AddRectFilled(end - new Vector2(half), end + new Vector2(half), packed, 1.5f);
            }
            else
            {
                var headLength = MathF.Min(15f, length * 0.35f);
                var shaftEnd = end - direction * headLength;

                drawList.AddLine(start, shaftEnd, packed, thickness);
                drawList.AddTriangleFilled(end, shaftEnd + perpendicular * 6f, shaftEnd - perpendicular * 6f, packed);
            }

            var label = axis.ToString();
            var labelSize = ImGui.CalcTextSize(label);
            drawList.AddText(end + direction * 8f - new Vector2(labelSize.X * 0.5f, labelSize.Y * 0.5f), packed, label);
        }

        DrawCentre(drawList, origin);
    }

    private void DrawRing(ImDrawListPtr drawList, GizmoAxis axis, Vector3 origin, float scale, uint color,
        float thickness, Vector3 normal)
    {
        BuildRing(axis, origin, scale, out var ring);

        for (var i = 0; i < ring.Length - 1; i++)
        {
            if (!TryProjectToScreen(ring[i], out var a) || !TryProjectToScreen(ring[i + 1], out var b))
                continue;

            drawList.AddLine(a, b, color, thickness);
        }

        // Put the axis letter on the ring so the three circles can be told apart.
        var reference = MathF.Abs(normal.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
        var u = Vector3.Normalize(Vector3.Cross(normal, reference));

        if (TryProjectToScreen(origin + u * scale, out var labelPosition))
        {
            var label = axis.ToString();
            var size = ImGui.CalcTextSize(label);
            drawList.AddText(labelPosition - size * 0.5f, color, label);
        }
    }

    private void DrawCentre(ImDrawListPtr drawList, Vector3 origin)
    {
        if (!TryProjectToScreen(origin, out var centre))
            return;

        if (Mode == GizmoMode.Scale)
        {
            var active = ActiveAxis == GizmoAxis.Uniform || HoveredAxis == GizmoAxis.Uniform;
            var color = ImGui.ColorConvertFloat4ToU32(active ? HighlightColor : UniformColor);
            var half = 5f;
            drawList.AddRectFilled(centre - new Vector2(half), centre + new Vector2(half), color, 1.5f);
            return;
        }

        drawList.AddCircleFilled(centre, 4f, ImGui.ColorConvertFloat4ToU32(UniformColor), 12);
    }

    private bool TryProjectToScreen(Vector3 world, out Vector2 screen)
    {
        screen = default;

        if (_camera is null)
            return false;

        var clip = Vector4.Transform(new Vector4(world, 1f), _camera.GetViewProjectionMatrix());

        if (clip.W <= 1e-5f)
            return false;

        var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;

        screen = new Vector2(
            _imageMin.X + (ndc.X + 1f) * 0.5f * _imageSize.X,
            _imageMin.Y + (ndc.Y + 1f) * 0.5f * _imageSize.Y);

        return true;
    }

    private bool TryRay(out Ray ray)
    {
        ray = default;

        if (_camera is null || _imageSize.X <= 0f || _imageSize.Y <= 0f)
            return false;

        var mouse = ImGui.GetMousePos();

        // Screen top is -1 because the projection carries the Vulkan Y flip.
        var ndc = new Vector2(
            (mouse.X - _imageMin.X) / _imageSize.X * 2f - 1f,
            (mouse.Y - _imageMin.Y) / _imageSize.Y * 2f - 1f);

        ray = _camera.ScreenPointToRay(ndc);
        return true;
    }

    /// <summary>Parameter along <paramref name="axis"/> of the point closest to the mouse ray.</summary>
    private float AxisParameter(Vector3 origin, Vector3 axis)
    {
        if (axis == Vector3.Zero || !TryRay(out var ray))
            return 0f;

        var offset = ray.Origin - origin;
        var alignment = Vector3.Dot(ray.Direction, axis);
        var denominator = 1f - alignment * alignment;

        // Looking straight down the axis leaves the closest point undefined.
        if (MathF.Abs(denominator) < 1e-4f)
            return 0f;

        return (Vector3.Dot(axis, offset) - alignment * Vector3.Dot(ray.Direction, offset)) / denominator;
    }

    /// <summary>Unit vector from <paramref name="origin"/> to where the mouse ray crosses the plane.</summary>
    private bool TryPlaneVector(Vector3 normal, Vector3 origin, out Vector3 direction)
    {
        direction = default;

        if (!TryRay(out var ray))
            return false;

        var denominator = Vector3.Dot(normal, ray.Direction);

        if (MathF.Abs(denominator) < 1e-4f)
            return false;

        var distance = Vector3.Dot(normal, origin - ray.Origin) / denominator;

        if (distance <= 0f)
            return false;

        var offset = ray.Origin + ray.Direction * distance - origin;

        if (offset.LengthSquared() < 1e-8f)
            return false;

        direction = Vector3.Normalize(offset);
        return true;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();

        if (lengthSquared < 1e-6f)
            return Vector2.Distance(point, start);

        var t = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
        return Vector2.Distance(point, start + segment * t);
    }
}
