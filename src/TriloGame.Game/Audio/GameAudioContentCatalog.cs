using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;

namespace TriloGame.Game.Audio;

public static class GameAudioContentCatalog
{
    public readonly record struct CueRegistration(GameAudioCue Cue, string AssetName);
    public readonly record struct TrackRegistration(MusicTrack Track, string AssetName);

    public static IReadOnlyList<CueRegistration> CueRegistrations { get; } =
    [
        new(GameAudioCue.BuildingPlace, "Audio/Effects/BuildingPlace"),
        new(GameAudioCue.BuildingFinished, "Audio/Effects/BuildingFinished"),
        new(GameAudioCue.AntHoleSpawn, "Audio/Effects/AntHoleSpawn"),
        new(GameAudioCue.TrilobiteExplosion, "Audio/Effects/TrilobiteExplosion"),
        new(GameAudioCue.OpalChangeStart, "Audio/Effects/OpalChangeStart"),
        new(GameAudioCue.OpalAlarm, "Audio/Effects/OpalAlarm"),
        new(GameAudioCue.OpalRestore, "Audio/Effects/OpalRestore"),
        new(GameAudioCue.TrilobiteBirth, "Audio/Effects/TrilobiteBirth"),
        new(GameAudioCue.TrilobiteSelected, "Audio/Effects/TrilobiteSelected"),
        new(GameAudioCue.UiSelect, "Audio/Effects/UiSelect"),
        new(GameAudioCue.InvalidBranchPlacement, "Audio/Invalid"),
        new(GameAudioCue.UnlockNode, "Audio/UnlockNode"),
        new(GameAudioCue.VolumeSound, "Audio/Effects/VolumeSound"),
        new(GameAudioCue.AlgaeFarmFocus, "Audio/Effects/mulch"),
        new(GameAudioCue.RadarFocus, "Audio/Effects/RadarSound"),
        new(GameAudioCue.MiningStrike, "Audio/Effects/pickaxe"),
        new(GameAudioCue.CombatStrike, "Audio/Effects/OnAttack"),
        new(GameAudioCue.HitAffect, "Audio/Effects/OnHitAffect"),
        new(GameAudioCue.CreatureDeposit, "Audio/Effects/DepositSound"),
        new(GameAudioCue.CreatureDeath, "Audio/Effects/CreatureDeath")
    ];

    public static IReadOnlyList<TrackRegistration> TrackRegistrations { get; } =
    [
        new(MusicTrack.PlaceholderTrack, "Audio/Music/cheerwine_diddy_party"),
        new(MusicTrack.AdaptiveTest1, "Audio/Music/shapes and colors demo1"),
        new(MusicTrack.AdaptiveTest2, "Audio/Music/shapes and colors drumsonly demo1")
    ];

    // Load every configured gameplay or UI cue through one shared registry.
    public static void RegisterCues(ContentManager content, AudioService audio)
    {
        for (var index = 0; index < CueRegistrations.Count; index++)
        {
            var registration = CueRegistrations[index];
            audio.Register(registration.Cue, content.Load<SoundEffect>(registration.AssetName));
        }
    }

    // Load every configured soundtrack layer through one shared registry.
    public static void RegisterTracks(ContentManager content, MusicService music)
    {
        for (var index = 0; index < TrackRegistrations.Count; index++)
        {
            var registration = TrackRegistrations[index];
            music.Register(registration.Track, content.Load<SoundEffect>(registration.AssetName));
        }
    }
}
