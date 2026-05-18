using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.State;

namespace TriloGame.Game.Rendering;

public sealed class SessionParticleBridge
{
    private readonly Action<DeathMistRequest> _emitDeathMist;
    private GameSession? _attachedSession;

    public SessionParticleBridge(Action<DeathMistRequest> emitDeathMist)
    {
        _emitDeathMist = emitDeathMist;
    }

    public void Attach(GameSession session)
    {
        if (ReferenceEquals(_attachedSession, session))
        {
            return;
        }

        Detach();
        _attachedSession = session;
        _attachedSession.DeathMistRequested += HandleDeathMistRequested;
    }

    public void Detach()
    {
        if (_attachedSession is null)
        {
            return;
        }

        _attachedSession.DeathMistRequested -= HandleDeathMistRequested;
        _attachedSession = null;
    }

    private void HandleDeathMistRequested(DeathMistRequest request)
    {
        _emitDeathMist(request);
    }
}
