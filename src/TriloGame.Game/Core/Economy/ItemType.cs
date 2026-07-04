namespace TriloGame.Game.Core.Economy;

public sealed record ItemType(string Name, string TextureKey)
{
    public override string ToString() => Name;
}
