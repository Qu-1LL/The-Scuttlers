using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Audio;

public sealed class SessionAudioBridge
{
    private readonly AudioService _audio;
    private GameSession? _attachedSession;

    public SessionAudioBridge(AudioService audio)
    {
        _audio = audio;
    }

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

    public void Detach()
    {
        if (_attachedSession is null)
        {
            return;
        }

        _attachedSession.AudioCueRequested -= HandleAudioCueRequested;
        _attachedSession = null;
    }

    private void HandleAudioCueRequested(GameAudioCue cue)
    {
        _audio.Play(cue);
    }
}
