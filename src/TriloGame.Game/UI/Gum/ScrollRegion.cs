namespace TriloGame.Game.UI.Gum;

// One scrollable area's state: where it is scrolled to, how far it can go, and what content it is
// showing.
//
// Replaces a loose float per panel. Six of them had accumulated on MenuController, each needing its
// own field, its own clamp line in HandleWheel, its own reset in Reset - plus a separate
// `_buildPreviewScrollKey` string whose only job was to zero one of them when the previewed building
// changed. Every new scrollable area cost four edits in four places, and forgetting the reset showed
// up as a panel that opened already scrolled halfway down someone else's content.
//
// Holding the content key here is what makes that reset automatic: Track() zeroes the offset when
// the key changes, so a panel showing something new always starts at the top without the caller
// remembering to ask.
public sealed class ScrollRegion
{
    private string? _contentKey;

    public float Offset { get; private set; }

    public float MaxOffset { get; private set; }

    public bool CanScroll => MaxOffset > 0f;

    // Point this region at some content. Returns true when the content CHANGED, which is also when
    // the offset was reset - a caller that needs to know (to drop a cached layout, say) can act on
    // it, and one that does not can ignore the result.
    //
    // A null key means "content that does not change identity", which never triggers a reset.
    public bool Track(string? contentKey)
    {
        if (string.Equals(_contentKey, contentKey, StringComparison.Ordinal))
        {
            return false;
        }

        _contentKey = contentKey;
        Offset = 0f;
        return true;
    }

    // Publish the measured extent and re-clamp. Called from layout, once the content's real height is
    // known: the offset has to survive a resize that shortens the content, and clamping at the point
    // of measurement is the only place that can be guaranteed.
    public void SetMaxOffset(float maxOffset)
    {
        MaxOffset = Math.Max(0f, maxOffset);
        Offset = Math.Clamp(Offset, 0f, MaxOffset);
    }

    // Scroll by a delta, clamped. Returns whether anything moved, so a wheel event over a region
    // already at its end can fall through to whatever is behind it instead of being swallowed.
    public bool ScrollBy(float delta)
    {
        var next = Math.Clamp(Offset + delta, 0f, MaxOffset);
        if (Math.Abs(next - Offset) < 0.0001f)
        {
            return false;
        }

        Offset = next;
        return true;
    }

    public void ScrollTo(float offset)
    {
        Offset = Math.Clamp(offset, 0f, MaxOffset);
    }

    public void Reset()
    {
        Offset = 0f;
        MaxOffset = 0f;
        _contentKey = null;
    }
}
