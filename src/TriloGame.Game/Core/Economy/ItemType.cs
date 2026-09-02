namespace TriloGame.Game.Core.Economy;

public sealed record ItemType(ResourceName Resource, string Name, string TextureKey, ResourceCategory Category, int NutritionValue = 0)
{
    public override string ToString() => Name;
}
