using System.Reflection;
using TriloGame.Game;

namespace TriloGame.Tests.UI;

public sealed class GameAppInputTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void IsCameraControlDragBlocked_AllowsBuildPlacementDrags(
        bool dragging,
        bool buildPlacementDragActive,
        bool expectedBlocked)
    {
        var isBlocked = typeof(GameApp).GetMethod(
            "IsCameraControlDragBlocked",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(isBlocked);
        Assert.Equal(expectedBlocked, (bool)isBlocked!.Invoke(null, [dragging, buildPlacementDragActive])!);
    }

    [Theory]
    [InlineData(false, true, "wall", true, false)]
    [InlineData(false, false, "empty", true, false)]
    [InlineData(true, true, "wall", true, true)]
    [InlineData(true, false, "empty", false, true)]
    [InlineData(true, false, "empty", true, false)]
    public void IsManualMiningTileSelectable_RequiresToggleAndValidTileState(
        bool allowManualMining,
        bool hasOpal,
        string baseType,
        bool isRevealed,
        bool expectedSelectable)
    {
        var isSelectable = typeof(GameApp).GetMethod(
            "IsManualMiningTileSelectable",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(isSelectable);
        Assert.Equal(expectedSelectable, (bool)isSelectable!.Invoke(null, [allowManualMining, hasOpal, baseType, isRevealed])!);
    }

    [Theory]
    [InlineData(false, "wall", true, true)]
    [InlineData(false, "Malachite", true, true)]
    [InlineData(true, "empty", true, true)]
    [InlineData(false, "empty", true, false)]
    [InlineData(false, "wall", false, false)]
    public void ShouldShowMiningTileHoverLabel_RequiresRevealedMineableOrOpal(
        bool hasOpal,
        string baseType,
        bool isRevealed,
        bool expectedVisible)
    {
        var isVisible = typeof(GameApp).GetMethod(
            "ShouldShowMiningTileHoverLabel",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(isVisible);
        Assert.Equal(expectedVisible, (bool)isVisible!.Invoke(null, [hasOpal, baseType, isRevealed])!);
    }
}
