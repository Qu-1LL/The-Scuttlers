using System.Numerics;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Runtime.Automation;

public sealed record GamePlaySnapshot(
    int TickCount,
    bool IsPaused,
    double TickSpeedMs,
    bool Danger,
    int TrilobiteCount,
    int EnemyCount,
    int BuildingCount,
    IReadOnlyList<CreatureSnapshot> Trilobites,
    IReadOnlyList<CreatureSnapshot> Enemies,
    IReadOnlyList<BuildingSnapshot> Buildings,
    CombatDirectivePlanSnapshot CombatPlan,
    IReadOnlyList<CombatDirectiveSnapshot> CombatDirectives,
    IReadOnlyList<CombatHitboxSnapshot> ActiveCombatHitboxes,
    IReadOnlyList<CombatHurtboxSnapshot> CombatHurtboxes,
    IReadOnlyList<CombatHitEventSnapshot> RecentCombatHits);

public sealed record CreatureSnapshot(
    int Id,
    string Name,
    string Assignment,
    GridPoint Location,
    int Health,
    int MaxHealth,
    Vector2 WorldPosition,
    GridPoint CurrentCell,
    CreatureRole Role,
    CreatureActivity Activity,
    float CollisionRadius,
    Vector2 Velocity,
    Vector2 FacingDirection,
    MovementCohort MovementCohort,
    Vector2? IdleDestination,
    int IdleRestTicks,
    IReadOnlyList<Vector2> DesiredRoute,
    CombatHitboxSnapshot? ActiveHurtbox,
    int? CombatTargetId,
    MiningClaim? MiningClaim,
    int DamageFlashSequence,
    FighterState? CombatState,
    EnemyCombatState? EnemyCombatState);

public sealed record CombatDirectiveSnapshot(
    int FighterId,
    int SectorId,
    CombatDirectiveKind Kind,
    Vector2 Destination,
    int TargetId,
    int AssignmentVersion);

public sealed record CombatHitboxSnapshot(
    int Id,
    int SourceId,
    int AttackInstanceId,
    CombatShapeKind ShapeKind,
    Vector2 First,
    Vector2 Second,
    float Radius,
    int ActiveFromTick,
    int ActiveUntilTick,
    int Damage,
    int MaximumTargetCount);

public sealed record CombatHurtboxSnapshot(
    int Id,
    int EntityId,
    CombatShapeKind ShapeKind,
    Vector2 First,
    Vector2 Second,
    float Radius,
    int Faction);

public sealed record CombatHitEventSnapshot(
    int Tick,
    int HitboxId,
    int AttackInstanceId,
    int SourceId,
    int TargetId,
    int Damage);

public sealed record BuildingSnapshot(
    string Name,
    GridPoint? Location,
    int Health,
    int MaxHealth);
