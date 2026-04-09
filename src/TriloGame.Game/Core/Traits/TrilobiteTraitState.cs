namespace TriloGame.Game.Core.Traits;

public sealed class TrilobiteTraitState
{
    private readonly HashSet<TrilobiteTrait> _traits = [];
    private readonly Dictionary<TrilobiteTrait, int> _expressedUntilTick = [];

    public TrilobiteTraitState(IEnumerable<TrilobiteTrait>? initialTraits = null)
    {
        SetTraits(initialTraits);
    }

    public IReadOnlyList<TrilobiteTrait> GetTraits()
    {
        return [.. _traits.OrderBy(static trait => trait)];
    }

    public void SetTraits(IEnumerable<TrilobiteTrait>? traits)
    {
        _traits.Clear();
        _expressedUntilTick.Clear();

        if (traits is null)
        {
            return;
        }

        foreach (var trait in traits)
        {
            _traits.Add(trait);
        }
    }

    public bool HasTrait(TrilobiteTrait trait)
    {
        return _traits.Contains(trait);
    }

    public bool Express(TrilobiteTrait trait, int currentTick, int durationTicks)
    {
        if (!_traits.Contains(trait) || durationTicks <= 0)
        {
            return false;
        }

        var untilTick = currentTick + durationTicks;
        if (_expressedUntilTick.TryGetValue(trait, out var existingUntilTick) && existingUntilTick > untilTick)
        {
            untilTick = existingUntilTick;
        }

        _expressedUntilTick[trait] = untilTick;
        return true;
    }

    public void Tick(int currentTick)
    {
        if (_expressedUntilTick.Count == 0)
        {
            return;
        }

        foreach (var trait in _expressedUntilTick
                     .Where(pair => pair.Value <= currentTick)
                     .Select(static pair => pair.Key)
                     .ToArray())
        {
            _expressedUntilTick.Remove(trait);
        }
    }

    public IReadOnlyList<TrilobiteTrait> GetExpressedTraits(int currentTick)
    {
        Tick(currentTick);
        return
        [
            .. _expressedUntilTick.Keys
                .OrderBy(static trait => trait)
        ];
    }

    public bool IsExpressing(TrilobiteTrait trait, int currentTick)
    {
        Tick(currentTick);
        return _expressedUntilTick.ContainsKey(trait);
    }

    public string GetTraitSummary()
    {
        var traits = GetTraits();
        return traits.Count == 0
            ? "None"
            : string.Join(", ", traits.Select(static trait => trait.GetDisplayName()));
    }

    public string GetExpressionSummary(int currentTick)
    {
        var expressedTraits = GetExpressedTraits(currentTick);
        return expressedTraits.Count == 0
            ? "Calm"
            : string.Join(", ", expressedTraits.Select(static trait => trait.GetDisplayName()));
    }
}
