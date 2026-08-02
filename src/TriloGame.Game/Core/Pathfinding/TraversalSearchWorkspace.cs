namespace TriloGame.Game.Core.Pathfinding;

// Reuses visit state for sequential main-thread path searches without clearing whole-cave arrays.
internal sealed class TraversalSearchWorkspace
{
    private int[] _visitStamps = [];
    private int[] _previousIds = [];
    private int[] _queue = [];
    private int _visitStamp;
    private int _head;
    private int _tail;

    public void Begin(int tileCapacity)
    {
        EnsureCapacity(tileCapacity);
        if (_visitStamp == int.MaxValue)
        {
            Array.Clear(_visitStamps);
            _visitStamp = 1;
        }
        else
        {
            _visitStamp++;
        }

        _head = 0;
        _tail = 0;
    }

    public void AddStart(int tileId)
    {
        _visitStamps[tileId] = _visitStamp;
        _previousIds[tileId] = tileId;
        _queue[_tail++] = tileId;
    }

    public bool WasVisited(int tileId)
    {
        return (uint)tileId < (uint)_visitStamps.Length && _visitStamps[tileId] == _visitStamp;
    }

    public void Visit(int tileId, int previousTileId)
    {
        _visitStamps[tileId] = _visitStamp;
        _previousIds[tileId] = previousTileId;
        _queue[_tail++] = tileId;
    }

    public bool TryDequeue(out int tileId)
    {
        if (_head >= _tail)
        {
            tileId = -1;
            return false;
        }

        tileId = _queue[_head++];
        return true;
    }

    public int GetPreviousId(int tileId) => _previousIds[tileId];

    private void EnsureCapacity(int requiredCapacity)
    {
        if (_visitStamps.Length >= requiredCapacity)
        {
            return;
        }

        var newCapacity = Math.Max(requiredCapacity, Math.Max(64, _visitStamps.Length * 2));
        Array.Resize(ref _visitStamps, newCapacity);
        Array.Resize(ref _previousIds, newCapacity);
        Array.Resize(ref _queue, newCapacity);
    }
}
