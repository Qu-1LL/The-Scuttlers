using TriloGame.Game.Core.Entities;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Combat;

// Commands are sorted by tick, source, then local command id before resolution.
public sealed class CombatCommandBuffer
{
    private readonly List<CombatCommand> _commands = [];
    private int _nextCommandId = 1;

    public IReadOnlyList<CombatCommand> Commands => _commands;

    public int AddAttack(int tick, Creature source, CombatTargetRef? target, CombatShape shape, CombatAttackProfile profile)
    {
        var command = new CombatCommand(
            tick,
            source.Id,
            _nextCommandId++,
            CombatCommandKind.Attack,
            source,
            target,
            shape,
            profile,
            null);
        _commands.Add(command);
        return command.CommandId;
    }

    public int AddProjectileImpact(int tick, Creature source, Creature target, int damage, WorldVector knockback)
    {
        var profile = new CombatAttackProfile(
            damage,
            0,
            1,
            0,
            1,
            knockback,
            source is Enemy ? CombatFactionMask.Colony : CombatFactionMask.Hostile);
        var command = new CombatCommand(
            tick,
            source.Id,
            _nextCommandId++,
            CombatCommandKind.ProjectileImpact,
            source,
            CombatTargetRef.For(target),
            CombatShape.Circle(target.Position, target.CollisionRadius),
            profile,
            target);
        _commands.Add(command);
        return command.CommandId;
    }

    public int AddExplosion(int tick, Creature source, CombatShape shape, int damage)
    {
        var profile = new CombatAttackProfile(
            damage,
            0,
            1,
            0,
            int.MaxValue,
            WorldVector.Zero,
            CombatFactionMask.Any);
        var command = new CombatCommand(
            tick,
            source.Id,
            _nextCommandId++,
            CombatCommandKind.Explosion,
            source,
            null,
            shape,
            profile,
            null);
        _commands.Add(command);
        return command.CommandId;
    }

    public int AddMovementDirective(int tick, Creature source, WorldPoint destination, CombatDirectiveKind kind)
    {
        var command = new CombatCommand(
            tick,
            source.Id,
            _nextCommandId++,
            CombatCommandKind.MovementDirective,
            source,
            null,
            default,
            default,
            null,
            kind,
            destination);
        _commands.Add(command);
        return command.CommandId;
    }

    public int AddBreach(int tick, Creature source, WorldPoint destination) => AddMovementDirective(tick, source, destination, CombatDirectiveKind.Breach);

    public int AddRetreat(int tick, Creature source, WorldPoint destination) => AddMovementDirective(tick, source, destination, CombatDirectiveKind.Retreat);

    public void RemoveFor(Creature source)
    {
        for (var index = _commands.Count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(_commands[index].Source, source))
            {
                _commands.RemoveAt(index);
            }
        }
    }

    public void Sort()
    {
        for (var index = 1; index < _commands.Count; index++)
        {
            var value = _commands[index];
            var insert = index - 1;
            while (insert >= 0 && _commands[insert].CompareTo(value) > 0)
            {
                _commands[insert + 1] = _commands[insert];
                insert--;
            }

            _commands[insert + 1] = value;
        }
    }

    internal void RemoveAt(int index) => _commands.RemoveAt(index);

    public void Clear() => _commands.Clear();
}

public readonly record struct CombatCommand(
    int Tick,
    int SourceId,
    int CommandId,
    CombatCommandKind Kind,
    Creature Source,
    CombatTargetRef? Target,
    CombatShape Shape,
    CombatAttackProfile Profile,
    Creature? ProjectileTarget,
    CombatDirectiveKind? DirectiveKind = null,
    WorldPoint Destination = default)
{
    public int CompareTo(CombatCommand other)
    {
        var tick = Tick.CompareTo(other.Tick);
        if (tick != 0) return tick;
        var source = SourceId.CompareTo(other.SourceId);
        return source != 0 ? source : CommandId.CompareTo(other.CommandId);
    }
}
