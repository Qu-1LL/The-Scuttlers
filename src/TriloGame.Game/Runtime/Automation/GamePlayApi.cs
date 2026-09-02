using System.Numerics;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Combat;
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
            .Select(CreateCreatureSnapshot)
            .ToArray() ?? [];
        var enemies = cave?.GetEnemyList()
            .Select(CreateCreatureSnapshot)
            .ToArray() ?? [];
        var buildings = cave?.GetBuildingList()
            .Select(building => new BuildingSnapshot(
                building.Name,
                building.Location,
                building.Health,
                building.MaxHealth))
            .ToArray() ?? [];

        var directives = session.Combat.Directives.Values
            .OrderBy(directive => directive.FighterId)
            .Select(directive => new CombatDirectiveSnapshot(
                directive.FighterId,
                directive.SectorId,
                directive.Kind,
                directive.Destination.ToWorldPixels(),
                directive.TargetId,
                directive.AssignmentVersion))
            .ToArray();
        var hitboxes = session.Combat.ActiveHitboxes
            .Select(hitbox => new CombatHitboxSnapshot(
                hitbox.Id,
                hitbox.SourceId,
                hitbox.AttackInstanceId,
                hitbox.Shape.Kind,
                hitbox.Shape.First.ToWorldPixels(),
                hitbox.Shape.Second.ToWorldPixels(),
                hitbox.Shape.Radius / (float)WorldUnits.UnitsPerPixel,
                hitbox.ActiveFromTick,
                hitbox.ActiveUntilTick,
                hitbox.Damage,
                hitbox.MaximumTargetCount))
            .ToArray();
        var hurtboxes = session.Combat.Hurtboxes
            .Select(hurtbox => new CombatHurtboxSnapshot(
                hurtbox.Id,
                hurtbox.EntityId,
                hurtbox.Shape.Kind,
                hurtbox.Shape.First.ToWorldPixels(),
                hurtbox.Shape.Second.ToWorldPixels(),
                hurtbox.Shape.Radius / (float)WorldUnits.UnitsPerPixel,
                (int)hurtbox.Faction))
            .ToArray();
        var hits = session.Combat.RecentHitEvents
            .Select(hit => new CombatHitEventSnapshot(hit.Tick, hit.HitboxId, hit.AttackInstanceId, hit.SourceId, hit.Target.Id, hit.Damage))
            .ToArray();

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
            buildings,
            session.Combat.LastDirectivePlan,
            directives,
            hitboxes,
            hurtboxes,
            hits);
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

    // Queue movement for one named trilobite; its typed role task resumes afterward.
    public bool MoveTrilobite(string trilobiteName, GridPoint destination)
    {
        return MoveTrilobite(trilobiteName, WorldPoint.FromGridPoint(destination).ToWorldPixels());
    }

    // Queue movement to an exact continuous world position.
    public bool MoveTrilobite(string trilobiteName, Vector2 destination)
    {
        var trilobite = FindTrilobite(trilobiteName);
        return trilobite is not null && trilobite.NavigateTo(
            WorldPoint.FromWorldPixels(destination),
            clearExisting: true);
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

    private static CreatureSnapshot CreateCreatureSnapshot(Creature creature)
    {
        var route = new Vector2[Math.Max(0, creature.DesiredRoute.Count - creature.DesiredRouteIndex)];
        for (var index = creature.DesiredRouteIndex; index < creature.DesiredRoute.Count; index++)
        {
            route[index - creature.DesiredRouteIndex] = creature.DesiredRoute[index].ToWorldPixels();
        }

        var activeHurtbox = creature.Session.Combat.GetActiveFor(creature);
        var facing = new Vector2(creature.FacingDirection.X, creature.FacingDirection.Y);
        if (facing != Vector2.Zero)
        {
            facing = Vector2.Normalize(facing);
        }

        var combatTargetId = activeHurtbox?.PreferredTarget?.Id ?? creature switch
        {
            Trilobite trilobite => trilobite.FighterTarget?.Id,
            Enemy enemy => enemy.EnemyTarget?.Id,
            _ => null
        };
        var hurtboxSnapshot = activeHurtbox is null
            ? null
            : new CombatHitboxSnapshot(
                activeHurtbox.Id,
                activeHurtbox.SourceId,
                activeHurtbox.AttackInstanceId,
                activeHurtbox.Shape.Kind,
                activeHurtbox.Shape.First.ToWorldPixels(),
                activeHurtbox.Shape.Second.ToWorldPixels(),
                activeHurtbox.Shape.Radius / (float)WorldUnits.UnitsPerPixel,
                activeHurtbox.ActiveFromTick,
                activeHurtbox.ActiveUntilTick,
                activeHurtbox.Damage,
                activeHurtbox.MaximumTargetCount);
        return new CreatureSnapshot(
            creature.Id,
            creature.Name,
            creature.Assignment,
            creature.Location,
            creature.Health,
            creature.MaxHealth,
            creature.Position.ToWorldPixels(),
            creature.CurrentCell,
            creature.Role,
            creature.Activity,
            creature.CollisionRadius / (float)WorldUnits.UnitsPerPixel,
            new Vector2(
                creature.Velocity.X / (float)WorldUnits.UnitsPerPixel,
                creature.Velocity.Y / (float)WorldUnits.UnitsPerPixel),
            facing,
            creature.MovementCohort,
            creature.IdleDestination?.ToWorldPixels(),
            creature.IdleRestTicks,
            route,
            hurtboxSnapshot,
            combatTargetId,
            (creature as Trilobite)?.ActiveMiningClaim,
            creature.DamageFlashSequence,
            (creature as Trilobite)?.FighterState,
            (creature as Enemy)?.CombatState);
    }

    // Map automation-friendly building aliases onto concrete building instances.
    private static Building? CreateBuilding(GameSession session, string buildingType)
    {
        return buildingType.Trim().ToLowerInvariant() switch
        {
            "algaefarm" or "algae_farm" or "farm" => new AlgaeFarm(session),
            "bakery" => new Bakery(session),
            "barracks" => new Barracks(session),
            "garage" => new Garage(session),
            "grindingmill" or "grinding_mill" or "mill" => new GrindingMill(session),
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
