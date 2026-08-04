using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Gum;

public readonly record struct UiRegion<TId>(TId Id, Rectangle Bounds, bool Enabled)
    where TId : notnull;

// The clickable regions of a panel, as data.
//
// Hit-testing used to be a hand-ordered if-chain - 24 `Contains(point)` calls in MenuController, 16
// in SettingsMenuController - written separately from the drawing code that positioned the same
// rectangles. Nothing tied the two together, so a control could be drawn and not be clickable, be
// clickable somewhere it was not drawn, or be silently shadowed by an earlier branch covering the
// same pixels. Adding one control meant adding a branch in the right position and hoping.
//
// Building the list once and asking IT both questions removes the class: a region that is not in the
// list is neither drawn nor clickable, and Overlaps() can be asserted in a test rather than reasoned
// about.
//
// Order still matters - first match wins, as with the if-chain - but it is now the order of a list
// you can read end to end, and topmost-first is the documented rule rather than an accident of how
// the branches were appended.
public sealed class UiRegionMap<TId>
    where TId : notnull
{
    private readonly List<UiRegion<TId>> _regions = [];

    public IReadOnlyList<UiRegion<TId>> Regions => _regions;

    // Adds a region. Degenerate rectangles are dropped rather than stored: a zero-width control
    // cannot be clicked, so keeping it would only make Overlaps() and the region count lie.
    public UiRegionMap<TId> Add(TId id, Rectangle bounds, bool enabled = true)
    {
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            _regions.Add(new UiRegion<TId>(id, bounds, enabled));
        }

        return this;
    }

    // Adds only when `condition` holds - for controls that exist in some states of a panel and not
    // others, so the caller does not need a branch around every Add.
    public UiRegionMap<TId> AddIf(bool condition, TId id, Rectangle bounds, bool enabled = true)
    {
        return condition ? Add(id, bounds, enabled) : this;
    }

    // The topmost region containing the point, or none. Disabled regions still MATCH: a disabled
    // control has to swallow the click rather than let it fall through to whatever is behind it,
    // which for a panel is usually the dismiss-on-click-outside handler. The caller decides what a
    // disabled hit means; see TryHit's `enabled` out-parameter.
    public bool TryHit(Point point, out TId id, out bool enabled)
    {
        foreach (var region in _regions)
        {
            if (region.Bounds.Contains(point))
            {
                id = region.Id;
                enabled = region.Enabled;
                return true;
            }
        }

        id = default!;
        enabled = false;
        return false;
    }

    // The topmost ENABLED region containing the point. The common case for "what did the player
    // actually activate".
    public bool TryHitEnabled(Point point, out TId id)
    {
        return TryHit(point, out id, out var enabled) && enabled
            ? true
            : Fail(out id);

        static bool Fail(out TId id)
        {
            id = default!;
            return false;
        }
    }

    public Rectangle GetBounds(TId id)
    {
        foreach (var region in _regions)
        {
            if (EqualityComparer<TId>.Default.Equals(region.Id, id))
            {
                return region.Bounds;
            }
        }

        return Rectangle.Empty;
    }

    public bool IsEnabled(TId id)
    {
        foreach (var region in _regions)
        {
            if (EqualityComparer<TId>.Default.Equals(region.Id, id))
            {
                return region.Enabled;
            }
        }

        return false;
    }

    public bool Contains(TId id)
    {
        foreach (var region in _regions)
        {
            if (EqualityComparer<TId>.Default.Equals(region.Id, id))
            {
                return true;
            }
        }

        return false;
    }

    // Every pair of regions that overlap. Exists to be asserted on in tests: two controls sharing
    // pixels means the lower one is unreachable, which is the failure the if-chain made invisible.
    public IReadOnlyList<(TId First, TId Second)> FindOverlaps()
    {
        var overlaps = new List<(TId, TId)>();
        for (var i = 0; i < _regions.Count; i++)
        {
            for (var j = i + 1; j < _regions.Count; j++)
            {
                if (_regions[i].Bounds.Intersects(_regions[j].Bounds))
                {
                    overlaps.Add((_regions[i].Id, _regions[j].Id));
                }
            }
        }

        return overlaps;
    }
}
