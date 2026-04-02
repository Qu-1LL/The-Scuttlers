using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Selection;

namespace TriloGame.Tests.UI;

public sealed class SelectionFocusLayoutTests
{
    [Fact]
    public void GetGameplayBounds_ReservesOpenMenuWidthOnRight()
    {
        var bounds = SelectionFocusLayout.GetGameplayBounds(new Point(1440, 900), 520f);

        Assert.Equal(new Rectangle(0, 0, 920, 900), bounds);
    }

    [Fact]
    public void FocusHintChecks_UseGameplayCenterAndBounds()
    {
        var viewport = new Point(1440, 900);
        const float openMenuWidth = 520f;

        Assert.True(SelectionFocusLayout.IsNearGameplayCenter(new Vector2(460f, 450f), viewport, openMenuWidth));
        Assert.False(SelectionFocusLayout.IsInsideGameplayBounds(new Vector2(1110f, 450f), viewport, openMenuWidth));
    }
}
