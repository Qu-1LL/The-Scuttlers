using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    Action<GumUiRenderer>? DrawCustomContent = null);

internal readonly record struct ResearchTreeInfoPanelModel(
    ResearchTreeNodeInfo? NodeInfo,
    string EmptyTitle,
    string EmptyText);

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
