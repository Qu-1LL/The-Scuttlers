using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Entities;

namespace TriloGame.Game.UI.Selection;

public enum MiningOrderMenuOutcome
{
    NotHandled,
    Consumed,
    SelectionChanged,
    SendRequested
}

public readonly record struct MiningOrderMenuInteractionResult(MiningOrderMenuOutcome Outcome, bool PlaySelectSound)
{
    public bool Consumed => Outcome is not MiningOrderMenuOutcome.NotHandled;

    public static MiningOrderMenuInteractionResult NotHandled { get; } = new(MiningOrderMenuOutcome.NotHandled, false);

    public static MiningOrderMenuInteractionResult ConsumedSilently { get; } = new(MiningOrderMenuOutcome.Consumed, false);

    public static MiningOrderMenuInteractionResult SelectionChanged { get; } = new(MiningOrderMenuOutcome.SelectionChanged, true);

    public static MiningOrderMenuInteractionResult SendRequested { get; } = new(MiningOrderMenuOutcome.SendRequested, true);
}

public readonly record struct MiningOrderMenuRow(Trilobite Miner, Rectangle Bounds);

public sealed class MiningOrderMenuController
{
    private Trilobite[] _miners = [];
    private readonly HashSet<Trilobite> _selectedMiners = [];

    public bool IsOpen { get; private set; }

    public Vector2 AnchorScreen { get; private set; }

    public IReadOnlyList<Trilobite> Miners => _miners;

    public IReadOnlySet<Trilobite> SelectedMiners => _selectedMiners;

    public float Scroll { get; private set; }

    public void Open(Vector2 anchorScreen, IReadOnlyList<Trilobite> miners)
    {
        IsOpen = true;
        AnchorScreen = anchorScreen;
        _miners = CopyMiners(miners);
        _selectedMiners.Clear();
        Scroll = 0f;

        foreach (var miner in _miners)
        {
            _selectedMiners.Add(miner);
        }
    }

    public void Close()
    {
        IsOpen = false;
        AnchorScreen = Vector2.Zero;
        _miners = [];
        _selectedMiners.Clear();
        Scroll = 0f;
    }

    public void ClampScroll(float maxScroll)
    {
        Scroll = Math.Clamp(Scroll, 0f, Math.Max(0f, maxScroll));
    }

    public MiningOrderMenuInteractionResult HandleWheel(
        Point point,
        Rectangle panelBounds,
        Rectangle listViewportBounds,
        float maxScroll,
        int wheelDelta)
    {
        if (!IsOpen)
        {
            return MiningOrderMenuInteractionResult.NotHandled;
        }

        if (!listViewportBounds.Contains(point))
        {
            return panelBounds.Contains(point)
                ? MiningOrderMenuInteractionResult.ConsumedSilently
                : MiningOrderMenuInteractionResult.NotHandled;
        }

        Scroll = Math.Clamp(Scroll + wheelDelta, 0f, Math.Max(0f, maxScroll));
        return MiningOrderMenuInteractionResult.ConsumedSilently;
    }

    public MiningOrderMenuInteractionResult HandleClick(
        Point point,
        IReadOnlyList<MiningOrderMenuRow> rows,
        Rectangle panelBounds,
        Rectangle sendButtonBounds,
        bool appendSelection)
    {
        if (!IsOpen)
        {
            return MiningOrderMenuInteractionResult.NotHandled;
        }

        if (!panelBounds.Contains(point))
        {
            return MiningOrderMenuInteractionResult.NotHandled;
        }

        foreach (var row in rows)
        {
            if (!row.Bounds.Contains(point))
            {
                continue;
            }

            SelectMiner(row.Miner, appendSelection);
            return MiningOrderMenuInteractionResult.SelectionChanged;
        }

        return sendButtonBounds.Contains(point)
            ? MiningOrderMenuInteractionResult.SendRequested
            : MiningOrderMenuInteractionResult.ConsumedSilently;
    }

    public Trilobite[] GetSelectedMiners()
    {
        var selected = new Trilobite[_selectedMiners.Count];
        _selectedMiners.CopyTo(selected);
        return selected;
    }

    public bool SyncMiners(IReadOnlyList<Trilobite> activeMiners)
    {
        if (!IsOpen)
        {
            return false;
        }

        if (SameMinerOrder(_miners, activeMiners))
        {
            return false;
        }

        var selectedMinerNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var miner in _selectedMiners)
        {
            selectedMinerNames.Add(miner.Name);
        }

        _miners = CopyMiners(activeMiners);
        _selectedMiners.Clear();
        foreach (var miner in _miners)
        {
            if (selectedMinerNames.Contains(miner.Name))
            {
                _selectedMiners.Add(miner);
            }
        }

        if (_selectedMiners.Count == 0)
        {
            foreach (var miner in _miners)
            {
                _selectedMiners.Add(miner);
            }
        }

        return true;
    }

    private void SelectMiner(Trilobite miner, bool appendSelection)
    {
        if (appendSelection)
        {
            if (!_selectedMiners.Add(miner))
            {
                _selectedMiners.Remove(miner);
            }

            return;
        }

        _selectedMiners.Clear();
        _selectedMiners.Add(miner);
    }

    private static Trilobite[] CopyMiners(IReadOnlyList<Trilobite> miners)
    {
        var copy = new Trilobite[miners.Count];
        for (var index = 0; index < miners.Count; index++)
        {
            copy[index] = miners[index];
        }

        return copy;
    }

    private static bool SameMinerOrder(IReadOnlyList<Trilobite> current, IReadOnlyList<Trilobite> next)
    {
        if (current.Count != next.Count)
        {
            return false;
        }

        for (var index = 0; index < current.Count; index++)
        {
            if (!ReferenceEquals(current[index], next[index]))
            {
                return false;
            }
        }

        return true;
    }
}
