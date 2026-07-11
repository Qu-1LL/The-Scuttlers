namespace TriloGame.Game.Core.Entities;

internal static class TrilobiteRoles
{
    public const string Unassigned = "unassigned";
    public const string Miner = "miner";
    public const string Farmer = "farmer";
    public const string Builder = "builder";
    public const string Fighter = "fighter";

    public static bool IsWorker(string assignment)
    {
        return string.Equals(assignment, Miner, StringComparison.Ordinal) ||
               string.Equals(assignment, Farmer, StringComparison.Ordinal) ||
               string.Equals(assignment, Builder, StringComparison.Ordinal);
    }
}

internal sealed class TrilobiteRoleBehavior
{
    private readonly Action<Trilobite> _start;
    private readonly Func<Trilobite, Action>? _navigationFallback;

    public TrilobiteRoleBehavior(
        string assignment,
        Action<Trilobite> start,
        Func<Trilobite, Action>? navigationFallback = null)
    {
        Assignment = assignment;
        _start = start;
        _navigationFallback = navigationFallback;
    }

    public string Assignment { get; }

    public void Start(Trilobite trilobite)
    {
        _start(trilobite);
    }

    public Action? GetNavigationFallback(Trilobite trilobite)
    {
        return _navigationFallback?.Invoke(trilobite);
    }
}

internal static class TrilobiteRoleCatalog
{
    private static readonly Dictionary<string, TrilobiteRoleBehavior> Behaviors = new(StringComparer.Ordinal)
    {
        [TrilobiteRoles.Unassigned] = new(
            TrilobiteRoles.Unassigned,
            static trilobite => trilobite.StartUnassignedRoleBehavior()),
        [TrilobiteRoles.Miner] = new(
            TrilobiteRoles.Miner,
            static trilobite => trilobite.EnqueueMinerRoleBehavior(),
            static trilobite => () => { trilobite.MinerStep1(); }),
        [TrilobiteRoles.Farmer] = new(
            TrilobiteRoles.Farmer,
            static trilobite => trilobite.EnqueueFarmerRoleBehavior(),
            static trilobite => () => { trilobite.FarmerStep1(); }),
        [TrilobiteRoles.Builder] = new(
            TrilobiteRoles.Builder,
            static trilobite => trilobite.EnqueueBuilderRoleBehavior(),
            static trilobite => () => { trilobite.BuilderStep1(); }),
        [TrilobiteRoles.Fighter] = new(
            TrilobiteRoles.Fighter,
            static trilobite => trilobite.EnqueueFighterRoleBehavior(),
            static trilobite => () => { trilobite.FighterStep1(); })
    };

    public static TrilobiteRoleBehavior GetOrUnassigned(string? assignment)
    {
        return assignment is not null && Behaviors.TryGetValue(assignment, out var behavior)
            ? behavior
            : Behaviors[TrilobiteRoles.Unassigned];
    }

    public static bool IsRole(string? assignment, string role)
    {
        return string.Equals(assignment, role, StringComparison.Ordinal);
    }
}
