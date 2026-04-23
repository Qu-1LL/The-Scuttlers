using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Runtime.Bootstrap;

public sealed class GameSessionBootstrapper
{
    public GameBootstrapResult CreateNewGame()
    {
        var session = new GameSession();
        InitializeSkillTreeRoot(session);
        PopulateUnlockedBuildings(session);

        var cave = new Cave(session);
        var initialColony = BuildInitialColony(cave, session);
        var spawnX = initialColony.QueenLocation.X;
        var spawnY = initialColony.QueenLocation.Y;

        var jeffery = new Trilobite("Jeffery", new GridPoint(spawnX + 2, spawnY), session)
        {
            Assignment = "miner"
        };
        var quinton = new Trilobite("Quinton", new GridPoint(spawnX + 2, spawnY + 2), session)
        {
            Assignment = "builder"
        };
        var yeetmuncher = new Trilobite("Yeetmuncher", new GridPoint(spawnX, spawnY), session)
        {
            Assignment = "farmer"
        };
        var sigma = new Trilobite("Sigma", new GridPoint(spawnX, spawnY + 2), session)
        {
            Assignment = "fighter"
        };

        cave.Spawn(jeffery, cave.GetTile(new GridPoint(spawnX + 2, spawnY))!);
        cave.Spawn(quinton, cave.GetTile(new GridPoint(spawnX + 2, spawnY + 2))!);
        cave.Spawn(yeetmuncher, cave.GetTile(new GridPoint(spawnX, spawnY))!);
        cave.Spawn(sigma, cave.GetTile(new GridPoint(spawnX, spawnY + 2))!);

        cave.RevealCave();
        if (GameConstants.EnableOpal)
        {
            cave.TrySpawnQueenOpal();
        }

        return new GameBootstrapResult(session, initialColony.QueenLocation, initialColony.MiningPostLocation);
    }

    private static void InitializeSkillTreeRoot(GameSession session)
    {
        var rootTemplate = new SkillNode(
            "Hive Core",
            "The colony's structural research anchor for drafted branches.");
        var rootNode = session.SkillTree.SetRoot(session.SkillTree.IntakeSkillNode(rootTemplate, GridPoint.Zero));
        rootNode.TryUnlock(session);
    }

    private static void PopulateUnlockedBuildings(GameSession session)
    {
        session.UnlockedBuildings.Add(new Factory(game => new AlgaeFarm(game), session));
        session.UnlockedBuildings.Add(new Factory(game => new Barracks(game), session));
        session.UnlockedBuildings.Add(new Factory(game => new Turret(game), session));
        session.UnlockedBuildings.Add(new Factory(game => new Wall(game), session));
        session.UnlockedBuildings.Add(new Factory(game => new MiningPost(game), session));
        session.UnlockedBuildings.Add(new Factory(game => new Radar(game), session));
    }

    private static (GridPoint QueenLocation, GridPoint MiningPostLocation) BuildInitialColony(Cave cave, GameSession session)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var queenLocation = new GridPoint(RandomUtil.NextInt(-10, 10), RandomUtil.NextInt(-10, 10));
            var queen = new Queen(session);
            if (!cave.Build(queen, queenLocation))
            {
                continue;
            }

            var post = new MiningPost(session);
            var postLocation = FindStarterMiningPostLocation(cave, post);
            if (postLocation is not null && cave.Build(post, postLocation.Value))
            {
                return (queenLocation, postLocation.Value);
            }

            cave.RemoveBuilding(queen, "initialPlacementRetry");
        }

        foreach (var queenLocation in cave.GetTiles().Select(tile => GridPoint.Parse(tile.Key)).OrderBy(point => GridPoint.ManhattanDistance(point, GridPoint.Zero)))
        {
            var queen = new Queen(session);
            if (!cave.Build(queen, queenLocation))
            {
                continue;
            }

            var post = new MiningPost(session);
            var postLocation = FindStarterMiningPostLocation(cave, post);
            if (postLocation is not null && cave.Build(post, postLocation.Value))
            {
                return (queenLocation, postLocation.Value);
            }

            cave.RemoveBuilding(queen, "initialPlacementRetry");
        }

        throw new InvalidOperationException("Failed to place the initial queen and starter mining post.");
    }

    private static GridPoint? FindStarterMiningPostLocation(Cave cave, Building building)
    {
        var queenCenter = cave.GetQueenBuilding()?.GetCenter() ?? GridPoint.Zero;
        GridPoint? bestLocation = null;
        var bestDistance = int.MaxValue;

        foreach (var tile in cave.GetTiles())
        {
            var location = GridPoint.Parse(tile.Key);
            if (!cave.CanBuild(building, location) || !HasWallClearance(cave, building, location, 5))
            {
                continue;
            }

            var buildingCenter = new GridPoint(location.X + (building.Size.X / 2), location.Y + (building.Size.Y / 2));
            var distance = GridPoint.ManhattanDistance(buildingCenter, queenCenter);
            if (distance > 10)
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestLocation = location;
            }
        }

        return bestLocation;
    }

    private static bool HasWallClearance(Cave cave, Building building, GridPoint location, int minDistance)
    {
        for (var x = 0; x < building.Size.X; x++)
        {
            for (var y = 0; y < building.Size.Y; y++)
            {
                if (building.OpenMap[y][x] > 1)
                {
                    continue;
                }

                var tileLocation = new GridPoint(location.X + x, location.Y + y);
                for (var dx = -(minDistance - 1); dx <= minDistance - 1; dx++)
                {
                    for (var dy = -(minDistance - 1); dy <= minDistance - 1; dy++)
                    {
                        if (Math.Abs(dx) + Math.Abs(dy) >= minDistance)
                        {
                            continue;
                        }

                        var nearbyTile = cave.GetTile(new GridPoint(tileLocation.X + dx, tileLocation.Y + dy));
                        if (nearbyTile is null || nearbyTile.Base == "wall")
                        {
                            return false;
                        }
                    }
                }
            }
        }

        return true;
    }
}
