using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
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
        Assert.True(trilobite.HasActiveMovement);
        while (trilobite.HasActiveMovement)
        {
            cave.AdvanceCreatureMovement();
        }

        var destinationTile = cave.GetTile(trilobite.Location)
            ?? throw new InvalidOperationException("Expected the trilobite to remain on a valid tile.");

        Assert.Null(moveResult);
        Assert.NotEqual(buildLocation, trilobite.Location);
        Assert.Equal("empty", destinationTile.Base);
        Assert.Null(destinationTile.Built);
    }

    [Theory]
    [InlineData("builder")]
    [InlineData("farmer")]
    [InlineData("fighter")]
    public void Move_ForIdleAssignedRole_UsesSharedIdleMovement(string assignment)
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 14, new GridPoint(1, 1));
        var trilobite = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(10, 7), $"Idle {assignment}", assignment);
        var startingPosition = trilobite.Position;

        var startedIdleMove = false;
        for (var tick = 0; tick < 90; tick++)
        {
            trilobite.Move();
            if (trilobite.HasActiveMovement || trilobite.IdleDestination.HasValue)
            {
                startedIdleMove = true;
                break;
            }

            cave.AdvanceCreatureMovement();
        }

        Assert.True(startedIdleMove);
        Assert.Equal(IdleBehaviorState.WanderNearAnchor, trilobite.IdleState);
        Assert.Equal(MovementGoalKind.Idle, trilobite.MovementCohort.GoalKind);
        Assert.NotEqual(startingPosition, trilobite.IdleDestination ?? trilobite.Position);
    }

    [Fact]
    public void FighterIdleMovement_IsNotInterruptedWhenDangerIsClear()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(5, 1));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 7), "Fighter", "fighter");

        for (var tick = 0; tick < 140 && !fighter.HasActiveMovement; tick++)
        {
            fighter.Move();
            if (!fighter.HasActiveMovement)
            {
                cave.AdvanceCreatureMovement();
            }
        }

        Assert.True(fighter.HasActiveMovement);
        Assert.Equal(MovementGoalKind.Idle, fighter.MovementCohort.GoalKind);
        Assert.False(session.Danger);
        var moveResult = fighter.Move();

        Assert.Null(moveResult);
        Assert.True(fighter.HasActiveMovement);
    }

    [Fact]
    public void FighterCombatRoute_IsCancelledWhenDangerEnds()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 14, new GridPoint(1, 1));
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(5, 7), "Fighter", "fighter");
        var enemy = new Enemy("Target", new GridPoint(18, 7), session);

        Assert.True(cave.Spawn(enemy, cave.GetTile(enemy.Location)!));
        Assert.True(fighter.RunRoleState(FighterState.MoveToTarget));
        Assert.True(fighter.HasActiveMovement);
        Assert.Equal(MovementGoalKind.Combat, fighter.MovementCohort.GoalKind);

        Assert.True(cave.RemoveCreature(enemy));
        fighter.Move();

        Assert.False(fighter.HasActiveMovement);
        Assert.Equal(MovementGoalKind.None, fighter.MovementCohort.GoalKind);
        Assert.Null(fighter.FighterTarget);
    }

    [Fact]
    public void MinerChangingProfession_DepositsMixedOreInventoryBeforeNewRoleWork()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 12, new GridPoint(1, 1));
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(6, 5));
        var miner = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 5), "Mixed Miner", "miner");
        Assert.Equal(2, miner.AddToInventory(ResourceName.Lumenite, 2));
        Assert.Equal(3, miner.AddToInventory(ResourceName.Malachite, 3));

        Assert.True(miner.ChangeAssignment("builder"));
        for (var tick = 0; tick < 90 && miner.HasInventory(); tick++)
        {
            miner.Move();
            cave.AdvanceCreatureMovement();
        }

        Assert.False(miner.HasInventory());
        Assert.Equal(2, post.GetStoredAmount(ResourceName.Lumenite));
        Assert.Equal(3, post.GetStoredAmount(ResourceName.Malachite));
        Assert.Equal(CreatureRole.Builder, miner.Role);
    }

    [Fact]
    public void Movement_ForOpposingTrilobites_AllowsOverlapWhilePassing()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(24, 16, new GridPoint(1, 1));
        var left = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(7, 8), "Left");
        var right = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(15, 8), "Right");
        var leftStart = left.Position;
        var rightStart = right.Position;

        Assert.True(left.NavigateTo(new GridPoint(15, 8)));
        Assert.True(right.NavigateTo(new GridPoint(7, 8)));

        var overlapped = false;
        for (var tick = 0; tick < 90 && (left.HasActiveMovement || right.HasActiveMovement); tick++)
        {
            cave.AdvanceCreatureMovement();
            var minimumDistance = left.CollisionRadius + left.SeparationPadding +
                                  right.CollisionRadius + right.SeparationPadding;
            overlapped |= (left.Position - right.Position).LengthSquared < (long)minimumDistance * minimumDistance;
        }

        Assert.True(overlapped);
        Assert.True(left.Position.X > leftStart.X);
        Assert.True(right.Position.X < rightStart.X);
    }

    [Fact(Skip = "Replaced by automatic combat-directive coverage.")]
    public void FighterAcquireTarget_KeepsTargetingEnemyThatMovesToDifferentAdjacentTile()
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

        var firstAttack = fighter.RunRoleState(FighterState.AcquireTarget);
        session.Combat.ResolveTick(session);
        session.TickCount++;
        session.Combat.ResolveTick(session);
        var healthAfterFirstAttack = enemy.Health;

        Assert.True(firstAttack);
        Assert.True(healthAfterFirstAttack < enemy.MaxHealth);
        Assert.Same(enemy, fighter.FighterTarget);

        session.TickCount += 3;
        session.Combat.ResolveTick(session);
        Assert.True(cave.PlaceCreatureOnTile(enemy, secondEnemyTile.Coordinates));

        var secondAttack = fighter.RunRoleState(FighterState.AcquireTarget);
        session.Combat.ResolveTick(session);
        session.TickCount++;
        session.Combat.ResolveTick(session);

        Assert.True(secondAttack);
        Assert.True(enemy.Health < healthAfterFirstAttack);
        Assert.Same(enemy, fighter.FighterTarget);
    }

    [Fact(Skip = "Replaced by automatic combat-directive coverage.")]
    public void FighterMoveToTarget_QueuesContinuousRouteChunkTowardEnemy()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(30, 10, new GridPoint(1, 1));
        var fighterLocation = new GridPoint(5, 5);
        var enemyLocation = new GridPoint(25, 5);
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, fighterLocation, "Fighter", "fighter");
        var enemy = new Enemy("Target", enemyLocation, session);

        Assert.True(cave.Spawn(enemy, cave.GetTile(enemyLocation)!));
        cave.RefreshBfsField("enemy");

        Assert.True(fighter.RunRoleState(FighterState.MoveToTarget));

        Assert.True(fighter.HasActiveMovement);
        Assert.NotEmpty(fighter.DesiredRoute);
        Assert.True(GridPoint.ManhattanDistance(fighterLocation, fighter.DesiredRoute[^1].ToGridPoint()) > 1);
        Assert.Equal(RouteContinuationKind.SharedBfsField, fighter.ActiveRouteContinuationKind);
    }

    [Fact]
    public void FighterMoveToTarget_PursuesEnemyWhenTargetIsInAnotherCell()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(14, 14, new GridPoint(1, 1));
        var fighterLocation = new GridPoint(6, 6);
        var enemyLocation = new GridPoint(6, 7);
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, fighterLocation, "Fighter", "fighter");
        var enemy = new Enemy("Target", enemyLocation, session);

        Assert.True(cave.Spawn(enemy, cave.GetTile(enemyLocation)!));
        cave.RefreshBfsField("enemy");

        Assert.Equal(0, cave.GetBfsFieldValue("enemy", fighterLocation));
        Assert.True(fighter.RunRoleState(FighterState.MoveToTarget));

        Assert.True(fighter.HasActiveMovement);
        Assert.Same(enemy, fighter.FighterTarget);
        Assert.False(session.Combat.HasActiveOrPending(fighter));
        Assert.Equal(CreatureActivity.Moving, fighter.Activity);
    }

    [Fact]
    public void FighterGroup_PursuesEnemyFromTargetBandUnderLoad()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 18, new GridPoint(1, 1));
        var enemyLocation = new GridPoint(9, 9);
        var enemy = new Enemy("Target", enemyLocation, session);
        Assert.True(cave.Spawn(enemy, cave.GetTile(enemyLocation)!));
        cave.RefreshBfsField("enemy");

        var attackTiles = new[]
        {
            new GridPoint(9, 8),
            new GridPoint(10, 9),
            new GridPoint(9, 10),
            new GridPoint(8, 9)
        };
        var fighters = new List<Trilobite>(16);
        for (var index = 0; index < 16; index++)
        {
            var location = attackTiles[index % attackTiles.Length];
            var fighter = TestWorldFactory.SpawnTrilobite(cave, session, location, $"Fighter {index}", "fighter");
            Assert.Equal(0, cave.GetBfsFieldValue("enemy", location));
            Assert.True(fighter.RunRoleState(FighterState.MoveToTarget));
            fighters.Add(fighter);
        }

        foreach (var fighter in fighters)
        {
            Assert.Same(enemy, fighter.FighterTarget);
            Assert.True(fighter.HasActiveMovement);
            Assert.Equal(CreatureActivity.Moving, fighter.Activity);
        }
    }

    [Fact(Skip = "Replaced by automatic combat-directive coverage.")]
    public void FighterGroup_StreamsSharedEnemyFieldWithoutIdleChunkGaps()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(42, 28, new GridPoint(1, 1));
        var enemyLocation = new GridPoint(36, 14);
        var enemy = new Enemy("Target", enemyLocation, session);
        Assert.True(cave.Spawn(enemy, cave.GetTile(enemyLocation)!));
        cave.RefreshBfsField("enemy");

        var fighters = new List<Trilobite>(20);
        for (var index = 0; index < 20; index++)
        {
            var location = new GridPoint(4, 4 + index);
            var fighter = TestWorldFactory.SpawnTrilobite(cave, session, location, $"Fighter {index}", "fighter");
            Assert.True(fighter.RunRoleState(FighterState.MoveToTarget));
            Assert.Equal(RouteContinuationKind.SharedBfsField, fighter.ActiveRouteContinuationKind);
            fighters.Add(fighter);
        }

        for (var tick = 0; tick < 24; tick++)
        {
            TickRunner.RunTick(session);
            foreach (var fighter in fighters)
            {
                Assert.NotEqual(CreatureActivity.Idle, fighter.Activity);
                Assert.NotEqual(CreatureActivity.Planning, fighter.Activity);
            }
        }
    }

    [Fact(Skip = "Legacy per-fighter route interruption is replaced by CombatWorld directives.")]
    public void FighterMove_InterruptsActiveRouteForReachableEnemy()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(30, 10, new GridPoint(1, 1));
        var fighterLocation = new GridPoint(5, 5);
        var farEnemyLocation = new GridPoint(25, 5);
        var fighter = TestWorldFactory.SpawnTrilobite(cave, session, fighterLocation, "Fighter", "fighter");
        var farEnemy = new Enemy("Far Target", farEnemyLocation, session);

        Assert.True(cave.Spawn(farEnemy, cave.GetTile(farEnemyLocation)!));
        cave.RefreshBfsField("enemy");
        Assert.True(fighter.RunRoleState(FighterState.MoveToTarget));
        Assert.True(fighter.HasActiveMovement);

        var nearEnemyLocation = new GridPoint(5, 6);
        var nearEnemy = new Enemy("Near Target", nearEnemyLocation, session);
        Assert.True(cave.Spawn(nearEnemy, cave.GetTile(nearEnemyLocation)!));

        Assert.True(fighter.Move() is true);

        Assert.False(fighter.HasActiveMovement);
        Assert.Same(nearEnemy, fighter.FighterTarget);
    }
}
