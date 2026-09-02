using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Debug;

namespace TriloGame.Tests.UI;

public sealed class WorldDebugInspectorTests
{
    [Fact]
    public void Inspect_ReportsContainingCreatureHitbox()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 12, new GridPoint(4, 4));
        var creature = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 4), "Inspector");

        var result = WorldDebugInspector.Inspect(cave, creature.Position, showHitboxes: true);

        Assert.Same(creature, result.Creature);
        Assert.Contains("Radius", result.Tooltip);
        Assert.Contains("Diameter", result.Tooltip);
    }

    [Fact]
    public void Inspect_ReturnsEmptyWhenHitboxesAreDisabled()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 12, new GridPoint(4, 4));
        var creature = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 4), "Inspector");

        var result = WorldDebugInspector.Inspect(cave, creature.Position, showHitboxes: false);

        Assert.False(result.HasValue);
    }
}
