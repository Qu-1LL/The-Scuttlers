using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Shared.Diagnostics;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Buildings;

public sealed class BuildingInteractionTests
{
    [Fact]
    public void Queen_UsesAllEightPassableFootprintTilesForFeeding()
    {
        var (_, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(12, 12, new GridPoint(4, 4));

        var feedTiles = queen.GetFeedTiles();

        Assert.Equal(8, feedTiles.Count);
        Assert.All(feedTiles, tile =>
        {
            Assert.Same(queen, tile.Built);
            Assert.True(tile.CreatureFits());
            Assert.True(queen.CanBeFedAt(tile.Coordinates));
        });
        Assert.False(queen.CanBeFedAt(new GridPoint(5, 5)));
        Assert.Equal(8, queen.GetNavigationSeedTiles(cave).Count);
    }

    [Fact]
    public void Queen_AllowsEveryFarmerOnThePassableFootprintToFeedWithoutAReservation()
    {
        var (session, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(14, 14, new GridPoint(5, 5));
        var feedTiles = queen.GetFeedTiles();

        for (var index = 0; index < feedTiles.Count; index++)
        {
            var farmer = TestWorldFactory.SpawnTrilobite(cave, session, feedTiles[index].Coordinates, $"Farmer {index}", "farmer");
            Assert.True(queen.CanBeFedBy(farmer));
        }
    }

    [Fact]
    public void StationableBuilding_InteractsFromPassableOwnedTiles()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(14, 14, new GridPoint(1, 1));
        var post = new MiningPost(session);
        Assert.True(cave.Build(post, new GridPoint(5, 5)));
        var worker = TestWorldFactory.SpawnTrilobite(cave, session, post.TileArray[0].Coordinates, "Worker");

        Assert.True(worker.IsAtBuildingInteractionTile(post));
        Assert.Contains(post, worker.GetActions());
    }

    [Fact]
    public void NonStationableBuilding_InteractsFromAnAdjacentPassableTile()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(14, 14, new GridPoint(1, 1));
        var storage = new Storage(session);
        Assert.True(cave.Build(storage, new GridPoint(5, 5)));
        var interactionTile = GetInteractionTile(storage);
        var worker = TestWorldFactory.SpawnTrilobite(cave, session, interactionTile.Coordinates, "Worker");

        Assert.True(worker.IsAtBuildingInteractionTile(storage));
        Assert.Contains(storage, worker.GetActions());
        var navigationSeeds = storage.GetNavigationSeedTiles(cave);
        Assert.NotEmpty(navigationSeeds);
        Assert.All(navigationSeeds, tile => Assert.True(storage.IsInteractionTile(tile)));
    }

    [Fact]
    public void Scaffolding_InteractsFromAnyAdjacentPassableTile()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(16, 16, new GridPoint(1, 1));
        var scaffold = new Scaffolding(session, new Storage(session));
        Assert.True(cave.Build(scaffold, new GridPoint(6, 6)));
        var interactionTile = GetInteractionTile(scaffold);
        var builder = TestWorldFactory.SpawnTrilobite(cave, session, interactionTile.Coordinates, "Builder", "builder");

        Assert.True(builder.IsAtBuildingInteractionTile(scaffold));
        Assert.Contains(scaffold, builder.GetActions());
    }

    [Fact]
    public void NavigateToBuilding_UsesTheSharedBuildingField()
    {
        var (session, cave, queen) = TestWorldFactory.CreateRectangularSessionWithQueen(16, 14, new GridPoint(6, 6));
        var farmer = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(1, 10), "Farmer");

        NavigationInstrumentation.BeginTick();
        Assert.True(farmer.NavigateToBuilding(queen));
        var navigation = NavigationInstrumentation.CompleteTick();

        Assert.Equal(0, navigation.PointPathRequestCount);
        Assert.Equal(0, navigation.BuildPointBfsFieldCallCount);
    }

    private static TriloGame.Game.Core.World.Tile GetInteractionTile(Building building)
    {
        foreach (var tile in building.TileArray)
        {
            foreach (var neighbor in tile.Neighbors)
            {
                if (building.IsInteractionTile(neighbor))
                {
                    return neighbor;
                }
            }
        }

        throw new InvalidOperationException($"No interaction tile exists for {building.Name}.");
    }
}
