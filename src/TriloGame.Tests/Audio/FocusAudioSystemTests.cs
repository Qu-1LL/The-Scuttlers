using TriloGame.Game.Audio;
using TriloGame.Game.Core.Constants;

namespace TriloGame.Tests.Audio;

public sealed class FocusAudioSystemTests
{
    [Theory]
    [InlineData(GameAudioCue.MiningPostFocus)]
    [InlineData(GameAudioCue.AlgaeFarmFocus)]
    public void FocusCue_DefaultZoomCenteredStaysBelowAudibleThreshold(GameAudioCue cue)
    {
        var score = FocusAudioSystem.CalculateFocusScoreForTesting(cue, 0f, GameConstants.DefaultCameraScale);
        var tuning = FocusAudioSystem.GetTuning(cue);

        Assert.True(score < tuning.PlayThreshold);
    }

    [Theory]
    [InlineData(GameAudioCue.MiningPostFocus)]
    [InlineData(GameAudioCue.AlgaeFarmFocus)]
    public void FocusCue_MaxZoomCenteredExceedsAudibleThreshold(GameAudioCue cue)
    {
        var score = FocusAudioSystem.CalculateFocusScoreForTesting(cue, 0f, GameConstants.MaxScale);
        var tuning = FocusAudioSystem.GetTuning(cue);

        Assert.True(score >= tuning.PlayThreshold);
        Assert.True(tuning.FullAudibleScale <= GameConstants.MaxScale);
    }

    [Theory]
    [InlineData(GameAudioCue.MiningPostFocus)]
    [InlineData(GameAudioCue.AlgaeFarmFocus)]
    public void FocusCue_BeyondAudibleRadiusFallsSilent(GameAudioCue cue)
    {
        var tuning = FocusAudioSystem.GetTuning(cue);
        var score = FocusAudioSystem.CalculateFocusScoreForTesting(
            cue,
            tuning.AudibleRadiusPixels,
            GameConstants.MaxScale);

        Assert.Equal(0f, score);
    }
}
