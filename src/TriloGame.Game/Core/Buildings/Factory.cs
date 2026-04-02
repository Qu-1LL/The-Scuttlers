using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.Core.Buildings;

public sealed class Factory
{
    private readonly Func<GameSession, Building> _builder;

    public Factory(Func<GameSession, Building> builder, GameSession session)
    {
        _builder = builder;
        var sample = builder(session);
        Name = sample.Name;
        TextureKey = sample.TextureKey;
        OpenMap = sample.OpenMap.Select(row => row.ToArray()).ToArray();
        Size = sample.Size;
        Description = sample.Description;
        HasStation = sample.HasStation;
    }

    public string Name { get; }

    public string TextureKey { get; }

    public int[][] OpenMap { get; }

    public Shared.Math.GridPoint Size { get; }

    public string Description { get; }

    public bool HasStation { get; }

    public Building Build(GameSession session)
    {
        return _builder(session);
    }
}
