namespace TriloGame.Game.UI.Menu;

public readonly record struct MenuInteractionResult(
    bool Consumed,
    bool PlaySelectSound,
    BuildingPlacementRequest? BuildingPlacement)
{
    public static MenuInteractionResult NotHandled { get; } = new(false, false, null);

    public static MenuInteractionResult ConsumedSilently { get; } = new(true, false, null);

    public static MenuInteractionResult ConsumedWithSelectSound { get; } = new(true, true, null);

    public static MenuInteractionResult WithSelectSound(bool consumed) => new(consumed, true, null);

    public static MenuInteractionResult RequestBuildingPlacement(BuildingPlacementRequest request) => new(true, true, request);
}
