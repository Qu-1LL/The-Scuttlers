namespace TriloGame.Game.UI.Input;

public sealed class DoubleClickTracker
{
    private string? _pendingKey;
    private double _armedAtMs;

    public bool HasPending => _pendingKey is not null;

    public void Arm(string key, double armedAtMs)
    {
        _pendingKey = key;
        _armedAtMs = armedAtMs;
    }

    public bool TryConsume(string key, double nowMs, double thresholdMs)
    {
        if (_pendingKey is null)
        {
            return false;
        }

        var matched = _pendingKey == key && (nowMs - _armedAtMs) <= thresholdMs;
        Clear();
        return matched;
    }

    public void Expire(double nowMs, double thresholdMs)
    {
        if (_pendingKey is null)
        {
            return;
        }

        if ((nowMs - _armedAtMs) > thresholdMs)
        {
            Clear();
        }
    }

    public void Clear()
    {
        _pendingKey = null;
        _armedAtMs = 0d;
    }
}
