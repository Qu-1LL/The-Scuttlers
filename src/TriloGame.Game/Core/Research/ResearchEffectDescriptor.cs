namespace TriloGame.Game.Core.Research;

public enum ResearchTargetKind
{
    Global,
    BuildingType,
    BuildingTag,
    CreatureType,
    EnemyType
}

public enum ResearchOperation
{
    AddFlat,
    AddPercent,
    Multiply,
    Set
}

// Data-driven effect descriptor used by progression nodes.
// These descriptors are consumed by GlobalResearch and can target game-wide
// stats or scoped stats (for example by building/creature/enemy type).
public sealed record ResearchEffectDescriptor(
    string StatKey,
    ResearchOperation Operation,
    double Value,
    ResearchTargetKind TargetKind = ResearchTargetKind.Global,
    string? TargetKey = null)
{
    public string StatKey { get; } = RequireText(StatKey, nameof(StatKey));

    public ResearchOperation Operation { get; } = Operation;

    public double Value { get; } = Value;

    public ResearchTargetKind TargetKind { get; } = TargetKind;

    public string? TargetKey { get; } = NormalizeTargetKey(TargetKey);

    // Normalize authored stat keys before effect descriptors enter runtime lookups.
    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
        }

        return value.Trim();
    }

    // Collapse blank scoped target keys to null so matching stays consistent.
    private static string? NormalizeTargetKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public readonly record struct ResearchQuery(
    string StatKey,
    ResearchTargetKind TargetKind = ResearchTargetKind.Global,
    string? TargetKey = null)
{
    public string StatKey { get; } = string.IsNullOrWhiteSpace(StatKey)
        ? throw new ArgumentException("Value cannot be null or whitespace.", nameof(StatKey))
        : StatKey.Trim();

    public ResearchTargetKind TargetKind { get; } = TargetKind;

    public string? TargetKey { get; } = string.IsNullOrWhiteSpace(TargetKey) ? null : TargetKey.Trim();
}

