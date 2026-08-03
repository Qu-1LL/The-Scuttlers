using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.UI.Debug;

public readonly record struct WorldDebugInspection(Creature? Creature, InteractionZone? Zone, string Tooltip)
{
    public bool HasValue => Creature is not null || Zone is not null;
}

public static class WorldDebugInspector
{
    public static WorldDebugInspection Inspect(Cave cave, WorldPoint point, bool showHitboxes, bool showZones)
    {
        if (showHitboxes && FindNearestCreature(cave, point) is { } creature)
        {
            return new WorldDebugInspection(creature, null, FormatCreature(creature));
        }

        if (showZones && FindSmallestZone(cave, point) is { } zone)
        {
            return new WorldDebugInspection(null, zone, FormatZone(zone));
        }

        return default;
    }

    public static Creature? FindNearestCreature(Cave cave, WorldPoint point)
    {
        Creature? best = null;
        var bestDistance = long.MaxValue;
        FindNearest(cave.GetTrilobiteList(), point, ref best, ref bestDistance);
        FindNearest(cave.GetEnemyList(), point, ref best, ref bestDistance);
        return best;
    }

    private static void FindNearest<T>(IReadOnlyList<T> creatures, WorldPoint point, ref Creature? best, ref long bestDistance)
        where T : Creature
    {
        for (var index = 0; index < creatures.Count; index++)
        {
            var creature = creatures[index];
            if (!creature.IsVisible || creature.Cave is null)
            {
                continue;
            }

            var distance = (point - creature.Position).LengthSquared;
            var radius = creature.CollisionRadius + creature.SeparationPadding;
            if (distance > (long)radius * radius ||
                (best is not null && (distance > bestDistance || (distance == bestDistance && creature.Id >= best.Id))))
            {
                continue;
            }

            best = creature;
            bestDistance = distance;
        }
    }

    private static InteractionZone? FindSmallestZone(Cave cave, WorldPoint point)
    {
        InteractionZone? best = null;
        var buildings = cave.GetBuildingList();
        for (var buildingIndex = 0; buildingIndex < buildings.Count; buildingIndex++)
        {
            var zones = buildings[buildingIndex].InteractionZones;
            for (var zoneIndex = 0; zoneIndex < zones.Count; zoneIndex++)
            {
                var zone = zones[zoneIndex];
                if (!zone.WorldBounds.Contains(point) ||
                    (best is not null &&
                     (zone.WorldBounds.Area > best.WorldBounds.Area ||
                      (zone.WorldBounds.Area == best.WorldBounds.Area && zone.Id >= best.Id))))
                {
                    continue;
                }

                best = zone;
            }
        }

        return best;
    }

    private static string FormatCreature(Creature creature)
    {
        var radius = creature.CollisionRadius / WorldUnits.UnitsPerPixel;
        return $"{creature.Name} ({creature.GetType().Name})\nRadius {radius} px | Diameter {radius * 2} px";
    }

    private static string FormatZone(InteractionZone zone)
    {
        var widthPixels = zone.WorldBounds.Width / WorldUnits.UnitsPerPixel;
        var heightPixels = zone.WorldBounds.Height / WorldUnits.UnitsPerPixel;
        var widthTiles = zone.WorldBounds.Width / WorldUnits.UnitsPerTile;
        var heightTiles = zone.WorldBounds.Height / WorldUnits.UnitsPerTile;
        return $"{zone.Owner.Name} | {zone.Purpose}\n{widthPixels} x {heightPixels} px ({widthTiles} x {heightTiles} tiles) | {zone.OccupiedCount}/{zone.Capacity}";
    }
}
