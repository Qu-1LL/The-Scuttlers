using TriloGame.Game.Core.Buildings;

namespace TriloGame.Game.UI.Menu;

public readonly record struct MenuInteractionResult(
    bool Consumed,
    bool PlaySelectSound,
    Scaffolding? BuildingPlacement)
{
    public static MenuInteractionResult NotHandled { get; } = new(false, false, null);

    public static MenuInteractionResult ConsumedSilently { get; } = new(true, false, null);

    public static MenuInteractionResult ConsumedWithSelectSound { get; } = new(true, true, null);

    public static MenuInteractionResult WithSelectSound(bool consumed) => new(consumed, true, null);

    public static MenuInteractionResult RequestBuildingPlacement(Scaffolding scaffolding) => new(true, true, scaffolding);
}
