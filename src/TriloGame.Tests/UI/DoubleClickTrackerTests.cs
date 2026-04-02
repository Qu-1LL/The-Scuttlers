using TriloGame.Game.Core.Constants;
using TriloGame.Game.UI.Input;

namespace TriloGame.Tests.UI;

public sealed class DoubleClickTrackerTests
{
    [Fact]
    public void TryConsume_ReturnsTrueForMatchingKeyWithinThreshold()
    {
        var tracker = new DoubleClickTracker();
        tracker.Arm("4,7", 100d);

        Assert.True(tracker.TryConsume("4,7", 100d + GameConstants.DoubleClickThresholdMs - 1d, GameConstants.DoubleClickThresholdMs));
        Assert.False(tracker.HasPending);
    }

    [Fact]
    public void TryConsume_ClearsPendingStateForDifferentKey()
    {
        var tracker = new DoubleClickTracker();
        tracker.Arm("4,7", 100d);

        Assert.False(tracker.TryConsume("5,7", 200d, GameConstants.DoubleClickThresholdMs));
        Assert.False(tracker.HasPending);
    }

    [Fact]
    public void Expire_RemovesPendingStateAfterThreshold()
    {
        var tracker = new DoubleClickTracker();
        tracker.Arm("4,7", 100d);

        tracker.Expire(100d + GameConstants.DoubleClickThresholdMs + 1d, GameConstants.DoubleClickThresholdMs);

        Assert.False(tracker.HasPending);
    }
}
