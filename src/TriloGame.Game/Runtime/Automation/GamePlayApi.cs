using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Runtime.Automation;

public sealed class GamePlayApi
{
    private readonly IGamePlayHost _host;

    // Hold the live host abstraction that backs automation commands.
    public GamePlayApi(IGamePlayHost host)
    {
        _host = host;
    }

    // Capture a lightweight runtime snapshot for automation and inspection flows.
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

    // Rebuild the host's game session from scratch.
    public void RestartGame()
    {
        _host.RestartGame();
    }

    // Pause simulation advancement through the host clock.
    public void Pause()
    {
        _host.IsPaused = true;
    }

    // Resume simulation advancement through the host clock.
    public void Resume()
    {
        _host.IsPaused = false;
    }

    // Override the live fixed-tick cadence used by the runtime clock.
    public void SetTickSpeed(double tickSpeedMs)
    {
        if (tickSpeedMs <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(tickSpeedMs));
        }

        _host.TickSpeedMs = tickSpeedMs;
    }

    // Advance the simulation by an exact number of ticks through the host seam.
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

    // Change one trilobite's assignment when the target and role input are valid.
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

    // Queue movement for one named trilobite using its existing behavior as fallback.
    public bool MoveTrilobite(string trilobiteName, GridPoint destination)
    {
        var trilobite = FindTrilobite(trilobiteName);
        return trilobite is not null && trilobite.NavigateTo(destination, trilobite.GetBehavior(), clearExisting: true);
    }

    // Spawn a new trilobite onto a legal tile and immediately start its role behavior.
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

    // Spawn a debug enemy onto a legal tile, assigning a generated name when needed.
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

    // Spawn an ant hole at a specific tile when the request count is valid.
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

    // Build one recognized structure type at the requested location and display rotation.
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

        var normalizedRotationTurns = ((displayRotationTurns % 4) + 4) % 4;
        for (var turn = 0; turn < normalizedRotationTurns; turn++)
        {
            building.RotateMap();
        }

        building.SetDisplayRotationTurns(normalizedRotationTurns);
        return cave.Build(building, location, preserveReachability: true);
    }

    // Resolve a trilobite by name through the live cave state.
    private Trilobite? FindTrilobite(string trilobiteName)
    {
        return _host.Session.Cave?.GetTrilobiteList()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, trilobiteName, StringComparison.OrdinalIgnoreCase));
    }

    // Map automation-friendly building aliases onto concrete building instances.
    private static Building? CreateBuilding(GameSession session, string buildingType)
    {
        return buildingType.Trim().ToLowerInvariant() switch
        {
            "algaefarm" or "algae_farm" or "farm" => new AlgaeFarm(session),
            "barracks" => new Barracks(session),
            "garage" => new Garage(session),
            "miningpost" or "mining_post" or "mine" => new MiningPost(session),
            "radar" => new Radar(session),
            "silo" => new Silo(session),
            "soilpatch" or "soil_patch" or "soil" => new SoilPatch(session),
            "storage" => new Storage(session),
            "smith" => new Smith(session),
            "wall" => new Wall(session),
            _ => null
        };
    }
}
