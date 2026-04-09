namespace TriloGame.Game.UI.Selection;

public static class SelectionRetention
{
    public static bool ShouldPreserveCurrentSelection<T>(
        IReadOnlyCollection<T> selectedItems,
        T clickedItem,
        IEqualityComparer<T>? comparer = null)
    {
        if (selectedItems.Count == 0)
        {
            return false;
        }

        comparer ??= EqualityComparer<T>.Default;
        foreach (var selectedItem in selectedItems)
        {
            if (comparer.Equals(selectedItem, clickedItem))
            {
                return true;
            }
        }

        return false;
    }
}
