using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Settings;

namespace TriloGame.Tests.UI;

public sealed class SettingsMenuLayoutTests
{
    [Fact]
    public void GetPanelBounds_AnchorsBelowTopLeftSettingsButton()
    {
        var viewport = new Point(1440, 900);

        var buttonBounds = SettingsMenuLayout.GetSettingsButtonBounds(viewport);
        var panelBounds = SettingsMenuLayout.GetPanelBounds(viewport);

        Assert.Equal(buttonBounds.X, panelBounds.X);
        Assert.True(panelBounds.Y > buttonBounds.Bottom);
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
