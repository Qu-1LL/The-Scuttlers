using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Audio;

public sealed class OpalAudioSystem
{
    private readonly AudioService _audio;
    private bool _opalWasInWarningPhase;
    private double _opalAlarmDelayMs;

    // Hold the shared audio service used to drive opal warning cues.
    public OpalAudioSystem(AudioService audio)
    {
        _audio = audio;
    }

    // Clear warning-phase state and silence any active opal loop.
    public void Reset()
    {
        _opalWasInWarningPhase = false;
        _opalAlarmDelayMs = 0d;
        _audio.StopLoop(GameAudioCue.OpalAlarm);
    }

    // Transition the opal audio state machine based on the latest warning progress.
    public void Update(GameSession session, double elapsedMs)
    {
        var opal = session.Cave?.GetOpalNode();
        var warningActive = opal is not null && opal.GetWarningProgress() > 0f;

        // Start the warning transition once when the opal first enters its danger phase.
        if (warningActive && !_opalWasInWarningPhase)
        {
            _audio.Play(GameAudioCue.OpalChangeStart);
            _audio.StopLoop(GameAudioCue.OpalAlarm);
            _opalAlarmDelayMs = _audio.GetDuration(GameAudioCue.OpalChangeStart).TotalMilliseconds;
        }
        else if (!warningActive && _opalWasInWarningPhase)
        {
            _audio.StopLoop(GameAudioCue.OpalAlarm);
            if (opal is not null && opal.TicksSinceLastMine == 0)
            {
                _audio.Play(GameAudioCue.OpalRestore);
            }

            _opalAlarmDelayMs = 0d;
        }

        // Delay the alarm loop until the transition stinger has had time to play.
        if (warningActive)
        {
            if (!_audio.IsLoopPlaying(GameAudioCue.OpalAlarm))
            {
                _opalAlarmDelayMs = Math.Max(0d, _opalAlarmDelayMs - elapsedMs);
                if (_opalAlarmDelayMs <= 0d)
                {
                    _audio.StartLoop(GameAudioCue.OpalAlarm);
                }
            }
        }
        else
        {
            _opalAlarmDelayMs = 0d;
        }

        _opalWasInWarningPhase = warningActive;
    }
}
