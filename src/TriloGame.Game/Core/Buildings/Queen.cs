using TriloGame.Game.Audio;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.Buildings;

public sealed class Queen : Building
{
    public Queen(GameSession session)
        : base("Queen", new GridPoint(3, 3), [[1, 1, 1], [1, 0, 1], [1, 1, 1]], session, true)
    {
        TextureKey = "Queen";
        AlgaeQuota = 20;
        AlgaeCount = 0;
        BroodlingCount = 1;
        Description = "The one and only Queen of your colony! Protect her at all costs!";
    }

    public int AlgaeQuota { get; private set; }

    public int AlgaeCount { get; private set; }

    public int BroodlingCount { get; private set; }

    public IReadOnlyList<World.Tile> GetFeedTiles()
    {
        return TileArray.Where(tile => tile.CreatureFits()).ToArray();
    }

    public bool CanBeFedBy(Creature creature)
    {
        return GetFeedTiles().Any(tile => tile.Key == creature.Location.ToString());
    }

    public World.Tile? GetBirthTile()
    {
        var feedTiles = GetFeedTiles();
        return feedTiles.Count == 0 ? null : feedTiles[RandomUtil.NextInt(feedTiles.Count)];
    }

    public bool Birth(World.Cave? cave, Trilobite? feeder)
    {
        if (cave is null || feeder is null)
        {
            return false;
        }

        var birthTile = GetBirthTile();
        if (birthTile is null || !birthTile.CreatureFits())
        {
            return false;
        }

        var spawnName = $"Broodling {BroodlingCount}";
        BroodlingCount++;
        var brood = new Trilobite(spawnName, GridPoint.Parse(birthTile.Key), feeder.Session);
        var spawned = cave.Spawn(brood, birthTile);
        if (spawned)
        {
            Session.RequestAudioCue(GameAudioCue.TrilobiteBirth);
        }

        return spawned;
    }

    public (int Accepted, int SpawnCount) FeedAlgae(int amount, Trilobite? creature = null, World.Cave? cave = null)
    {
        if (amount <= 0)
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
            if (Birth(cave, creature))
            {
                spawnCount++;
            }
        }

        return (amount, spawnCount);
    }
}
