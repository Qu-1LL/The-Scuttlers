using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Events;
using TriloGame.Game.Core.Traits;
using TriloGame.Game.Audio;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.State;

namespace TriloGame.Tests.Traits;

public sealed class TrilobiteTraitHandlerTests
{
    [Fact]
    public void StarterTrilobite_StartsWithNoTraits()
    {
        var (_, _, _, trilobite) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();

        Assert.Equal("None", trilobite.TraitState.GetTraitSummary());
    }

    [Fact]
    public void ExplosiveDeath_KillsNearbyUnits_DestroysNearbyBuilding_AndRequestsScreenShake()
    {
        var (session, cave, _) = TestWorldFactory.CreateSessionWithQueen();
        var requestedShake = 0f;
        GameAudioCue? requestedCue = null;
        DeathMistRequest? requestedMist = null;
        session.ScreenShakeRequested += intensity => requestedShake = Math.Max(requestedShake, intensity);
        session.AudioCueRequested += cue => requestedCue = cue;
        session.DeathMistRequested += request => requestedMist = request;

        var post = new MiningPost(session);
        var postLocation = TestWorldFactory.FindBuildLocation(cave, post);
        Assert.True(cave.Build(post, postLocation));

        var explosionTile = post.TileArray
            .First(tile =>
                tile.CreatureFits() &&
                cave.GetReachableTiles().Count(other =>
                    other.CreatureFits() &&
                    !cave.HasCreatureInCell(other.Coordinates) &&
                    !string.Equals(other.Key, tile.Key, StringComparison.Ordinal) &&
                    GridPoint.ManhattanDistance(other.Coordinates, tile.Coordinates) <= GameConstants.ExplosiveTraitBlastRadius) >= 2);

        var nearbyOpenTiles = cave.GetReachableTiles()
            .Where(tile =>
                tile.CreatureFits() &&
                !cave.HasCreatureInCell(tile.Coordinates) &&
                !string.Equals(tile.Key, explosionTile.Key, StringComparison.Ordinal) &&
                GridPoint.ManhattanDistance(tile.Coordinates, explosionTile.Coordinates) <= GameConstants.ExplosiveTraitBlastRadius)
            .Take(2)
            .ToArray();

        Assert.Equal(2, nearbyOpenTiles.Length);

        var explosive = new Trilobite("Boom", explosionTile.Coordinates, session);
        explosive.SetTraits([TrilobiteTrait.Explosive]);
        Assert.True(cave.Spawn(explosive, explosionTile));
        var explosionOrigin = explosive.Location;

        var victim = new Trilobite("Victim", nearbyOpenTiles[0].Coordinates, session);
        Assert.True(cave.Spawn(victim, nearbyOpenTiles[0]));

        var enemy = new Enemy("Target", nearbyOpenTiles[1].Coordinates, session);
        Assert.True(cave.Spawn(enemy, nearbyOpenTiles[1]));

        explosive.TakeDamage(explosive.Health, "test");

        Assert.True(requestedShake > 0f);
        Assert.Equal(GameAudioCue.TrilobiteExplosion, requestedCue);
        Assert.True(requestedMist.HasValue);
        Assert.Equal(GameConstants.ExplosiveTraitBlastRadius, requestedMist.Value.Radius);
        Assert.Null(explosive.Cave);
        Assert.Null(victim.Cave);
        Assert.Null(enemy.Cave);
        Assert.Null(post.Cave);
        Assert.DoesNotContain(post, cave.GetBuildingList());
    }

    [Fact]
    public void ExplosiveDeath_MinesAndRevealsDestroyedWallTiles()
    {
        var (session, cave, _, _) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();
        var blastPair = cave.GetReachableTiles()
            .Where(tile => tile.CreatureFits() && !cave.HasCreatureInCell(tile.Coordinates))
            .SelectMany(openTile => openTile.Neighbors
                .Where(neighbor => string.Equals(neighbor.Base, "wall", StringComparison.Ordinal))
                .Select(neighbor => new { OpenTile = openTile, WallTile = neighbor }))
            .First();

        var wallTile = blastPair.WallTile;
        var explosiveTile = blastPair.OpenTile;

        var explosive = new Trilobite("Boom Miner", explosiveTile.Coordinates, session);
        explosive.SetTraits([TrilobiteTrait.Explosive]);
        Assert.True(cave.Spawn(explosive, explosiveTile));

        explosive.TakeDamage(explosive.Health, "test");

        Assert.Equal("empty", wallTile.Base);
        Assert.True(cave.IsTileRevealed(wallTile));
        Assert.True(session.Stats.Get(GameEvents.WallMined) > 0);
    }

    [Fact]
    public void ExplosiveDeath_RevealsBoundaryWallsAroundNewlyOpenedSpace()
    {
        var (session, cave, _, _) = TestWorldFactory.CreateSessionWithQueenAndTrilobite();
        var directions = new[]
        {
            new GridPoint(0, -1),
            new GridPoint(0, 1),
            new GridPoint(1, 0),
            new GridPoint(-1, 0)
        };

        var blastPair = cave.GetReachableTiles()
            .Where(tile => tile.CreatureFits() && !cave.HasCreatureInCell(tile.Coordinates))
            .SelectMany(openTile => openTile.Neighbors
                .Where(neighbor => string.Equals(neighbor.Base, "wall", StringComparison.Ordinal))
                .Select(neighbor => new { OpenTile = openTile, WallTile = neighbor }))
            .FirstOrDefault(pair => directions.Any(direction =>
                cave.GetTile(new GridPoint(
                    pair.WallTile.Coordinates.X + direction.X,
                    pair.WallTile.Coordinates.Y + direction.Y)) is null));

        Assert.NotNull(blastPair);

        var wallTile = blastPair!.WallTile;
        var explosiveTile = blastPair.OpenTile;
        var missingBoundaryCoords = directions
            .Select(direction => new GridPoint(wallTile.Coordinates.X + direction.X, wallTile.Coordinates.Y + direction.Y))
            .Where(coords => cave.GetTile(coords) is null)
            .ToArray();

        Assert.NotEmpty(missingBoundaryCoords);

        var explosive = new Trilobite("Boom Miner", explosiveTile.Coordinates, session);
        explosive.SetTraits([TrilobiteTrait.Explosive]);
        Assert.True(cave.Spawn(explosive, explosiveTile));

        explosive.TakeDamage(explosive.Health, "test");

        foreach (var coords in missingBoundaryCoords)
        {
            var boundaryTile = cave.GetTile(coords);
            Assert.NotNull(boundaryTile);
            Assert.Equal("wall", boundaryTile!.Base);
            Assert.True(cave.IsTileRevealed(boundaryTile));
        }
    }
}
