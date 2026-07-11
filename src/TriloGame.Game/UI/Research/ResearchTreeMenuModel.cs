using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Research;

internal enum ResearchTreeMenuMode
{
    Drafting,
    TrilodexCatalog,
    ReadOnlyDetail
}

internal enum ResearchTreeCardAreaMode
{
    None,
    DraftRow,
    CatalogGrid
}

internal sealed record ResearchTreeMenuModel(
    ResearchTreeMenuMode Mode,
    ResearchTreeMenuConfig Config,
    ResearchTreeMenuLayoutInfo Layout,
    string Title,
    string Subtitle,
    string CardHeaderText,
    string TreeHeaderText,
    IReadOnlyList<ResearchTreeCardModel> Cards,
    ResearchTreeViewportModel TreeViewport,
    ResearchTreeInfoPanelModel InfoPanel,
    string FooterText);

internal readonly record struct ResearchTreeMenuConfig(
    bool ShowBackButton = false,
    bool ShowCloseButton = true,
    ResearchTreeCardAreaMode CardAreaMode = ResearchTreeCardAreaMode.None,
    bool ShowTreeViewport = true,
    bool ShowInfoPanel = true,
    bool ShowFooter = true,
    bool EnablePanZoom = true,
    bool EnableNodeHover = true,
    bool EnableNodeSelection = true,
    bool EnableBranchDrafting = false,
    bool EnablePlacementPreview = false,
    bool EnableReadOnlyPreview = false,
    bool ShowRootNode = true,
    bool CanPlaceBranches = false);

internal readonly record struct ResearchTreeCardModel(
    string Title,
    string Subtitle,
    ResearchTreeViewNode? Root,
    bool IsHovered,
    bool IsSelected);

internal sealed record ResearchTreeViewportModel(
    ResearchTreeViewNode? Root,
    Vector2 PanOffset,
    float Zoom,
    Texture2D? BackgroundTexture,
    Func<ResearchTreeViewportOverlayContext, ResearchNodeInfo?>? DrawOverlay = null,
    bool OverlayReplacesTreeContent = false,
    double VisualTimeMs = 0d);

internal readonly record struct ResearchTreeViewportOverlayContext(
    GumUiRenderer GumUi,
    GameSession Session,
    Rectangle ViewportBounds,
    ResearchTreeDetailMetrics Metrics,
    Point PointerPoint);

internal readonly record struct ResearchTreeInfoPanelModel(
    ResearchNodeInfo? NodeInfo,
    string EmptyTitle,
    string EmptyText,
    float ScrollOffset = 0f,
    ResearchNodeUnlockActionModel? UnlockAction = null);

internal readonly record struct ResearchNodeUnlockActionModel(
    string ResourceType,
    int Available,
    int Cost,
    bool CanUnlock,
    bool IsUnlocked,
    SkillTreeUnlockBlockReason BlockReason);

internal static class ResearchTreeInfoPanelLayout
{
    public static Rectangle GetUnlockButtonBounds(Rectangle infoPanelBounds)
    {
        if (infoPanelBounds.IsEmpty)
        {
            return Rectangle.Empty;
        }

        const int margin = 14;
        const int width = 132;
        const int height = 34;
        return new Rectangle(
            infoPanelBounds.Right - width - margin,
            infoPanelBounds.Bottom - height - margin,
            width,
            height);
    }

    public static Rectangle GetUnlockCostBounds(Rectangle infoPanelBounds)
    {
        var button = GetUnlockButtonBounds(infoPanelBounds);
        if (button.IsEmpty)
        {
            return Rectangle.Empty;
        }

        return new Rectangle(
            infoPanelBounds.X + 14,
            button.Y - 25,
            infoPanelBounds.Width - 28,
            20);
    }

    public static Rectangle GetUnlockTooltipBounds(Rectangle buttonBounds, Point pointerPoint, Rectangle panelBounds, string text)
    {
        if (buttonBounds.IsEmpty || string.IsNullOrWhiteSpace(text))
        {
            return Rectangle.Empty;
        }

        var measured = GumTextLayout.Measure(text, GumTextStyle.Small);
        var width = Math.Clamp(measured.X + 28, 132, 236);
        var height = 34;
        var x = pointerPoint.X + 14;
        if (x + width > panelBounds.Right - 8)
        {
            x = pointerPoint.X - width - 14;
        }

        x = Math.Clamp(x, panelBounds.Left + 8, Math.Max(panelBounds.Left + 8, panelBounds.Right - width - 8));
        var y = pointerPoint.Y - height - 10;
        if (y < panelBounds.Top + 8)
        {
            y = pointerPoint.Y + 14;
        }

        y = Math.Clamp(y, panelBounds.Top + 8, Math.Max(panelBounds.Top + 8, panelBounds.Bottom - height - 8));
        return new Rectangle(x, y, width, height);
    }
}

internal readonly record struct ResearchTreeMenuLayoutInfo(
    Rectangle PanelBounds,
    Rectangle CloseButtonBounds,
    Rectangle BackButtonBounds,
    Rectangle TitleBounds,
    Rectangle SubtitleBounds,
    Rectangle CardFrameBounds,
    Rectangle CardHeaderBounds,
    Rectangle CardViewportBounds,
    IReadOnlyList<Rectangle> CardBounds,
    Rectangle TreeFrameBounds,
    Rectangle TreeHeaderBounds,
    Rectangle TreeViewportBounds,
    Rectangle InfoPanelBounds,
    Rectangle FooterBounds,
    float MaxCardScroll,
    Rectangle ScrollbarTrackBounds,
    Rectangle ScrollbarThumbBounds);
