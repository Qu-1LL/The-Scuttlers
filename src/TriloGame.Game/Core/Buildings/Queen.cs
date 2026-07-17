using TriloGame.Game.Audio;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Queen : Building
{
    private static readonly IReadOnlyList<InteractionZoneDefinition> ZoneDefinitions =
    [
        new("Feeding", InteractionZonePurpose.Feeding, new GridPoint(0, 0), new GridPoint(3, 1),
            [new GridPoint(0, 0), new GridPoint(1, 0), new GridPoint(2, 0)]),
        new("Brood emergence", InteractionZonePurpose.Brooding, new GridPoint(0, 2), new GridPoint(3, 1),
            [new GridPoint(0, 2), new GridPoint(1, 2), new GridPoint(2, 2)])
    ];
    private List<World.Tile>? _feedTilesCache;
    private HashSet<string>? _feedTileKeysCache;
    private readonly Queue<Trilobite> _broodQueue = [];

    public Queen(GameSession session)
        : base("Queen", new GridPoint(3, 3), [[1, 1, 1], [1, 0, 1], [1, 1, 1]], session, true)
    {
        TextureKey = "Queen";
        AlgaeQuota = 20;
        AlgaeCount = 0;
        BroodlingCount = 1;
        Description = "The one and only Queen of your colony! Protect her at all costs!";
    }

    public override int ProjectionRadius => GameConstants.QueenEnemySpawnExclusionRadius;

    public int AlgaeQuota { get; private set; }

    public int AlgaeCount { get; private set; }

    public int BroodlingCount { get; private set; }

    public int PendingBroodlingCount => _broodQueue.Count;

    protected override IReadOnlyList<InteractionZoneDefinition> GetInteractionZoneDefinitions() => ZoneDefinitions;

    public IReadOnlyList<World.Tile> GetFeedTiles()
    {
        if (_feedTilesCache is not null)
        {
            return _feedTilesCache;
        }

        _feedTilesCache = [];
        if (Cave is null || !TryGetInteractionZone(InteractionZonePurpose.Feeding, out var feedingZone))
        {
            return _feedTilesCache;
        }

        for (var index = 0; index < feedingZone.SlotPositions.Count; index++)
        {
            var tile = Cave.GetTile(feedingZone.SlotPositions[index].ToGridPoint());
            if (tile is not null)
            {
                _feedTilesCache.Add(tile);
            }
        }

        return _feedTilesCache;
    }

    private HashSet<string> GetFeedTileKeys()
    {
        _feedTileKeysCache ??= GetFeedTiles()
            .Select(tile => tile.Key)
            .ToHashSet(StringComparer.Ordinal);
        return _feedTileKeysCache;
    }

    public bool CanBeFedAt(GridPoint location)
    {
        return GetFeedTileKeys().Contains(location.ToString());
    }

    public bool CanBeFedBy(Creature creature)
    {
        return creature.ReservedZone is { Purpose: InteractionZonePurpose.Feeding } zone &&
               ReferenceEquals(zone.Owner, this) &&
               creature.IsAtReservedInteractionSlot();
    }

    public bool CanConsumeResource(ResourceName? resourceType)
    {
        if (!resourceType.HasValue)
        {
            return false;
        }

        var growableResources = GrowableResourceType.GetAll();
        for (var index = 0; index < growableResources.Count; index++)
        {
            if (growableResources[index].Resource == resourceType.Value)
            {
                return true;
            }
        }

        return false;
    }

    public World.Tile? GetBirthTile()
    {
        if (Cave is null || !TryGetInteractionZone(InteractionZonePurpose.Brooding, out var zone))
        {
            return null;
        }

        for (var index = 0; index < zone.SlotPositions.Count; index++)
        {
            var tile = Cave.GetTile(zone.SlotPositions[index].ToGridPoint());
            if (tile is not null)
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
        if (_broodQueue.Count == 0 ||
            !TryGetInteractionZone(InteractionZonePurpose.Brooding, out var zone))
        {
            return 0;
        }

        var spawned = 0;
        while (_broodQueue.Count > 0)
        {
            var brood = _broodQueue.Peek();
            var foundSlot = false;
            for (var slotIndex = 0; slotIndex < zone.SlotPositions.Count; slotIndex++)
            {
                if (!cave.SpawnAtWorldPosition(brood, zone.SlotPositions[slotIndex]))
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

    // The queen currently values every growable crop as one food unit so idle farmers can haul any stored crop.
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

        AlgaeCount += amount;
        var spawnCount = 0;
        while (AlgaeCount >= AlgaeQuota)
        {
            AlgaeCount -= AlgaeQuota;
            AlgaeQuota += 5;
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
}
