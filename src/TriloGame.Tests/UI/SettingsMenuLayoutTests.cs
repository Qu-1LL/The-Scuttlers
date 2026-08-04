using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Tests.UI;

public sealed class SettingsMenuLayoutTests
{
    private static SettingsPanelLayout Build(int width = 1440, int height = 900, bool includeQuit = true)
    {
        return SettingsMenuLayout.BuildPanel(new Point(width, height), includeQuit);
    }

    [Fact]
    public void Panel_IsCenteredInViewport()
    {
        var viewport = new Point(1440, 900);

        var panel = SettingsMenuLayout.BuildPanel(viewport).Panel;

        Assert.Equal((viewport.X - panel.Width) / 2, panel.X);
        Assert.Equal((viewport.Y - panel.Height) / 2, panel.Y);
    }

    // The panel's height comes from its rows now, so dropping a row must shrink it - that is the
    // property the old hand-maintained height constant could not guarantee.
    [Fact]
    public void Panel_WithoutQuitToMainMenu_IsShorterByExactlyThatRow()
    {
        var withQuit = Build(includeQuit: true);
        var withoutQuit = Build(includeQuit: false);

        Assert.True(withQuit.Panel.Height > withoutQuit.Panel.Height);
        Assert.Equal(Rectangle.Empty, withoutQuit.ReturnToMainMenu);
        Assert.NotEqual(Rectangle.Empty, withQuit.ReturnToMainMenu);
    }

    [Fact]
    public void GetSnappedVolumeFromBar_SnapsToFivePercentIncrements()
    {
        var barBounds = new Rectangle(80, 100, 200, 18);

        Assert.Equal(0, SettingsMenuLayout.GetSnappedVolumeFromBar(barBounds, barBounds.Left));
        Assert.Equal(25, SettingsMenuLayout.GetSnappedVolumeFromBar(barBounds, barBounds.Left + 49));
        Assert.Equal(65, SettingsMenuLayout.GetSnappedVolumeFromBar(barBounds, barBounds.Left + 129));
        Assert.Equal(100, SettingsMenuLayout.GetSnappedVolumeFromBar(barBounds, barBounds.Right));
    }

    // The whole reason for the stack: rows are laid out in order and cannot overlap. Asserting the
    // order end to end is now one test rather than a pairwise check per row added.
    [Fact]
    public void Rows_AreOrderedTopToBottomAndNeverOverlap()
    {
        var layout = Build();
        var rows = new (string Name, Rectangle Bounds)[]
        {
            ("title", layout.Title),
            ("volume value", layout.VolumeValue),
            ("volume bar", layout.VolumeBar),
            ("music", layout.MusicToggle),
            ("display label", layout.DisplayModeLabel),
            ("display buttons", layout.Fullscreen),
            ("resolution label", layout.ResolutionLabel),
            ("resolution arrows", layout.ResolutionDown),
            ("trilodex", layout.Trilodex),
            ("return", layout.ReturnToMainMenu),
            ("hint", layout.DismissHint)
        };

        for (var index = 1; index < rows.Length; index++)
        {
            var previous = rows[index - 1];
            var current = rows[index];
            Assert.True(
                previous.Bounds.Bottom <= current.Bounds.Y,
                $"'{previous.Name}' (bottom {previous.Bounds.Bottom}) overlaps '{current.Name}' (top {current.Bounds.Y})");
        }
    }

    [Fact]
    public void EveryControl_StaysInsideThePanel()
    {
        foreach (var includeQuit in new[] { true, false })
        {
            var layout = Build(includeQuit: includeQuit);
            var controls = new (string Name, Rectangle Bounds)[]
            {
                ("close", layout.Close),
                ("title", layout.Title),
                ("volume value", layout.VolumeValue),
                ("volume down", layout.VolumeDown),
                ("volume bar", layout.VolumeBar),
                ("volume up", layout.VolumeUp),
                ("music", layout.MusicToggle),
                ("music checkbox", layout.MusicCheckbox),
                ("display label", layout.DisplayModeLabel),
                ("fullscreen", layout.Fullscreen),
                ("windowed", layout.Windowed),
                ("resolution label", layout.ResolutionLabel),
                ("resolution down", layout.ResolutionDown),
                ("resolution value", layout.ResolutionValue),
                ("resolution up", layout.ResolutionUp),
                ("trilodex", layout.Trilodex),
                ("back", layout.Back),
                ("hint", layout.DismissHint)
            };

            foreach (var (name, bounds) in controls)
            {
                Assert.True(
                    layout.Panel.Contains(bounds),
                    $"'{name}' escapes the panel (includeQuit: {includeQuit})");
            }
        }
    }

    [Fact]
    public void MusicCheckbox_SitsInsideItsRow()
    {
        var layout = Build();

        Assert.True(layout.MusicToggle.Contains(layout.MusicCheckbox));
    }

    // The stepper rows derive their middle from their two ends, so the three can never collide
    // however narrow the panel gets.
    [Fact]
    public void StepperRows_KeepTheirEndsAndValueApartAtEveryPanelWidth()
    {
        // The panel width is clamped to 320..420, so these viewports cover both ends and the middle.
        foreach (var viewportWidth in new[] { 360, 500, 900, 1440, 2560 })
        {
            var layout = Build(viewportWidth);

            Assert.True(layout.ResolutionDown.Right < layout.ResolutionValue.Left, $"width {viewportWidth}");
            Assert.True(layout.ResolutionValue.Right < layout.ResolutionUp.Left, $"width {viewportWidth}");
            Assert.True(layout.ResolutionValue.Width > 0, $"value collapsed at width {viewportWidth}");
            Assert.True(layout.VolumeDown.Right < layout.VolumeBar.Left, $"width {viewportWidth}");
            Assert.True(layout.VolumeBar.Right < layout.VolumeUp.Left, $"width {viewportWidth}");
        }
    }

    [Fact]
    public void DisplayModeButtons_SplitTheirRowEvenlyWithoutOverlapping()
    {
        var layout = Build();

        Assert.Equal(layout.Fullscreen.Y, layout.Windowed.Y);
        Assert.Equal(layout.Fullscreen.Height, layout.Windowed.Height);
        Assert.Equal(layout.Fullscreen.Width, layout.Windowed.Width);
        Assert.True(layout.Fullscreen.Right < layout.Windowed.Left);
    }

    // The panel is centred, so growing it must not push it off the top of a short window.
    [Fact]
    public void Panel_FitsTheSmallestOfferedResolution()
    {
        var smallest = GameResolutions.Presets[0];
        var layout = SettingsMenuLayout.BuildPanel(new Point(smallest.Width, smallest.Height), includeQuitToMainMenu: true);

        Assert.True(layout.Panel.Y >= 0, $"panel starts above the window at {smallest.Label}");
        Assert.True(layout.Panel.Bottom <= smallest.Height, $"panel runs past the bottom at {smallest.Label}");
    }
}
