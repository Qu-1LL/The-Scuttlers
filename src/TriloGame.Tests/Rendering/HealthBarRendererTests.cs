using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Vehicles;
using TriloGame.Game.Core.World;
using TriloGame.Game.Rendering;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Rendering;

public sealed class HealthBarRendererTests
{
    [Theory]
    [InlineData(20, 20)]
    [InlineData(25, 20)]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void Layout_HidesFullDeadOrInvalidHealth(int health, int maximum)
    {
        Assert.False(HealthBarLayout.TryCreate(new TestHealth(health, maximum), Vector2.Zero, CreateCamera(), out _));
    }

    [Theory]
    [InlineData(0.05f)]
    [InlineData(0.15625f)]
    [InlineData(0.4f)]
    public void Layout_StaysThreeQuartersOfATileAndCenteredAboveEntityAtEveryZoom(float scale)
    {
        var camera = CreateCamera(scale);
        camera.SetOrigin(new Vector2(-700f, 350f));
        var topCenter = new Vector2(500f, 100f);

        Assert.True(HealthBarLayout.TryCreate(new TestHealth(5, 20), topCenter, camera, out var layout));

        Assert.Equal(TileConstants.TileSize * scale * 0.75f, layout.Size.X, 3);
        var screenAnchor = camera.WorldToScreen(topCenter);
        Assert.Equal(screenAnchor.X, layout.Position.X + (layout.Size.X / 2f), 3);
        Assert.Equal(screenAnchor.Y - (HealthBarLayout.Gap * scale), layout.Position.Y + layout.Size.Y, 3);
        Assert.Equal(0.25f, layout.Fraction);
        Assert.Equal(layout.TrackSize.X * 0.25f, layout.FillSize.X, 3);
        Assert.Equal(layout.TrackSize.Y, layout.FillSize.Y);
        Assert.Equal(layout.Position + new Vector2(layout.Border), layout.FillPosition);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreatureBar_TracksDamageHealingAndDeath(bool enemy)
    {
        var session = new GameSession();
        Creature creature = enemy
            ? new Enemy("Ant", GridPoint.Zero, session)
            : new Trilobite("Worker", GridPoint.Zero, session);
        var camera = CreateCamera();
        var spriteSize = new Vector2(TileConstants.TileSize);

        Assert.False(HealthBarRenderer.TryGetCreatureLayout(creature, spriteSize, camera, 1f, out _));
        creature.TakeDamage(5);
        Assert.True(HealthBarRenderer.TryGetCreatureLayout(creature, spriteSize, camera, 1f, out var damaged));
        Assert.Equal(creature.Health / (float)creature.MaxHealth, damaged.Fraction);
        creature.TakeDamage(5);
        Assert.True(HealthBarRenderer.TryGetCreatureLayout(creature, spriteSize, camera, 1f, out var moreDamaged));
        Assert.True(moreDamaged.FillSize.X < damaged.FillSize.X);
        Assert.Equal(creature.MaxHealth, creature.RestoreHealth());
        Assert.False(HealthBarRenderer.TryGetCreatureLayout(creature, spriteSize, camera, 1f, out _));
        creature.TakeDamage(int.MaxValue);
        Assert.False(HealthBarRenderer.TryGetCreatureLayout(creature, spriteSize, camera, 1f, out _));
    }

    [Fact]
    public void CreatureBar_FollowsInterpolatedPositionAndHidesWithCreature()
    {
        var creature = new Trilobite("Worker", GridPoint.Zero, new GameSession());
        creature.TakeDamage(1);
        creature.SetWorldPosition(WorldPoint.FromWorldPixels(new System.Numerics.Vector2(512f, 256f)), snapPrevious: false);
        var camera = CreateCamera();
        var spriteSize = new Vector2(TileConstants.TileSize);

        Assert.True(HealthBarRenderer.TryGetCreatureLayout(creature, spriteSize, camera, 0f, out var start));
        Assert.True(HealthBarRenderer.TryGetCreatureLayout(creature, spriteSize, camera, 0.5f, out var middle));
        Assert.True(HealthBarRenderer.TryGetCreatureLayout(creature, spriteSize, camera, 1f, out var end));
        Assert.Equal(start.Position + (new Vector2(256f, 128f) * camera.CurrentScale), middle.Position);
        Assert.Equal((start.Position + end.Position) / 2f, middle.Position);

        creature.IsVisible = false;
        Assert.False(HealthBarRenderer.TryGetCreatureLayout(creature, spriteSize, camera, 1f, out _));
    }

    [Theory]
    [InlineData("queen")]
    [InlineData("wall")]
    [InlineData("soil")]
    [InlineData("scaffold")]
    public void BuildingBar_TracksDamageHealingAndDeathAcrossBuildingTypes(string kind)
    {
        var session = new GameSession();
        var cave = new Cave(session, generateDefaultMap: false);
        cave.AddTile(GridPoint.Zero.ToString());
        Building building = kind switch
        {
            "queen" => new Queen(session),
            "wall" => new Wall(session),
            "soil" => new SoilPatch(session),
            _ => new Scaffolding(session, new Wall(session))
        };
        building.Location = GridPoint.Zero;
        var camera = CreateCamera();

        Assert.False(HealthBarRenderer.TryGetBuildingLayout(building, cave, camera, true, out _));
        building.TakeDamage(1);
        Assert.True(HealthBarRenderer.TryGetBuildingLayout(building, cave, camera, true, out var damaged));
        Assert.Equal(building.Health / (float)building.MaxHealth, damaged.Fraction);
        Assert.Equal(building.MaxHealth, building.RestoreHealth());
        Assert.False(HealthBarRenderer.TryGetBuildingLayout(building, cave, camera, true, out _));
        building.TakeDamage(int.MaxValue);
        Assert.False(HealthBarRenderer.TryGetBuildingLayout(building, cave, camera, true, out _));
    }

    [Fact]
    public void BuildingBar_SitsAboveRotatedFootprintAndHidesUnplacedBuildings()
    {
        var session = new GameSession();
        var cave = new Cave(session, generateDefaultMap: false);
        var building = new AlgaeFarm(session) { Location = new GridPoint(-3, 7) };
        building.TakeDamage(1);
        building.RotateMap();
        var camera = CreateCamera();

        Assert.True(HealthBarRenderer.TryGetBuildingLayout(building, cave, camera, false, out var layout));
        var topCenter = new Vector2(-2f * TileConstants.TileSize, (7f * TileConstants.TileSize) - TileConstants.TileHalfSize);
        var screenTopCenter = camera.WorldToScreen(topCenter);
        Assert.Equal(screenTopCenter.X, layout.Position.X + (layout.Size.X / 2f), 3);
        Assert.Equal(screenTopCenter.Y - (HealthBarLayout.Gap * camera.CurrentScale), layout.Position.Y + layout.Size.Y, 3);
        Assert.Equal(TileConstants.TileSize * 0.75f * camera.CurrentScale, layout.Size.X, 3);

        building.Location = null;
        Assert.False(HealthBarRenderer.TryGetBuildingLayout(building, cave, camera, false, out _));
    }

    [Fact]
    public void ScaffoldBar_UsesTheSameRevealRuleAsItsSprites()
    {
        var session = new GameSession();
        var cave = new Cave(session, generateDefaultMap: false);
        var tile = cave.AddTile(GridPoint.Zero.ToString());
        var scaffold = new Scaffolding(session, new Wall(session)) { Location = GridPoint.Zero };
        scaffold.TakeDamage(1);
        var camera = CreateCamera();

        Assert.False(HealthBarRenderer.TryGetBuildingLayout(scaffold, cave, camera, false, out _));
        Assert.True(HealthBarRenderer.TryGetBuildingLayout(scaffold, cave, camera, true, out _));
        cave.RevealedTiles.Add(tile);
        Assert.True(HealthBarRenderer.TryGetBuildingLayout(scaffold, cave, camera, false, out _));
    }

    [Fact]
    public void VehicleBar_TracksDamageHealingMovementAndRemoval()
    {
        var (session, cave, _) = TestWorldFactory.CreateRectangularSessionWithQueen(18, 14, GridPoint.Zero);
        var plow = new Plow(session);
        var camera = CreateCamera();
        var spriteSize = new Vector2(TileConstants.TileSize * 2f);
        Assert.True(cave.SpawnVehicle(plow, new GridPoint(5, 6)));

        Assert.False(HealthBarRenderer.TryGetVehicleLayout(plow, spriteSize, camera, 1f, out _));
        plow.TakeDamage(10);
        Assert.True(HealthBarRenderer.TryGetVehicleLayout(plow, spriteSize, camera, 1f, out var damaged));
        Assert.Equal(0.75f, damaged.Fraction);
        Assert.Equal(TileConstants.TileSize * 0.75f * camera.CurrentScale, damaged.Size.X, 3);

        plow.EnqueueMove(new GridPoint(6, 6));
        Assert.True(plow.Move() is true);
        Assert.True(HealthBarRenderer.TryGetVehicleLayout(plow, spriteSize, camera, 0f, out var start));
        Assert.True(HealthBarRenderer.TryGetVehicleLayout(plow, spriteSize, camera, 0.5f, out var middle));
        Assert.True(HealthBarRenderer.TryGetVehicleLayout(plow, spriteSize, camera, 1f, out var end));
        Assert.True(end.Position.X > start.Position.X);
        Assert.Equal((start.Position + end.Position) / 2f, middle.Position);

        IVehicle vehicle = plow;
        Assert.Equal(vehicle.MaxHealth, vehicle.RestoreHealth());
        Assert.False(HealthBarRenderer.TryGetVehicleLayout(plow, spriteSize, camera, 1f, out _));
        plow.TakeDamage(1);
        Assert.True(plow.RemoveFromGame());
        Assert.False(HealthBarRenderer.TryGetVehicleLayout(plow, spriteSize, camera, 1f, out _));
    }

    [Theory]
    [InlineData(0f, 100f, 100f)]
    [InlineData(1.57079632679f, 100f, 0f)]
    [InlineData(0.78539816339f, 100f, -53.55339f)]
    public void SpriteAnchor_StaysAboveRotatedBounds(float rotation, float expectedX, float expectedY)
    {
        var topCenter = HealthBarRenderer.GetSpriteTopCenter(new Vector2(100f, 300f),
            new Vector2(600f, 400f), new Vector2(300f, 200f), rotation);

        Assert.Equal(expectedX, topCenter.X, 3);
        Assert.Equal(expectedY, topCenter.Y, 3);
    }

    [Fact]
    public void SpriteAnchor_AccountsForVehicleTextureExtendingBeyondItsPivot()
    {
        var topCenter = HealthBarRenderer.GetSpriteTopCenter(new Vector2(100f, 300f),
            new Vector2(600f, 400f), new Vector2(200f, 200f), MathF.PI / 2f);

        Assert.Equal(100f, topCenter.X, 3);
        Assert.Equal(100f, topCenter.Y, 3);
    }

    [Fact]
    public void Layout_CullsOffscreenBarsButKeepsBarsPartlyInsideViewport()
    {
        var viewport = new Point(800, 600);
        var layout = new HealthBarLayout(new Vector2(-10f, -2f), new Vector2(60f, 5f), 1f, 0.5f);

        Assert.True(layout.IntersectsViewport(viewport));
        Assert.False((layout with { Position = new Vector2(-60f, 0f) }).IntersectsViewport(viewport));
        Assert.False((layout with { Position = new Vector2(800f, 0f) }).IntersectsViewport(viewport));
        Assert.False((layout with { Position = new Vector2(0f, -5f) }).IntersectsViewport(viewport));
        Assert.False((layout with { Position = new Vector2(0f, 600f) }).IntersectsViewport(viewport));
    }

    private static CameraController CreateCamera(float scale = 0.15625f)
    {
        var camera = new CameraController { CurrentScale = scale };
        camera.SetViewport(1280, 720);
        return camera;
    }

    private sealed record TestHealth(int Health, int MaxHealth) : IHealth;
}
