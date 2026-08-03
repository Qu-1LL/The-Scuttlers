using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Rendering.Lighting;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.Rendering;

public sealed class BuildingLightEmissionTests
{
    [Fact]
    public void MiningPostEmitsSteadyWhiteLight()
    {
        var (session, _) = TestWorldFactory.CreateRectangularSession(12, 12);
        var post = new MiningPost(session);

        Assert.True(BuildingLightSettings.TryGetEmission(post, 0f, out var first));
        Assert.True(BuildingLightSettings.TryGetEmission(post, 3.7f, out var later));

        // White, and the same white at every point in time: the post is work lighting, not a flame.
        Assert.Equal(BuildingLightSettings.MiningPostColor, first.Color);
        // Near-neutral: bright on every channel, with no channel far enough from the others to read
        // as a tint.
        var channels = new[] { first.Color.R, first.Color.G, first.Color.B };
        Assert.True(channels.Min() > 240 && channels.Max() - channels.Min() < 16,
            $"expected a white light, got {first.Color}");
        Assert.Equal(first.Intensity, later.Intensity);
        Assert.Equal(BuildingLightSettings.MiningPostIntensity, first.Intensity);
    }

    [Fact]
    public void CampfireEmitsOrangeLight()
    {
        var (session, _) = TestWorldFactory.CreateRectangularSession(12, 12);
        var barracks = new Barracks(session);

        Assert.True(BuildingLightSettings.TryGetEmission(barracks, 0f, out var emission));

        var color = emission.Color;
        Assert.True(color.R > color.G && color.G > color.B,
            $"expected a warm orange ramp R > G > B, got {color}");
    }

    [Fact]
    public void CampfireIntensityPulsesWithinItsAmplitude()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(12, 12);
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(2, 2));

        var samples = new List<float>();
        // Two full periods, sampled finely enough to catch the peaks of the faster component.
        for (var step = 0; step <= 400; step++)
        {
            var seconds = step / 400f * BuildingLightSettings.CampfirePulseSeconds * 2f;
            Assert.True(BuildingLightSettings.TryGetEmission(barracks, seconds, out var emission));
            samples.Add(emission.Intensity);
        }

        var minimum = samples.Min();
        var maximum = samples.Max();
        Assert.True(maximum - minimum > 0.05f,
            $"expected the fire to visibly pulse, got a swing of {maximum - minimum}");
        // The swing stays inside the authored amplitude, so tuning that constant is the only way to
        // make the fire flicker harder.
        var bound = BuildingLightSettings.CampfireIntensity
            * (1f + BuildingLightSettings.CampfirePulseAmplitude);
        Assert.All(samples, sample => Assert.InRange(sample, 0f, MathF.Min(1f, bound) + 0.0001f));
    }

    // Two fires in the same room must not breathe in unison - that reads as a pair of blinking
    // lights rather than as two fires.
    [Fact]
    public void CampfiresAtDifferentPlacesFlickerOutOfStep()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(24, 12);
        var first = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(1, 1));
        var second = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(9, 6));

        var separated = false;
        for (var step = 0; step <= 200; step++)
        {
            var seconds = step / 200f * BuildingLightSettings.CampfirePulseSeconds;
            BuildingLightSettings.TryGetEmission(first, seconds, out var a);
            BuildingLightSettings.TryGetEmission(second, seconds, out var b);
            if (MathF.Abs(a.Intensity - b.Intensity) > 0.02f)
            {
                separated = true;
                break;
            }
        }

        Assert.True(separated, "expected the two campfires to hold different phases");
    }

    [Fact]
    public void NonEmittingBuildingsStayDark()
    {
        var (session, _) = TestWorldFactory.CreateRectangularSession(12, 12);

        Assert.False(BuildingLightSettings.TryGetEmission(new Silo(session), 0f, out _));
        Assert.False(BuildingLightSettings.TryGetEmission(new Wall(session), 0f, out _));
        Assert.False(BuildingLightSettings.TryGetEmission(new Turret(session), 0f, out _));
    }

    // The world tile grid is what the ray march reads, so this is the pass that decides whether a
    // building lights the cave at all - the screen-space halo only puts a glow on the sprite.
    [Fact]
    public void ApplyBuildingEmission_SeedsTheCentreCellOfEachEmittingBuilding()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(16, 16);
        var post = TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(2, 2));
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(9, 9));
        var layout = new LightingTileGridLayout(Point.Zero, new Point(16, 16));
        var (cells, colors) = CreateRevealedGrid(layout);

        LightingTileGrid.ApplyBuildingEmission(cave, layout, 0f, cells, colors);

        var postCell = layout.GetIndex(ToPoint(post.GetCenter()));
        var fireCell = layout.GetIndex(ToPoint(barracks.GetCenter()));
        Assert.True(cells[postCell].B > 0, "expected the mining post to emit");
        Assert.True(cells[fireCell].B > 0, "expected the campfire to emit");
        Assert.Equal(BuildingLightSettings.MiningPostColor, colors[postCell]);
        Assert.Equal(BuildingLightSettings.CampfireColor, colors[fireCell]);
    }

    // One cell per building, not one per footprint tile: the march gathers each emitting cell
    // independently, so a 3x3 of them would be nine times the source a deposit is.
    [Fact]
    public void ApplyBuildingEmission_LightsOneCellPerBuilding()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(16, 16);
        TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(2, 2));
        var layout = new LightingTileGridLayout(Point.Zero, new Point(16, 16));
        var (cells, colors) = CreateRevealedGrid(layout);

        LightingTileGrid.ApplyBuildingEmission(cave, layout, 0f, cells, colors);

        Assert.Equal(1, cells.Count(cell => cell.B > 0));
    }

    [Fact]
    public void ApplyBuildingEmission_LeavesUnrevealedCellsDark()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(16, 16);
        TestWorldFactory.BuildBarracks(cave, session, new GridPoint(4, 4));
        var layout = new LightingTileGridLayout(Point.Zero, new Point(16, 16));
        // G = 0 is an unknown cell - see LightingTileGrid.EncodeCell.
        var cells = new Color[layout.Width * layout.Height];
        var colors = new Color[layout.Width * layout.Height];

        LightingTileGrid.ApplyBuildingEmission(cave, layout, 0f, cells, colors);

        Assert.All(cells, cell => Assert.Equal(0, cell.B));
    }

    [Fact]
    public void ApplyBuildingEmission_PreservesTheCellsOpacityAndKnownFlag()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(16, 16);
        var barracks = TestWorldFactory.BuildBarracks(cave, session, new GridPoint(4, 4));
        var layout = new LightingTileGridLayout(Point.Zero, new Point(16, 16));
        var (cells, colors) = CreateRevealedGrid(layout);
        var centre = layout.GetIndex(ToPoint(barracks.GetCenter()));
        // A partly occluding cell, as the building occluder pass would have left it.
        cells[centre] = new Color(0.6f, 1f, 0f, 1f);

        LightingTileGrid.ApplyBuildingEmission(cave, layout, 0f, cells, colors);

        Assert.Equal(153, cells[centre].R);
        Assert.Equal(255, cells[centre].G);
        Assert.True(cells[centre].B > 0);
    }

    [Fact]
    public void CollectBuildingEmitters_CollectsOnlyEmittingBuildings()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(24, 24);
        TestWorldFactory.BuildMiningPost(cave, session, new GridPoint(1, 1));
        TestWorldFactory.BuildBarracks(cave, session, new GridPoint(6, 6));
        TestWorldFactory.BuildSilo(cave, session, new GridPoint(12, 12));
        var emitters = new List<BuildingLightEmitter>();

        var count = new LightingSourceCollector().CollectBuildingEmitters(
            cave,
            showFullMapVisibility: true,
            elapsedSeconds: 0f,
            emitters);

        Assert.Equal(2, count);
        Assert.Contains(emitters, emitter => emitter.LightColor == BuildingLightSettings.MiningPostColor);
        Assert.Contains(emitters, emitter => emitter.LightColor == BuildingLightSettings.CampfireColor);
        Assert.All(emitters, emitter => Assert.True(emitter.RadiusTiles > 0f));
    }

    [Fact]
    public void CollectBuildingEmitters_SkipsBuildingsInUnrevealedGround()
    {
        var (session, cave) = TestWorldFactory.CreateRectangularSession(24, 24);
        TestWorldFactory.BuildBarracks(cave, session, new GridPoint(6, 6));
        cave.RevealedTiles.Clear();
        var emitters = new List<BuildingLightEmitter>();

        var count = new LightingSourceCollector().CollectBuildingEmitters(
            cave,
            showFullMapVisibility: false,
            elapsedSeconds: 0f,
            emitters);

        Assert.Equal(0, count);
        Assert.Empty(emitters);
    }

    // Every cell revealed (G = 255), clear (R = 0) and unlit (B = 0).
    private static (Color[] Cells, Color[] Colors) CreateRevealedGrid(LightingTileGridLayout layout)
    {
        var cells = new Color[layout.Width * layout.Height];
        Array.Fill(cells, new Color(0f, 1f, 0f, 1f));
        return (cells, new Color[layout.Width * layout.Height]);
    }

    private static Point ToPoint(GridPoint coordinates) => new(coordinates.X, coordinates.Y);
}
