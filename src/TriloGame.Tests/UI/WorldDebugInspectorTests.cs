using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.UI.Debug;

namespace TriloGame.Tests.UI;

public sealed class WorldDebugInspectorTests
{
    [Fact]
    public void Inspect_PrefersContainingHitboxOverZone()
    {
        var (session, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 12, new GridPoint(4, 4));
        var creature = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 4), "Inspector");

        var result = WorldDebugInspector.Inspect(cave, creature.Position, showHitboxes: true, showZones: true);

        Assert.Same(creature, result.Creature);
        Assert.Null(result.Zone);
        Assert.Contains("Radius", result.Tooltip);
        Assert.Contains("Diameter", result.Tooltip);
        Assert.Contains(queen.InteractionZones, zone => zone.Purpose == InteractionZonePurpose.Feeding);
    }

    [Fact]
    public void Inspect_ReportsZonePurposeDimensionsAndCapacity()
    {
        var (_, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 12, new GridPoint(4, 4));
        var zone = Assert.Single(queen.InteractionZones, item => item.Purpose == InteractionZonePurpose.Feeding);

        var result = WorldDebugInspector.Inspect(cave, zone.SlotPositions[0], showHitboxes: false, showZones: true);

        Assert.Same(zone, result.Zone);
        Assert.Contains("Feeding", result.Tooltip);
        Assert.Contains("3 x 1 tiles", result.Tooltip);
        Assert.Contains("0/3", result.Tooltip);
    }
}
