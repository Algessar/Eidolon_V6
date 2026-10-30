using System.Numerics;
using EidolonCore.ECS;
using EidolonCore.Nexus.Animation;
using EidolonCore.Simulation;
using ImGuiNET;

namespace Eidolon.Editor;

/// <summary>
/// The animation timeline: pick a player, scrub its clip, and key poses onto tracks.
/// </summary>
/// <remarks>
/// Playback normally belongs to the simulation, which only ticks in play mode, so this panel advances the
/// playhead itself while the editor is stopped. Previewing resolves desired poses into the authored
/// transforms, exactly as play mode would; that is what makes editing visible, and it means the authored
/// pose stays wherever the playhead last left it.
/// </remarks>
internal sealed class TimelinePanel
{
    private const float RowHeight = 20f;
    private const float RulerHeight = 24f;
    private const float KeyRadius = 4f;

    /// <summary>Minimum pixel gap between drawn keys; dense rows would overlap into a smear anyway.</summary>
    private const float KeySpacing = 4f;
    private const float PremadeClipSeconds = 2f;

    private readonly PoseResolutionSystem _resolver = new();

    /// <summary>What was previewed last, so a still playhead does not keep overwriting edits.</summary>
    private float _previewedTime = float.NaN;

    private Entity? _previewedPlayer;

    public void Draw(World world, SceneSession session, Entity? selection)
    {
        ImGui.SetNextWindowSize(new Vector2(940f, 300f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Timeline");

        if (!FindPlayer(world, selection, out var owner, out var player))
        {
            DrawNoPlayerState(world, selection);
            ImGui.End();
            return;
        }

        var clip = player.Clip;

        if (clip is null)
        {
            ImGui.TextDisabled($"{Describe(world, owner)} has an AnimationPlayer but no clip.");

            if (!ImGui.Button("Create clip"))
            {
                ImGui.End();
                return;
            }

            clip = NewClip(world, owner);
            player.Clip = clip;
            _previewedTime = float.NaN;
        }

        DrawControls(world, session, owner, player, clip);
        DrawTracks(world, owner, player, clip);

        ImGui.End();
    }

    /// <summary>Offers to start animating the selection when there is no player anywhere near it.</summary>
    private static void DrawNoPlayerState(World world, Entity? selection)
    {
        ImGui.TextDisabled("No AnimationPlayer on the selection or its parents.");

        if (selection is not { } selected || !world.IsAlive(selected) || !world.HasComponent<Transform>(selected))
            return;

        if (ImGui.Button("Add Animation Player"))
            world.SetComponent(selected, new AnimationPlayer());
    }

    private static bool FindPlayer(World world, Entity? selection, out Entity owner, out AnimationPlayer player)
    {
        owner = default;
        player = null!;

        // The nearest player up the chain owns the clip that drives this entity.
        var current = selection ?? default;

        for (var depth = 0; current.IsValid && depth < Transform.MaxHierarchyDepth; depth++)
        {
            if (world.TryGetComponent<AnimationPlayer>(current, out player))
            {
                owner = current;
                return true;
            }

            if (!world.TryGetComponent<Transform>(current, out var transform))
                break;

            current = transform.Parent;
        }

        // With nothing selected, the scene's first player is a reasonable default.
        foreach (var candidate in world.GetEntitiesWith<AnimationPlayer>())
        {
            owner = candidate;
            player = world.GetComponent<AnimationPlayer>(candidate);
            return true;
        }

        return false;
    }

    private void DrawControls(World world, SceneSession session, Entity owner, AnimationPlayer player, AnimationClip clip)
    {
        if (ImGui.Button(player.Playing ? "Pause" : "Play"))
            player.Playing = !player.Playing;

        ImGui.SameLine();

        var loop = player.Loop;

        if (ImGui.Checkbox("Loop", ref loop))
            player.Loop = loop;

        ImGui.SameLine();
        ImGui.SetNextItemWidth(90f);

        var speed = player.Speed;

        if (ImGui.DragFloat("Speed", ref speed, 0.01f, -4f, 4f))
            player.Speed = speed;

        ImGui.SameLine();
        ImGui.TextDisabled($"{clip.Name}  {clip.Duration:0.00}s, {clip.Tracks.Count} track(s)  at {player.Time:0.000}s");

        if (ImGui.Button("Key at playhead"))
            KeyTracks(world, owner, player, clip);

        ImGui.SameLine();

        if (ImGui.Button("Add track"))
            ImGui.OpenPopup("##addTrack");

        if (ImGui.BeginPopup("##addTrack"))
        {
            if (ImGui.MenuItem("(this entity)"))
                AddTrack(world, owner, player, clip, owner);

            DrawBoneMenu(world, owner, player, clip, owner, owner);
            ImGui.EndPopup();
        }

        ImGui.SetNextItemWidth(-1f);
        var time = player.Time;

        if (ImGui.SliderFloat("##playhead", ref time, 0f, clip.Duration, "%.3f s"))
            player.Time = time;

        // Editing is live: advance and preview here, because the simulation only ticks in play mode.
        if (!session.InPlayMode && player.Playing && clip.Duration > 0f)
            player.Time = Wrap(player.Time + ImGui.GetIO().DeltaTime * player.Speed, clip.Duration, player.Loop);

        Preview(world, owner, player, clip);
    }

    /// <summary>Lists named descendants, which are the paths a clip can bind.</summary>
    private void DrawBoneMenu(World world, Entity owner, AnimationPlayer player, AnimationClip clip, Entity parent,
        Entity boneOfPath)
    {
        foreach (var child in Hierarchy.GetChildren(world, parent).ToArray())
        {
            if (world.TryGetComponent<Name>(child, out _) && PathOf(world, owner, child) is { } path &&
                ImGui.MenuItem(path))
            {
                AddTrack(world, owner, player, clip, child);
            }

            DrawBoneMenu(world, owner, player, clip, child, boneOfPath);
        }
    }

    /// <summary>The clip path of a bone, or null when some ancestor on the way up has no name.</summary>
    private static string? PathOf(World world, Entity owner, Entity bone)
    {
        if (bone == owner)
            return string.Empty;

        var segments = new List<string>();

        for (var current = bone; current.IsValid;)
        {
            if (!world.TryGetComponent<Name>(current, out var currentName) ||
                !world.TryGetComponent<Transform>(current, out var transform))
            {
                return null;
            }

            segments.Insert(0, currentName.Value);

            if (transform.Parent == owner)
                return string.Join('/', segments);

            current = transform.Parent;
        }

        return null;
    }

    private void DrawTracks(World world, Entity owner, AnimationPlayer player, AnimationClip clip)
    {
        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var rows = MathF.Max(1f, clip.Tracks.Count);

        ImGui.Dummy(new Vector2(width, RulerHeight + RowHeight * rows));

        var origin = new Vector2(start.X, start.Y + RulerHeight);
        var scale = width / clip.Duration;

        DrawRuler(drawList, start, width, clip.Duration);

        // ImDrawList geometry is emitted whether or not it lands inside the window, and ImGui indexes
        // vertices with 16 bits, so a rig with hundreds of tracks of keys would overflow one draw list.
        // Only what is actually on screen gets drawn.
        var windowMin = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var visibleMinY = windowMin.Y;
        var visibleMaxY = windowMin.Y + windowSize.Y;
        var visibleMinX = windowMin.X;
        var visibleMaxX = windowMin.X + windowSize.X;

        for (var row = 0; row < clip.Tracks.Count; row++)
        {
            var y = origin.Y + row * RowHeight;

            if (y + RowHeight < visibleMinY || y > visibleMaxY)
                continue;

            var track = clip.Tracks[row];

            ImGui.SetCursorScreenPos(new Vector2(start.X + 6f, y + 2f));
            ImGui.TextUnformatted(track.Path.Length == 0 ? "(this entity)" : track.Path);

            // Imported rigs key every bone every frame: hundreds of keys per row, far more than a pixel
            // apart. Draw at most one diamond per few pixels, and always the one under the mouse.
            var lastDrawnX = float.NegativeInfinity;
            var mouse = ImGui.GetMousePos();

            foreach (var key in track.Keys)
            {
                var centre = new Vector2(origin.X + key.Time * scale, y + RowHeight * 0.5f);

                if (centre.X < visibleMinX || centre.X > visibleMaxX)
                    continue;

                var hovered = MathF.Abs(mouse.Y - centre.Y) <= RowHeight * 0.5f &&
                              MathF.Abs(mouse.X - centre.X) <= KeyRadius + 3f;

                if (!hovered && centre.X - lastDrawnX < KeySpacing)
                    continue;

                lastDrawnX = centre.X;

                // A diamond is four vertices; a filled circle is dozens, which is what overflowed.
                drawList.AddQuadFilled(
                    centre + new Vector2(0f, -KeyRadius),
                    centre + new Vector2(KeyRadius, 0f),
                    centre + new Vector2(0f, KeyRadius),
                    centre + new Vector2(-KeyRadius, 0f),
                    ImGui.GetColorU32(hovered ? ImGuiCol.ButtonHovered : ImGuiCol.Text));

                if (!hovered || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    continue;

                player.Time = key.Time;
                Preview(world, owner, player, clip);
            }
        }

        var playheadX = origin.X + player.Time * scale;

        if (playheadX >= visibleMinX && playheadX <= visibleMaxX)
        {
            drawList.AddLine(new Vector2(playheadX, origin.Y - RulerHeight + 6f),
                new Vector2(playheadX, MathF.Min(origin.Y + RowHeight * rows, visibleMaxY)),
                ImGui.GetColorU32(ImGuiCol.PlotLinesHovered));
        }
    }

    private static void DrawRuler(ImDrawListPtr drawList, Vector2 origin, float width, float duration)
    {
        var baseline = origin.Y + RulerHeight;

        drawList.AddLine(new Vector2(origin.X, baseline), new Vector2(origin.X + width, baseline),
            ImGui.GetColorU32(ImGuiCol.Separator));

        for (var second = 0; second <= MathF.Ceiling(duration); second++)
        {
            if (second > duration)
                break;

            var x = origin.X + width * second / duration;

            drawList.AddLine(new Vector2(x, baseline - 6f), new Vector2(x, baseline),
                ImGui.GetColorU32(ImGuiCol.Separator));

            drawList.AddText(new Vector2(x + 3f, origin.Y + 2f),
                ImGui.GetColorU32(ImGuiCol.TextDisabled), $"{second}s");
        }
    }

    /// <summary>Writes the clip into the authored transforms, once per playhead change.</summary>
    private void Preview(World world, Entity owner, AnimationPlayer player, AnimationClip clip)
    {
        if (_previewedPlayer == owner && MathF.Abs(_previewedTime - player.Time) < 1e-5f)
            return;

        _previewedPlayer = owner;
        _previewedTime = player.Time;

        if (AnimationPreview.Apply(world, owner, clip, player.Time) == 0)
            return;

        _resolver.Update(world, default);
    }

    /// <summary>Keys every existing track from the bound bones' current local transforms.</summary>
    private void KeyTracks(World world, Entity owner, AnimationPlayer player, AnimationClip clip)
    {
        var updated = clip;

        foreach (var binding in AnimationBindings.Resolve(world, owner, clip))
        {
            if (!world.TryGetComponent<Transform>(binding.Entity, out var transform))
                continue;

            updated = AnimationEditing.Key(updated, binding.Track.Path, player.Time, LocalPose.From(transform));
        }

        player.Clip = updated;
        _previewedTime = float.NaN;
    }

    /// <summary>Starts a track for a bone, keyed at the playhead with its current pose.</summary>
    private void AddTrack(World world, Entity owner, AnimationPlayer player, AnimationClip clip, Entity bone)
    {
        if (!world.TryGetComponent<Transform>(bone, out var transform))
            return;

        var path = PathOf(world, owner, bone) ?? string.Empty;

        player.Clip = AnimationEditing.Key(clip, path, player.Time, LocalPose.From(transform));
        _previewedTime = float.NaN;
    }

    /// <summary>A starting clip with one key on the player's own transform.</summary>
    private static AnimationClip NewClip(World world, Entity owner)
    {
        var clip = new AnimationClip("Clip", PremadeClipSeconds, []);

        if (!world.TryGetComponent<Transform>(owner, out var transform))
            return clip;

        return AnimationEditing.Key(clip, string.Empty, 0f, LocalPose.From(transform));
    }

    private static string Describe(World world, Entity entity) =>
        world.TryGetComponent<Name>(entity, out var name) ? name.Value : $"#{entity.Id}";

    private static float Wrap(float time, float duration, bool loop)
    {
        if (!loop)
            return System.Math.Clamp(time, 0f, duration);

        var wrapped = time % duration;
        return wrapped < 0f ? wrapped + duration : wrapped;
    }
}
