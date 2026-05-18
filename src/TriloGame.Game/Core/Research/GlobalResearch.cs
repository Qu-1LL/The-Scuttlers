using TriloGame.Game.Core.Progression;

namespace TriloGame.Game.Core.Research;

// GlobalResearch is the central intake and processing service for unlocked
// progression effects. It stores effect descriptors and resolves effective
// stat values for any target scope (global/building/creature/enemy).
//
// Integration plan (not wired yet):
// - Buildings query this service using BuildingType or BuildingTag targets.
// - Trilobites and enemies query this service using CreatureType/EnemyType.
// - Systems that need game-wide modifiers query using Global targets.
public sealed class GlobalResearch
{
    private readonly List<ResearchEffectDescriptor> _descriptors = [];
    private readonly Dictionary<string, List<ResearchEffectDescriptor>> _descriptorsBySkillNode =
        new(StringComparer.Ordinal);

    public IReadOnlyList<ResearchEffectDescriptor> Descriptors => _descriptors;

    public int Count => _descriptors.Count;

    // Intake a skill node's descriptors once and retain them for future stat queries.
    public void Intake(string sourceSkillNodeName, IEnumerable<ResearchEffectDescriptor> descriptors)
    {
        if (string.IsNullOrWhiteSpace(sourceSkillNodeName))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(sourceSkillNodeName));
        }

        ArgumentNullException.ThrowIfNull(descriptors);

        var normalizedSourceName = sourceSkillNodeName.Trim();
        var descriptorList = descriptors.ToArray();
        if (descriptorList.Length == 0)
        {
            return;
        }

        if (!_descriptorsBySkillNode.TryAdd(normalizedSourceName, descriptorList.ToList()))
        {
            throw new InvalidOperationException($"Skill node '{normalizedSourceName}' has already been intaken.");
        }

        _descriptors.AddRange(descriptorList);
    }

    // Intake descriptors directly from an authored skill node.
    public void Intake(SkillNode skillNode)
    {
        ArgumentNullException.ThrowIfNull(skillNode);
        Intake(skillNode.Name, skillNode.EffectDescriptors);
    }

    // Intake descriptors directly from a per-run binary skill node.
    public void Intake(BinarySkillNode skillNode)
    {
        ArgumentNullException.ThrowIfNull(skillNode);
        Intake(skillNode.Name, skillNode.EffectDescriptors);
    }

    // Return the descriptors contributed by one named skill node, if any were unlocked.
    public IReadOnlyList<ResearchEffectDescriptor> GetDescriptorsForSkillNode(string sourceSkillNodeName)
    {
        if (string.IsNullOrWhiteSpace(sourceSkillNodeName))
        {
            return [];
        }

        return _descriptorsBySkillNode.TryGetValue(sourceSkillNodeName.Trim(), out var descriptors)
            ? descriptors
            : [];
    }

    // Resolve a final stat value by applying set, flat, percent, and multiply effects in order.
    public double ResolveEffectiveValue(ResearchQuery query, double baseValue)
    {
        var matching = GetMatchingDescriptors(query);

        // Only the last set operation overrides the incoming base value.
        var setOverride = matching.LastOrDefault(static d => d.Operation == ResearchOperation.Set);
        var value = setOverride is null ? baseValue : setOverride.Value;

        foreach (var modifier in matching.Where(static d => d.Operation == ResearchOperation.AddFlat))
        {
            value += modifier.Value;
        }

        var additivePercent = matching
            .Where(static d => d.Operation == ResearchOperation.AddPercent)
            .Sum(static d => d.Value);
        value *= 1 + additivePercent;

        foreach (var multiplier in matching.Where(static d => d.Operation == ResearchOperation.Multiply))
        {
            value *= multiplier.Value;
        }

        return value;
    }

    // Filter the unlocked descriptor pool down to effects that match one exact stat scope.
    public IReadOnlyList<ResearchEffectDescriptor> GetMatchingDescriptors(ResearchQuery query)
    {
        var statKey = query.StatKey;
        var targetKey = string.IsNullOrWhiteSpace(query.TargetKey) ? null : query.TargetKey.Trim();

        return _descriptors
            .Where(descriptor =>
                string.Equals(descriptor.StatKey, statKey, StringComparison.Ordinal)
                && descriptor.TargetKind == query.TargetKind
                && string.Equals(descriptor.TargetKey, targetKey, StringComparison.Ordinal))
            .ToArray();
    }
}

