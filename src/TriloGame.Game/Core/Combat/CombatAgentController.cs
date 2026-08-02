using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Combat;

// Fighters follow director assignments; they do not select or expose tactical subroles.
internal sealed class CombatAgentController
{
    private const int DirectPursuitMaximumDistance = WorldUnits.UnitsPerTile * 8;
    private int _trackedTargetId;
    private GridPoint? _trackedTargetCell;

    public bool Advance(Trilobite fighter)
    {
        if (!fighter.EnsureFighterState()) return false;
        if (!fighter.Session.Danger)
        {
            fighter.ClearFighterTarget();
            ClearPursuitTracking();
            if (fighter.FighterState is FighterState.SelectStation or FighterState.ReturnToStation)
            {
                return fighter.AdvanceFighterReturnToStation(true);
            }

            return fighter.AdvanceSharedIdleBehavior();
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

        var started = fighter.NavigateTo(directive.Destination);
        if (started && fighter.HasActiveMovement)
        {
            fighter.SetMovementCohort(new MovementCohort(
                CreatureFaction.Colony,
                MovementGoalKind.Combat,
                directive.TargetId != 0 ? directive.TargetId : directive.SectorId));
        }

        return started;
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
            var wasCombatRoute = fighter.MovementCohort.GoalKind == MovementGoalKind.Combat;
            fighter.ClearFighterTarget();
            ClearPursuitTracking();
            fighter.ClearTaskQueue();
            if (wasCombatRoute)
            {
                // Drop stale pursuit routes when danger ends; idle, station, and manual routes continue normally.
                fighter.CancelMovement();
            }

            return false;
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
            StartPursuitRoute(fighter, target);
            return !fighter.HasActiveMovement;
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
            StartPursuitRoute(fighter, target);
            return !fighter.HasActiveMovement;
        }

        return false;
    }

    private void TrackTarget(Enemy target)
    {
        _trackedTargetId = target.Id;
        _trackedTargetCell = target.CurrentCell;
    }

    // Use the shared enemy field for travel and a direct final approach for exact melee spacing.
    private static bool StartPursuitRoute(Trilobite fighter, Enemy target)
    {
        var engagementPoint = CombatWorld.GetMeleeEngagementPoint(fighter, target);
        var started = fighter.TryBeginOrReplaceDirectCombatRoute(engagementPoint, DirectPursuitMaximumDistance) ||
                      fighter.TryBeginOrContinueSharedFieldRoute("enemy");
        if (started && fighter.HasActiveMovement)
        {
            fighter.SetMovementCohort(new MovementCohort(
                CreatureFaction.Colony,
                MovementGoalKind.Combat,
                target.Id));
        }

        return started;
    }

    private void ClearPursuitTracking()
    {
        _trackedTargetId = 0;
        _trackedTargetCell = null;
    }
}
