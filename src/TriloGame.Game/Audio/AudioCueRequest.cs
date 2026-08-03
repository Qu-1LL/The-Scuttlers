using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Audio;

public readonly record struct AudioCueRequest(
    GameAudioCue Cue,
    WorldPoint? WorldPosition = null,
    float FootprintTiles = 1f)
{
    public const float CreatureEffectFootprintTiles = 36f;
}
