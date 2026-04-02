using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests;

internal static class TestWorldFactory
{
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
}
