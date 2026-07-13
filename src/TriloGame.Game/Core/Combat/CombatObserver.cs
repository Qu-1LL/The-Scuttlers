using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Combat;

public enum CombatEventKind
{
    EnteredCombat,
    CreatureDied
}

public sealed record CombatEvent(
    CombatEventKind Kind,
    int Tick,
    Creature Creature,
    object? Opponent,
    object? Source,
    GridPoint Location);

public sealed class CombatObserver
{
    // Combat entry is episode-based: each creature records one entry until death or Clear().
    private readonly HashSet<Creature> _activeCombatants = [];
    private readonly List<CombatEvent> _events = [];

    public IReadOnlyList<CombatEvent> Events => _events;

    public int EventCount => _events.Count;

    public bool IsInCombat(Creature creature)
    {
        return _activeCombatants.Contains(creature);
    }

    public void RecordAttack(Creature attacker, object? target, int tick)
    {
        if (attacker.Health <= 0 || attacker.Cave is null || ReferenceEquals(attacker, target))
        {
            return;
        }

        RecordEnteredCombat(attacker, target, attacker, tick);

        // Creature targets enter combat on the same attack so observers can track both sides.
        if (target is Creature targetCreature &&
            targetCreature.Health > 0 &&
            targetCreature.Cave is not null &&
            ReferenceEquals(attacker.Cave, targetCreature.Cave) &&
            !ReferenceEquals(attacker, targetCreature))
        {
            RecordEnteredCombat(targetCreature, attacker, attacker, tick);
        }
    }

    public void RecordDeath(Creature creature, object? source, GridPoint location, int tick)
    {
        // Death ends the current combat episode; future attacks can start a fresh one if respawned.
        _activeCombatants.Remove(creature);
        _events.Add(new CombatEvent(
            CombatEventKind.CreatureDied,
            tick,
            creature,
            source,
            source,
            location));
    }

    public void Clear()
    {
        _activeCombatants.Clear();
        _events.Clear();
    }

    private void RecordEnteredCombat(Creature creature, object? opponent, object? source, int tick)
    {
        if (!_activeCombatants.Add(creature))
        {
            return;
        }

        _events.Add(new CombatEvent(
            CombatEventKind.EnteredCombat,
            tick,
            creature,
            opponent,
            source,
            creature.Location));
    }
}
