using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.Buildings;

public sealed class Radar : Building
{
    public Radar(GameSession session)
        : base("Radar", new GridPoint(4, 4), [[0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0]], session, false)
    {
        TextureKey = "Radar";
        Recipe = new Dictionary<string, int>(StringComparer.Ordinal) { ["Sandstone"] = 20 };
        RadiusMax = 50;
        CurrentRadius = 0;
        GrowthChance = 0.1;
        Description = $"Reveals tiles in an expanding radius. Has a 1 in 10 chance each tick to grow until radius {RadiusMax}.";
    }

    public int RadiusMax { get; }

    public int CurrentRadius { get; private set; }

    public double GrowthChance { get; }

    public IReadOnlyList<GridPoint> GetCenterLocations()
    {
        if (Location is null)
        {
            return [];
        }

        var centerX = Location.Value.X + 1;
        var centerY = Location.Value.Y + 1;
        return
        [
            new GridPoint(centerX, centerY),
            new GridPoint(centerX + 1, centerY),
            new GridPoint(centerX, centerY + 1),
            new GridPoint(centerX + 1, centerY + 1)
        ];
    }

    public int RevealUnlockedArea(World.Cave cave, int previousRadius = -1)
    {
        return cave.RevealTilesBetweenRadii(GetCenterLocations(), previousRadius, CurrentRadius);
    }

    public override void OnBuilt(World.Cave cave)
    {
        cave.RevealTiles(TileArray);
        RevealUnlockedArea(cave, -1);
    }

    public override int Tick(World.Cave cave)
    {
        if (CurrentRadius >= RadiusMax || RandomUtil.NextDouble() >= GrowthChance)
        {
            return 0;
        }

        var previous = CurrentRadius;
        CurrentRadius++;
        return RevealUnlockedArea(cave, previous);
    }
}
