namespace TriloGame.Game.UI.Selection;

public sealed class MiningTileSelectionState
{
    private readonly List<string> _tileKeys = [];
    private readonly StringComparer _comparer;

    public MiningTileSelectionState()
        : this(StringComparer.Ordinal)
    {
    }

    public MiningTileSelectionState(StringComparer comparer)
    {
        _comparer = comparer;
    }

    public IReadOnlyList<string> TileKeys => _tileKeys;

    public int Count => _tileKeys.Count;

    public bool HasSelection => _tileKeys.Count > 0;

    public void Clear() => _tileKeys.Clear();

    public void Select(string tileKey, bool append, bool toggleIfAlreadySelected)
    {
        if (!append)
        {
            _tileKeys.Clear();
        }

        var existingIndex = IndexOf(tileKey);
        if (toggleIfAlreadySelected && existingIndex >= 0)
        {
            _tileKeys.RemoveAt(existingIndex);
            return;
        }

        if (existingIndex < 0)
        {
            _tileKeys.Add(tileKey);
        }
    }

    public void SelectMany(IEnumerable<string> tileKeys, bool append)
    {
        if (!append)
        {
            _tileKeys.Clear();
        }

        foreach (var tileKey in tileKeys)
        {
            if (IndexOf(tileKey) < 0)
            {
                _tileKeys.Add(tileKey);
            }
        }
    }

    public bool Contains(string tileKey) => IndexOf(tileKey) >= 0;

    private int IndexOf(string tileKey)
    {
        for (var index = 0; index < _tileKeys.Count; index++)
        {
            if (_comparer.Equals(_tileKeys[index], tileKey))
            {
                return index;
            }
        }

        return -1;
    }
}
