using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Barracks : Building
{
    private readonly HashSet<Creature> _assignments = [];

    public Barracks(GameSession session)
        : base("Barracks", new GridPoint(3, 3), [[1, 1, 1], [1, 0, 1], [1, 1, 1]], session, true)
    {
        TextureKey = "Barracks";
        Description = "Fighters will wait here until danger arises.";
        Recipe = new Dictionary<string, int>(StringComparer.Ordinal) { ["Sandstone"] = 20 };
    }

    public IReadOnlyCollection<Creature> Assignments => _assignments;

    public int GetVolume() => _assignments.Count;

    public void Assign(Creature creature) => _assignments.Add(creature);

    public void RemoveAssignment(Creature creature) => _assignments.Remove(creature);
}
