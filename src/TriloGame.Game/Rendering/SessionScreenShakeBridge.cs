using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Rendering;

public sealed class SessionScreenShakeBridge
{
    private readonly CameraController _camera;
    private GameSession? _attachedSession;

    public SessionScreenShakeBridge(CameraController camera)
    {
        _camera = camera;
    }

    public void Attach(GameSession session)
    {
        if (ReferenceEquals(_attachedSession, session))
        {
            return;
        }

        Detach();
        _attachedSession = session;
        _attachedSession.ScreenShakeRequested += HandleScreenShakeRequested;
    }

    public void Detach()
    {
        if (_attachedSession is null)
        {
            return;
        }

        _attachedSession.ScreenShakeRequested -= HandleScreenShakeRequested;
        _attachedSession = null;
    }

    private void HandleScreenShakeRequested(float intensity)
    {
        _camera.TriggerExplosionShake(intensity);
    }
}
