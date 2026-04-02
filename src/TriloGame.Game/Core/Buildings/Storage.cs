using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Storage : Building
{
    public Storage(GameSession session)
        : base("Storage", new GridPoint(2, 2), [[0, 0], [0, 0]], session, false)
    {
        TextureKey = "Storage";
        Recipe = new Dictionary<string, int>(StringComparer.Ordinal) { ["Sandstone"] = 20 };
        Capacity = 20;
        Description = $"A container that can hold up to {Capacity} items.";
    }

    public int Capacity { get; }
}
