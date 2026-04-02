using TriloGame.Game.Core.Entities;

namespace TriloGame.Game.UI.Selection;

public static class RoleSelectionState
{
    public static string? GetUniformAssignment(IEnumerable<Trilobite> trilobites)
    {
        string? uniformAssignment = null;

        foreach (var trilobite in trilobites)
        {
            if (uniformAssignment is null)
            {
                uniformAssignment = trilobite.Assignment;
                continue;
            }

            if (!string.Equals(uniformAssignment, trilobite.Assignment, StringComparison.Ordinal))
            {
                return null;
            }
        }

        return uniformAssignment;
    }
}
