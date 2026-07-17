using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Core.Vehicles;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Combat;

[Flags]
public enum CombatFactionMask
{
    None = 0,
    Colony = 1,
    Hostile = 2,
    Neutral = 4,
    Any = Colony | Hostile | Neutral
}

public enum CombatCommandKind
{
    Attack,
    ProjectileImpact,
    Explosion,
    MovementDirective,
    Breach,
    Retreat
}

public enum CombatDirectiveKind
{
    Advance,
    Engage,
    Breach,
    Retarget,
    Recover,
    Retreat,
    Regroup
}

public sealed class CombatHurtbox
{
    public required int Id { get; init; }
    public required object Target { get; init; }
    public required CombatShape Shape { get; init; }
    public required CombatFactionMask Faction { get; init; }
    public int EntityId => Target switch
    {
        Creature creature => creature.Id,
        Building building => building.Id,
        IVehicle vehicle => vehicle.Id,
        _ => Id
    };
}

public sealed class CombatHitbox
{
    public required int Id { get; init; }
    public required int SourceId { get; init; }
    public required Creature Source { get; init; }
    public required int AttackInstanceId { get; init; }
    public required CombatShape Shape { get; init; }
    public required CombatFactionMask TargetMask { get; init; }
    public required int ActiveFromTick { get; init; }
    public required int ActiveUntilTick { get; init; }
    public required int Damage { get; init; }
    public required WorldVector Knockback { get; init; }
    public required int MaximumTargetCount { get; init; }
    public bool Resolved { get; internal set; }
    public CombatTargetRef? PreferredTarget { get; init; }
    internal HashSet<int> HitTargetIds { get; } = [];
}

public readonly record struct CombatAttackProfile(
    int Damage,
    int WindupTicks,
    int ActiveTicks,
    int RecoveryTicks,
    int MaximumTargetCount,
    WorldVector Knockback,
    CombatFactionMask TargetMask)
{
    public static CombatAttackProfile Melee(Creature source) => new(
        source.Damage,
        1,
        1,
        3,
        1,
        WorldVector.Zero,
        source is Enemy ? CombatFactionMask.Colony : CombatFactionMask.Hostile);
}

public readonly record struct CombatHitEvent(
    int Tick,
    int HitboxId,
    int AttackInstanceId,
    int SourceId,
    CombatTargetRef Target,
    int Damage,
    WorldVector Knockback);

public enum CombatTargetKind
{
    Creature,
    Building,
    Vehicle
}

public readonly record struct CombatTargetRef(CombatTargetKind Kind, int Id, object Target)
{
    public static CombatTargetRef For(Creature target) => new(CombatTargetKind.Creature, target.Id, target);
    public static CombatTargetRef For(Building target) => new(CombatTargetKind.Building, target.Id, target);
    public static CombatTargetRef For(IVehicle target) => new(CombatTargetKind.Vehicle, target.Id, target);
}

public readonly record struct CombatDirective(
    int FighterId,
    int SectorId,
    CombatDirectiveKind Kind,
    WorldPoint Destination,
    int TargetId,
    int AssignmentVersion);
