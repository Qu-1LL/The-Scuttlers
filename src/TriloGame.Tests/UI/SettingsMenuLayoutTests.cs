using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Tests.UI;

public sealed class SettingsMenuLayoutTests
{
    [Fact]
    public void GetPanelBounds_IsCenteredInViewport()
    {
        var viewport = new Point(1440, 900);

        var panelBounds = SettingsMenuLayout.GetPanelBounds(viewport);

        Assert.Equal((viewport.X - panelBounds.Width) / 2, panelBounds.X);
        Assert.Equal((viewport.Y - panelBounds.Height) / 2, panelBounds.Y);
    }

    [Fact]
    public void GetPanelBounds_WithoutQuitToMainMenu_IsShorter()
    {
        var viewport = new Point(1440, 900);

        var panelWithQuit = SettingsMenuLayout.GetPanelBounds(viewport, includeQuitToMainMenu: true);
        var panelWithoutQuit = SettingsMenuLayout.GetPanelBounds(viewport, includeQuitToMainMenu: false);

        Assert.True(panelWithQuit.Height > panelWithoutQuit.Height);
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
}
