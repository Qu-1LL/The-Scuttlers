using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Smith : Building
{
    public Smith(GameSession session)
        : base("Smith", new GridPoint(2, 2), [[0, 0], [0, 1]], session, true)
    {
        TextureKey = "Smith";
        Recipe = new Dictionary<string, int>(StringComparer.Ordinal) { ["Sandstone"] = 20 };
        Description = "A building that allows you to craft new items for your species.";
    }
}
