using Microsoft.Xna.Framework;
using TriloGame.Game.Rendering;

namespace TriloGame.Tests.Rendering;

public sealed class CameraControllerTests
{
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
}
