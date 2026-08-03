using TriloGame.Game.Audio;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Vehicles;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Combat;

// Owns all combat commands, spatial lookup, hitbox lifetime, and deterministic damage application.
public sealed class CombatWorld
{
    // Structures need the original half-cell reach because their blocked tile cannot
    // be entered; creature-vs-creature combat uses the centered body hitbox below.
    private const int StructureMeleeRadius = WorldUnits.UnitsPerTile / 2;
    private const int MaxMeleeKnockbackImpulse = 64 * WorldUnits.UnitsPerPixel;
    private const int MeleeReachSafetyMargin = 8 * WorldUnits.UnitsPerPixel;
    private const int HostileTargetAcquireRadius = WorldUnits.UnitsPerTile * 4;
    private readonly List<CombatHitbox> _activeHitboxes = [];
    private readonly List<CombatHurtbox> _hurtboxes = [];
    private readonly List<CombatHitEvent> _recentHitEvents = [];
    private readonly CombatCommandBuffer _commands = new();
    private readonly CombatSpatialGrid _grid = new();
    private readonly Dictionary<int, CombatDirective> _directives = [];
    private readonly List<Enemy> _enemyCandidates = [];
    private readonly List<Enemy> _assignmentEnemies = [];
    private readonly List<int> _assignmentLoads = [];
    private readonly List<Trilobite> _fighterCandidates = [];
    private readonly List<SectorScore> _sectors = [];
    private readonly List<CombatHurtbox> _hitCandidates = [];
    private int _nextHitboxId = 1;
    private int _nextAttackInstanceId = 1;
    private int _assignmentVersion;
    private int _lastDirectorTick = -1;
    private int _targetGridTick = -1;
    private bool _directorDirty = true;

    public IReadOnlyList<CombatHitbox> ActiveHitboxes => _activeHitboxes;
    public IReadOnlyList<CombatHurtbox> Hurtboxes => _hurtboxes;
    public IReadOnlyList<CombatHitEvent> RecentHitEvents => _recentHitEvents;
    public IReadOnlyDictionary<int, CombatDirective> Directives => _directives;
    public CombatCommandBuffer CommandBuffer => _commands;
    public CombatDiagnosticsSnapshot LastDiagnostics { get; private set; }
    public CombatDirectivePlanSnapshot LastDirectivePlan { get; private set; }

    public bool HasActiveOrPending(Creature source)
    {
        for (var index = 0; index < _activeHitboxes.Count; index++)
        {
            if (_activeHitboxes[index].SourceId == source.Id)
            {
                return true;
            }
        }

        var commands = _commands.Commands;
        for (var index = 0; index < commands.Count; index++)
        {
            if (commands[index].SourceId == source.Id)
            {
                return true;
            }
        }

        return false;
    }

    public CombatHitbox? GetActiveFor(Creature source)
    {
        for (var index = 0; index < _activeHitboxes.Count; index++)
        {
            if (_activeHitboxes[index].SourceId == source.Id)
            {
                return _activeHitboxes[index];
            }
        }

        return null;
    }

    // Queue a melee volume; it will not inspect or mutate targets until the combat phase.
    public bool TryQueueMelee(Creature source, CombatTargetRef target)
    {
        if (source.Health <= 0 || source.Cave is null || HasActiveOrPending(source) ||
            !IsTargetAlive(target, source.Cave) || !IsHostile(source, target))
        {
            return false;
        }

        var hitbox = BuildMeleeShape(source, target);
        if (!hitbox.Intersects(GetTargetShape(target)))
        {
            return false;
        }

        _commands.AddAttack(
            source.Session.TickCount,
            source,
            target,
            hitbox,
            CombatAttackProfile.Melee(source));
        source.SetActivity(CreatureActivity.Fighting);
        return true;
    }

    public void SubmitProjectileImpact(Creature source, Creature target, int damage)
    {
        if (source.Health <= 0 || target.Health <= 0 || source.Cave is null ||
            !ReferenceEquals(source.Cave, target.Cave) || !IsHostile(source, CombatTargetRef.For(target)))
        {
            return;
        }

        _commands.AddProjectileImpact(source.Session.TickCount, source, target, damage, WorldVector.Zero);
    }

    // Explosions are typed area commands. They may originate during death cleanup,
    // after the source has reached zero health, but still resolve through hurtboxes.
    public void SubmitExplosion(Creature source, WorldPoint center, int radius, int damage)
    {
        if (source.Cave is null || radius <= 0 || damage <= 0)
        {
            return;
        }

        _commands.AddExplosion(
            source.Session.TickCount,
            source,
            CombatShape.Circle(center, radius),
            damage);
    }

    public void SubmitMovementDirective(Creature source, WorldPoint destination) =>
        _commands.AddMovementDirective(source.Session.TickCount, source, destination, CombatDirectiveKind.Advance);

    public void SubmitBreach(Creature source, WorldPoint destination) =>
        _commands.AddBreach(source.Session.TickCount, source, destination);

    public void SubmitRetreat(Creature source, WorldPoint destination) =>
        _commands.AddRetreat(source.Session.TickCount, source, destination);

    public static bool CanMeleeReach(Creature source, CombatTargetRef target)
    {
        if (source.Cave is null || !IsTargetAlive(target, source.Cave))
        {
            return false;
        }

        return BuildMeleeShape(source, target).Intersects(GetTargetShape(target));
    }

    internal static bool IsWithinMaximumMeleeDistance(Creature source, Creature target)
    {
        var maximumDistance = GetCombatRadius(source) + GetCombatRadius(target);
        return (source.Position - target.Position).LengthSquared <= (long)maximumDistance * maximumDistance;
    }

    // Stop attackers at the edge of the combat body instead of routing them through the target center.
    internal static WorldPoint GetMeleeEngagementPoint(Creature source, Creature target)
    {
        var direction = source.Position - target.Position;
        if (direction.IsZero)
        {
            direction = source.FacingDirection;
            if (direction.IsZero)
            {
                direction = new WorldVector(WorldUnits.UnitsPerPixel, 0);
            }
        }

        var standOffDistance = GetCombatRadius(source) + GetCombatRadius(target);
        return target.Position + direction.WithMagnitude(standOffDistance);
    }

    public void BeginTick(Cave cave)
    {
        LastDiagnostics = new CombatDiagnosticsSnapshot(cave.Session.TickCount, 0, 0, 0, 0, 0, 0);
        if (_directorDirty || _lastDirectorTick != cave.Session.TickCount)
        {
            RebuildDirectives(cave);
            _directorDirty = false;
            _lastDirectorTick = cave.Session.TickCount;
        }

        // Target acquisition and hit resolution share the same current-pose hurtbox grid.
        if (_targetGridTick != cave.Session.TickCount)
        {
            BuildHurtboxes(cave);
            RebuildHurtboxGrid();
            _targetGridTick = cave.Session.TickCount;
        }
        LastDirectivePlan = new CombatDirectivePlanSnapshot(cave.Session.TickCount, _fighterCandidates.Count, _sectors.Count, _sectors.Count, _directives.Count);
    }

    public void PrepareForCombatDecisions(Cave cave) => BeginTick(cave);

    public void MarkSpatialDirty()
    {
        _directorDirty = true;
        _targetGridTick = -1;
    }

    // Movement changes hurtbox positions without changing threat assignments.
    internal void MarkTargetSpatialDirty() => _targetGridTick = -1;

    internal void RecordFighterIntent(Trilobite fighter, CombatActorIntent intent)
    {
        LastDiagnostics = LastDiagnostics with { FighterIntentCount = LastDiagnostics.FighterIntentCount + 1 };
    }

    internal void RecordEnemyIntent(Enemy enemy, CombatActorIntent intent)
    {
        LastDiagnostics = LastDiagnostics with { EnemyIntentCount = LastDiagnostics.EnemyIntentCount + 1 };
    }

    internal bool RecoverEnemy(Enemy enemy, CombatNoOpReason reason)
    {
        enemy.SetActivity(CreatureActivity.Fighting);
        LastDiagnostics = LastDiagnostics with { RecoverIntentCount = LastDiagnostics.RecoverIntentCount + 1 };
        return true;
    }

    public Enemy? FindReachableEnemy(Trilobite source)
    {
        if (source.Cave is not { } cave)
        {
            return null;
        }

        BeginTick(cave);
        _enemyCandidates.Clear();
        var radius = source.CollisionRadius + (WorldUnits.UnitsPerTile * 2);
        for (var index = 0; index < cave.GetEnemyList().Count; index++)
        {
            var enemy = cave.GetEnemyList()[index];
            if (enemy.Health <= 0 || !IsWithinMaximumMeleeDistance(source, enemy) ||
                !CanMeleeReach(source, CombatTargetRef.For(enemy)))
            {
                continue;
            }

            if ((enemy.Position - source.Position).Length > radius)
            {
                continue;
            }

            _enemyCandidates.Add(enemy);
        }

        Enemy? best = null;
        var bestDistance = long.MaxValue;
        for (var index = 0; index < _enemyCandidates.Count; index++)
        {
            var enemy = _enemyCandidates[index];
            var distance = (enemy.Position - source.Position).LengthSquared;
            if (best is null || distance < bestDistance || (distance == bestDistance && enemy.Id < best.Id))
            {
                best = enemy;
                bestDistance = distance;
            }
        }

        return best;
    }

    // Acquire a nearby colony target from live combat geometry instead of coarse tile occupancy.
    public CombatTargetRef? FindNearestHostileTarget(Enemy source)
    {
        if (source.Cave is not { } cave)
        {
            return null;
        }

        BeginTick(cave);
        _grid.Query(CombatShape.Circle(source.Position, source.CollisionRadius + HostileTargetAcquireRadius));
        CombatTargetRef? best = null;
        var bestDistance = long.MaxValue;
        var candidates = _grid.Results;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if ((candidate.Faction & CombatFactionMask.Colony) == 0 ||
                candidate.Target is not Trilobite ||
                !IsAlive(candidate.Target, cave))
            {
                continue;
            }

            var distance = (GetTargetCenter(ToTargetRef(candidate.Target)) - source.Position).LengthSquared;
            if (distance > (long)(source.CollisionRadius + HostileTargetAcquireRadius) *
                (source.CollisionRadius + HostileTargetAcquireRadius))
            {
                continue;
            }

            var entityId = candidate.EntityId;
            if (best.HasValue &&
                (distance > bestDistance ||
                 (distance == bestDistance && entityId >= best.Value.Id)))
            {
                continue;
            }

            best = ToTargetRef(candidate.Target);
            bestDistance = distance;
        }

        return best;
    }

    // Resolve the director's local assignment before considering nearby unassigned threats.
    public Enemy? FindDirectedEnemy(Trilobite source)
    {
        if (source.Cave is not { } cave || !TryGetDirective(source.Id, out var directive)) return null;
        var enemies = cave.GetEnemyList();
        for (var index = 0; index < enemies.Count; index++)
        {
            var enemy = enemies[index];
            if (enemy.Id == directive.TargetId && enemy.Health > 0 && IsWithinMaximumMeleeDistance(source, enemy)) return enemy;
        }
        Enemy? nearest = null;
        var bestDistance = long.MaxValue;
        for (var index = 0; index < enemies.Count; index++)
        {
            var enemy = enemies[index];
            if (enemy.Health <= 0 || SectorId(enemy.CurrentCell) != directive.SectorId) continue;
            var distance = (enemy.Position - source.Position).LengthSquared;
            if (distance < bestDistance || (distance == bestDistance && (nearest is null || enemy.Id < nearest.Id))) { nearest = enemy; bestDistance = distance; }
        }
        return nearest;
    }

    // Return the director's live assignment without substituting a stale sector target.
    internal Enemy? FindLiveDirectedEnemy(Trilobite source)
    {
        if (source.Cave is not { } cave || !TryGetDirective(source.Id, out var directive) || directive.TargetId == 0)
        {
            return null;
        }

        var enemies = cave.GetEnemyList();
        for (var index = 0; index < enemies.Count; index++)
        {
            var enemy = enemies[index];
            if (enemy.Id == directive.TargetId && enemy.Health > 0 && ReferenceEquals(enemy.Cave, cave))
            {
                return enemy;
            }
        }

        return null;
    }

    public bool TryGetDirective(int fighterId, out CombatDirective directive) => _directives.TryGetValue(fighterId, out directive);

    public void RemoveFor(Creature source)
    {
        for (var index = _activeHitboxes.Count - 1; index >= 0; index--)
        {
            if (_activeHitboxes[index].SourceId == source.Id)
            {
                _activeHitboxes.RemoveAt(index);
            }
        }

        _commands.RemoveFor(source);
        _directives.Remove(source.Id);
        _directorDirty = true;
    }

    // Resolve mature hitboxes against post-movement hurtboxes, then apply events in stable order.
    public void ResolveTick(GameSession session)
    {
        _recentHitEvents.Clear();
        BuildHurtboxes(session.Cave);
        _grid.Clear();
        for (var index = 0; index < _hurtboxes.Count; index++)
        {
            _grid.Add(_hurtboxes[index]);
        }
        _targetGridTick = -1;

        _commands.Sort();
        SpawnCommands(session);
        for (var hitboxIndex = 0; hitboxIndex < _activeHitboxes.Count; hitboxIndex++)
        {
            var hitbox = _activeHitboxes[hitboxIndex];
            if (hitbox.Source.Health <= 0 || hitbox.Source.Cave is null)
            {
                continue;
            }

            if (!hitbox.Resolved && session.TickCount >= hitbox.ActiveFromTick)
            {
                ResolveHitbox(session, hitbox);
                hitbox.Resolved = true;
            }
        }

        ApplyHitEvents(session);
        for (var index = _activeHitboxes.Count - 1; index >= 0; index--)
        {
            if (session.TickCount >= _activeHitboxes[index].ActiveUntilTick)
            {
                _activeHitboxes.RemoveAt(index);
            }
        }
    }

    // Death cleanup can submit an explosion after the normal combat phase. Resolve only
    // those commands now while preserving the rest of the fixed-tick command batch.
    public void ResolveImmediateExplosions(GameSession session)
    {
        _recentHitEvents.Clear();
        BuildHurtboxes(session.Cave);
        _grid.Clear();
        for (var index = 0; index < _hurtboxes.Count; index++)
        {
            _grid.Add(_hurtboxes[index]);
        }
        _targetGridTick = -1;

        _commands.Sort();
        for (var index = _commands.Commands.Count - 1; index >= 0; index--)
        {
            var command = _commands.Commands[index];
            if (command.Kind != CombatCommandKind.Explosion || command.Tick > session.TickCount)
            {
                continue;
            }

            var hitbox = CreateHitbox(command);
            ResolveHitbox(session, hitbox);
            _commands.RemoveAt(index);
        }

        ApplyHitEvents(session);
    }

    private void SpawnCommands(GameSession session)
    {
        var commands = _commands.Commands;
        for (var index = 0; index < commands.Count; index++)
        {
            var command = commands[index];
            if (command.Kind is not (CombatCommandKind.Attack or CombatCommandKind.ProjectileImpact) ||
                command.Source.Health <= 0 || command.Source.Cave is null || command.Tick > session.TickCount)
            {
                continue;
            }

            _activeHitboxes.Add(CreateHitbox(command));
        }

        _commands.Clear();
    }

    private CombatHitbox CreateHitbox(CombatCommand command)
    {
        return new CombatHitbox
        {
            Id = _nextHitboxId++,
            SourceId = command.SourceId,
            Source = command.Source,
            AttackInstanceId = _nextAttackInstanceId++,
            Shape = command.Shape,
            TargetMask = command.Profile.TargetMask,
            ActiveFromTick = command.Tick + command.Profile.WindupTicks,
            ActiveUntilTick = command.Tick + command.Profile.WindupTicks + command.Profile.ActiveTicks + command.Profile.RecoveryTicks,
            Damage = command.Profile.Damage,
            Knockback = command.Profile.Knockback,
            MaximumTargetCount = Math.Max(1, command.Profile.MaximumTargetCount),
            PreferredTarget = command.Target
        };
    }

    private void ResolveHitbox(GameSession session, CombatHitbox hitbox)
    {
        _grid.Query(hitbox.Shape);
        _hitCandidates.Clear();
        var candidates = _grid.Results;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (candidate.Target is not object || hitbox.HitTargetIds.Contains(candidate.EntityId) ||
                (candidate.Faction & hitbox.TargetMask) == 0 || !IsAlive(candidate.Target, hitbox.Source.Cave) ||
                !hitbox.Shape.Intersects(candidate.Shape))
            {
                continue;
            }

            _hitCandidates.Add(candidate);
        }

        for (var index = 0; index < _hitCandidates.Count && hitbox.HitTargetIds.Count < hitbox.MaximumTargetCount; index++)
        {
            var candidate = _hitCandidates[index];
            if (hitbox.PreferredTarget is { } preferred && !ReferenceEquals(preferred.Target, candidate.Target))
            {
                continue;
            }

            hitbox.HitTargetIds.Add(candidate.EntityId);
            _recentHitEvents.Add(new CombatHitEvent(
                session.TickCount,
                hitbox.Id,
                hitbox.AttackInstanceId,
                hitbox.SourceId,
                ToTargetRef(candidate.Target),
                hitbox.Damage,
                hitbox.Knockback));
        }

        if (hitbox.PreferredTarget is not null && _recentHitEvents.Count == 0)
        {
            // A projectile keeps its target lock, but still has to pass the live hurtbox/faction checks.
            return;
        }
    }

    private static void ApplyHitEvents(GameSession session)
    {
        // The caller's list is already ordered by hitbox id and grid entity id.
        var events = session.Combat.RecentHitEvents;
        for (var index = 0; index < events.Count; index++)
        {
            var hit = events[index];
            if (!IsAlive(hit.Target.Target, session.Cave))
            {
                continue;
            }

            switch (hit.Target.Target)
            {
                case Creature creature:
                    if (creature.TakeDamage(hit.Damage, FindSource(session, hit.SourceId)) > 0)
                    {
                        var source = FindSource(session, hit.SourceId);
                        if (source is not null)
                        {
                            ApplyKnockback(creature, source);
                        }
                    }
                    break;
                case Building building:
                    building.TakeDamage(hit.Damage, FindSource(session, hit.SourceId));
                    break;
                case IVehicle vehicle:
                    vehicle.TakeDamage(hit.Damage, FindSource(session, hit.SourceId));
                    break;
            }

            session.RequestAudioCueOncePerTick(GameAudioCue.HitAffect, GetTargetCenter(hit.Target), AudioCueRequest.CreatureEffectFootprintTiles);
        }
    }

    private static Creature? FindSource(GameSession session, int sourceId)
    {
        var cave = session.Cave;
        if (cave is null) return null;
        var trilobites = cave.GetTrilobiteList();
        for (var index = 0; index < trilobites.Count; index++) if (trilobites[index].Id == sourceId) return trilobites[index];
        var enemies = cave.GetEnemyList();
        for (var index = 0; index < enemies.Count; index++) if (enemies[index].Id == sourceId) return enemies[index];
        return null;
    }

    private static void ApplyKnockback(Creature target, Creature source)
    {
        var direction = target.Position - source.Position;
        if (direction.IsZero) direction = source.FacingDirection;
        var maximumDistance = GetCombatRadius(source) + GetCombatRadius(target) - MeleeReachSafetyMargin;
        var available = Math.Max(0, maximumDistance - direction.Length) / 2;
        var magnitude = Math.Min(MaxMeleeKnockbackImpulse, available);
        if (magnitude > 0) target.ApplyImpulse(direction.WithMagnitude(magnitude), source.Id);
    }

    private void BuildHurtboxes(Cave? cave)
    {
        _hurtboxes.Clear();
        if (cave is null) return;
        var id = 1;
        var trilobites = cave.GetTrilobiteList();
        for (var index = 0; index < trilobites.Count; index++)
        {
            var creature = trilobites[index];
            if (creature.Health > 0) _hurtboxes.Add(new CombatHurtbox { Id = id++, Target = creature, Shape = GetCreatureHitbox(creature), Faction = CombatFactionMask.Colony });
        }
        var enemies = cave.GetEnemyList();
        for (var index = 0; index < enemies.Count; index++)
        {
            var creature = enemies[index];
            if (creature.Health > 0) _hurtboxes.Add(new CombatHurtbox { Id = id++, Target = creature, Shape = GetCreatureHitbox(creature), Faction = CombatFactionMask.Hostile });
        }
        var buildings = cave.GetBuildingList();
        for (var index = 0; index < buildings.Count; index++)
        {
            var building = buildings[index];
            if (building.Health > 0) _hurtboxes.Add(new CombatHurtbox { Id = id++, Target = building, Shape = CombatShape.Aabb(building.GetWorldBounds()), Faction = CombatFactionMask.Colony });
        }
        var vehicles = cave.GetVehicles();
        for (var index = 0; index < vehicles.Count; index++)
        {
            var vehicle = vehicles[index];
            if (vehicle.Health > 0) _hurtboxes.Add(new CombatHurtbox { Id = id++, Target = vehicle, Shape = CombatShape.Aabb(vehicle.GetWorldBounds()), Faction = CombatFactionMask.Colony });
        }
    }

    private void RebuildHurtboxGrid()
    {
        _grid.Clear();
        for (var index = 0; index < _hurtboxes.Count; index++)
        {
            _grid.Add(_hurtboxes[index]);
        }
    }

    private void RebuildDirectives(Cave cave)
    {
        _directives.Clear();
        _assignmentVersion++;
        _sectors.Clear();
        _assignmentEnemies.Clear();
        _assignmentLoads.Clear();
        var sectors = _sectors;
        var enemies = cave.GetEnemyList();
        for (var index = 0; index < enemies.Count; index++)
        {
            var enemy = enemies[index];
            if (enemy.Health <= 0) continue;
            _assignmentEnemies.Add(enemy);
            _assignmentLoads.Add(0);
            var cell = enemy.CurrentCell;
            var sector = SectorId(cell);
            var score = 1000 - Math.Min(800, Math.Abs(cell.X) + Math.Abs(cell.Y)) + (enemy.Id % 7);
            AddSector(sectors, sector, score, cell);
        }
        for (var index = 1; index < _assignmentEnemies.Count; index++)
        {
            var enemy = _assignmentEnemies[index];
            var load = _assignmentLoads[index];
            var insert = index - 1;
            while (insert >= 0 && _assignmentEnemies[insert].Id > enemy.Id)
            {
                _assignmentEnemies[insert + 1] = _assignmentEnemies[insert];
                _assignmentLoads[insert + 1] = _assignmentLoads[insert];
                insert--;
            }

            _assignmentEnemies[insert + 1] = enemy;
            _assignmentLoads[insert + 1] = load;
        }
        sectors.Sort(static (left, right) => right.Score != left.Score ? right.Score.CompareTo(left.Score) : left.SectorId.CompareTo(right.SectorId));
        _fighterCandidates.Clear();
        var fighters = cave.GetTrilobiteList();
        for (var index = 0; index < fighters.Count; index++) if (fighters[index].Health > 0 && fighters[index].IsFighter()) _fighterCandidates.Add(fighters[index]);
        for (var index = 1; index < _fighterCandidates.Count; index++)
        {
            var value = _fighterCandidates[index]; var insert = index - 1;
            while (insert >= 0 && _fighterCandidates[insert].Id > value.Id) { _fighterCandidates[insert + 1] = _fighterCandidates[insert]; insert--; }
            _fighterCandidates[insert + 1] = value;
        }
        for (var fighterIndex = 0; fighterIndex < _fighterCandidates.Count; fighterIndex++)
        {
            var fighter = _fighterCandidates[fighterIndex];
            var chosen = sectors.Count == 0
                ? new SectorScore(SectorId(fighter.CurrentCell), 0, fighter.CurrentCell)
                : sectors[fighterIndex % sectors.Count];
            var targetId = 0;
            var destination = WorldPoint.FromGridPoint(chosen.Center);
            var bestEnemyIndex = -1;
            var bestLoad = int.MaxValue;
            var bestDistance = long.MaxValue;
            for (var enemyIndex = 0; enemyIndex < _assignmentEnemies.Count; enemyIndex++)
            {
                var enemy = _assignmentEnemies[enemyIndex];
                var load = _assignmentLoads[enemyIndex];
                var distance = (enemy.Position - fighter.Position).LengthSquared;
                if (load < bestLoad ||
                    (load == bestLoad && (distance < bestDistance ||
                        (distance == bestDistance && (bestEnemyIndex < 0 || enemy.Id < _assignmentEnemies[bestEnemyIndex].Id)))))
                {
                    bestEnemyIndex = enemyIndex;
                    bestLoad = load;
                    bestDistance = distance;
                }
            }

            if (bestEnemyIndex >= 0)
            {
                var enemy = _assignmentEnemies[bestEnemyIndex];
                _assignmentLoads[bestEnemyIndex]++;
                chosen = new SectorScore(SectorId(enemy.CurrentCell), 0, enemy.CurrentCell);
                targetId = enemy.Id;
                destination = WorldPoint.FromGridPoint(enemy.CurrentCell);
            }

            _directives[fighter.Id] = new CombatDirective(
                fighter.Id,
                chosen.SectorId,
                targetId == 0 ? CombatDirectiveKind.Advance : CombatDirectiveKind.Engage,
                destination,
                targetId,
                _assignmentVersion);
        }
    }

    private static void AddSector(List<SectorScore> sectors, int sectorId, int score, GridPoint center)
    {
        for (var index = 0; index < sectors.Count; index++)
        {
            if (sectors[index].SectorId == sectorId) { sectors[index] = sectors[index] with { Score = sectors[index].Score + score }; return; }
        }
        sectors.Add(new SectorScore(sectorId, score, center));
    }

    private static int SectorId(GridPoint cell) => ((cell.X / 8) * 100000) + (cell.Y / 8);

    private static CombatTargetRef ToTargetRef(object target) => target switch
    {
        Creature creature => CombatTargetRef.For(creature),
        Building building => CombatTargetRef.For(building),
        IVehicle vehicle => CombatTargetRef.For(vehicle),
        _ => throw new InvalidOperationException("Combat hurtbox target is not damageable.")
    };

    private static bool IsAlive(object target, Cave? cave) => target switch
    {
        Creature creature => creature.Health > 0 && ReferenceEquals(creature.Cave, cave),
        Building building => building.Health > 0 && ReferenceEquals(building.Cave, cave),
        IVehicle vehicle => vehicle.Health > 0 && ReferenceEquals(vehicle.Cave, cave),
        _ => false
    };

    private static bool IsTargetAlive(CombatTargetRef target, Cave cave) => IsAlive(target.Target, cave);

    private static bool IsHostile(Creature source, CombatTargetRef target) => source switch
    {
        Enemy => target.Target is Trilobite or Building or IVehicle,
        Trilobite => target.Target is Enemy,
        _ => false
    };

    private static CombatShape GetTargetShape(CombatTargetRef target) => target.Target switch
    {
        Creature creature => GetCreatureHitbox(creature),
        Building building => CombatShape.Aabb(building.GetWorldBounds()),
        IVehicle vehicle => CombatShape.Aabb(vehicle.GetWorldBounds()),
        _ => CombatShape.Circle(WorldPoint.Zero, 0)
    };

    private static CombatShape GetCreatureHitbox(Creature creature) =>
        CombatShape.Circle(creature.Position, GetCombatRadius(creature));

    // Match the creature's movement body plus its deterministic separation allowance.
    private static int GetCombatRadius(Creature creature) =>
        creature.CollisionRadius + creature.SeparationPadding;

    private static CombatShape BuildMeleeShape(Creature source, CombatTargetRef target)
    {
        if (target.Target is Creature)
        {
            return GetCreatureHitbox(source);
        }

        var direction = GetTargetCenter(target) - source.Position;
        if (direction.IsZero)
        {
            direction = source.FacingDirection;
        }

        var center = source.Position + direction.WithMagnitude(source.CollisionRadius + (StructureMeleeRadius / 2));
        return CombatShape.Circle(center, StructureMeleeRadius);
    }

    private static WorldPoint GetTargetCenter(CombatTargetRef target) => target.Target switch
    {
        Creature creature => creature.Position,
        Building building => RectangleCenter(building.GetWorldBounds()),
        IVehicle vehicle => RectangleCenter(vehicle.GetWorldBounds()),
        _ => WorldPoint.Zero
    };

    private static WorldPoint GetTargetCenter(CombatTargetRef target, bool unused = false) => GetTargetCenter(target);

    private static WorldPoint RectangleCenter(WorldRectangle bounds) => new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);

    private readonly record struct SectorScore(int SectorId, int Score, GridPoint Center);
}
