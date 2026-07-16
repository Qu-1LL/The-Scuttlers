using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.AI;

public sealed class TrilobiteBehaviorTests
{
    [Fact]
    public void RestartBehavior_ForUnassignedTrilobiteLeavesItIdle()
    {
        var (_, _, _, trilobite) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();
        var startingLocation = trilobite.Location;

        var restarted = trilobite.RestartBehavior();
        var moveResult = trilobite.Move();

        Assert.True(restarted);
        Assert.Null(moveResult);
        Assert.Equal(startingLocation, trilobite.Location);
    }

    [Fact]
    public void Move_ForWaitingTrilobiteOnScaffolding_StepsTowardNearestEmptyTile()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(1, 1));
        var scaffolding = new Scaffolding(session, new Storage(session));
        var buildLocation = new GridPoint(6, 4);

        Assert.True(cave.Build(scaffolding, buildLocation));
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, buildLocation, "Waiting Trilobite", "unassigned");

        var moveResult = trilobite.Move();
        var destinationTile = cave.GetTile(trilobite.Location)
            ?? throw new InvalidOperationException("Expected the trilobite to remain on a valid tile.");

        Assert.NotNull(moveResult);
        Assert.NotEqual(buildLocation, trilobite.Location);
        Assert.Equal("empty", destinationTile.Base);
        Assert.Null(destinationTile.Built);
    }

    [Fact]
    public void BuilderStep4_DepositsFromAdjacentTileWithoutMovingOntoScaffold()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, new GridPoint(1, 1));
        var scaffolding = new Scaffolding(session, new Storage(session));
        var buildLocation = new GridPoint(6, 4);

        Assert.True(cave.Build(scaffolding, buildLocation));

        var startingLocation = new GridPoint(5, 4);
        var builder = TestWorldFactory.SpawnTrilobite(cave, session, startingLocation, "Builder", "builder");
        builder.SetAssignedBuilding(scaffolding);
        scaffolding.Assign(builder);
        builder.AddToInventory(ResourceName.Sandstone, 1);

        builder.BuilderStep4();

        Assert.Equal(startingLocation, builder.Location);
        Assert.False(builder.HasInventory());
        Assert.Equal(1, scaffolding.GetTotalDepositedAmount());
    }

    [Fact]
    public void FighterStep1_KeepsTargetingEnemyThatMovesToDifferentAdjacentTile()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(14, 14, new GridPoint(1, 1));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 6), "Fighter", "fighter");
        var firstEnemyTile = cave.GetTile(new GridPoint(6, 7))
            ?? throw new InvalidOperationException("Expected the first adjacent enemy tile to exist.");
        var secondEnemyTile = cave.GetTile(new GridPoint(7, 6))
            ?? throw new InvalidOperationException("Expected the second adjacent enemy tile to exist.");
        var enemy = new Enemy("Target", firstEnemyTile.Coordinates, session);

        Assert.True(cave.Spawn(enemy, firstEnemyTile));
        Assert.True(session.Danger);

        var firstAttack = fighter.FighterStep1();
        var healthAfterFirstAttack = enemy.Health;

        Assert.True(firstAttack);
        Assert.True(healthAfterFirstAttack < enemy.MaxHealth);
        Assert.Equal(firstEnemyTile.Key, fighter.FighterTargetTileKey);

        Assert.True(cave.PlaceCreatureOnTile(enemy, secondEnemyTile.Coordinates));

        var secondAttack = fighter.FighterStep1();

        Assert.True(secondAttack);
        Assert.True(enemy.Health < healthAfterFirstAttack);
        Assert.Equal(secondEnemyTile.Key, fighter.FighterTargetTileKey);
    }
}
