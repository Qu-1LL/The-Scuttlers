namespace TriloGame.Game.Core.Economy;

public sealed record ItemType
{
    public ItemType(
        ResourceName resource,
        string name,
        string textureKey,
        ResourceCategory category,
        int nutritionValue = 0,
        IReadOnlyDictionary<string, string>? classifications = null)
    {
        Resource = resource;
        Name = name;
        TextureKey = textureKey;
        Category = category;
        NutritionValue = nutritionValue;
        Classifications = classifications ?? ResourceClassifications.Create(category);
    }

    public ResourceName Resource { get; }

    public string Name { get; }

    public string TextureKey { get; }

    public ResourceCategory Category { get; }

    public int NutritionValue { get; }

    public IReadOnlyDictionary<string, string> Classifications { get; }

    // Missing classifications are ordinary non-matches rather than exceptional resource states.
    public string? GetClassification(string? classificationKey)
    {
        return !string.IsNullOrWhiteSpace(classificationKey) &&
               Classifications.TryGetValue(classificationKey, out var value)
            ? value
            : null;
    }

    public bool HasClassification(string? classificationKey, string? classificationValue)
    {
        return !string.IsNullOrWhiteSpace(classificationValue) &&
               string.Equals(GetClassification(classificationKey), classificationValue, StringComparison.Ordinal);
    }

    public override string ToString() => Name;
}
