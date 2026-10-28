using System.Numerics;
using EidolonCore.ECS;
using EidolonCore.Nexus.Animation;
using EidolonCore.Simulation;

namespace Eidolon.Tests;

public class AnimationTests
{
    private const float Dt = 0.5f;

    private static AnimationTrack Track(string path, params PoseKey[] keys) => new(path, keys);

    private static AnimationClip MoveClip(float duration, string path = "Rotor") =>
        new("Move", duration,
        [
            Track(path,
                new PoseKey(0f, LocalPose.Identity),
                new PoseKey(duration, new LocalPose(new Vector3(0f, 1f, 0f), Quaternion.Identity, Vector3.One))),
        ]);

    private static (World World, Entity Root, Entity Rotor) BuildHierarchy()
    {
        var world = new World();

        var root = world.CreateEntity();
        world.SetComponent(root, new Transform(Vector3.Zero));
        world.SetComponent(root, new Name("Root"));

        var rotor = world.CreateEntity();
        world.SetComponent(rotor, new Transform(new Vector3(0f, 1f, 0f)));
        world.SetComponent(rotor, new Name("Rotor"));
        Hierarchy.TrySetParent(world, rotor, root);

        return (world, root, rotor);
    }

    private static FrameTime Step(int frame) => new(Dt, frame * Dt, frame);

    [Fact]
    public void AKeyHoldsItsPoseWithNoOthers()
    {
        var track = Track("Rotor", new PoseKey(1f, new LocalPose(new Vector3(2f, 3f, 4f), Quaternion.Identity, Vector3.One)));

        Assert.True(track.TrySample(0f, out var pose));
        VectorAssert.Near(new Vector3(2f, 3f, 4f), pose.Position);

        Assert.True(track.TrySample(9f, out pose));
        VectorAssert.Near(new Vector3(2f, 3f, 4f), pose.Position);
    }

    [Fact]
    public void KeysAreSortedSoAuthoredOrderDoesNotMatter()
    {
        var track = Track("Rotor",
            new PoseKey(1f, new LocalPose(new Vector3(0f, 1f, 0f), Quaternion.Identity, Vector3.One)),
            new PoseKey(0f, LocalPose.Identity));

        Assert.True(track.TrySample(0.5f, out var pose));
        VectorAssert.Near(new Vector3(0f, 0.5f, 0f), pose.Position);
    }

    [Fact]
    public void SamplingInterpolatesPositionAndScale()
    {
        var track = Track("Rotor",
            new PoseKey(0f, new LocalPose(Vector3.Zero, Quaternion.Identity, Vector3.One)),
            new PoseKey(2f, new LocalPose(new Vector3(4f, 0f, 0f), Quaternion.Identity, new Vector3(3f))));

        Assert.True(track.TrySample(1f, out var pose));
        VectorAssert.Near(new Vector3(2f, 0f, 0f), pose.Position);
        VectorAssert.Near(new Vector3(2f), pose.Scale);
    }

    [Fact]
    public void SamplingBlendsRotationAlongTheShortestArc()
    {
        // 0 to 270 degrees: the short way round is backwards through -90, so half way is -45.
        var track = Track("Rotor",
            new PoseKey(0f, LocalPose.Identity),
            new PoseKey(1f, new LocalPose(Vector3.Zero,
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI * 1.5f), Vector3.One)));

        Assert.True(track.TrySample(0.5f, out var pose));

        var expected = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -MathF.PI * 0.25f);
        var dot = MathF.Abs(Quaternion.Dot(Quaternion.Normalize(pose.Rotation), expected));

        Assert.True(dot > 0.999f, $"expected a -45 degree turn, got {pose.Rotation}");
    }

    [Fact]
    public void SamplingHoldsTheEndsInsteadOfExtrapolating()
    {
        var track = Track("Rotor",
            new PoseKey(1f, new LocalPose(new Vector3(1f, 0f, 0f), Quaternion.Identity, Vector3.One)),
            new PoseKey(2f, new LocalPose(new Vector3(2f, 0f, 0f), Quaternion.Identity, Vector3.One)));

        Assert.True(track.TrySample(0f, out var before));
        VectorAssert.Near(new Vector3(1f, 0f, 0f), before.Position);

        Assert.True(track.TrySample(5f, out var after));
        VectorAssert.Near(new Vector3(2f, 0f, 0f), after.Position);
    }

    [Fact]
    public void ClipsAndTracksRejectDegenerateData()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnimationClip("Empty", 0f, []));
        Assert.Throws<ArgumentException>(() => new AnimationTrack("Rotor", []));
        Assert.Throws<ArgumentNullException>(() =>
            new AnimationTrack(null!, [new PoseKey(0f, LocalPose.Identity)]));
    }

    [Fact]
    public void AnimatingPublishesDesiredPoseWithoutTouchingTheTransform()
    {
        var (world, root, rotor) = BuildHierarchy();
        world.SetComponent(root, new AnimationPlayer { Clip = MoveClip(2f) });

        var system = new AnimationSystem();
        system.Update(world, Step(1));
        system.Update(world, Step(2));

        // One second into a two second move: half way up, and still only *desired*.
        Assert.True(world.HasComponent<DesiredPose>(rotor));
        var pose = world.GetComponent<DesiredPose>(rotor).Pose;
        VectorAssert.Near(new Vector3(0f, 0.5f, 0f), pose.Position, 1e-4f);

        VectorAssert.Near(new Vector3(0f, 1f, 0f), world.GetComponent<Transform>(rotor).Position);
        Assert.Equal(1f, world.GetComponent<AnimationPlayer>(root).Time, 5);

        new PoseResolutionSystem().Update(world, Step(3));

        VectorAssert.Near(new Vector3(0f, 0.5f, 0f), world.GetComponent<Transform>(rotor).Position, 1e-4f);
    }

    [Fact]
    public void APausedPlayerKeepsItsPoseButDoesNotAdvance()
    {
        var (world, root, rotor) = BuildHierarchy();
        var player = new AnimationPlayer { Clip = MoveClip(2f) };
        world.SetComponent(root, player);

        var system = new AnimationSystem();
        system.Update(world, Step(1));

        player.Playing = false;
        system.Update(world, Step(2));

        Assert.Equal(Dt, player.Time, 5);
        Assert.True(world.HasComponent<DesiredPose>(rotor), "a paused player should still publish its pose");
    }

    [Fact]
    public void LoopingWrapsThePlayhead()
    {
        var (world, root, rotor) = BuildHierarchy();
        world.SetComponent(root, new AnimationPlayer { Clip = MoveClip(1f), Loop = true });

        var system = new AnimationSystem();
        system.Update(world, Step(1));
        system.Update(world, Step(2));
        system.Update(world, Step(3));

        var player = world.GetComponent<AnimationPlayer>(root);

        Assert.Equal(1.5f, player.Time, 5);
        Assert.True(player.Playing);

        // One and a half seconds into a one second clip samples the half second pose, not the end pose.
        VectorAssert.Near(new Vector3(0f, 0.5f, 0f), world.GetComponent<DesiredPose>(rotor).Pose.Position, 1e-4f);
    }

    [Fact]
    public void AOneShotStopsOnItsLastPose()
    {
        var (world, root, rotor) = BuildHierarchy();
        world.SetComponent(root, new AnimationPlayer { Clip = MoveClip(1f), Loop = false });

        var system = new AnimationSystem();

        for (var frame = 1; frame <= 3; frame++)
            system.Update(world, Step(frame));

        var player = world.GetComponent<AnimationPlayer>(root);

        Assert.Equal(1f, player.Time, 5);
        Assert.False(player.Playing);
        VectorAssert.Near(new Vector3(0f, 1f, 0f), world.GetComponent<DesiredPose>(rotor).Pose.Position, 1e-4f);
    }

    [Fact]
    public void NestedPathsBindDescendants()
    {
        var (world, root, rotor) = BuildHierarchy();

        var blade = world.CreateEntity();
        world.SetComponent(blade, new Transform(new Vector3(0f, 1f, 0f)));
        world.SetComponent(blade, new Name("Blade"));
        Hierarchy.TrySetParent(world, blade, rotor);

        world.SetComponent(root, new AnimationPlayer { Clip = MoveClip(2f, "Rotor/Blade") });

        var system = new AnimationSystem();
        system.Update(world, Step(1));

        Assert.True(world.HasComponent<DesiredPose>(blade));
        Assert.False(world.HasComponent<DesiredPose>(rotor));
        Assert.Equal(1, system.BindingCount);
    }

    [Fact]
    public void AnEmptyPathAnimatesThePlayerEntityItself()
    {
        var (world, root, rotor) = BuildHierarchy();
        world.SetComponent(root, new AnimationPlayer { Clip = MoveClip(2f, string.Empty) });

        var system = new AnimationSystem();
        system.Update(world, Step(1));

        Assert.True(world.HasComponent<DesiredPose>(root));
        Assert.False(world.HasComponent<DesiredPose>(rotor));
    }

    [Fact]
    public void UnknownPathsAreSkippedWithoutFailing()
    {
        var (world, root, rotor) = BuildHierarchy();
        world.SetComponent(root, new AnimationPlayer { Clip = MoveClip(2f, "Rotor/Missing") });

        var system = new AnimationSystem();
        system.Update(world, Step(1));

        Assert.Equal(0, system.PlayerCount);
        Assert.Equal(0, system.BindingCount);
        Assert.False(world.HasComponent<DesiredPose>(rotor));
    }

    [Fact]
    public void PlayModeClonesRebindToTheNewWorld()
    {
        var (world, root, rotor) = BuildHierarchy();
        world.SetComponent(root, new AnimationPlayer { Clip = MoveClip(2f) });

        var system = new AnimationSystem();
        system.Update(world, Step(1));

        var clone = SceneCloner.Clone(world);
        system.Update(clone, Step(2));

        Assert.True(clone.HasComponent<DesiredPose>(rotor));
        Assert.Equal(1, system.PlayerCount);
    }

    [Fact]
    public void PlayModeClonesGetTheirOwnPlayhead()
    {
        var (world, root, _) = BuildHierarchy();
        var player = new AnimationPlayer { Clip = MoveClip(2f) };
        world.SetComponent(root, player);

        var clone = SceneCloner.Clone(world);
        var copy = clone.GetComponent<AnimationPlayer>(root);

        Assert.NotSame(player, copy);
        Assert.Same(player.Clip, copy.Clip);
    }

    [Fact]
    public void KeyingAddsSortsAndReplacesWithoutMutatingTheOldClip()
    {
        var clip = MoveClip(2f);
        var keyed = AnimationEditing.Key(clip, "Rotor", 1f, new LocalPose(new Vector3(0f, 3f, 0f), Quaternion.Identity, Vector3.One));

        Assert.NotSame(clip, keyed);
        Assert.Equal(2, clip.Tracks[0].Keys.Count);
        Assert.Equal(3, keyed.Tracks[0].Keys.Count);

        // Sorted by time, so the new middle key sits between the ends.
        Assert.Equal(1f, keyed.Tracks[0].Keys[1].Time, 5);

        // Keying on top of an existing key overwrites it instead of stacking another.
        var replaced = AnimationEditing.Key(keyed, "Rotor", 1f, LocalPose.Identity);
        Assert.Equal(3, replaced.Tracks[0].Keys.Count);
        VectorAssert.Near(Vector3.Zero, replaced.Tracks[0].Keys[1].Pose.Position);
    }

    [Fact]
    public void KeyingANewPathCreatesItsTrack()
    {
        var clip = MoveClip(2f);
        var keyed = AnimationEditing.Key(clip, "Rotor/Blade", 0.5f, LocalPose.Identity);

        Assert.Equal(2, keyed.Tracks.Count);
        Assert.Contains(keyed.Tracks, track => track.Path == "Rotor/Blade");
    }

    [Fact]
    public void KeyTimesAreClampedIntoTheClip()
    {
        var clip = MoveClip(2f);

        var past = AnimationEditing.Key(clip, "Rotor", 9f, LocalPose.Identity);
        Assert.Equal(2f, past.Tracks[0].Keys[^1].Time, 5);

        var before = AnimationEditing.Key(clip, "Rotor", -3f, LocalPose.Identity);
        Assert.Equal(0f, before.Tracks[0].Keys[0].Time, 5);
    }

    [Fact]
    public void RemovingTheLastKeyRemovesItsTrack()
    {
        var clip = AnimationEditing.Key(MoveClip(2f), "Extra", 1f, LocalPose.Identity);
        Assert.Equal(2, clip.Tracks.Count);

        var trimmed = AnimationEditing.RemoveKey(clip, "Extra", 1f);

        Assert.Single(trimmed.Tracks);
        Assert.DoesNotContain(trimmed.Tracks, track => track.Path == "Extra");
    }

    [Fact]
    public void PreviewPosesBoundBonesWithoutRunningTheSimulation()
    {
        var (world, root, rotor) = BuildHierarchy();
        var clip = MoveClip(2f);

        var posed = AnimationPreview.Apply(world, root, clip, 1f);

        Assert.Equal(1, posed);
        VectorAssert.Near(new Vector3(0f, 0.5f, 0f), world.GetComponent<DesiredPose>(rotor).Pose.Position, 1e-4f);
        VectorAssert.Near(new Vector3(0f, 1f, 0f), world.GetComponent<Transform>(rotor).Position);

        new PoseResolutionSystem().Update(world, Step(1));
        VectorAssert.Near(new Vector3(0f, 0.5f, 0f), world.GetComponent<Transform>(rotor).Position, 1e-4f);
    }

    [Fact]
    public void BindingsSkipUnknownPathsAndMapTheEmptyPathToTheOwner()
    {
        var (world, root, rotor) = BuildHierarchy();

        var clip = new AnimationClip("Mixed", 1f,
        [
            Track(string.Empty, new PoseKey(0f, LocalPose.Identity)),
            Track("Rotor", new PoseKey(0f, LocalPose.Identity)),
            Track("Missing", new PoseKey(0f, LocalPose.Identity)),
        ]);

        var bindings = AnimationBindings.Resolve(world, root, clip);

        Assert.Equal(2, bindings.Count);
        Assert.Contains(bindings, binding => binding.Entity == root);
        Assert.Contains(bindings, binding => binding.Entity == rotor);
    }
}
