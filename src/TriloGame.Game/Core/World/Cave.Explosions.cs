using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Audio;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.World;

public sealed partial class Cave
{
    public void TriggerDeathExplosion(Trilobite source, object? deathSource = null)
    {
        var origin = !source.IsTrackedInTileSystem && source.HostedBuilding is not null
            ? source.HostedBuilding.GetCenter()
            : source.Location;
        var originTileKey = origin.ToString();
        var affectedTiles = GetExplosionTiles(origin, GameConstants.ExplosiveTraitBlastRadius);
        if (affectedTiles.Count == 0)
        {
            return;
        }

        Session.RequestScreenShake(GameConstants.ExplosiveTraitScreenShakeIntensity);
        Session.RequestAudioCue(GameAudioCue.TrilobiteExplosion);
        Session.RequestDeathMist(origin, GameConstants.ExplosiveTraitBlastRadius);

        var buildingsToDestroy = new HashSet<Building>();
        var creaturesToKill = new HashSet<Creature>();
        foreach (var tile in affectedTiles)
        {
            if (tile.Built is not null)
            {
                buildingsToDestroy.Add(tile.Built);
            }

            foreach (var trilobite in tile.Trilobites)
            {
                if (!ReferenceEquals(trilobite, source) && trilobite.Cave == this)
                {
                    creaturesToKill.Add(trilobite);
                }
            }

            if (tile.EnemyOccupant is { Cave: not null } enemy && enemy.Cave == this)
            {
                creaturesToKill.Add(enemy);
            }
        }

        var changedTileKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tile in affectedTiles)
        {
            DestroySurfaceFeaturesAt(tile);
            DestroyMineableTile(tile, originTileKey, source, changedTileKeys);
        }

        if (changedTileKeys.Count > 0)
        {
            RefreshExplosionTerrain(changedTileKeys);
        }

        foreach (var creature in creaturesToKill.ToArray())
        {
            if (creature.Cave != this || creature.Health <= 0)
            {
                continue;
            }

            creature.TakeDamage(creature.Health, source);
        }

        foreach (var building in buildingsToDestroy.ToArray())
        {
            if (building.Cave != this || building.Health <= 0)
            {
                continue;
            }

            building.TakeDamage(building.Health, source);
        }
    }

    private List<Tile> GetExplosionTiles(GridPoint center, int radius)
    {
        var result = new List<Tile>();
        for (var dx = -radius; dx <= radius; dx++)
        {
            var maxDy = radius - Math.Abs(dx);
            for (var dy = -maxDy; dy <= maxDy; dy++)
            {
                var tile = GetTile(new GridPoint(center.X + dx, center.Y + dy));
                if (tile is not null)
                {
                    result.Add(tile);
                }
            }
        }

        return result;
    }

    private void DestroySurfaceFeaturesAt(Tile tile)
    {
        if (_antHolesByTileKey.Remove(tile.Key, out var antHole))
        {
            foreach (var ant in antHole.Ants.ToArray())
            {
                _antHoleByEnemy.Remove(ant);
            }
        }

        if (HasOpal(tile))
        {
            _opalNode = null;
        }
    }

    private void DestroyMineableTile(Tile tile, string dropTargetTileKey, object? source, ISet<string> changedTileKeys)
    {
        if (string.Equals(tile.Base, "wall", StringComparison.Ordinal))
        {
            var dropTile = ResolveExplosionDropTile(tile, dropTargetTileKey);
            tile.SetBase("empty");
            tile.ClearResourceState();
            tile.CreatureCanFit = true;
            if (dropTile is not null)
            {
                dropTile.AddDroppedResource(Core.Economy.OreType.SANDSTONE.Name, GameConstants.WallDropAmount);
            }

            Session.EmitMineEvents("wall", this, tile.Key, source);
            changedTileKeys.Add(tile.Key);
            return;
        }

        if (tile.IsOreTile())
        {
            var tileType = tile.Base;
            var yield = tile.ResourceYield;
            tile.SetBase("empty");
            tile.ClearResourceState();
            for (var index = 0; index < yield; index++)
            {
                Session.EmitMineEvents(tileType, this, tile.Key, source);
            }

            changedTileKeys.Add(tile.Key);
        }
    }

    private void RefreshExplosionTerrain(IEnumerable<string> changedTileKeys)
    {
        var dirtyKeys = new HashSet<string>(changedTileKeys, StringComparer.Ordinal);
        var shouldRevealCave = false;

        foreach (var tileKey in dirtyKeys.ToArray())
        {
            var tile = GetTile(tileKey);
            if (tile is null)
            {
                continue;
            }

            RevealTile(tile);

            var remainingDirections = new Dictionary<string, GridPoint>
            {
                ["n"] = new GridPoint(0, -1),
                ["s"] = new GridPoint(0, 1),
                ["e"] = new GridPoint(1, 0),
                ["w"] = new GridPoint(-1, 0)
            };

            foreach (var neighbor in tile.Neighbors)
            {
                var dx = neighbor.Coordinates.X - tile.Coordinates.X;
                var dy = neighbor.Coordinates.Y - tile.Coordinates.Y;
                if (dx == 1)
                {
                    remainingDirections.Remove("e");
                }
                else if (dx == -1)
                {
                    remainingDirections.Remove("w");
                }
                else if (dy == -1)
                {
                    remainingDirections.Remove("n");
                }
                else if (dy == 1)
                {
                    remainingDirections.Remove("s");
                }

                RevealTile(neighbor);
                dirtyKeys.Add(neighbor.Key);
                if (!string.Equals(neighbor.Base, "wall", StringComparison.Ordinal) && !IsTileReachable(neighbor))
                {
                    shouldRevealCave = true;
                }
            }

            foreach (var direction in remainingDirections.Values)
            {
                var neighborCoords = new GridPoint(tile.Coordinates.X + direction.X, tile.Coordinates.Y + direction.Y);
                var neighborKey = neighborCoords.ToString();
                var neighbor = GetTile(neighborKey);
                if (neighbor is null)
                {
                    neighbor = AddTile(neighborKey);
                    neighbor.SetBase("wall");
                    neighbor.CreatureCanFit = false;
                    neighbor.ConfigureWall(GameConstants.WallHitsRequired);
                }

                tile.AddNeighbor(neighbor);
                dirtyKeys.Add(neighbor.Key);
                RevealTile(neighbor);

                if (!string.Equals(neighbor.Base, "wall", StringComparison.Ordinal) && !IsTileReachable(neighbor))
                {
                    shouldRevealCave = true;
                }
            }
        }

        if (shouldRevealCave)
        {
            RevealCave();
        }

        var dirtyKeyArray = dirtyKeys.ToArray();
        var reachability = RefreshReachableTiles();
        var dirtyFieldKeys = dirtyKeyArray.Concat(reachability.ChangedKeys).Distinct(StringComparer.Ordinal).ToArray();
        MarkAllBuildingFieldsDirty(dirtyFieldKeys, [], []);
        NotifyMineableTilesChanged(dirtyKeyArray);
        RebalanceAllBfsFields(dirtyFieldKeys, [], []);
    }

    private Tile? ResolveExplosionDropTile(Tile minedTile, string? dropTargetTileKey)
    {
        if (!string.IsNullOrWhiteSpace(dropTargetTileKey))
        {
            var explicitTile = GetTile(dropTargetTileKey);
            if (explicitTile is not null && explicitTile.CreatureFits())
            {
                return explicitTile;
            }
        }

        return minedTile.Neighbors.FirstOrDefault(tile => tile.CreatureFits()) ?? minedTile;
    }
}
