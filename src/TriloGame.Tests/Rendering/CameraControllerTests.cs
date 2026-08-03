using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Rendering;

namespace TriloGame.Tests.Rendering;

public sealed class CameraControllerTests
{
    // The wheel used to multiply the live scale by 4/3 or 0.75 and clamp the product. Neither half
    // of that survives a round trip: the products drift off any fixed set of levels, and once the
    // clamp bites, a step in the opposite direction starts from the clamped value rather than from
    // the level you were on. Zooming out and back in therefore never returned to where it started.
    [Fact]
    public void ZoomBySteps_IsExactlyReversible()
    {
        var camera = new CameraController();

        camera.ZoomBySteps(-3);
        Settle(camera);
        camera.ZoomBySteps(3);
        Settle(camera);

        Assert.Equal(0, camera.ZoomStep);
        Assert.Equal(GameConstants.DefaultCameraScale, camera.CurrentScale);
    }

    [Fact]
    public void ZoomBySteps_ClampsOntoTheOutermostRungsRatherThanBetweenThem()
    {
        var zoomedOut = new CameraController();
        var zoomedIn = new CameraController();

        zoomedOut.ZoomBySteps(-40);
        zoomedIn.ZoomBySteps(40);
        Settle(zoomedOut);
        Settle(zoomedIn);

        Assert.Equal(GameConstants.MinScale, zoomedOut.CurrentScale, 6);
        Assert.Equal(GameConstants.MaxScale, zoomedIn.CurrentScale, 6);

        // Still on the ladder at the limit, so the exact number of steps back lands on the default.
        zoomedOut.ZoomBySteps(GameConstants.MaxZoomSteps);
        Settle(zoomedOut);
        Assert.Equal(GameConstants.DefaultCameraScale, zoomedOut.CurrentScale, 6);
    }

    [Fact]
    public void ZoomBySteps_MovesTheTargetImmediatelyAndGlidesTheDrawnScaleOntoIt()
    {
        var camera = new CameraController();

        camera.ZoomBySteps(1);

        // The rung is chosen at once; only the scale the frame is drawn at lags behind. A snap here
        // is what invalidated the lighting's temporal history in a single frame.
        Assert.Equal(GameConstants.DefaultCameraScale * GameConstants.ZoomStepRatio, camera.TargetScale, 6);
        Assert.Equal(GameConstants.DefaultCameraScale, camera.CurrentScale);
        Assert.True(camera.IsZooming);

        camera.Update(Frame());
        Assert.InRange(camera.CurrentScale, GameConstants.DefaultCameraScale, camera.TargetScale);

        Settle(camera);
        Assert.Equal(camera.TargetScale, camera.CurrentScale);
        Assert.False(camera.IsZooming);
    }

    [Fact]
    public void UpdateZoom_ApproachesTheTargetMonotonicallyWithoutOvershooting()
    {
        var camera = new CameraController();
        camera.ZoomBySteps(-2);
        var previous = camera.CurrentScale;

        for (var frame = 0; frame < 40; frame++)
        {
            camera.Update(Frame());
            Assert.True(camera.CurrentScale <= previous, $"scale rose while zooming out on frame {frame}");
            Assert.True(camera.CurrentScale >= camera.TargetScale, $"overshot the target on frame {frame}");
            previous = camera.CurrentScale;
        }

        Assert.Equal(camera.TargetScale, camera.CurrentScale);
    }

    [Fact]
    public void ResetZoom_ReturnsToTheDefaultRungWithNoGlideLeftOver()
    {
        var camera = new CameraController();
        camera.ZoomBySteps(4);

        camera.ResetZoom();

        Assert.Equal(0, camera.ZoomStep);
        Assert.Equal(GameConstants.DefaultCameraScale, camera.CurrentScale);
        Assert.False(camera.IsZooming);
    }

    // A directly assigned scale is off the ladder; the next notch has to continue from the nearest
    // rung to it, not from whatever rung the camera was on before the assignment.
    [Fact]
    public void CurrentScale_AssignmentRejoinsTheLadderAtTheNearestRung()
    {
        var camera = new CameraController();
        camera.ZoomBySteps(-4);
        Settle(camera);

        camera.CurrentScale = GameConstants.DefaultCameraScale * 1.05f;
        Assert.Equal(0, camera.ZoomStep);
        Assert.False(camera.IsZooming);

        camera.ZoomBySteps(1);
        Assert.Equal(GameConstants.DefaultCameraScale * GameConstants.ZoomStepRatio, camera.TargetScale, 6);
    }

    private static GameTime Frame()
    {
        return new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16));
    }

    private static void Settle(CameraController camera)
    {
        for (var frame = 0; frame < 120 && camera.IsZooming; frame++)
        {
            camera.Update(Frame());
        }
    }

    [Fact]
    public void TriggerExplosionShake_UpdateProducesTransientOffsetThatDecaysAway()
    {
        var camera = new CameraController();
        camera.SetViewport(1440, 900);

        camera.TriggerExplosionShake();
        camera.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)));

        Assert.True(camera.ShakeTrauma > 0f);
        Assert.NotEqual(Vector2.Zero, camera.ShakeOffset);

        for (var index = 0; index < 120; index++)
        {
            camera.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)));
        }

        Assert.Equal(0f, camera.ShakeTrauma);
        Assert.Equal(Vector2.Zero, camera.ShakeOffset);
    }

    [Fact]
    public void WorldToScreen_AndScreenToWorldStayAlignedDuringShake()
    {
        var camera = new CameraController();
        camera.SetViewport(1440, 900);
        camera.SetOrigin(new Vector2(120f, 75f));
        camera.CurrentScale = 1.5f;
        camera.TriggerExplosionShake(0.8f);
        camera.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)));

        var world = new Vector2(220f, 180f);
        var screen = camera.WorldToScreen(world);
        var roundTripped = camera.ScreenToWorld(new Point((int)MathF.Round(screen.X), (int)MathF.Round(screen.Y)));

        Assert.InRange(MathF.Abs(roundTripped.X - world.X), 0f, 0.75f);
        Assert.InRange(MathF.Abs(roundTripped.Y - world.Y), 0f, 0.75f);
    }

    [Fact]
    public void GetVisibleWorldBounds_ReturnsTheCurrentCameraFootprint()
    {
        var camera = new CameraController();
        camera.SetViewport(1440, 900);
        camera.SetOrigin(new Vector2(120f, 75f));
        camera.CurrentScale = 1.5f;

        camera.GetVisibleWorldBounds(new Point(1440, 900), out var topLeft, out var bottomRight);

        Assert.Equal(new Vector2(-360f, -225f), topLeft);
        Assert.Equal(new Vector2(600f, 375f), bottomRight);
    }

    [Fact]
    public void ParallaxScreenOffset_DoesNotChangeWhenOnlyScaleChanges()
    {
        var camera = new CameraController();
        camera.SetOrigin(new Vector2(100f, 50f));
        var offsetBeforeZoom = camera.ParallaxScreenOffset;

        camera.CurrentScale = 3f;

        Assert.Equal(offsetBeforeZoom, camera.ParallaxScreenOffset);
    }

    [Fact]
    public void PanByScreenDelta_AddsScreenDeltaToParallaxOffsetIndependentOfScale()
    {
        var lowZoomCamera = new CameraController { CurrentScale = 0.75f };
        var highZoomCamera = new CameraController { CurrentScale = 3f };

        lowZoomCamera.PanByScreenDelta(40f, -12f);
        highZoomCamera.PanByScreenDelta(40f, -12f);

        Assert.Equal(new Vector2(40f, -12f), lowZoomCamera.ParallaxScreenOffset);
        Assert.Equal(lowZoomCamera.ParallaxScreenOffset, highZoomCamera.ParallaxScreenOffset);
    }
}
