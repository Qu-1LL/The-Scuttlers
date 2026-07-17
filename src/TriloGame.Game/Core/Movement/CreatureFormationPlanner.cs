using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Movement;

public readonly record struct CreatureFormationAssignment(Creature Creature, WorldPoint Destination);

public static class CreatureFormationPlanner
{
    private static readonly GridPoint[] AxialDirections =
    [
        new GridPoint(1, 0), new GridPoint(1, -1), new GridPoint(0, -1),
        new GridPoint(-1, 0), new GridPoint(-1, 1), new GridPoint(0, 1)
    ];

    public static List<CreatureFormationAssignment> Build(
        Cave cave,
        IReadOnlyList<Trilobite> creatures,
        WorldPoint center)
    {
        var assignments = new List<CreatureFormationAssignment>(creatures.Count);
        if (creatures.Count == 0)
        {
            return assignments;
        }

        var maximumBodyRadius = 0;
        for (var index = 0; index < creatures.Count; index++)
        {
            maximumBodyRadius = Math.Max(
                maximumBodyRadius,
                creatures[index].CollisionRadius + creatures[index].SeparationPadding);
        }

        var spacing = maximumBodyRadius * 2;
        var candidates = new List<WorldPoint>(creatures.Count * 2);
        AddCandidateIfClear(cave, creatures[0], center, candidates);
        for (var ring = 1; candidates.Count < creatures.Count && ring <= creatures.Count + 4; ring++)
        {
            var axial = new GridPoint(-ring, ring);
            for (var side = 0; side < AxialDirections.Length && candidates.Count < creatures.Count; side++)
            {
                for (var step = 0; step < ring && candidates.Count < creatures.Count; step++)
                {
                    var candidate = FromAxial(center, axial, spacing);
                    AddCandidateIfClear(cave, creatures[0], candidate, candidates);
                    axial = new GridPoint(
                        axial.X + AxialDirections[side].X,
                        axial.Y + AxialDirections[side].Y);
                }
            }
        }

        var pairs = new List<FormationPair>(creatures.Count * candidates.Count);
        for (var creatureIndex = 0; creatureIndex < creatures.Count; creatureIndex++)
        {
            for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                var delta = candidates[candidateIndex] - creatures[creatureIndex].Position;
                pairs.Add(new FormationPair(
                    creatureIndex,
                    candidateIndex,
                    delta.LengthSquared,
                    creatures[creatureIndex].Id));
            }
        }

        pairs.Sort(static (left, right) =>
        {
            var distance = left.DistanceSquared.CompareTo(right.DistanceSquared);
            if (distance != 0)
            {
                return distance;
            }

            var id = left.CreatureId.CompareTo(right.CreatureId);
            return id != 0 ? id : left.CandidateIndex.CompareTo(right.CandidateIndex);
        });

        var assignedCreatures = new bool[creatures.Count];
        var assignedCandidates = new bool[candidates.Count];
        for (var pairIndex = 0; pairIndex < pairs.Count && assignments.Count < creatures.Count; pairIndex++)
        {
            var pair = pairs[pairIndex];
            if (assignedCreatures[pair.CreatureIndex] || assignedCandidates[pair.CandidateIndex])
            {
                continue;
            }

            assignedCreatures[pair.CreatureIndex] = true;
            assignedCandidates[pair.CandidateIndex] = true;
            assignments.Add(new CreatureFormationAssignment(
                creatures[pair.CreatureIndex],
                candidates[pair.CandidateIndex]));
        }

        assignments.Sort(static (left, right) => left.Creature.Id.CompareTo(right.Creature.Id));
        return assignments;
    }

    private static void AddCandidateIfClear(
        Cave cave,
        Creature creature,
        WorldPoint candidate,
        List<WorldPoint> candidates)
    {
        if (cave.CanCreatureOccupyWorldPosition(creature, candidate))
        {
            candidates.Add(candidate);
        }
    }

    private static WorldPoint FromAxial(WorldPoint center, GridPoint axial, int spacing)
    {
        return center + new WorldVector(
            (axial.X * spacing) + ((axial.Y * spacing) / 2),
            (axial.Y * spacing * 867) / 1000);
    }

    private readonly record struct FormationPair(
        int CreatureIndex,
        int CandidateIndex,
        long DistanceSquared,
        int CreatureId);
}
