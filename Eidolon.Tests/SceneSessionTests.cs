using EidolonCore.ECS;
using EidolonCore.Simulation;

namespace Eidolon.Tests;

public class SceneSessionTests
{
    [Fact]
    public void ActiveIsTheAuthoredWorldWhileStopped()
    {
        var authored = new World();
        var session = new SceneSession(authored);

        Assert.Same(authored, session.Active);
        Assert.Same(authored, session.Authored);
        Assert.Equal(PlayState.Stopped, session.State);
        Assert.False(session.InPlayMode);
        Assert.Null(session.Runtime);
    }

    [Fact]
    public void EnterPlaySwitchesToTheRuntimeWorld()
    {
        var authored = new World();
        var runtime = new World();
        var session = new SceneSession(authored);

        session.EnterPlay(runtime);

        Assert.Same(runtime, session.Active);
        Assert.Same(runtime, session.Runtime);
        Assert.Same(authored, session.Authored);
        Assert.Equal(PlayState.Playing, session.State);
        Assert.True(session.InPlayMode);
    }

    [Fact]
    public void PauseAndResumeOnlyChangeTheState()
    {
        var session = new SceneSession(new World());
        session.EnterPlay(new World());

        session.Pause();
        Assert.Equal(PlayState.Paused, session.State);
        Assert.True(session.InPlayMode);

        session.Resume();
        Assert.Equal(PlayState.Playing, session.State);
    }

    [Fact]
    public void StopDiscardsTheRuntimeWorld()
    {
        var authored = new World();
        var session = new SceneSession(authored);
        session.EnterPlay(new World());
        session.Pause();

        session.Stop();

        Assert.Same(authored, session.Active);
        Assert.Null(session.Runtime);
        Assert.Equal(PlayState.Stopped, session.State);
        Assert.False(session.InPlayMode);
    }

    [Fact]
    public void EnteringPlayTwiceIsRejected()
    {
        var session = new SceneSession(new World());
        session.EnterPlay(new World());

        Assert.Throws<InvalidOperationException>(() => session.EnterPlay(new World()));
    }

    [Fact]
    public void PauseAndResumeAreNoOpsWhileStopped()
    {
        var session = new SceneSession(new World());

        session.Pause();
        Assert.Equal(PlayState.Stopped, session.State);

        session.Resume();
        Assert.Equal(PlayState.Stopped, session.State);
    }

    [Fact]
    public void StopWhileStoppedStaysStopped()
    {
        var authored = new World();
        var session = new SceneSession(authored);

        session.Stop();

        Assert.Same(authored, session.Active);
        Assert.Equal(PlayState.Stopped, session.State);
    }

    [Fact]
    public void NullWorldsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new SceneSession(null!));

        var session = new SceneSession(new World());
        Assert.Throws<ArgumentNullException>(() => session.EnterPlay(null!));
    }
}
