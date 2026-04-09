using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Core.Traits;

public sealed class TrilobiteTraitHandler
{
    private readonly GameSession _session;

    public TrilobiteTraitHandler(GameSession session)
    {
        _session = session;
    }

    public void Tick()
    {
        var cave = _session.Cave;
        if (cave is null)
        {
            return;
        }

        foreach (var trilobite in cave.GetTrilobiteList())
        {
            trilobite.TraitState.Tick(_session.TickCount);
        }
    }
}
