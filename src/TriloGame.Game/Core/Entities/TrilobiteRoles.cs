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
