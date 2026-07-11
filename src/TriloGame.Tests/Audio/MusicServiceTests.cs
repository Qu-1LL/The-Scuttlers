using TriloGame.Game.Audio;

namespace TriloGame.Tests.Audio;

public sealed class MusicServiceTests
{
    [Fact]
    public void SetMusicEnabled_TogglesAndReportsOnlyRealChanges()
    {
        var music = new MusicService();

        Assert.True(music.IsMusicEnabled);
        Assert.True(music.SetMusicEnabled(false));
        Assert.False(music.IsMusicEnabled);
        Assert.False(music.SetMusicEnabled(false));
        Assert.True(music.SetMusicEnabled(true));
        Assert.True(music.IsMusicEnabled);
        Assert.False(music.SetMusicEnabled(true));
    }
}
