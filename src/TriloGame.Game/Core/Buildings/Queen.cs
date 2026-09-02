using TriloGame.Game.Audio;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Queen : Building
{
    private readonly Queue<Trilobite> _broodQueue = [];

    public Queen(GameSession session)
        : base("Queen", new GridPoint(3, 3), [[1, 1, 1], [1, 0, 1], [1, 1, 1]], session, true)
    {
        TextureKey = "Queen";
        NutritionQuota = 20;
        NutritionCount = 0;
        BroodlingCount = 1;
        Description = "The one and only Queen of your colony! Protect her at all costs!";
    }

    public override bool MaintainsNavigationField => true;

    public override BuildingNavigationMaintenanceMode NavigationFieldMaintenanceMode => BuildingNavigationMaintenanceMode.Synchronous;

    public override int ProjectionRadius => GameConstants.QueenEnemySpawnExclusionRadius;

    public int NutritionQuota { get; private set; }

    public int NutritionCount { get; private set; }

    // Keep the old names available to existing integrations while nutrition is the canonical model.
    public int AlgaeQuota
    {
        get => NutritionQuota;
        private set => NutritionQuota = value;
    }

    public int AlgaeCount
    {
        get => NutritionCount;
        private set => NutritionCount = value;
    }

    public int BroodlingCount { get; private set; }

    public int PendingBroodlingCount => _broodQueue.Count;

    public IReadOnlyList<World.Tile> GetFeedTiles()
    {
        var feedTiles = new List<World.Tile>(TileArray.Count);
        for (var index = 0; index < TileArray.Count; index++)
        {
            var tile = TileArray[index];
            if (ReferenceEquals(tile.Built, this) && tile.CreatureFits())
            {
                feedTiles.Add(tile);
            }
        }

        return feedTiles;
    }

    public bool CanBeFedAt(GridPoint location)
    {
        var tile = Cave?.GetTile(location);
        return tile is not null && IsInteractionTile(tile);
    }

    public bool CanBeFedBy(Creature creature)
    {
        return creature.IsAtBuildingInteractionTile(this);
    }

    public bool CanConsumeResource(ResourceName? resourceType)
    {
        if (!resourceType.HasValue)
        {
            return false;
        }

        return ItemCatalog.TryGet(resourceType.Value, out var itemType) && itemType.NutritionValue > 0;
    }

    public World.Tile? GetBirthTile()
    {
        for (var index = 0; index < TileArray.Count; index++)
        {
            var tile = TileArray[index];
            if (ReferenceEquals(tile.Built, this) && tile.CreatureFits())
            {
                return tile;
            }
        }

        return null;
    }

    public bool Birth(World.Cave? cave, Trilobite? feeder)
    {
        var brood = new Trilobite($"Broodling {BroodlingCount}", GridPoint.Zero, Session);
        brood.SetActivity(CreatureActivity.Brooding);
        BroodlingCount++;
        _broodQueue.Enqueue(brood);
        return cave is not null && TrySpawnQueuedBrood(cave) > 0;
    }

    public override int Tick(World.Cave cave)
    {
        return TrySpawnQueuedBrood(cave);
    }

    private int TrySpawnQueuedBrood(World.Cave cave)
    {
        if (_broodQueue.Count == 0)
        {
            return 0;
        }

        var spawned = 0;
        while (_broodQueue.Count > 0)
        {
            var brood = _broodQueue.Peek();
            var foundSlot = false;
            for (var tileIndex = 0; tileIndex < TileArray.Count; tileIndex++)
            {
                var tile = TileArray[tileIndex];
                if (!ReferenceEquals(tile.Built, this) ||
                    !tile.CreatureFits() ||
                    !cave.SpawnAtWorldPosition(brood, WorldPoint.FromGridPoint(tile.Coordinates)))
                {
                    continue;
                }

                foundSlot = true;
                spawned++;
                _broodQueue.Dequeue();
                break;
            }

            if (!foundSlot)
            {
                break;
            }

            Session.RequestAudioCue(
                GameAudioCue.TrilobiteBirth,
                WorldPoint.FromGridPoint(GetCenter()),
                AudioCueRequest.CreatureEffectFootprintTiles);
        }

        return spawned;
    }

    // Food resources contribute their catalog nutrition value toward the next broodling quota.
    public (int Accepted, int SpawnCount) FeedResource(ResourceName resourceType, int amount, Trilobite? creature = null, World.Cave? cave = null)
    {
        if (amount <= 0 || !CanConsumeResource(resourceType))
        {
            return (0, 0);
        }

        if (creature is not null && !CanBeFedBy(creature))
        {
            return (0, 0);
        }

        NutritionCount += amount * ItemCatalog.GetNutritionValue(resourceType);
        var spawnCount = 0;
        while (NutritionCount >= NutritionQuota)
        {
            NutritionCount -= NutritionQuota;
            NutritionQuota += 5;
            var before = _broodQueue.Count;
            if (Birth(cave, creature))
            {
                spawnCount++;
            }
            else if (_broodQueue.Count == before)
            {
                break;
            }
        }

        return (amount, spawnCount);
    }

    public (int Accepted, int SpawnCount) FeedAlgae(int amount, Trilobite? creature = null, World.Cave? cave = null)
    {
        return FeedResource(ResourceName.Algae, amount, creature, cave);
    }

    public (int Accepted, int SpawnCount) FeedAlgaeMeal(int amount, Trilobite? creature = null, World.Cave? cave = null)
    {
        return FeedResource(ResourceName.AlgaeMeal, amount, creature, cave);
    }
}
