namespace TriloGame.Game.Core.Simulation;

public enum TickPhase
{
    TraitTick,
    SurfaceFeatureTick,
    NaturalEnemySpawn,
    DangerRefresh,
    EnemyBfs,
    TrilobiteMove,
    ColonyBfs,
    EnemyMove,
    CreatureMovement,
    CombatResolution,
    MiningResolution,
    BuildingTick
}
