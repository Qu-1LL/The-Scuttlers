using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Runtime.Bootstrap;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests;

internal static class TestWorldFactory
{
    public static GameBootstrapResult CreateBootstrappedGame()
    {
        return new GameSessionBootstrapper().CreateNewGame();
    }

    public static (GameSession Session, Cave Cave, Queen Queen) CreateSessionWithQueen()
    {
        var session = new GameSession();
        var cave = new Cave(session);
        var queen = new Queen(session);
        var queenLocation = FindBuildLocation(cave, queen);
        if (!cave.Build(queen, queenLocation))
        {
            throw new InvalidOperationException("Failed to build the queen in a generated cave.");
        }

        return (session, cave, queen);
    }

    public static (GameSession Session, Cave Cave, Queen Queen, Trilobite Trilobite) CreateSessionWithQueenAndTrilobite()
    {
        var (session, cave, queen) = CreateSessionWithQueen();
        var spawnTile = queen.GetFeedTiles().FirstOrDefault(tile => tile.CreatureFits())
            ?? throw new InvalidOperationException("Queen has no reachable feed tile for test trilobite spawn.");
        var trilobite = new Trilobite("Tester", GridPoint.Parse(spawnTile.Key), session);
        if (!cave.Spawn(trilobite, spawnTile))
        {
            throw new InvalidOperationException("Failed to spawn the test trilobite.");
        }

        return (session, cave, queen, trilobite);
    }

    public static GridPoint FindBuildLocation(Cave cave, Building building, bool preserveReachability = false)
    {
        foreach (var location in cave.GetTiles()
                     .Select(tile => GridPoint.Parse(tile.Key))
                     .OrderBy(point => GridPoint.ManhattanDistance(point, GridPoint.Zero)))
        {
            if (cave.CanBuild(building, location, preserveReachability))
            {
                return location;
            }
        }

        throw new InvalidOperationException($"No build location was found for {building.Name}.");
    }

    public static (GameSession Session, Cave Cave, Queen Queen, AlgaeFarm Farm, IReadOnlyList<Trilobite> Farmers) CreateSessionWithFarmAndFarmers(int farmerCount)
    {
        var (session, cave, queen) = CreateSessionWithQueen();
        var farm = new AlgaeFarm(session);
        var farmLocation = FindBuildLocation(cave, farm);
        if (!cave.Build(farm, farmLocation))
        {
            throw new InvalidOperationException("Failed to build the algae farm for the benchmark scenario.");
        }

        var spawnTile = farm.TileArray.FirstOrDefault(tile => tile.CreatureFits())
            ?? throw new InvalidOperationException("Farm has no passable tile for farmer spawn.");

        var farmers = new List<Trilobite>(farmerCount);
        for (var index = 0; index < farmerCount; index++)
        {
            var trilobite = new Trilobite($"Farmer {index + 1}", spawnTile.Coordinates, session)
            {
                Assignment = "farmer"
            };

            if (!cave.Spawn(trilobite, spawnTile))
            {
                throw new InvalidOperationException($"Failed to spawn benchmark farmer {index + 1}.");
            }

            trilobite.RestartBehavior();
            farmers.Add(trilobite);
        }

        return (session, cave, queen, farm, farmers);
    }

    public static (GameSession Session, Cave Cave, Queen Queen, MiningPost Post, IReadOnlyList<Trilobite> Miners) CreateSessionWithMiningPostAndMiners(int minerCount)
    {
        var (session, cave, queen) = CreateSessionWithQueen();
        var post = new MiningPost(session);
        var postLocation = FindBuildLocation(cave, post);
        if (!cave.Build(post, postLocation))
        {
            throw new InvalidOperationException("Failed to build the mining post for the benchmark scenario.");
        }

        if (!post.HasQueuedMineableTiles(cave))
        {
            throw new InvalidOperationException("Mining post benchmark scenario has no mineable tiles in range.");
        }

        var spawnTile = post.TileArray.FirstOrDefault(tile => tile.CreatureFits())
            ?? throw new InvalidOperationException("Mining post has no passable tile for miner spawn.");

        var miners = new List<Trilobite>(minerCount);
        for (var index = 0; index < minerCount; index++)
        {
            var trilobite = new Trilobite($"Miner {index + 1}", spawnTile.Coordinates, session)
            {
                Assignment = "miner"
            };

            if (!cave.Spawn(trilobite, spawnTile))
            {
                throw new InvalidOperationException($"Failed to spawn benchmark miner {index + 1}.");
            }

            trilobite.RestartBehavior();
            miners.Add(trilobite);
        }

        return (session, cave, queen, post, miners);
    }
}
