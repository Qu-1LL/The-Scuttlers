using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Audio;

public sealed class SessionAudioBridge
{
    private readonly AudioService _audio;
    private GameSession? _attachedSession;

    // Hold the audio service that will play cues emitted by the active session.
    public SessionAudioBridge(AudioService audio)
    {
        _audio = audio;
    }

    // Swap the event subscription over to the newly active game session.
    public void Attach(GameSession session)
    {
        if (ReferenceEquals(_attachedSession, session))
        {
            return;
        }

        Detach();
        _attachedSession = session;
        _attachedSession.AudioCueRequested += HandleAudioCueRequested;
    }

    // Remove the current session subscription, if one is active.
    public void Detach()
    {
        if (_attachedSession is null)
        {
            return;
        }

        _attachedSession.AudioCueRequested -= HandleAudioCueRequested;
        _attachedSession = null;
    }

    // Relay simulation cue requests through the shared audio service.
    private void HandleAudioCueRequested(GameAudioCue cue)
    {
        _audio.Play(cue);
    }
}
