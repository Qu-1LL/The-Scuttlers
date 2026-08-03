using TriloGame.Game.Core.Entities;

namespace TriloGame.Game.Core.Combat;

internal enum CombatActorState { None, Search, Pursue, Engage, Recover, PursueColony, EngageHostile, BreachWall }
internal enum CombatActorIntentKind { None, Attack, Move, Mine, Hold, Recover }
internal enum CombatNoOpReason { None, NotInDanger, NoValidTarget, NoPath, Cooldown, NoReachableBreach, NoQueen, InvalidState, SilentIdleRecovered }
internal readonly record struct CombatActorIntent(
    CombatActorState State,
    CombatActorIntentKind Kind,
    CombatTargetKind TargetKind = CombatTargetKind.Creature,
    int TargetId = 0,
    string? RouteMode = null,
    CombatNoOpReason NoOpReason = CombatNoOpReason.None);

public readonly record struct CombatDiagnosticsSnapshot(
    int Tick,
    int FighterIntentCount,
    int EnemyIntentCount,
    int RecoverIntentCount,
    int IdleInDangerCount,
    int EnemyIdleInDangerCount,
    int SilentIdleRecoveryCount);

public readonly record struct CombatDirectivePlanSnapshot(
    int Tick,
    int FighterCount,
    int ThreatSectorCount,
    int InterceptSlotCount,
    int AssignedFighterCount);
