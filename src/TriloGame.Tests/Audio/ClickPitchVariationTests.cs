using TriloGame.Game.Audio;

namespace TriloGame.Tests.Audio;

public sealed class ClickPitchVariationTests
{
    [Fact]
    public void AllCues_ExposeThreePitchVariants()
    {
        var buildingPitches = ClickPitchVariation.GetPitches(GameAudioCue.BuildingFinished);
        var birthPitches = ClickPitchVariation.GetPitches(GameAudioCue.TrilobiteBirth);
        var uiPitches = ClickPitchVariation.GetPitches(GameAudioCue.UiSelect);
        var selectPitches = ClickPitchVariation.GetPitches(GameAudioCue.TrilobiteSelected);
        var volumePitches = ClickPitchVariation.GetPitches(GameAudioCue.VolumeSound);

        Assert.Equal(3, buildingPitches.Count);
        Assert.Equal(buildingPitches, birthPitches);
        Assert.Equal(3, uiPitches.Count);
        Assert.Equal(buildingPitches, uiPitches);
        Assert.Equal(uiPitches, selectPitches);
        Assert.Equal(uiPitches, volumePitches);
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
