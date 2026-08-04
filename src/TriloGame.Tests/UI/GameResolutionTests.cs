using TriloGame.Game.UI.Settings;

namespace TriloGame.Tests.UI;

public sealed class GameResolutionTests
{
    [Fact]
    public void GetSelectable_KeepsOnlyResolutionsThatFitTheDesktop()
    {
        var selectable = GameResolutions.GetSelectable(1920, 1080);

        Assert.All(selectable, resolution =>
        {
            Assert.True(resolution.Width <= 1920);
            Assert.True(resolution.Height <= 1080);
        });
        Assert.Contains(new GameResolution(1920, 1080), selectable);
        Assert.DoesNotContain(new GameResolution(2560, 1440), selectable);
    }

    // A window larger than the desktop puts its title bar off-screen, and the title bar is now the
    // only way to move or close the window in windowed mode.
    [Fact]
    public void GetSelectable_NeverOffersAResolutionTallerThanTheDesktop()
    {
        // A 1920-wide but short desktop: width alone must not qualify 1920x1080.
        var selectable = GameResolutions.GetSelectable(1920, 900);

        Assert.DoesNotContain(new GameResolution(1920, 1080), selectable);
        Assert.Contains(new GameResolution(1600, 900), selectable);
    }

    [Fact]
    public void GetSelectable_StillOffersSomethingOnATinyDesktop()
    {
        var selectable = GameResolutions.GetSelectable(800, 600);

        Assert.NotEmpty(selectable);
    }

    [Fact]
    public void Step_MovesOneEntryAtATime()
    {
        var resolutions = GameResolutions.GetSelectable(2560, 1440);

        var up = GameResolutions.Step(resolutions, new GameResolution(1440, 900), 1);
        var down = GameResolutions.Step(resolutions, new GameResolution(1440, 900), -1);

        Assert.Equal(new GameResolution(1600, 900), up);
        Assert.Equal(new GameResolution(1366, 768), down);
    }

    // Clamped rather than wrapped: stepping past the largest should not drop the player onto the
    // smallest, which on a big monitor is a jarring change to undo.
    [Fact]
    public void Step_ClampsAtBothEndsInsteadOfWrapping()
    {
        var resolutions = GameResolutions.GetSelectable(2560, 1440);
        var smallest = resolutions[0];
        var largest = resolutions[^1];

        Assert.Equal(smallest, GameResolutions.Step(resolutions, smallest, -1));
        Assert.Equal(largest, GameResolutions.Step(resolutions, largest, 1));
    }

    [Fact]
    public void CanStep_ReportsFalseOnlyAtTheEnds()
    {
        var resolutions = GameResolutions.GetSelectable(2560, 1440);
        var smallest = resolutions[0];
        var largest = resolutions[^1];

        Assert.False(GameResolutions.CanStep(resolutions, smallest, -1));
        Assert.True(GameResolutions.CanStep(resolutions, smallest, 1));
        Assert.True(GameResolutions.CanStep(resolutions, largest, -1));
        Assert.False(GameResolutions.CanStep(resolutions, largest, 1));
    }

    // Dragging the window edge produces a size that is not a preset. Stepping from there must still
    // do something visible in the direction asked for.
    [Fact]
    public void Step_FromADraggedCustomSizeLandsOnTheNeighbourInThatDirection()
    {
        var resolutions = GameResolutions.GetSelectable(2560, 1440);
        var custom = new GameResolution(1500, 950);

        var up = GameResolutions.Step(resolutions, custom, 1);
        var down = GameResolutions.Step(resolutions, custom, -1);

        Assert.True((long)up.Width * up.Height > (long)custom.Width * custom.Height, $"stepping up gave {up.Label}");
        Assert.True((long)down.Width * down.Height < (long)custom.Width * custom.Height, $"stepping down gave {down.Label}");
    }

    [Fact]
    public void Step_FromACustomSizeAlwaysChangesTheResolution()
    {
        var resolutions = GameResolutions.GetSelectable(2560, 1440);
        var custom = new GameResolution(1500, 950);

        Assert.NotEqual(custom, GameResolutions.Step(resolutions, custom, 1));
        Assert.NotEqual(custom, GameResolutions.Step(resolutions, custom, -1));
    }

    [Fact]
    public void GetNearestIndex_FindsAnExactPresetAndTheClosestCustomSize()
    {
        var resolutions = GameResolutions.GetSelectable(2560, 1440);

        var exact = GameResolutions.GetNearestIndex(resolutions, new GameResolution(1920, 1080));
        var nearby = GameResolutions.GetNearestIndex(resolutions, new GameResolution(1918, 1078));

        Assert.Equal(new GameResolution(1920, 1080), resolutions[exact]);
        Assert.Equal(new GameResolution(1920, 1080), resolutions[nearby]);
    }

    [Fact]
    public void PresetsAreOrderedSmallestFirst()
    {
        var presets = GameResolutions.Presets;

        for (var index = 1; index < presets.Count; index++)
        {
            var previous = (long)presets[index - 1].Width * presets[index - 1].Height;
            var current = (long)presets[index].Width * presets[index].Height;
            Assert.True(current > previous, $"{presets[index].Label} is not larger than {presets[index - 1].Label}");
        }
    }

    [Fact]
    public void DefaultIsAPresetThatFitsACommonDesktop()
    {
        Assert.Contains(GameResolutions.Default, GameResolutions.Presets);
        Assert.Contains(GameResolutions.Default, GameResolutions.GetSelectable(1920, 1080));
    }
}
