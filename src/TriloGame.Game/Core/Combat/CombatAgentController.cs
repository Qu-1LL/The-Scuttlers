using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Combat;

// Fighters follow director assignments; they do not select or expose tactical subroles.
internal sealed class CombatAgentController
{
    private int _trackedTargetId;
    private GridPoint? _trackedTargetCell;

    public bool Advance(Trilobite fighter)
    {
        if (!fighter.EnsureFighterState()) return false;
        if (!fighter.Session.Danger)
        {
            fighter.ClearFighterTarget();
            ClearPursuitTracking();
            return fighter.AdvanceFighterReturnToStation(true);
        }

        // Turret crews remain hosted so the building can own its reload cadence;
        // the director assigns mobile fighters around the threat sectors.
        if (fighter.IsHostedOnBuilding() && fighter.HostedBuilding is Turret)
        {
            return false;
        }

        if (fighter.Cave is null || !fighter.Session.Combat.TryGetDirective(fighter.Id, out var directive)) return false;
        var target = fighter.Session.Combat.FindLiveDirectedEnemy(fighter);
        if (target is not null)
        {
            fighter.SetFighterTarget(target);
            TrackTarget(target);
            if (CombatWorld.CanMeleeReach(fighter, CombatTargetRef.For(target)))
            {
                fighter.SetActivity(CreatureActivity.Fighting);
                return fighter.Session.Combat.HasActiveOrPending(fighter) || fighter.Session.Combat.TryQueueMelee(fighter, CombatTargetRef.For(target));
            }

            // Follow the target's live pose; a sector center is only an assignment hint
            // and can lie beyond the enemy, causing a fighter to run past the engagement.
            fighter.SetActivity(CreatureActivity.Fighting);
            return StartPursuitRoute(fighter, target);
        }

        fighter.ClearFighterTarget();
        ClearPursuitTracking();
        fighter.SetActivity(CreatureActivity.Fighting);
        if (directive.TargetId != 0)
        {
            // The assignment was invalidated this tick. Do not route to its old sector center.
            return false;
        }

        return fighter.NavigateTo(directive.Destination);
    }

    // Revalidate active fighter routes before movement can consume another stale waypoint.
    public bool RefreshActivePursuit(Trilobite fighter)
    {
        if (!fighter.IsFighter() || fighter.Cave is null)
        {
            return false;
        }

        if (!fighter.Session.Danger)
        {
            fighter.ClearFighterTarget();
            ClearPursuitTracking();
            fighter.ClearTaskQueue();
            return true;
        }

        if (fighter.IsHostedOnBuilding() && fighter.HostedBuilding is Turret)
        {
            return false;
        }

        var target = fighter.Session.Combat.FindLiveDirectedEnemy(fighter);
        if (target is null)
        {
            fighter.ClearFighterTarget();
            ClearPursuitTracking();
            fighter.ClearTaskQueue();
            return true;
        }

        var targetChanged = fighter.FighterTarget is null || fighter.FighterTarget.Id != target.Id;
        if (targetChanged)
        {
            fighter.SetFighterTarget(target);
            TrackTarget(target);
            var routeStarted = StartPursuitRoute(fighter, target);
            return !routeStarted || !fighter.HasActiveMovement;
        }

        if (CombatWorld.CanMeleeReach(fighter, CombatTargetRef.For(target)))
        {
            fighter.ClearTaskQueue();
            fighter.SetActivity(CreatureActivity.Fighting);
            return true;
        }

        var targetMoved = _trackedTargetId != target.Id ||
                          _trackedTargetCell != target.CurrentCell;
        if (targetMoved)
        {
            TrackTarget(target);
            fighter.SetActivity(CreatureActivity.Fighting);
            var routeStarted = StartPursuitRoute(fighter, target);
            return !routeStarted || !fighter.HasActiveMovement;
        }

        return false;
    }

    private void TrackTarget(Enemy target)
    {
        _trackedTargetId = target.Id;
        _trackedTargetCell = target.CurrentCell;
    }

    // Replace a stale pursuit route before the movement phase, while preserving this tick's movement.
    private static bool StartPursuitRoute(Trilobite fighter, Enemy target)
    {
        fighter.ClearTaskQueue();
        return fighter.NavigateTo(target.Position, clearExisting: true);
    }

    private void ClearPursuitTracking()
    {
        _trackedTargetId = 0;
        _trackedTargetCell = null;
    }
}
