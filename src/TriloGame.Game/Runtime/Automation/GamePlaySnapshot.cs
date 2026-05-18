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
    IReadOnlyList<BuildingSnapshot> Buildings);

public sealed record CreatureSnapshot(
    string Name,
    string Assignment,
    GridPoint Location,
    int Health,
    int MaxHealth);

public sealed record BuildingSnapshot(
    string Name,
    GridPoint? Location,
    int Health,
    int MaxHealth);
