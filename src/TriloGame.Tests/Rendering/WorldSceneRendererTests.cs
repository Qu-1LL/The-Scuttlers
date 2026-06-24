using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Rendering;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Rendering;

public sealed class WorldSceneRendererTests
{
    [Fact]
    public void NormalizeParallaxOffset_WrapsToNearestTilePeriod()
    {
        Assert.Equal(10f, WorldSceneRenderer.NormalizeParallaxOffset(110f, 100f));
        Assert.Equal(-10f, WorldSceneRenderer.NormalizeParallaxOffset(-110f, 100f));
    }

    [Fact]
    public void NormalizeParallaxOffset_ReturnsZeroForInvalidPeriod()
    {
        Assert.Equal(0f, WorldSceneRenderer.NormalizeParallaxOffset(42f, 0f));
        Assert.Equal(0f, WorldSceneRenderer.NormalizeParallaxOffset(42f, -4f));
    }

    [Fact]
    public void CalculateParallaxOffset_UsesScreenOffsetWithoutCameraScale()
    {
        var parallaxScreenOffset = new Vector2(200f, -80f);

        var offset = WorldSceneRenderer.CalculateParallaxOffset(parallaxScreenOffset, periodWidth: 1000f, periodHeight: 1000f);

        Assert.Equal(200f * GameConstants.CaveBackgroundParallaxFactor, offset.X);
        Assert.Equal(-80f * GameConstants.CaveBackgroundParallaxFactor, offset.Y);
    }

    [Fact]
    public void GetWorldSpritePhaseOffsetSeconds_OnlyOffsetsLumenite()
    {
        var coordinates = new GridPoint(7, -3);

        var lumeniteOffset = WorldSceneRenderer.GetWorldSpritePhaseOffsetSeconds(OreType.LUMENITE.Name, coordinates);
        var chitinstoneOffset = WorldSceneRenderer.GetWorldSpritePhaseOffsetSeconds(OreType.CHITINSTONE.Name, coordinates);

        Assert.InRange(lumeniteOffset, 0f, 0.999f);
        Assert.Equal(0f, chitinstoneOffset);
    }

    [Fact]
    public void ShouldDrawFloorTile_RequiresFloorCoverWhenTileHasNoBuilding()
    {
        var tile = new Tile(0, "0,0");
        Assert.True(WorldSceneRenderer.ShouldDrawFloorTile(tile));

        tile.SetFloorCover(false);
        Assert.False(WorldSceneRenderer.ShouldDrawFloorTile(tile));
    }

    [Fact]
    public void GetTileOverlayRotationRadians_UsesGeneratedOreQuarterTurns()
    {
        var tile = new Tile(0, "0,0");
        tile.SetBase(OreType.CHITINSTONE.Name);
        tile.ConfigureOre(2, 1);
        tile.SetOreRotationQuarterTurns(3);

        Assert.Equal(MathF.PI * 1.5f, WorldSceneRenderer.GetTileOverlayRotationRadians(tile));

        tile.ClearResourceState();
        Assert.Equal(0f, WorldSceneRenderer.GetTileOverlayRotationRadians(tile));
    }

    [Fact]
    public void IsMeleeCombatPair_ReturnsTrueWhenFighterTargetsAdjacentEnemy()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(2, 1);
        var trilobite = SpawnTrilobite(cave, session, new GridPoint(0, 0), "fighter");
        var enemy = SpawnEnemy(cave, session, new GridPoint(1, 0));
        trilobite.SetFighterTargetTileKey(enemy.Location.ToString());

        Assert.True(WorldSceneRenderer.IsMeleeCombatPair(cave, trilobite, enemy));
    }

    [Fact]
    public void IsMeleeCombatPair_ReturnsTrueWhenEnemyTargetsAdjacentWorker()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(2, 1);
        var trilobite = SpawnTrilobite(cave, session, new GridPoint(0, 0), "miner");
        var enemy = SpawnEnemy(cave, session, new GridPoint(1, 0));

        Assert.True(enemy.EnemyStep1());
        Assert.True(WorldSceneRenderer.IsMeleeCombatPair(cave, trilobite, enemy));
    }

    [Fact]
    public void IsMeleeCombatPair_ReturnsFalseForUntargetedAdjacentEntities()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(2, 1);
        var trilobite = SpawnTrilobite(cave, session, new GridPoint(0, 0), "miner");
        var enemy = SpawnEnemy(cave, session, new GridPoint(1, 0));

        Assert.False(WorldSceneRenderer.IsMeleeCombatPair(cave, trilobite, enemy));
    }

    [Fact]
    public void CalculateCombatIndicatorWorldPosition_ReturnsEntityMidpoint()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(2, 1);
        var trilobite = SpawnTrilobite(cave, session, new GridPoint(0, 0), "fighter");
        var enemy = SpawnEnemy(cave, session, new GridPoint(1, 0));

        var position = WorldSceneRenderer.CalculateCombatIndicatorWorldPosition(trilobite, enemy);

        Assert.Equal(TileConstants.TileHalfSize, position.X);
        Assert.Equal(0f, position.Y);
    }

    private static Trilobite SpawnTrilobite(Cave cave, GameSession session, GridPoint location, string assignment)
    {
        cave.ReachableTiles.Add(cave.GetTile(location)!);
        var trilobite = new Trilobite("Test Trilobite", location, session)
        {
            Assignment = assignment
        };
        Assert.True(cave.Spawn(trilobite, cave.GetTile(location)!));
        return trilobite;
    }

    private static Enemy SpawnEnemy(Cave cave, GameSession session, GridPoint location)
    {
        var enemy = new Enemy("Test Enemy", location, session);
        Assert.True(cave.Spawn(enemy, cave.GetTile(location)!));
        return enemy;
    }
}
