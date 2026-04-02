using TriloGame.Game.Core.Events;

namespace TriloGame.Game.Core.Economy;

public sealed class StatsTracker
{
    private readonly GameEventBus _bus;
    private readonly List<Action> _unsubscribe = [];
    private readonly Dictionary<string, int> _values = new(StringComparer.Ordinal);

    public StatsTracker(GameEventBus bus)
    {
        _bus = bus;
        CreateDefaults();

        Listen(GameEvents.TileMined);
        Listen(GameEvents.WallMined);
        foreach (var ore in OreType.GetOres())
        {
            Listen($"{ore.Name}Mined");
        }
    }

    private void CreateDefaults()
    {
        _values[GameEvents.TileMined] = 0;
        _values[GameEvents.WallMined] = 0;

        foreach (var ore in OreType.GetOres())
        {
            _values[$"{ore.Name}Mined"] = 0;
        }
    }

    private void Listen(string eventName)
    {
        _unsubscribe.Add(_bus.Subscribe(eventName, _ => Increment(eventName)));
    }

    public int Get(string eventName)
    {
        return string.IsNullOrWhiteSpace(eventName) ? 0 : _values.GetValueOrDefault(eventName, 0);
    }

    public int Increment(string eventName, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(eventName) || amount <= 0)
        {
            return Get(eventName);
        }

        _values[eventName] = Get(eventName) + amount;
        return _values[eventName];
    }

    public IReadOnlyDictionary<string, int> GetAll()
    {
        return new Dictionary<string, int>(_values, StringComparer.Ordinal);
    }

    public void Dispose()
    {
        foreach (var unsubscribe in _unsubscribe)
        {
            unsubscribe();
        }

        _unsubscribe.Clear();
    }
}
