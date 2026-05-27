using Microsoft.Xna.Framework;
using TriloGame.Game.Rendering;

namespace TriloGame.Tests.Rendering;

public sealed class WorldSpriteEffectSystemTests
{
    [Fact]
    public void ApplyColor_LeavesUnregisteredSpritesUnchanged()
    {
        var system = new WorldSpriteEffectSystem();
        var baseColor = new Color(90, 140, 210, 255);

        var result = system.ApplyColor("Unregistered", baseColor);

        Assert.Equal(baseColor, result);
    }

    [Fact]
    public void ApplyColor_PulsesRegisteredSpriteAlphaOverTime()
    {
        var system = new WorldSpriteEffectSystem();
        system.RegisterAlphaPulse("Lumenite", new AlphaPulseEffect(0.68f, 1f, 2.1f));
        var baseColor = Color.White;

        var initial = system.ApplyColor("Lumenite", baseColor);

        system.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(500)));
        var pulsed = system.ApplyColor("Lumenite", baseColor);

        Assert.True(initial.A < pulsed.A, $"Expected pulsed alpha to rise. Initial: {initial.A}, pulsed: {pulsed.A}.");
        Assert.InRange(initial.A, 212, 215);
        Assert.InRange(pulsed.A, 224, 255);
    }

    [Fact]
    public void ApplyColor_UsesPhaseOffsetToDesynchronizeInstances()
    {
        var system = new WorldSpriteEffectSystem();
        system.RegisterAlphaPulse("Lumenite", new AlphaPulseEffect(0.68f, 1f, 2.1f));
        var baseColor = Color.White;

        var first = system.ApplyColor("Lumenite", baseColor, 0f);
        var second = system.ApplyColor("Lumenite", baseColor, 0.6f);

        Assert.NotEqual(first.A, second.A);
    }
}
