using TriloGame.Game.Audio;

namespace TriloGame.Tests.Audio;

public sealed class AudioServiceTests
{
    [Fact]
    public void SetVolumePercent_ClampsAndReportsOnlyRealChanges()
    {
        var audio = new AudioService();

        Assert.False(audio.SetVolumePercent(100));
        Assert.True(audio.SetVolumePercent(95));
        Assert.Equal(95, audio.VolumePercent);
        Assert.True(audio.SetVolumePercent(105));
        Assert.Equal(100, audio.VolumePercent);
        Assert.True(audio.SetVolumePercent(-15));
        Assert.Equal(0, audio.VolumePercent);
        Assert.False(audio.SetVolumePercent(0));
    }

    [Fact]
    public void ChangeVolume_AppliesDeltaFromCurrentValue()
    {
        var audio = new AudioService();

        Assert.True(audio.ChangeVolume(-35));
        Assert.Equal(65, audio.VolumePercent);
        Assert.True(audio.ChangeVolume(15));
        Assert.Equal(80, audio.VolumePercent);
    }
}
