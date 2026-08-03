using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Entities;

public enum CreatureTaskKind
{
    NavigateTo,
    RunRole,
    EnemyStep1,
}

public readonly record struct CreatureTask(
    CreatureTaskKind Kind,
    GridPoint Target,
    WorldPoint WorldTarget,
    bool UsesWorldTarget)
{
    public CreatureTask(CreatureTaskKind kind)
        : this(kind, GridPoint.Zero, default, false)
    {
    }

    public CreatureTask(CreatureTaskKind kind, GridPoint target)
        : this(kind, target, default, false)
    {
    }

    public static CreatureTask NavigateTo(GridPoint target) => new(CreatureTaskKind.NavigateTo, target);

    public static CreatureTask NavigateTo(WorldPoint target) => new(
        CreatureTaskKind.NavigateTo,
        target.ToGridPoint(),
        target,
        true);
}
