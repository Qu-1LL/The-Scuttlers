using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Tests.UI;

public sealed class UiFrameworkPrimitiveTests
{
    // ---- UiMath -----------------------------------------------------------------------------------

    // The bug this exists to prevent: Math.Clamp throws when min exceeds max, and UI layout asks for
    // "at least this, but no more than the space available" constantly.
    [Fact]
    public void ClampAtMost_YieldsTheMinimumToTheMaximumInsteadOfThrowing()
    {
        Assert.Equal(10, UiMath.ClampAtMost(50, preferredMinimum: 32, maximum: 10));
        Assert.Equal(32, UiMath.ClampAtMost(4, preferredMinimum: 32, maximum: 100));
        Assert.Equal(50, UiMath.ClampAtMost(50, preferredMinimum: 32, maximum: 100));
    }

    // The exact shape that crashed the settings menu: a scrollable viewport shorter than the minimum
    // scrollbar thumb.
    [Fact]
    public void GumScrollableText_SurvivesAViewportShorterThanItsMinimumThumb()
    {
        var text = string.Join(' ', Enumerable.Repeat("overflowing content", 60));

        var layout = GumScrollableText.Build(new Rectangle(0, 0, 160, 12), text, GumTextStyle.Small, 0f);

        Assert.True(layout.MaxScroll > 0f, "expected the text to overflow so a scrollbar is produced");
        Assert.NotNull(layout.ScrollbarThumbBounds);
        Assert.True(layout.ScrollbarThumbBounds!.Value.Height <= 12, "thumb cannot be taller than its track");
        Assert.True(layout.ScrollbarThumbBounds.Value.Height > 0);
    }

    [Fact]
    public void GumScrollableText_HandlesDegenerateViewportsWithoutThrowing()
    {
        foreach (var bounds in new[]
                 {
                     new Rectangle(0, 0, 0, 0),
                     new Rectangle(0, 0, -5, 40),
                     new Rectangle(0, 0, 100, 0)
                 })
        {
            var layout = GumScrollableText.Build(bounds, "content", GumTextStyle.Small, 0f);
            Assert.Null(layout.ScrollbarThumbBounds);
        }
    }

    // ---- StackLayout ------------------------------------------------------------------------------

    [Fact]
    public void StackLayout_RowsFlowDownwardAndNeverOverlap()
    {
        var stack = new StackLayout(new Rectangle(10, 20, 200, 500), inset: 8);

        var first = stack.Row(30);
        var second = stack.Row(40, gap: 6);
        var third = stack.Row(20, gap: 10);

        Assert.Equal(18, first.X);
        Assert.Equal(184, first.Width);
        Assert.Equal(first.Bottom + 6, second.Y);
        Assert.Equal(second.Bottom + 10, third.Y);
        Assert.Equal(third.Bottom - first.Y, stack.ConsumedHeight);
    }

    // The property that makes a panel unable to be too short for its own contents.
    [Fact]
    public void StackLayout_ConsumedHeightCoversEveryRowItIssued()
    {
        var stack = new StackLayout(new Rectangle(0, 0, 100, 1000), inset: 4);
        var rows = new[] { stack.Row(20), stack.Row(30, gap: 5), stack.Row(15, gap: 5) };

        var bottom = rows[^1].Bottom;

        Assert.True(bottom <= 4 + stack.ConsumedHeight);
    }

    [Fact]
    public void StackLayout_ColumnsSplitEvenlyAndDoNotOverlap()
    {
        var columns = StackLayout.Columns(new Rectangle(0, 0, 100, 20), 3, gap: 5);

        Assert.Equal(3, columns.Length);
        Assert.All(columns, column => Assert.Equal(30, column.Width));
        Assert.True(columns[0].Right < columns[1].Left);
        Assert.True(columns[1].Right < columns[2].Left);
        Assert.True(columns[^1].Right <= 100);
    }

    [Fact]
    public void StackLayout_ColumnsHandleZeroAndNegativeCounts()
    {
        Assert.Empty(StackLayout.Columns(new Rectangle(0, 0, 100, 20), 0));
        Assert.Empty(StackLayout.Columns(new Rectangle(0, 0, 100, 20), -2));
    }

    // The stepper's middle is derived from its ends, so it collapses to zero before it can overlap.
    [Fact]
    public void StackLayout_StepperMiddleCollapsesRatherThanOverlappingItsEnds()
    {
        var (left, middle, right) = StackLayout.Stepper(new Rectangle(0, 0, 60, 20), endWidth: 40, gap: 8);

        Assert.Equal(0, middle.Width);
        Assert.True(left.Width == 40 && right.Width == 40);
        Assert.True(middle.Width >= 0);
    }

    [Fact]
    public void StackLayout_StepperKeepsThreePartsApartWhenThereIsRoom()
    {
        var (left, middle, right) = StackLayout.Stepper(new Rectangle(0, 0, 300, 20), endWidth: 40, gap: 8);

        Assert.True(left.Right < middle.Left);
        Assert.True(middle.Right < right.Left);
        Assert.Equal(300, right.Right);
    }

    // ---- UiRegionMap ------------------------------------------------------------------------------

    [Fact]
    public void UiRegionMap_ReturnsTheFirstMatchingRegion()
    {
        var map = new UiRegionMap<string>()
            .Add("top", new Rectangle(0, 0, 50, 50))
            .Add("bottom", new Rectangle(0, 0, 100, 100));

        Assert.True(map.TryHit(new Point(10, 10), out var id, out _));
        Assert.Equal("top", id);
    }

    [Fact]
    public void UiRegionMap_DropsDegenerateRegionsRatherThanStoringThem()
    {
        var map = new UiRegionMap<string>()
            .Add("real", new Rectangle(0, 0, 10, 10))
            .Add("empty", Rectangle.Empty)
            .Add("negative", new Rectangle(0, 0, -4, 10));

        Assert.Single(map.Regions);
        Assert.False(map.Contains("empty"));
        Assert.False(map.Contains("negative"));
    }

    // A disabled control has to swallow its click rather than let it fall through to the panel's
    // dismiss-on-click-outside handler.
    [Fact]
    public void UiRegionMap_DisabledRegionsStillMatchButAreNotEnabled()
    {
        var map = new UiRegionMap<string>().Add("greyed", new Rectangle(0, 0, 20, 20), enabled: false);

        Assert.True(map.TryHit(new Point(5, 5), out var id, out var enabled));
        Assert.Equal("greyed", id);
        Assert.False(enabled);
        Assert.False(map.TryHitEnabled(new Point(5, 5), out _));
    }

    [Fact]
    public void UiRegionMap_AddIfSkipsRegionsThatDoNotApply()
    {
        var map = new UiRegionMap<string>()
            .AddIf(false, "hidden", new Rectangle(0, 0, 10, 10))
            .AddIf(true, "shown", new Rectangle(20, 0, 10, 10));

        Assert.False(map.Contains("hidden"));
        Assert.True(map.Contains("shown"));
    }

    [Fact]
    public void UiRegionMap_FindOverlapsReportsControlsSharingPixels()
    {
        var clean = new UiRegionMap<string>()
            .Add("a", new Rectangle(0, 0, 10, 10))
            .Add("b", new Rectangle(20, 0, 10, 10));
        var dirty = new UiRegionMap<string>()
            .Add("a", new Rectangle(0, 0, 30, 10))
            .Add("b", new Rectangle(20, 0, 10, 10));

        Assert.Empty(clean.FindOverlaps());
        Assert.Single(dirty.FindOverlaps());
    }

    // ---- ScrollRegion -----------------------------------------------------------------------------

    [Fact]
    public void ScrollRegion_ClampsToTheMeasuredExtent()
    {
        var region = new ScrollRegion();
        region.SetMaxOffset(100f);

        region.ScrollBy(250f);
        Assert.Equal(100f, region.Offset);

        region.ScrollBy(-500f);
        Assert.Equal(0f, region.Offset);
    }

    // Content shrinking under a scrolled region must pull the offset back, or the panel renders
    // scrolled past its own end.
    [Fact]
    public void ScrollRegion_ReclampsWhenTheContentShrinks()
    {
        var region = new ScrollRegion();
        region.SetMaxOffset(200f);
        region.ScrollBy(200f);

        region.SetMaxOffset(40f);

        Assert.Equal(40f, region.Offset);
    }

    // The behaviour that replaced the bespoke _buildPreviewScrollKey field.
    [Fact]
    public void ScrollRegion_ResetsItsOffsetWhenTheContentChanges()
    {
        var region = new ScrollRegion();
        region.Track("algae-farm");
        region.SetMaxOffset(100f);
        region.ScrollBy(60f);

        var changed = region.Track("mining-post");

        Assert.True(changed);
        Assert.Equal(0f, region.Offset);
    }

    [Fact]
    public void ScrollRegion_KeepsItsOffsetWhenTheContentIsUnchanged()
    {
        var region = new ScrollRegion();
        region.Track("algae-farm");
        region.SetMaxOffset(100f);
        region.ScrollBy(60f);

        var changed = region.Track("algae-farm");

        Assert.False(changed);
        Assert.Equal(60f, region.Offset);
    }

    // A wheel event over a region already at its end should fall through rather than be swallowed.
    [Fact]
    public void ScrollRegion_ReportsWhetherItActuallyMoved()
    {
        var region = new ScrollRegion();
        region.SetMaxOffset(50f);

        Assert.True(region.ScrollBy(20f));
        Assert.True(region.ScrollBy(40f));
        Assert.False(region.ScrollBy(10f));
    }

    [Fact]
    public void ScrollRegion_CannotScrollWhenContentFits()
    {
        var region = new ScrollRegion();
        region.SetMaxOffset(0f);

        Assert.False(region.CanScroll);
        Assert.False(region.ScrollBy(100f));
        Assert.Equal(0f, region.Offset);
    }

    // ---- GumUiButtonStyle disabled state ---------------------------------------------------------

    [Fact]
    public void ButtonStyle_DisabledBeatsHoverSoAnInertControlNeverLightsUp()
    {
        var style = new GumUiButtonStyle(
            new GumUiFrameStyle(UiPalette.SurfaceControl, UiPalette.BorderControl),
            new GumUiFrameStyle(UiPalette.SurfaceControlHover, UiPalette.BorderHover),
            UiPalette.TextPrimary);

        var hoveredDisabled = style.ResolveFrame(hovered: true, enabled: false);

        Assert.Equal(style.ResolveDisabledFrame(), hoveredDisabled);
        Assert.NotEqual(style.HoverFrame, hoveredDisabled);
        Assert.Equal(UiPalette.DisabledText, style.ResolveTextColor(hovered: true, enabled: false));
    }

    // The default disabled look must be distinct from the un-hovered look, or "unavailable" reads as
    // merely "not hovered" and players keep clicking.
    [Fact]
    public void ButtonStyle_DefaultDisabledFrameIsDistinctFromTheNormalFrame()
    {
        var style = new GumUiButtonStyle(
            new GumUiFrameStyle(UiPalette.SurfaceControl, UiPalette.BorderControl),
            new GumUiFrameStyle(UiPalette.SurfaceControlHover, UiPalette.BorderHover),
            UiPalette.TextPrimary);

        Assert.NotEqual(style.NormalFrame, style.ResolveDisabledFrame());
        Assert.NotEqual(style.TextColor, style.ResolveDisabledTextColor());
    }

    [Fact]
    public void ButtonStyle_EnabledStillHonoursHover()
    {
        var style = new GumUiButtonStyle(
            new GumUiFrameStyle(UiPalette.SurfaceControl, UiPalette.BorderControl),
            new GumUiFrameStyle(UiPalette.SurfaceControlHover, UiPalette.BorderHover),
            UiPalette.TextPrimary,
            HoverTextColor: UiPalette.TextBody);

        Assert.Equal(style.HoverFrame, style.ResolveFrame(hovered: true, enabled: true));
        Assert.Equal(style.NormalFrame, style.ResolveFrame(hovered: false, enabled: true));
        Assert.Equal(UiPalette.TextBody, style.ResolveTextColor(hovered: true, enabled: true));
    }
}
