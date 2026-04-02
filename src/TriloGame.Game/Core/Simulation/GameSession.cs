using System.Text;
using TriloGame.Game.Audio;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Events;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Simulation;

public sealed class GameSession
{
    public GameSession()
    {
        EventBus = new GameEventBus();
        Stats = new StatsTracker(EventBus);
        Resources = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["algae"] = 0,
            ["sandstone"] = 0,
            ["malachite"] = 0,
            ["magnetite"] = 0,
            ["perotene"] = 0,
            ["ilmenite"] = 0,
            ["cochinium"] = 0
        };
        BfsFields = new Dictionary<string, BfsField>(StringComparer.Ordinal);
        UnlockedBuildings = [];
        Danger = false;
        TickCount = 0;
        DebugEnemyCount = 1;
        TickProfiler = new TickProfiler();
    }

    public GameEventBus EventBus { get; }

    public StatsTracker Stats { get; }

    public Dictionary<string, int> Resources { get; }

    public Dictionary<string, BfsField> BfsFields { get; set; }

    public List<Factory> UnlockedBuildings { get; }

    public Cave? Cave { get; set; }

    public bool Danger { get; set; }

    public int TickCount { get; set; }

    public int DebugEnemyCount { get; set; }

    public TickProfiler TickProfiler { get; }

    public event Action<GameAudioCue>? AudioCueRequested;

    public Action On(string eventName, Action<GameEventPayload> listener)
    {
        return EventBus.Subscribe(eventName, listener);
    }

    public int Emit(string eventName, GameEventPayload payload)
    {
        return EventBus.Emit(eventName, payload);
    }

    public void RequestAudioCue(GameAudioCue cue)
    {
        AudioCueRequested?.Invoke(cue);
    }

    public bool IsOreTileType(string tileType)
    {
        return OreType.GetOres().Any(ore => string.Equals(ore.Name, tileType, StringComparison.Ordinal));
    }

    public void EmitMineEvents(string tileType, Cave cave, string tileKey, object? source = null)
    {
        var payload = new GameEventPayload(
            cave,
            tileKey,
            GridPoint.Parse(tileKey),
            tileType,
            string.Equals(tileType, "wall", StringComparison.Ordinal) ? OreType.SANDSTONE.Name : tileType,
            source);

        Emit(GameEvents.TileMined, payload);

        if (string.Equals(tileType, "wall", StringComparison.Ordinal))
        {
            Emit(GameEvents.WallMined, payload);
            return;
        }

        if (IsOreTileType(tileType))
        {
            Emit($"{tileType}Mined", payload);
        }
    }

    public bool MineTile(Cave cave, string tileKey, object? source = null)
    {
        var tile = cave.GetTile(tileKey);
        if (tile is null)
        {
            return false;
        }

        var tileType = tile.Base;
        if (string.Equals(tileType, "wall", StringComparison.Ordinal))
        {
            return MineWallTile(cave, tile, tileKey, source);
        }

        if (!IsOreTileType(tileType))
        {
            return false;
        }

        tile.SetBase("empty");
        cave.MarkAllBuildingFieldsDirty([tileKey], [], []);
        cave.NotifyMineableTilesChanged([tileKey]);
        EmitMineEvents(tileType, cave, tileKey, source);
        return true;
    }

    public bool MineWallTile(Cave cave, Tile tile, string emptyCoords, object? source = null)
    {
        if (!string.Equals(tile.Base, "wall", StringComparison.Ordinal))
        {
            return false;
        }

        var changedKeys = new HashSet<string>(StringComparer.Ordinal) { emptyCoords };

        static bool ShouldProcessAdjacentCaveTile(Cave activeCave, Tile adjacentTile)
        {
            if (adjacentTile.Base == "wall")
            {
                return false;
            }

            return !activeCave.IsTileReachable(adjacentTile);
        }

        tile.SetBase("empty");
        tile.CreatureCanFit = true;
        if (cave.RevealTile(tile) > 0)
        {
            changedKeys.Add(emptyCoords);
        }

        var myDeltas = new Dictionary<string, GridPoint>
        {
            ["n"] = new GridPoint(0, -1),
            ["s"] = new GridPoint(0, 1),
            ["e"] = new GridPoint(1, 0),
            ["w"] = new GridPoint(-1, 0)
        };

        var myCoords = GridPoint.Parse(emptyCoords);
        var shouldRevealCave = false;

        foreach (var neighbor in cave.GetTile(emptyCoords)?.Neighbors ?? [])
        {
            var neighborCoords = GridPoint.Parse(neighbor.Key);
            if (neighborCoords.X - myCoords.X == 1)
            {
                myDeltas.Remove("e");
            }
            else if (neighborCoords.X - myCoords.X == -1)
            {
                myDeltas.Remove("w");
            }
            else if (neighborCoords.Y - myCoords.Y == -1)
            {
                myDeltas.Remove("n");
            }
            else
            {
                myDeltas.Remove("s");
            }

            if (neighbor.Base == "wall")
            {
                if (cave.RevealTile(neighbor) > 0)
                {
                    changedKeys.Add(neighbor.Key);
                }

                continue;
            }

            if (ShouldProcessAdjacentCaveTile(cave, neighbor))
            {
                shouldRevealCave = true;
            }
        }

        foreach (var direction in myDeltas.Values)
        {
            var newCoords = new GridPoint(myCoords.X + direction.X, myCoords.Y + direction.Y);
            var newKey = newCoords.ToString();
            var wallTile = cave.GetTile(newKey);
            if (wallTile is not null)
            {
                tile.AddNeighbor(wallTile);
                changedKeys.Add(newKey);

                if (wallTile.Base == "wall")
                {
                    wallTile.CreatureCanFit = false;
                    if (cave.RevealTile(wallTile) > 0)
                    {
                        changedKeys.Add(wallTile.Key);
                    }

                    continue;
                }

                if (ShouldProcessAdjacentCaveTile(cave, wallTile))
                {
                    shouldRevealCave = true;
                }

                continue;
            }

            wallTile = cave.AddTile(newKey);
            wallTile.SetBase("wall");
            wallTile.CreatureCanFit = false;
            changedKeys.Add(newKey);

            var newDeltas = new[]
            {
                new GridPoint(0, -1),
                new GridPoint(0, 1),
                new GridPoint(1, 0),
                new GridPoint(-1, 0)
            };

            foreach (var delta in newDeltas)
            {
                var neighbor = cave.GetTile(new GridPoint(newCoords.X + delta.X, newCoords.Y + delta.Y).ToString());
                if (neighbor is not null)
                {
                    wallTile.AddNeighbor(neighbor);
                }
            }

            cave.RevealTile(wallTile);
        }

        if (shouldRevealCave)
        {
            cave.RevealCave();
        }

        var reachability = cave.RefreshReachableTiles();
        cave.MarkAllBuildingFieldsDirty(changedKeys.Concat(reachability.ChangedKeys), [], []);
        cave.NotifyMineableTilesChanged(changedKeys.ToArray());
        cave.RebalanceAllBfsFields(changedKeys.ToArray(), [], []);
        EmitMineEvents("wall", cave, emptyCoords, source);
        return true;
    }

    public string FormatInventory(Inventory inventory)
    {
        return !inventory.HasItems ? "empty" : $"{inventory.Amount} {inventory.Type}";
    }

    public string FormatStatsSnapshot()
    {
        var stats = Stats.GetAll().OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        if (stats.Length == 0)
        {
            return "  (no stats tracked)";
        }

        var longest = stats.Max(pair => pair.Key.Length);
        var builder = new StringBuilder();
        foreach (var pair in stats)
        {
            builder.Append("  ")
                .Append(pair.Key.PadRight(longest))
                .Append(" : ")
                .Append(pair.Value)
                .AppendLine();
        }

        return builder.ToString().TrimEnd();
    }
}
