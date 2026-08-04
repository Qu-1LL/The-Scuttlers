using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Gum;

// Rows down a column, each one taking the next slice of vertical space.
//
// Replaces panels built from absolute offsets - `panelBounds.Y + 194`, `+ 218`, `+ 254` - where
// inserting a row meant editing every constant below it and the panel's total height, in three
// separate places, with nothing to catch a missed one but the eye. Two rows given the same offset by
// mistake simply overlap, silently.
//
// Here a row cannot be positioned independently of the rows above it, so overlap is not expressible.
// The panel's height comes from ConsumedHeight rather than from a magic total kept in step by hand,
// so adding a row cannot leave the panel too short for its own contents.
//
// Deliberately a struct with no allocation and no retained tree: this runs inside layout methods that
// are called every frame and are pure functions of (viewport, state). It measures; it does not draw.
public struct StackLayout
{
    private readonly int _left;
    private readonly int _width;
    private readonly int _top;
    private int _cursor;

    public StackLayout(Rectangle bounds, int inset = 0, int topInset = 0)
    {
        _left = bounds.X + inset;
        _width = Math.Max(0, bounds.Width - (inset * 2));
        _top = bounds.Y + (topInset > 0 ? topInset : inset);
        _cursor = _top;
    }

    // Total height consumed so far, including gaps. What a panel should size itself to.
    public readonly int ConsumedHeight => _cursor - _top;

    // Where the next row would start.
    public readonly int Cursor => _cursor;

    public readonly int Left => _left;

    public readonly int Width => _width;

    // The next full-width row. `gap` is space inserted BEFORE the row, so a gap on the first row
    // indents it from the top of the stack rather than trailing off the bottom of the previous one.
    public Rectangle Row(int height, int gap = 0)
    {
        _cursor += gap;
        var row = new Rectangle(_left, _cursor, _width, Math.Max(0, height));
        _cursor += Math.Max(0, height);
        return row;
    }

    // Advance without producing a row - for space that belongs to something positioned by other
    // means, such as an absolutely placed icon the rows have to flow around.
    public void Skip(int height)
    {
        _cursor += Math.Max(0, height);
    }

    // Split a row into columns of equal width, separated by `gap`.
    //
    // Returns the columns rather than an index-at-a-time cursor so a caller cannot ask for more
    // columns than it declared and silently run off the end of the row.
    public static Rectangle[] Columns(Rectangle row, int count, int gap = 8)
    {
        if (count <= 0)
        {
            return [];
        }

        var totalGap = gap * (count - 1);
        var columnWidth = Math.Max(0, (row.Width - totalGap) / count);
        var columns = new Rectangle[count];
        for (var index = 0; index < count; index++)
        {
            columns[index] = new Rectangle(
                row.X + ((columnWidth + gap) * index),
                row.Y,
                columnWidth,
                row.Height);
        }

        return columns;
    }

    // A row with a fixed-width control pinned at each end and the remainder in the middle - the
    // stepper shape: `<`  value  `>`.
    //
    // The middle is derived from the two ends rather than from the row, so the three can never
    // overlap however narrow the row gets; it collapses to zero width first.
    public static (Rectangle Left, Rectangle Middle, Rectangle Right) Stepper(
        Rectangle row,
        int endWidth,
        int gap = 8)
    {
        var left = new Rectangle(row.X, row.Y, endWidth, row.Height);
        var right = new Rectangle(row.Right - endWidth, row.Y, endWidth, row.Height);
        var middleLeft = left.Right + gap;
        var middle = new Rectangle(
            middleLeft,
            row.Y,
            Math.Max(0, right.Left - gap - middleLeft),
            row.Height);
        return (left, middle, right);
    }

    // Shrink a rectangle inward on all sides.
    public static Rectangle Inset(Rectangle bounds, int amount)
    {
        return new Rectangle(
            bounds.X + amount,
            bounds.Y + amount,
            Math.Max(0, bounds.Width - (amount * 2)),
            Math.Max(0, bounds.Height - (amount * 2)));
    }
}
