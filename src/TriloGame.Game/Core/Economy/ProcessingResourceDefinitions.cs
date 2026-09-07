namespace TriloGame.Game.Core.Economy;

public static class ProcessingResourceDefinitions
{
    // Define one shared capacity bucket for every plant resource in the requested food form.
    public static ProcessingResourceDefinition ForFoodType(
        string label,
        string foodType,
        int amountPerProcess,
        int capacity)
    {
        return new ProcessingResourceDefinition(
            label,
            ResourceClassificationKeys.FoodType,
            foodType,
            amountPerProcess,
            capacity);
    }
}
