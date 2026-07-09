using TriloGame.Game.Audio;

namespace TriloGame.Tests.Audio;

public sealed class GameAudioContentCatalogTests
{
    [Fact]
    public void CueRegistrations_IncludePassiveBuildingLoopsAndGameplayFeedback()
    {
        string? miningPostAsset = null;
        string? algaeFarmAsset = null;
        string? invalidPlacementAsset = null;
        string? unlockNodeAsset = null;

        for (var index = 0; index < GameAudioContentCatalog.CueRegistrations.Count; index++)
        {
            var registration = GameAudioContentCatalog.CueRegistrations[index];
            switch (registration.Cue)
            {
                case GameAudioCue.MiningPostFocus:
                    miningPostAsset = registration.AssetName;
                    break;
                case GameAudioCue.AlgaeFarmFocus:
                    algaeFarmAsset = registration.AssetName;
                    break;
                case GameAudioCue.InvalidBranchPlacement:
                    invalidPlacementAsset = registration.AssetName;
                    break;
                case GameAudioCue.UnlockNode:
                    unlockNodeAsset = registration.AssetName;
                    break;
            }
        }

        Assert.Equal("Audio/Effects/pickaxe", miningPostAsset);
        Assert.Equal("Audio/Effects/mulch", algaeFarmAsset);
        Assert.Equal("Audio/Invalid", invalidPlacementAsset);
        Assert.Equal("Audio/UnlockNode", unlockNodeAsset);
    }

    [Fact]
    public void TrackRegistrations_IncludeAdaptiveSoundtrackLayers()
    {
        string? primaryLayerAsset = null;
        string? settingsLayerAsset = null;

        for (var index = 0; index < GameAudioContentCatalog.TrackRegistrations.Count; index++)
        {
            var registration = GameAudioContentCatalog.TrackRegistrations[index];
            switch (registration.Track)
            {
                case MusicTrack.AdaptiveTest1:
                    primaryLayerAsset = registration.AssetName;
                    break;
                case MusicTrack.AdaptiveTest2:
                    settingsLayerAsset = registration.AssetName;
                    break;
            }
        }

        Assert.Equal("Audio/Music/shapes and colors demo1", primaryLayerAsset);
        Assert.Equal("Audio/Music/shapes and colors drumsonly demo1", settingsLayerAsset);
    }

    [Fact]
    public void Registrations_DoNotDuplicateLogicalIds()
    {
        var cues = new HashSet<GameAudioCue>();
        for (var index = 0; index < GameAudioContentCatalog.CueRegistrations.Count; index++)
        {
            Assert.True(cues.Add(GameAudioContentCatalog.CueRegistrations[index].Cue));
        }

        var tracks = new HashSet<MusicTrack>();
        for (var index = 0; index < GameAudioContentCatalog.TrackRegistrations.Count; index++)
        {
            Assert.True(tracks.Add(GameAudioContentCatalog.TrackRegistrations[index].Track));
        }
    }
}
