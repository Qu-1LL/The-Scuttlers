using TriloGame.Game.Audio;

namespace TriloGame.Tests.Audio;

public sealed class GameAudioContentCatalogTests
{
    [Fact]
    public void CueRegistrations_IncludePassiveBuildingLoopsAndGameplayFeedback()
    {
        string? algaeFarmAsset = null;
        string? radarAsset = null;
        string? invalidPlacementAsset = null;
        string? unlockNodeAsset = null;
        string? combatStrikeAsset = null;
        string? hitAffectAsset = null;
        string? depositAsset = null;
        string? deathAsset = null;

        for (var index = 0; index < GameAudioContentCatalog.CueRegistrations.Count; index++)
        {
            var registration = GameAudioContentCatalog.CueRegistrations[index];
            switch (registration.Cue)
            {
                case GameAudioCue.AlgaeFarmFocus:
                    algaeFarmAsset = registration.AssetName;
                    break;
                case GameAudioCue.RadarFocus:
                    radarAsset = registration.AssetName;
                    break;
                case GameAudioCue.InvalidBranchPlacement:
                    invalidPlacementAsset = registration.AssetName;
                    break;
                case GameAudioCue.UnlockNode:
                    unlockNodeAsset = registration.AssetName;
                    break;
                case GameAudioCue.CombatStrike:
                    combatStrikeAsset = registration.AssetName;
                    break;
                case GameAudioCue.HitAffect:
                    hitAffectAsset = registration.AssetName;
                    break;
                case GameAudioCue.CreatureDeposit:
                    depositAsset = registration.AssetName;
                    break;
                case GameAudioCue.CreatureDeath:
                    deathAsset = registration.AssetName;
                    break;
            }
        }

        Assert.DoesNotContain(
            GameAudioContentCatalog.CueRegistrations,
            registration => registration.Cue == GameAudioCue.MiningPostFocus);
        Assert.Equal("Audio/Effects/mulch", algaeFarmAsset);
        Assert.Equal("Audio/Effects/RadarSound", radarAsset);
        Assert.Equal("Audio/Invalid", invalidPlacementAsset);
        Assert.Equal("Audio/UnlockNode", unlockNodeAsset);
        Assert.Equal("Audio/Effects/OnAttack", combatStrikeAsset);
        Assert.Equal("Audio/Effects/OnHitAffect", hitAffectAsset);
        Assert.Equal("Audio/Effects/DepositSound", depositAsset);
        Assert.Equal("Audio/Effects/CreatureDeath", deathAsset);
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
