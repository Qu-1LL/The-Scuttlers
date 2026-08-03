using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Movement;

public sealed class ContinuousMovementTests
{
    [Fact]
    public void ClearCorridor_SmoothsCellPathIntoDiagonalMovement()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(15, 15, GridPoint.Zero);
        var creature = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 4));
        var start = creature.Position;

        Assert.True(creature.NavigateTo(new GridPoint(10, 10)));
        Assert.Single(creature.DesiredRoute);

        TickRunner.RunTick(session);

        Assert.True(creature.Position.X > start.X);
        Assert.True(creature.Position.Y > start.Y);
        Assert.Equal(CreatureActivity.Moving, creature.Activity);
    }

    [Fact]
    public void RouteSmoothing_IgnoresCreatureHitboxes()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 10, GridPoint.Zero);
        var mover = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 4), "Mover");
        TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(8, 4), "Blocker");

        Assert.True(mover.NavigateTo(new GridPoint(12, 4)));

        var origin = mover.Position;
        for (var index = 0; index < mover.DesiredRoute.Count; index++)
        {
            Assert.True(cave.HasClearStaticSweep(mover, origin, mover.DesiredRoute[index]));
            origin = mover.DesiredRoute[index];
        }

        Assert.Equal(WorldPoint.FromGridPoint(new GridPoint(12, 4)), mover.DesiredRoute[^1]);
    }

    [Fact]
    public void AngledRoute_KeepsMovingThroughWaypointInsteadOfStopping()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(14, 14, GridPoint.Zero);
        var creature = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 4), "Corner");
        SetWallTile(cave, new GridPoint(6, 5));
        SetWallTile(cave, new GridPoint(6, 6));
        SetWallTile(cave, new GridPoint(6, 7));

        Assert.True(creature.QueueMovePath([
            new GridPoint(4, 4),
            new GridPoint(8, 4),
            new GridPoint(8, 8)
        ]));
        Assert.True(creature.DesiredRoute.Count >= 2);

        for (var tick = 0; tick < 40 && creature.DesiredRouteIndex == 0; tick++)
        {
            TickRunner.RunTick(session);
        }

        Assert.True(creature.DesiredRouteIndex > 0);
        Assert.Equal(CreatureActivity.Moving, creature.Activity);
        Assert.True(creature.Velocity.Length > 0);
    }

    [Fact]
    public void OpposingWalkers_CanOverlapWithoutBlockingOneAnother()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(16, 8, GridPoint.Zero);
        var left = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 4), "Left");
        var right = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(12, 4), "Right");
        var leftStart = left.Position;
        var rightStart = right.Position;
        var overlapped = false;

        Assert.True(left.NavigateTo(new GridPoint(12, 4)));
        Assert.True(right.NavigateTo(new GridPoint(4, 4)));

        for (var tick = 0; tick < 30; tick++)
        {
            TickRunner.RunTick(session);
            overlapped |= BodiesOverlap(left, right);
        }

        Assert.True(overlapped);
        Assert.True(left.Position.X >= leftStart.X);
        Assert.True(right.Position.X <= rightStart.X);
    }

    [Fact]
    public void CloseOpposingWalkers_MoveThroughEachOther()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 8, GridPoint.Zero);
        var left = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(7, 4), "Priority");
        var right = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(10, 4), "Yielding");
        var leftStart = left.Position;
        var rightStart = right.Position;
        var overlapped = false;

        Assert.True(left.NavigateTo(new GridPoint(12, 4)));
        Assert.True(right.NavigateTo(new GridPoint(5, 4)));

        for (var tick = 0; tick < 40; tick++)
        {
            TickRunner.RunTick(session);
            overlapped |= BodiesOverlap(left, right);
        }

        Assert.True(overlapped);
        Assert.True(left.Position.X > leftStart.X);
        Assert.True(right.Position.X < rightStart.X);
    }

    [Fact]
    public void Walker_MovesThroughCreatureOnPath()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(20, 12, GridPoint.Zero);
        var mover = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 6), "Rerouter");
        var blocker = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(8, 6), "Obstacle");

        Assert.True(mover.NavigateTo(new GridPoint(14, 6)));

        for (var tick = 0; tick < 120 && mover.Position.X <= blocker.Position.X; tick++)
        {
            TickRunner.RunTick(session);
        }

        Assert.True(mover.Position.X > blocker.Position.X);
    }

    [Fact]
    public void LongPointRoute_StreamsChunksWithoutDroppingActiveMovement()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(96, 12, GridPoint.Zero);
        var creature = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 6), "Streamer");
        var destination = new GridPoint(88, 6);

        Assert.True(creature.NavigateTo(destination));
        Assert.Equal(RouteContinuationKind.PointDestination, creature.ActiveRouteContinuationKind);

        var sawRouteAppend = false;
        var routeCount = creature.DesiredRoute.Count;
        for (var tick = 0; tick < 240 && creature.Position.ToGridPoint() != destination; tick++)
        {
            Assert.True(creature.HasActiveMovement);
            TickRunner.RunTick(session);
            if (creature.DesiredRoute.Count > routeCount)
            {
                sawRouteAppend = true;
            }

            routeCount = Math.Max(routeCount, creature.DesiredRoute.Count);
        }

        Assert.True(sawRouteAppend);
        Assert.Equal(destination, creature.Position.ToGridPoint());
    }

    [Fact]
    public void RouteRefill_AppendsWithoutResettingCurrentMovementSegment()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(96, 12, GridPoint.Zero);
        var creature = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 6), "Appender");

        Assert.True(creature.NavigateTo(new GridPoint(88, 6)));
        cave.AdvanceCreatureMovement();

        var targetBefore = creature.MovementTarget;
        var routeIndexBefore = creature.DesiredRouteIndex;
        var velocityBefore = creature.Velocity;
        var routeCountBefore = creature.DesiredRoute.Count;

        Assert.True(creature.TryAppendRouteContinuation(force: true));

        Assert.Equal(targetBefore, creature.MovementTarget);
        Assert.Equal(routeIndexBefore, creature.DesiredRouteIndex);
        Assert.Equal(velocityBefore, creature.Velocity);
        Assert.True(creature.DesiredRoute.Count > routeCountBefore);
    }

    [Fact]
    public void ExplicitImpulse_DoesNotPropagateThroughContactChain()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(16, 8, GridPoint.Zero);
        var first = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(4, 4), "First");
        var second = TestWorldFactory.SpawnTrilobite(cave, session, new GridPoint(6, 4), "Second");
        var secondStart = second.Position;

        first.ApplyImpulse(new WorldVector(WorldUnits.UnitsPerTile * 4, 0), sourceId: 99);
        for (var tick = 0; tick < 12; tick++)
        {
            TickRunner.RunTick(session);
        }

        Assert.Equal(secondStart, second.Position);
    }

    [Fact]
    public void PointRouteBudget_DefersExcessRequestsWithoutDroppingExactDestinations()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(96, 12, GridPoint.Zero);
        var creatures = new List<Trilobite>();
        for (var index = 0; index < Cave.MaximumPointRouteBuildsPerTick + 8; index++)
        {
            var start = new GridPoint(4 + (index * 2), 4);
            var creature = TestWorldFactory.SpawnTrilobite(cave, session, start, $"Budget {index}");
            var exactTarget = WorldPoint.FromGridPoint(new GridPoint(start.X, 8)) + new WorldVector(3, 5);
            Assert.True(creature.NavigateTo(exactTarget));
            creatures.Add(creature);
        }

        Assert.Equal(Cave.MaximumPointRouteBuildsPerTick, creatures.Count(creature => creature.HasActiveMovement));
        Assert.Equal(8, creatures.Count(creature => creature.Activity == CreatureActivity.Planning));

        TickRunner.RunTick(session);

        Assert.All(creatures, creature => Assert.True(creature.HasActiveMovement));
        for (var index = Cave.MaximumPointRouteBuildsPerTick; index < creatures.Count; index++)
        {
            var expected = WorldPoint.FromGridPoint(new GridPoint(4 + (index * 2), 8)) + new WorldVector(3, 5);
            Assert.Equal(expected, creatures[index].DesiredRoute[^1]);
        }
    }

    private static bool BodiesOverlap(Creature left, Creature right)
    {
        var required = left.CollisionRadius + left.SeparationPadding +
                       right.CollisionRadius + right.SeparationPadding;
        return (left.Position - right.Position).LengthSquared < (long)required * required;
    }

    private static void SetWallTile(Cave cave, GridPoint location)
    {
        var tile = cave.GetTile(location)
            ?? throw new InvalidOperationException($"Expected tile at {location}.");
        tile.SetBase("wall");
        tile.CreatureCanFit = false;
        tile.ConfigureWall(1);
    }
}
