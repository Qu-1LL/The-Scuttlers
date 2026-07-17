using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Entities;

public enum CreatureRole
{
    Unassigned,
    Miner,
    Builder,
    Farmer,
    Fighter,
    Enemy
}

public enum CreatureActivity
{
    Idle,
    Planning,
    Moving,
    WaitingForSlot,
    Working,
    Hauling,
    Depositing,
    Feeding,
    Fighting,
    Fleeing,
    KnockedBack,
    Stationed,
    Brooding
}

public enum CreatureFaction
{
    Colony,
    Ants
}

public enum MovementGoalKind
{
    None,
    Destination,
    ManualCommand,
    Work,
    Combat,
    Idle
}

internal enum RouteContinuationKind
{
    None,
    PointDestination,
    SharedBfsField,
    BuildingField,
    MiningPostField
}

public enum MinerState
{
    Idle,
    SelectPost,
    AcquireClaim,
    MoveToClaim,
    MineClaim,
    DepositInventory,
    WaitForWork,
    WaitForStorage
}

public enum FarmerState
{
    Idle,
    SelectFarm,
    MoveToFarmSlot,
    Harvest,
    MoveToQueen,
    FeedQueen,
    WaitForFarm
}

public enum BuilderState
{
    Idle,
    SelectScaffold,
    ReserveMaterial,
    MoveToSource,
    WithdrawMaterial,
    MoveToScaffold,
    DepositMaterial,
    BuildScaffold,
    DepositExtraInventory,
    WaitForMaterials
}

public enum FighterState
{
    Idle,
    SelectStation,
    ReturnToStation,
    SelectRole,
    AcquireTarget,
    MoveToTarget,
    AttackTarget,
    HoldStation,
    Regroup,
    Retreat,
    Recover,
    WaitForDanger
}

public enum EnemyCombatState
{
    Idle,
    AcquireTarget,
    MoveToColony,
    AttackTarget,
    BreachTarget,
    Recover
}

public enum WorkerRoleFailureReason
{
    None,
    NoAssignment,
    NoWork,
    NoStorage,
    NoReachablePath,
    ReservationUnavailable,
    TargetInvalid,
    InventoryBlocked,
    ActionBlocked
}

public readonly record struct MovementCohort(
    CreatureFaction Faction,
    MovementGoalKind GoalKind,
    int GoalId)
{
    public static readonly MovementCohort None = new(CreatureFaction.Colony, MovementGoalKind.None, 0);

    public bool IsActive => GoalKind != MovementGoalKind.None;
}

public readonly record struct CreatureMovementProfile(
    int CollisionRadius,
    int SeparationPadding,
    int BaseSpeed,
    int Mass)
{
    public static CreatureMovementProfile Trilobite => new(
        WorldUnits.FromPixels(128),
        WorldUnits.FromPixels(8),
        WorldUnits.UnitsPerTile / 2,
        100);

    public static CreatureMovementProfile Ant => new(
        WorldUnits.FromPixels(112),
        WorldUnits.FromPixels(8),
        WorldUnits.UnitsPerTile / 2,
        80);
}

public static class CreatureRoleNames
{
    public static CreatureRole Parse(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "miner" => CreatureRole.Miner,
            "builder" => CreatureRole.Builder,
            "farmer" => CreatureRole.Farmer,
            "fighter" => CreatureRole.Fighter,
            "enemy" => CreatureRole.Enemy,
            _ => CreatureRole.Unassigned
        };
    }

    public static string ToAssignment(CreatureRole role)
    {
        return role switch
        {
            CreatureRole.Miner => "miner",
            CreatureRole.Builder => "builder",
            CreatureRole.Farmer => "farmer",
            CreatureRole.Fighter => "fighter",
            CreatureRole.Enemy => "enemy",
            _ => "unassigned"
        };
    }
}
