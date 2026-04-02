namespace TriloGame.Game.Core.Events;

public sealed class GameEventBus
{
    private readonly Dictionary<string, HashSet<Action<GameEventPayload>>> _listeners = new(StringComparer.Ordinal);

    public Action Subscribe(string eventName, Action<GameEventPayload> listener)
    {
        if (string.IsNullOrWhiteSpace(eventName) || listener is null)
        {
            return static () => { };
        }

        if (!_listeners.TryGetValue(eventName, out var set))
        {
            set = new HashSet<Action<GameEventPayload>>();
            _listeners[eventName] = set;
        }

        set.Add(listener);
        return () => Unsubscribe(eventName, listener);
    }

    public bool Unsubscribe(string eventName, Action<GameEventPayload> listener)
    {
        if (!_listeners.TryGetValue(eventName, out var set))
        {
            return false;
        }

        var removed = set.Remove(listener);
        if (set.Count == 0)
        {
            _listeners.Remove(eventName);
        }

        return removed;
    }

    public int Emit(string eventName, GameEventPayload payload)
    {
        if (!_listeners.TryGetValue(eventName, out var set) || set.Count == 0)
        {
            return 0;
        }

        foreach (var listener in set.ToArray())
        {
            listener(payload);
        }

        return set.Count;
    }
}
