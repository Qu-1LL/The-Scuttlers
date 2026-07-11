using TriloGame.Game.Core.Buildings;

namespace TriloGame.Game.Core.Entities;

internal sealed class TrilobiteFighterBehavior
{
    private const string StationPathMode = "station";

    public bool Step1(Trilobite trilobite)
    {
        if (!trilobite.EnsureFighterState())
        {
            return false;
        }

        trilobite.SetFighterPathMode(null);

        if (ShouldHoldTurretPosition(trilobite))
        {
            trilobite.ClearFighterTarget();
            return false;
        }

        if (!trilobite.Session.Danger)
        {
            trilobite.ClearFighterTarget();
            return trilobite.FighterReturnToStation(true);
        }

        if (!trilobite.EnsureReadyForRoleTileNavigation())
        {
            return false;
        }

        if (trilobite.FighterTargetTileKey is not null && trilobite.IsAdjacentToTileKey(trilobite.FighterTargetTileKey))
        {
            return trilobite.FighterStep2();
        }

        var adjacentEnemyTileKey = trilobite.GetAdjacentEnemyTileKey();
        if (adjacentEnemyTileKey is not null)
        {
            trilobite.SetFighterTargetTileKey(adjacentEnemyTileKey);
            return trilobite.FighterStep2();
        }

        return trilobite.FighterStep3();
    }

    public bool Step2(Trilobite trilobite)
    {
        if (!trilobite.EnsureFighterState())
        {
            return false;
        }

        if (!trilobite.Session.Danger)
        {
            trilobite.ClearFighterTarget();
            return trilobite.FighterReturnToStation(true);
        }

        if (!trilobite.EnsureReadyForRoleTileNavigation())
        {
            return false;
        }

        if (trilobite.FighterTargetTileKey is null)
        {
            return trilobite.FighterStep3();
        }

        var enemy = trilobite.GetEnemyAtTileKey(trilobite.FighterTargetTileKey);
        if (enemy is null)
        {
            trilobite.ClearFighterTarget();
            return trilobite.FighterStep3();
        }

        if (!trilobite.IsAdjacentToTileKey(trilobite.FighterTargetTileKey))
        {
            return trilobite.FighterStep3();
        }

        var dealt = trilobite.DealDamage(enemy);
        if (trilobite.GetEnemyAtTileKey(trilobite.FighterTargetTileKey) is null)
        {
            trilobite.ClearFighterTarget();
        }

        return dealt > 0;
    }

    public bool Step3(Trilobite trilobite)
    {
        if (!trilobite.EnsureFighterState())
        {
            return false;
        }

        if (!trilobite.Session.Danger)
        {
            trilobite.ClearFighterTarget();
            return trilobite.FighterReturnToStation(true);
        }

        if (!trilobite.EnsureReadyForRoleTileNavigation())
        {
            return false;
        }

        if (trilobite.FighterTargetTileKey is not null && trilobite.GetEnemyAtTileKey(trilobite.FighterTargetTileKey) is null)
        {
            trilobite.ClearFighterTarget();
        }

        var cave = trilobite.Cave;
        var field = cave?.GetBfsFieldObject("enemy");
        if (field is null || cave is null)
        {
            trilobite.ClearFighterTarget();
            return trilobite.FighterReturnToStation(false);
        }

        trilobite.ClearActionQueue();
        var resolvedField = field;
        var resolvedNext = field.GetNextStep(trilobite.Location, refresh: false);
        if (resolvedNext is null || (cave.GetTile(resolvedNext.Value.ToString()) is { } attemptedTile && !cave.CanCreatureTraverseTile(trilobite, attemptedTile)))
        {
            var refreshedField = cave.GetBfsFieldObject("enemy");
            refreshedField?.Rebuild();
            if (refreshedField is null)
            {
                trilobite.ClearFighterTarget();
                return trilobite.FighterReturnToStation(false);
            }

            resolvedField = refreshedField;
            resolvedNext = refreshedField.GetNextStep(trilobite.Location, refresh: false);
            if (resolvedField.GetFieldValue(trilobite.Location, refresh: false) == 0)
            {
                trilobite.ClearActionQueue();
                return false;
            }
        }

        if (resolvedNext is null)
        {
            trilobite.ClearFighterTarget();
            return trilobite.FighterReturnToStation(false);
        }

        trilobite.ArmEnemyBfsTraversal(resolvedField);
        trilobite.PathPreview.Add(resolvedNext.Value);
        return trilobite.FighterStepMove(resolvedNext.Value);
    }

    public bool StepMove(Trilobite trilobite, Shared.Math.GridPoint nextLocation)
    {
        if (!trilobite.EnsureFighterState())
        {
            return false;
        }

        if (!trilobite.Session.Danger)
        {
            if (trilobite.FighterPathMode != StationPathMode)
            {
                trilobite.ClearActionQueue();
                return trilobite.FighterStep1();
            }

            var assignedStation = trilobite.GetAssignedFighterStation();
            if (assignedStation is not null && trilobite.TryStationAtFighterStation(assignedStation))
            {
                trilobite.SetFighterPathMode(null);
                trilobite.ClearActionQueue();
                return false;
            }
        }
        else if (trilobite.FighterPathMode == StationPathMode)
        {
            trilobite.SetFighterPathMode(null);
            trilobite.ClearActionQueue();
            return trilobite.FighterStep1();
        }

        if (trilobite.FighterPathMode != StationPathMode)
        {
            if (trilobite.FighterTargetTileKey is not null && trilobite.GetEnemyAtTileKey(trilobite.FighterTargetTileKey) is null)
            {
                trilobite.ClearFighterTarget();
                trilobite.ClearActionQueue();
                return trilobite.FighterStep3();
            }

            var adjacentEnemyTileKey = trilobite.GetAdjacentEnemyTileKey();
            if (adjacentEnemyTileKey is not null)
            {
                trilobite.SetFighterTargetTileKey(adjacentEnemyTileKey);
                trilobite.ClearActionQueue();
                return trilobite.FighterStep2();
            }
        }

        var wasStationMove = trilobite.FighterPathMode == StationPathMode;
        trilobite.ClearRoleBfsTraversal();
        var moved = trilobite.Cave?.MoveCreature(trilobite, nextLocation) ?? false;
        if (!moved)
        {
            if (wasStationMove)
            {
                trilobite.SetFighterPathMode(null);
            }

            trilobite.ClearActionQueue();
            return wasStationMove ? trilobite.FighterReturnToStation(true) : trilobite.FighterStep3();
        }

        if (trilobite.PathPreview.Count > 0)
        {
            trilobite.PathPreview.RemoveAt(0);
        }

        if (wasStationMove)
        {
            var assignedStation = trilobite.GetAssignedFighterStation();
            if (assignedStation is not null && trilobite.TryStationAtFighterStation(assignedStation))
            {
                trilobite.SetFighterPathMode(null);
                trilobite.ClearActionQueue();
                return false;
            }

            return true;
        }

        if (trilobite.FighterTargetTileKey is not null && trilobite.IsAdjacentToTileKey(trilobite.FighterTargetTileKey))
        {
            trilobite.ClearActionQueue();
            return trilobite.FighterStep2();
        }

        var nextAdjacentEnemyTileKey = trilobite.GetAdjacentEnemyTileKey();
        if (nextAdjacentEnemyTileKey is not null)
        {
            trilobite.SetFighterTargetTileKey(nextAdjacentEnemyTileKey);
            trilobite.ClearActionQueue();
            return trilobite.FighterStep2();
        }

        return true;
    }

    private static bool ShouldHoldTurretPosition(Trilobite trilobite)
    {
        return trilobite.GetAssignedFighterStation() is Turret turret &&
               trilobite.IsHostedOnBuilding(turret) &&
               turret.IsCreatureStationed(trilobite);
    }
}
