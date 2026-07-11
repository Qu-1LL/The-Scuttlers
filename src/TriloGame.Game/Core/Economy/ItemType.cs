namespace TriloGame.Game.Core.Economy;

public sealed record ItemType(ResourceName Resource, string Name, string TextureKey, ResourceCategory Category)
{
    public override string ToString() => Name;
}
