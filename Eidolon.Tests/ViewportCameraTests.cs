using System.Numerics;
using EidolonCore.Rendering;

namespace Eidolon.Tests;

public class ViewportCameraTests
{
    [Fact]
    public void OrbitKeepsTheDistanceToThePivot()
    {
        var pivot = new Vector3(2f, 1f, -3f);
        var position = pivot + new Vector3(0f, 0f, 5f);

        var moved = ViewportCamera.Orbit(position, pivot, MathF.PI / 2f, 0f);

        Assert.Equal(5f, Vector3.Distance(moved, pivot), 4);
    }

    [Fact]
    public void OrbitYawQuarterTurnMovesAroundThePivot()
    {
        var pivot = Vector3.Zero;
        var position = new Vector3(0f, 0f, 5f);

        var moved = ViewportCamera.Orbit(position, pivot, MathF.PI / 2f, 0f);

        // Dragging right (positive yaw) has to swing the camera to its own left, onto -X, so the
        // scene turns the way the mouse drags it. Pan uses the same grab-the-scene convention.
        VectorAssert.Near(new Vector3(-5f, 0f, 0f), moved);
        VectorAssert.Near(new Vector3(5f, 0f, 0f), ViewportCamera.Orbit(position, pivot, -MathF.PI / 2f, 0f));
    }

    [Fact]
    public void OrbitStillRisesAfterYawing()
    {
        // The pitch axis is derived from the already yawed offset, so a camera that has been yawed
        // around to -X must still be lifted by a positive pitch rather than tilted the other way.
        var yawed = ViewportCamera.Orbit(new Vector3(0f, 0f, 5f), Vector3.Zero, MathF.PI / 2f, 0f);
        var raised = ViewportCamera.Orbit(yawed, Vector3.Zero, 0f, 0.5f);

        Assert.True(raised.Y > 1f, $"expected the camera to rise, got {raised}");
        Assert.Equal(5f, Vector3.Distance(raised, Vector3.Zero), 3);
    }

    [Fact]
    public void OrbitClampsThePitch()
    {
        var pivot = Vector3.Zero;
        var position = new Vector3(5f, 0f, 0f);

        var moved = ViewportCamera.Orbit(position, pivot, 0f, 10f);

        var expectedHeight = 5f * MathF.Sin(ViewportCamera.MaxPitch);
        Assert.Equal(expectedHeight, moved.Y, 3);
    }

    [Fact]
    public void OrbitLeavesThePositionAloneOnThePivot()
    {
        var position = new Vector3(1f, 2f, 3f);

        Assert.Equal(position, ViewportCamera.Orbit(position, position, 1f, 1f));
    }

    [Fact]
    public void PanMovesAlongTheViewPlane()
    {
        var right = Vector3.UnitX;
        var up = Vector3.UnitY;

        var moved = ViewportCamera.Pan(10f, 0f, right, up, 0.5f);
        VectorAssert.Near(new Vector3(-5f, 0f, 0f), moved);

        var raised = ViewportCamera.Pan(0f, 10f, right, up, 0.5f);
        VectorAssert.Near(new Vector3(0f, 5f, 0f), raised);
    }

    [Fact]
    public void WorldPerPixelGrowsWithDistance()
    {
        var near = ViewportCamera.WorldPerPixel(1f, MathF.PI / 4f, 720f);
        var far = ViewportCamera.WorldPerPixel(10f, MathF.PI / 4f, 720f);

        Assert.True(near > 0f);
        Assert.Equal(10f, far / near, 3);
        Assert.Equal(0f, ViewportCamera.WorldPerPixel(5f, MathF.PI / 4f, 0f));
    }

    [Fact]
    public void DollyMovesTowardsOrAwayFromTheTarget()
    {
        var target = Vector3.Zero;
        var position = new Vector3(0f, 0f, 10f);

        var closer = ViewportCamera.Dolly(position, target, 1f);
        Assert.True(Vector3.Distance(closer, target) < Vector3.Distance(position, target));
        Assert.Equal(0f, closer.X, 4);
        Assert.Equal(0f, closer.Y, 4);

        var further = ViewportCamera.Dolly(position, target, -1f);
        Assert.True(Vector3.Distance(further, target) > Vector3.Distance(position, target));
    }
}
