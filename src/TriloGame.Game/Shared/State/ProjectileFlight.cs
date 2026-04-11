using System.Numerics;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Entities;

namespace TriloGame.Game.Shared.State;

public sealed class ProjectileFlight
{
    public ProjectileFlight(
        Projectile projectile,
        Creature source,
        Creature target,
        Vector2 sourceWorldPosition,
        float angleDegrees)
    {
        Projectile = projectile;
        Source = source;
        Target = target;
        SourceWorldPosition = sourceWorldPosition;
        CurrentWorldPosition = sourceWorldPosition;
        TargetWorldPosition = target.GetWorldPosition();
        AngleDegrees = angleDegrees;
    }

    public Projectile Projectile { get; }

    public Creature Source { get; }

    public Creature Target { get; }

    public Vector2 SourceWorldPosition { get; }

    public Vector2 CurrentWorldPosition { get; private set; }

    public Vector2 TargetWorldPosition { get; private set; }

    public float AngleDegrees { get; private set; }

    public double ElapsedMs { get; private set; }

    public void Advance(double elapsedMs, double currentTickSpeedMs)
    {
        if (elapsedMs <= 0d || HasArrived())
        {
            return;
        }

        ElapsedMs += elapsedMs;
        TargetWorldPosition = Target.GetWorldPosition();
        var delta = TargetWorldPosition - CurrentWorldPosition;
        var distanceToTarget = delta.Length();
        if (distanceToTarget <= 0f)
        {
            CurrentWorldPosition = TargetWorldPosition;
            return;
        }

        AngleDegrees = MathF.Atan2(delta.Y, delta.X) * (180f / MathF.PI);

        var clampedTickSpeedMs = System.Math.Max(1d, currentTickSpeedMs);
        if (Projectile.TravelPixelsPerTick <= 0f)
        {
            CurrentWorldPosition = TargetWorldPosition;
            return;
        }

        var speedPixelsPerMs = Projectile.TravelPixelsPerTick / (float)clampedTickSpeedMs;
        var maxTravelDistance = speedPixelsPerMs * (float)elapsedMs;
        if (maxTravelDistance >= distanceToTarget)
        {
            CurrentWorldPosition = TargetWorldPosition;
            return;
        }

        CurrentWorldPosition += (delta / distanceToTarget) * maxTravelDistance;
    }

    public bool HasArrived() => CurrentWorldPosition == TargetWorldPosition;
}
