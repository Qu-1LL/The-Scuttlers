using System.Text;
using System.Numerics;
using TriloGame.Game.Audio;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Events;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Traits;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Research;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.State;

namespace TriloGame.Game.Core.Simulation;

public sealed class GameSession
{
    public GameSession()
    {
        EventBus = new GameEventBus();
        Stats = new StatsTracker(EventBus);
        Resources = new Dictionary<ResourceName, int>
        {
            [ResourceName.Algae] = 0,
            [ResourceName.Sandstone] = 0,
            [ResourceName.Malachite] = 0,
            [ResourceName.Magnetite] = 0,
            [ResourceName.Perotene] = 0,
            [ResourceName.Ilmenite] = 0,
            [ResourceName.Cochinium] = 0
        };
        EventBus.Subscribe(GameEvents.StorageInventoryChanged, HandleStorageInventoryChanged);
        BfsFields = new Dictionary<string, BfsField>(StringComparer.Ordinal);
        UnlockedBuildings = [];
        ProgressionDex = TriloDex.Global;
        SkillTree = new SkillTree(ProgressionDex);
        GlobalResearch = new GlobalResearch();
        Danger = false;
        TickCount = 0;
        Runtime = new GameSessionRuntimeState();
        TraitHandler = new TrilobiteTraitHandler(this);
    }

    public GameEventBus EventBus { get; }

    public StatsTracker Stats { get; }

    public Dictionary<ResourceName, int> Resources { get; }

    public Dictionary<string, BfsField> BfsFields { get; set; }

    public List<Factory> UnlockedBuildings { get; }

    public TriloDex ProgressionDex { get; }

    public IReadOnlyList<FeatureTree> FeatureTrees => ProgressionDex.FeatureTrees;

    public SkillTree SkillTree { get; }

    public GlobalResearch GlobalResearch { get; }

    public Cave? Cave { get; set; }

    public bool Danger { get; set; }

    public int TickCount { get; set; }

    public GameSessionRuntimeState Runtime { get; }

    public TrilobiteTraitHandler TraitHandler { get; }

    public event Action<GameAudioCue>? AudioCueRequested;
    public event Action<float>? ScreenShakeRequested;
    public event Action<DeathMistRequest>? DeathMistRequested;

    public Action On(string eventName, Action<GameEventPayload> listener)
    {
        return EventBus.Subscribe(eventName, listener);
    }

    public int Emit(string eventName, GameEventPayload payload)
    {
        return EventBus.Emit(eventName, payload);
    }

    public int GetStoredResourceTotal(ResourceName resourceType)
    {
        return Resources.GetValueOrDefault(resourceType, 0);
    }

    public int GetStoredResourceTotal(string resourceType)
    {
        return ItemCatalog.TryGetResource(resourceType, out var resourceName)
            ? GetStoredResourceTotal(resourceName)
            : 0;
    }

    public void RequestAudioCue(GameAudioCue cue)
    {
        AudioCueRequested?.Invoke(cue);
    }

    public void RequestScreenShake(float intensity)
    {
        if (intensity <= 0f)
        {
            return;
        }

        ScreenShakeRequested?.Invoke(intensity);
    }

    public void RequestDeathMist(GridPoint originTile, int radius)
    {
        if (radius < 0)
        {
            return;
        }

        DeathMistRequested?.Invoke(new DeathMistRequest(originTile, radius));
    }

    private void HandleStorageInventoryChanged(GameEventPayload payload)
    {
        if (!payload.ResourceType.HasValue || payload.ResourceDelta == 0)
        {
            return;
        }

        var resourceType = payload.ResourceType.Value;
        Resources.TryAdd(resourceType, 0);
        var nextTotal = Resources[resourceType] + payload.ResourceDelta;
        if (nextTotal < 0)
        {
            throw new InvalidOperationException($"Stored resource total for {resourceType} cannot become negative.");
        }

        Resources[resourceType] = nextTotal;
    }

    public Shared.State.ProjectileFlight? LaunchProjectile(Entities.Creature source, Entities.Creature target, Projectile projectile)
    {
        if (source is null ||
            target is null ||
            projectile is null ||
            ReferenceEquals(source, target) ||
            source.Cave is null ||
            source.Health <= 0 ||
            target.Health <= 0 ||
            !ReferenceEquals(source.Cave, target.Cave))
        {
            return null;
        }

        var sourceWorldPosition = source.GetWorldPosition();
        var targetWorldPosition = target.GetWorldPosition();
        var delta = targetWorldPosition - sourceWorldPosition;
        var angleDegrees = delta.LengthSquared() <= 0f
            ? 0f
            : MathF.Atan2(delta.Y, delta.X) * (180f / MathF.PI);
        var flight = new Shared.State.ProjectileFlight(
            projectile,
            source,
            target,
            sourceWorldPosition,
            angleDegrees);
        Runtime.ActiveProjectileFlights.Add(flight);
        return flight;
    }

    public FeatureTree? GetFeatureTree(string name)
    {
        return ProgressionDex.FindFeatureTree(name);
    }

    public bool IsOreTileType(string tileType)
    {
        return OreType.GetOres().Any(ore => string.Equals(ore.Name, tileType, StringComparison.Ordinal));
    }

    private static ResourceName? ResolveMinedResourceType(string tileType)
    {
        if (string.Equals(tileType, "wall", StringComparison.Ordinal))
        {
            return GameConstants.WallMineResourceAmount > 0
                ? GameConstants.WallMineResourceType
                : null;
        }

        if (Tile.IsResourcelessBreakableBase(tileType))
        {
            return null;
        }

        if (OreType.TryGet(tileType, out var oreType))
        {
            return oreType.Resource;
        }

        return ItemCatalog.TryGetResource(tileType, out var resourceName)
            ? resourceName
            : null;
    }

    public void EmitMineEvents(string tileType, Cave cave, string tileKey, object? source = null)
    {
        var resourceType = ResolveMinedResourceType(tileType);
        var payload = new GameEventPayload(
            cave,
            tileKey,
            GridPoint.Parse(tileKey),
            tileType,
            resourceType,
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

    public MineTileResult MineTile(Cave cave, string tileKey, string? dropTargetTileKey = null, object? source = null)
    {
        var tile = cave.GetTile(tileKey);
        if (tile is null)
        {
            return MineTileResult.NotApplied;
        }

        var tileType = tile.Base;
        if (string.Equals(tileType, "wall", StringComparison.Ordinal))
        {
            return MineWallTile(cave, tile, tileKey, dropTargetTileKey, source);
        }

        if (tile.IsCaveCrystal())
        {
            return MineCaveCrystalTile(cave, tile, tileKey, source);
        }

        if (!IsOreTileType(tileType))
        {
            return MineTileResult.NotApplied;
        }

        var yieldedResource = tile.ApplyOreMineHit(out var depleted);
        if (!yieldedResource)
        {
            return new MineTileResult(true, false, null, 0, false, null, 0, tile.ResourceYield, tile.HitsRemaining);
        }

        if (depleted)
        {
            tile.SetBase("empty");
            tile.ClearResourceState();
            cave.NotifyMineableTilesChanged([tileKey]);
            cave.HandleNavigationTopologyChanged([tileKey], [], []);
        }

        EmitMineEvents(tileType, cave, tileKey, source);
        return new MineTileResult(
            true,
            true,
            OreType.TryGet(tileType, out var oreType) ? oreType.Resource : null,
            1,
            depleted,
            null,
            0,
            tile.ResourceYield,
            tile.HitsRemaining);
    }

    private MineTileResult MineCaveCrystalTile(Cave cave, Tile tile, string tileKey, object? source = null)
    {
        if (!tile.IsCaveCrystal())
        {
            return MineTileResult.NotApplied;
        }

        if (!tile.ApplyCaveCrystalMineHit())
        {
            return new MineTileResult(true, false, null, 0, false, null, 0, 0, tile.HitsRemaining);
        }

        tile.SetBase("empty");
        tile.ClearResourceState();
        tile.CreatureCanFit = true;

        var reachabilityResult = cave.RefreshReachableTiles();
        string[] fieldDirtyKeys = reachabilityResult.ChangedKeys.Count == 0
            ? [tileKey]
            : reachabilityResult.ChangedKeys.Append(tileKey).Distinct(StringComparer.Ordinal).ToArray();

        cave.HandleNavigationTopologyChanged(fieldDirtyKeys, [], []);
        cave.NotifyMineableTilesChanged([tileKey]);

        EmitMineEvents(Tile.CaveCrystalBase, cave, tileKey, source);
        return new MineTileResult(
            true,
            false,
            null,
            0,
            true,
            null,
            0,
            0,
            0);
    }

    public MineTileResult MineWallTile(Cave cave, Tile tile, string emptyCoords, string? dropTargetTileKey = null, object? source = null)
    {
        if (!string.Equals(tile.Base, "wall", StringComparison.Ordinal))
        {
            return MineTileResult.NotApplied;
        }

        if (!tile.ApplyWallMineHit())
        {
            return new MineTileResult(true, false, null, 0, false, null, 0, 0, tile.HitsRemaining);
        }

        var changedKeys = new HashSet<string>(StringComparer.Ordinal) { emptyCoords };
        var newlyRevealedKeys = new HashSet<string>(StringComparer.Ordinal);
        var reachabilityChangedKeys = new HashSet<string>(StringComparer.Ordinal);

        static bool ShouldProcessAdjacentCaveTile(Cave activeCave, Tile adjacentTile)
        {
            if (adjacentTile.Base == "wall")
            {
                return false;
            }

            return !activeCave.IsTileReachable(adjacentTile);
        }

        tile.SetBase("empty");
        tile.ClearResourceState();
        tile.CreatureCanFit = true;
        if (cave.RevealTile(tile, newlyRevealedKeys) > 0)
        {
            changedKeys.Add(emptyCoords);
        }
        cave.TryAddReachableTile(tile, reachabilityChangedKeys);

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
                if (cave.RevealTile(neighbor, newlyRevealedKeys) > 0)
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
                    if (cave.RevealTile(wallTile, newlyRevealedKeys) > 0)
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
            wallTile.ConfigureWall(GameConstants.WallHitsRequired);
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

            cave.RevealTile(wallTile, newlyRevealedKeys);
        }

        if (shouldRevealCave)
        {
            cave.RevealCave(newlyRevealedKeys, rebalanceFields: false, newlyReachableKeys: reachabilityChangedKeys);
        }

        cave.AdvanceTopologyVersionForCache();
        if (reachabilityChangedKeys.Count > 0)
        {
            cave.AdvanceReachabilityVersionForIncrementalReachability();
        }

        var fieldDirtyKeys = changedKeys.Concat(reachabilityChangedKeys).Distinct(StringComparer.Ordinal).ToArray();
        cave.HandleNavigationTopologyChanged(fieldDirtyKeys, [], []);
        var mineableChangedKeys = changedKeys
            .Concat(newlyRevealedKeys.Where(key => Building.IsMineableType(cave.GetTile(key)?.Base ?? string.Empty)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        cave.NotifyMineableTilesChanged(mineableChangedKeys);

        EmitMineEvents("wall", cave, emptyCoords, source);
        return new MineTileResult(
            true,
            GameConstants.WallMineResourceAmount > 0,
            GameConstants.WallMineResourceAmount > 0 ? GameConstants.WallMineResourceType : null,
            GameConstants.WallMineResourceAmount > 0 ? GameConstants.WallMineResourceAmount : 0,
            true,
            null,
            0,
            0,
            0);
    }

    public string FormatInventory(Inventory inventory)
    {
        return !inventory.HasItems ? "empty" : $"{inventory.Amount} {ItemCatalog.GetName(inventory.Type!.Value)}";
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

public readonly record struct MineTileResult(
    bool HitApplied,
    bool YieldedResource,
    ResourceName? ResourceType,
    int ResourceAmount,
    bool TileDepleted,
    string? DroppedAtTileKey,
    int DroppedAmount,
    int RemainingYield,
    int RemainingHits)
{
    public static MineTileResult NotApplied => new(false, false, null, 0, false, null, 0, 0, 0);
}
