using Microsoft.Xna.Framework;
using TriloGame.Game.Audio;
using TriloGame.Game.Core.Constants;

namespace TriloGame.Tests.Audio;

public sealed class FocusAudioSystemTests
{
    [Fact]
    public void CreatureEffectFootprint_DefaultZoomMatchesThirtySixTileScreenCoverage()
    {
        var coverage = FocusAudioSystem.CalculateSquareCoverageForTesting(
            AudioCueRequest.CreatureEffectFootprintTiles,
            GameConstants.DefaultCameraScale,
            1280,
            720);

        Assert.Equal(230400f / (1280f * 720f), coverage, precision: 6);
    }

    [Fact]
    public void CreatureEffectFootprint_GetsLouderWhenZoomedIn()
    {
        var defaultCoverage = FocusAudioSystem.CalculateSquareCoverageForTesting(
            AudioCueRequest.CreatureEffectFootprintTiles,
            GameConstants.DefaultCameraScale,
            1280,
            720);
        var zoomedCoverage = FocusAudioSystem.CalculateSquareCoverageForTesting(
            AudioCueRequest.CreatureEffectFootprintTiles,
            GameConstants.MaxScale,
            1280,
            720);

        Assert.True(zoomedCoverage > defaultCoverage);
    }

    [Fact]
    public void ScreenSpaceCoverage_OffscreenSourceFallsSilent()
    {
        var coverage = ScreenSpaceAudio.CalculateVisibleCoverage(
            new Vector2(-500f, -500f),
            2f,
            2f,
            GameConstants.DefaultCameraScale,
            1280,
            720);

        Assert.Equal(0f, coverage);
    }

    [Theory]
    [InlineData(GameAudioCue.AlgaeFarmFocus)]
    [InlineData(GameAudioCue.RadarFocus)]
    public void FocusCue_UsesFootprintCoverageThreshold(GameAudioCue cue)
    {
        var tuning = FocusAudioSystem.GetTuning(cue);

        Assert.Equal(0.001f, tuning.PlayThreshold);
    }
}
