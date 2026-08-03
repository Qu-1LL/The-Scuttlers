using TriloGame.Game.Audio;

namespace TriloGame.Tests.Audio;

public sealed class ClickPitchVariationTests
{
    [Fact]
    public void AllCues_ExposeThreePitchVariants()
    {
        var expected = ClickPitchVariation.GetPitches(GameAudioCue.BuildingFinished);
        foreach (var cue in Enum.GetValues<GameAudioCue>())
        {
            var pitches = ClickPitchVariation.GetPitches(cue);
            Assert.Equal(3, pitches.Count);
            Assert.Equal(expected, pitches);
        }
    }

    [Fact]
    public void NonUiCues_UseTheSameThreeTonePitchSet()
    {
        Assert.Equal(-0.06f, ClickPitchVariation.GetPitchForIndex(GameAudioCue.BuildingFinished, 0));
        Assert.Equal(0f, ClickPitchVariation.GetPitchForIndex(GameAudioCue.BuildingFinished, 1));
        Assert.Equal(0.06f, ClickPitchVariation.GetPitchForIndex(GameAudioCue.TrilobiteBirth, 2));
    }

    [Fact]
    public void GetPitchForIndex_CyclesAcrossTheThreeToneSet()
    {
        Assert.Equal(-0.06f, ClickPitchVariation.GetPitchForIndex(GameAudioCue.UiSelect, 0));
        Assert.Equal(0f, ClickPitchVariation.GetPitchForIndex(GameAudioCue.UiSelect, 1));
        Assert.Equal(0.06f, ClickPitchVariation.GetPitchForIndex(GameAudioCue.UiSelect, 2));
        Assert.Equal(-0.06f, ClickPitchVariation.GetPitchForIndex(GameAudioCue.UiSelect, 3));
    }
}
