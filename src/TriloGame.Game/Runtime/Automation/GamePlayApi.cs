using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Runtime.Automation;

public sealed class GamePlayApi
{
    private readonly IGamePlayHost _host;

    public GamePlayApi(IGamePlayHost host)
    {
        _host = host;
    }

    public GamePlaySnapshot GetSnapshot()
    {
        var session = _host.Session;
        var cave = session.Cave;
        var trilobites = cave?.GetTrilobiteList()
            .Select(trilobite => new CreatureSnapshot(
                trilobite.Name,
                trilobite.Assignment,
                trilobite.Location,
                trilobite.Health,
                trilobite.MaxHealth))
            .ToArray() ?? [];
        var enemies = cave?.GetEnemyList()
            .Select(enemy => new CreatureSnapshot(
                enemy.Name,
                enemy.Assignment,
                enemy.Location,
                enemy.Health,
                enemy.MaxHealth))
            .ToArray() ?? [];
        var buildings = cave?.GetBuildingList()
            .Select(building => new BuildingSnapshot(
                building.Name,
                building.Location,
                building.Health,
                building.MaxHealth))
            .ToArray() ?? [];

        return new GamePlaySnapshot(
            session.TickCount,
            _host.IsPaused,
            _host.TickSpeedMs,
            session.Danger,
            trilobites.Length,
            enemies.Length,
            buildings.Length,
            trilobites,
            enemies,
            buildings);
    }

    public void RestartGame()
    {
        _host.RestartGame();
    }

    public void Pause()
    {
        _host.IsPaused = true;
    }

    public void Resume()
    {
        _host.IsPaused = false;
    }

    public void SetTickSpeed(double tickSpeedMs)
    {
        if (tickSpeedMs <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(tickSpeedMs));
        }

        _host.TickSpeedMs = tickSpeedMs;
    }

    public void RunTicks(int tickCount)
    {
        if (tickCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickCount));
        }

        for (var index = 0; index < tickCount; index++)
        {
            _host.RunSingleTick();
        }
    }

    public bool AssignRole(string trilobiteName, string assignment)
    {
        var trilobite = FindTrilobite(trilobiteName);
        if (trilobite is null || string.IsNullOrWhiteSpace(assignment))
        {
            return false;
        }

        if (string.Equals(trilobite.Assignment, assignment, StringComparison.Ordinal))
        {
            return true;
        }

        return trilobite.ChangeAssignment(assignment);
    }

    public bool MoveTrilobite(string trilobiteName, GridPoint destination)
    {
        var trilobite = FindTrilobite(trilobiteName);
        return trilobite is not null && trilobite.NavigateTo(destination, trilobite.GetBehavior(), clearExisting: true);
    }

    public bool SpawnTrilobite(string name, GridPoint location, string assignment = "unassigned")
    {
        var session = _host.Session;
        var cave = session.Cave;
        var tile = cave?.GetTile(location);
        if (cave is null || tile is null || !tile.CreatureFits())
        {
            return false;
        }

        var trilobite = new Trilobite(name, location, session)
        {
            Assignment = assignment
        };

        if (!cave.Spawn(trilobite, tile))
        {
            return false;
        }

        trilobite.RestartBehavior();
        return true;
    }

    public bool SpawnEnemy(GridPoint location, string? name = null)
    {
        var session = _host.Session;
        var cave = session.Cave;
        var tile = cave?.GetTile(location);
        if (cave is null || tile is null || !tile.CreatureFits())
        {
            return false;
        }

        return cave.Spawn(new Enemy(name ?? $"Api Enemy {session.Runtime.AllocateDebugEnemyId()}", location, session), tile);
    }

    public bool SpawnAntHole(GridPoint location, int antCount = 1)
    {
        if (antCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(antCount));
        }

        var cave = _host.Session.Cave;
        var tile = cave?.GetTile(location);
        return cave is not null &&
               tile is not null &&
               cave.SpawnAntHole(tile, antCount);
    }

    public bool PlaceBuilding(string buildingType, GridPoint location, int displayRotationTurns = 0)
    {
        var session = _host.Session;
        var cave = session.Cave;
        if (cave is null)
        {
            return false;
        }

        var building = CreateBuilding(session, buildingType);
        if (building is null)
        {
            return false;
        }

        building.SetDisplayRotationTurns(displayRotationTurns);
        return cave.Build(building, location);
    }

    private Trilobite? FindTrilobite(string trilobiteName)
    {
        return _host.Session.Cave?.GetTrilobiteList()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, trilobiteName, StringComparison.OrdinalIgnoreCase));
    }

    private static Building? CreateBuilding(GameSession session, string buildingType)
    {
        return buildingType.Trim().ToLowerInvariant() switch
        {
            "algaefarm" or "algae_farm" or "farm" => new AlgaeFarm(session),
            "barracks" => new Barracks(session),
            "miningpost" or "mining_post" or "mine" => new MiningPost(session),
            "radar" => new Radar(session),
            "storage" => new Storage(session),
            "smith" => new Smith(session),
            "wall" => new Wall(session),
            _ => null
        };
    }
}
