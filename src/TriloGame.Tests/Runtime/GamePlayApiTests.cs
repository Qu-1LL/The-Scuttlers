using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Runtime.Automation;
using TriloGame.Game.Runtime.Bootstrap;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Runtime;

public sealed class GamePlayApiTests
{
    [Fact]
    public void AssignRole_ChangesMatchingTrilobiteAssignment()
    {
        var host = new FakeGamePlayHost();
        var api = new GamePlayApi(host);

        var changed = api.AssignRole("Jeffery", "fighter");

        Assert.True(changed);
        Assert.Equal("fighter", host.Session.Cave!.GetTrilobiteList().Single(trilobite => trilobite.Name == "Jeffery").Assignment);
    }

    [Fact]
    public void PlaceBuilding_BuildsRequestedBuildingAtLocation()
    {
        var host = new FakeGamePlayHost();
        var api = new GamePlayApi(host);
        var cave = host.Session.Cave!;
        var location = TestWorldFactory.FindBuildLocation(cave, new AlgaeFarm(host.Session));

        var built = api.PlaceBuilding("algaefarm", location);

        Assert.True(built);
        Assert.Contains(cave.GetBuildingList(), building => building is AlgaeFarm && building.Location == location);
    }

    [Fact]
    public void RunTicks_AdvancesLiveSnapshot()
    {
        var host = new FakeGamePlayHost();
        var api = new GamePlayApi(host);

        api.RunTicks(3);
        var snapshot = api.GetSnapshot();

        Assert.Equal(3, snapshot.TickCount);
        Assert.Equal(host.Session.Cave!.GetTrilobiteList().Count, snapshot.TrilobiteCount);
        Assert.Equal(host.Session.Cave.GetBuildingList().Count, snapshot.BuildingCount);
    }

    [Fact]
    public void SpawnAntHole_FighterKillsAntAndTicksContinue()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        cave.RevealCave();
        session.Runtime.DisableEnemySpawns = true;
        var host = new FakeGamePlayHost(session);
        var api = new GamePlayApi(host);

        var holeTile = cave.GetTiles()
            .Where(tile =>
                cave.IsTileRevealed(tile) &&
                cave.CanPlaceAntHole(tile) &&
                cave.PreviewAntHoleSpawnTiles(tile, 1).Count > 0 &&
                tile.Neighbors.Any(neighbor => neighbor.CreatureFits() && string.Equals(neighbor.Base, "empty", StringComparison.Ordinal)))
            .OrderBy(tile => GridPoint.ManhattanDistance(tile.Coordinates, GridPoint.Zero))
            .First();

        Assert.True(api.SpawnAntHole(holeTile.Coordinates));

        var hole = cave.GetAntHoles().Single();
        var ant = hole.Ants.Single();
        var antTile = cave.GetTile(ant.Location)!;
        cave.RevealTile(antTile);
        cave.RefreshDangerState();

        var fighterTile = antTile.Neighbors.FirstOrDefault(tile =>
                              tile.CreatureFits() &&
                              tile.Trilobites.Count == 0 &&
                              tile.EnemyOccupant is null &&
                              !cave.HasBlockingSurfaceFeature(tile))
                          ?? throw new InvalidOperationException("Could not find a clear tile beside the spawned ant.");

        Assert.True(api.SpawnTrilobite("Api Fighter", fighterTile.Coordinates, "fighter"));

        var tickLimit = 24;
        while (api.GetSnapshot().EnemyCount > 0 && tickLimit-- > 0)
        {
            api.RunTicks(1);
        }

        var afterKill = api.GetSnapshot();
        Assert.Equal(0, afterKill.EnemyCount);
        Assert.Empty(cave.GetAntHoles());

        var tickCountAfterKill = afterKill.TickCount;
        api.RunTicks(5);
        var afterFollowup = api.GetSnapshot();

        Assert.Equal(tickCountAfterKill + 5, afterFollowup.TickCount);
        Assert.False(afterFollowup.Danger);
    }

    private sealed class FakeGamePlayHost : IGamePlayHost
    {
        private readonly GameSessionBootstrapper _bootstrapper = new();
        private readonly GameSimulationClockSystem _clock = new();

        public FakeGamePlayHost()
        {
            var bootstrap = _bootstrapper.CreateNewGame();
            Session = bootstrap.Session;
            _clock.ResetToDefaults();
        }

        public FakeGamePlayHost(GameSession session)
        {
            Session = session;
            _clock.ResetToDefaults();
        }

        public GameSession Session { get; private set; }

        public bool IsPaused
        {
            get => _clock.IsPaused;
            set => _clock.IsPaused = value;
        }

        public double TickSpeedMs
        {
            get => _clock.TickSpeedMs;
            set => _clock.TickSpeedMs = value;
        }

        public void RestartGame()
        {
            Session = _bootstrapper.CreateNewGame().Session;
            _clock.ResetToDefaults();
        }

        public void RunSingleTick()
        {
            _clock.RunSingleTick(Session);
        }
    }
}
