using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Rendering;

namespace TriloGame.Game.Audio;

public sealed class SessionAudioBridge
{
    private readonly AudioService _audio;
    private readonly Func<CameraController?> _getCamera;
    private GameSession? _attachedSession;

    // Hold the audio service that will play cues emitted by the active session.
    public SessionAudioBridge(AudioService audio, Func<CameraController?> getCamera)
    {
        _audio = audio;
        _getCamera = getCamera;
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
        _attachedSession.AudioCuePlaybackRequested += HandleAudioCuePlaybackRequested;
    }

    // Remove the current session subscription, if one is active.
    public void Detach()
    {
        if (_attachedSession is null)
        {
            return;
        }

        _attachedSession.AudioCuePlaybackRequested -= HandleAudioCuePlaybackRequested;
        _attachedSession = null;
    }

    // Relay simulation cue requests through the shared audio service.
    private void HandleAudioCuePlaybackRequested(AudioCueRequest request)
    {
        if (request.WorldPosition is not { } worldPosition)
        {
            _audio.Play(request.Cue);
            return;
        }

        var camera = _getCamera();
        if (camera is null || camera.ViewCenter.X <= 0f || camera.ViewCenter.Y <= 0f)
        {
            _audio.Play(request.Cue);
            return;
        }

        var sideTiles = MathF.Sqrt(MathF.Max(0f, request.FootprintTiles));
        var gain = ScreenSpaceAudio.CalculateVisibleCoverage(
            camera.WorldToScreen(worldPosition.ToWorldPixels()),
            sideTiles,
            sideTiles,
            camera);
        _audio.Play(request.Cue, gain);
    }
}
