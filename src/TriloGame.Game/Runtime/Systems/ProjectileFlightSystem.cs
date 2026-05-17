using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Runtime.Systems;

public sealed class ProjectileFlightSystem
{
    // Advance active projectile travel and resolve any impacts that reach their targets.
    public int Advance(GameSession session, double elapsedMs)
    {
        if (elapsedMs <= 0d)
        {
            return 0;
        }

        var flights = session.Runtime.ActiveProjectileFlights;
        var resolvedImpacts = 0;
        // Walk backward so finished or invalid flights can be removed in place.
        for (var index = flights.Count - 1; index >= 0; index--)
        {
            var flight = flights[index];
            if (flight.Target.Health <= 0 || flight.Target.Cave is null)
            {
                flights.RemoveAt(index);
                continue;
            }

            flight.Advance(elapsedMs, session.Runtime.CurrentTickSpeedMs);
            if (!flight.HasArrived())
            {
                continue;
            }

            flight.Target.TakeDamage(flight.Projectile.Damage, flight.Source);
            flights.RemoveAt(index);
            resolvedImpacts++;
        }

        return resolvedImpacts;
    }
}
