using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.UI;

public sealed class RoleRadialLayoutTests
{
    [Fact]
    public void GetButtonBounds_ClampsButtonsInsideGameplayBounds()
    {
        var gameplayBounds = new Rectangle(0, 0, 920, 900);

        var bounds = RoleRadialLayout.GetButtonBounds(new Vector2(18f, 18f), -MathF.PI / 2f, gameplayBounds);

        Assert.True(bounds.Left >= gameplayBounds.Left + RoleRadialLayout.DefaultMargin);
        Assert.True(bounds.Top >= gameplayBounds.Top + RoleRadialLayout.DefaultMargin);
        Assert.True(bounds.Right <= gameplayBounds.Right - RoleRadialLayout.DefaultMargin);
        Assert.True(bounds.Bottom <= gameplayBounds.Bottom - RoleRadialLayout.DefaultMargin);
    }

    [Fact]
    public void GetLabelBounds_ShrinksAndClampsLargeLabelsToGameplayBounds()
    {
        var gameplayBounds = new Rectangle(0, 0, 220, 120);

        var bounds = RoleRadialLayout.GetLabelBounds(new Vector2(16f, 16f), new Point(320, 28), gameplayBounds);

        Assert.True(bounds.Width <= gameplayBounds.Width - (RoleRadialLayout.DefaultMargin * 2));
        Assert.True(bounds.Left >= gameplayBounds.Left + RoleRadialLayout.DefaultMargin);
        Assert.True(bounds.Top >= gameplayBounds.Top + RoleRadialLayout.DefaultMargin);
    }

    [Fact]
    public void GetLabelBounds_PlacesNameClearOfUpperRoleButtons()
    {
        var gameplayBounds = new Rectangle(0, 0, 920, 900);
        var radialCenter = new Vector2(460f, 450f);

        var labelBounds = RoleRadialLayout.GetLabelBounds(radialCenter, new Point(180, 20), gameplayBounds);
        var upperRight = RoleRadialLayout.GetButtonBounds(radialCenter, (-MathF.PI / 2f) + (MathF.Tau / 5f), gameplayBounds);
        var upperLeft = RoleRadialLayout.GetButtonBounds(radialCenter, (-MathF.PI / 2f) + ((MathF.Tau / 5f) * 4f), gameplayBounds);

        Assert.False(labelBounds.Intersects(upperRight));
        Assert.False(labelBounds.Intersects(upperLeft));
        Assert.True(labelBounds.Top > upperRight.Bottom);
    }
}
