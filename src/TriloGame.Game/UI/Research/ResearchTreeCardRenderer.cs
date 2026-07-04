using Microsoft.Xna.Framework;
using MonoGameGum.GueDeriving;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Research;

internal static class ResearchTreeCardRenderer
{
    public const int PreferredCardHeight = 190;

    private static readonly Color CardFill = new(13, 30, 44);
    private static readonly Color CardHoverFill = new(20, 45, 63);
    private static readonly Color CardSelectedFill = new(34, 70, 92);
    private static readonly Color CardBorder = new(66, 101, 118);
    private static readonly Color CardHoverBorder = new(132, 181, 198);
    private static readonly Color CardSelectedBorder = new(214, 236, 244);
    private static readonly Color CardTitleColor = Color.White;
    private static readonly Color EmptyPreviewTextColor = new(191, 204, 211);
    private const int HorizontalPadding = 12;
    private const int PreviewHorizontalPadding = 10;
    private const int TitleTopPadding = 8;
    private const int TitleHeight = 32;
    private const int PreviewTopPadding = 56;
    private const int PreviewBottomPadding = 10;

    public static ResearchTreeCardLayout BuildLayout(Rectangle bounds)
    {
        return new ResearchTreeCardLayout(
            bounds,
            new Rectangle(
                bounds.X + HorizontalPadding,
                bounds.Y + TitleTopPadding,
                Math.Max(0, bounds.Width - (HorizontalPadding * 2)),
                TitleHeight),
            Rectangle.Empty,
            new Rectangle(
                bounds.X + PreviewHorizontalPadding,
                bounds.Y + PreviewTopPadding,
                Math.Max(0, bounds.Width - (PreviewHorizontalPadding * 2)),
                Math.Max(0, bounds.Height - PreviewTopPadding - PreviewBottomPadding)));
    }

    public static ResearchTreeViewLayout CalculateTreeLayout(
        ResearchTreeViewNode root,
        Rectangle cardBounds,
        ResearchTreeRenderConfig config = default)
    {
        return ResearchTreeUiRenderer.CalculateCardTreeLayout(root, BuildLayout(cardBounds).PreviewBounds, config);
    }

    public static ResearchTreeViewNode? Draw(
        GumUiRenderer gumUi,
        GameSession session,
        ResearchTreeCardData card,
        ResearchTreeRenderConfig config,
        Point pointerPoint)
    {
        return Draw(gumUi, parent: null, session, card, config, pointerPoint);
    }

    public static ResearchTreeViewNode? Draw(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        GameSession session,
        ResearchTreeCardData card,
        ResearchTreeRenderConfig config,
        Point pointerPoint)
    {
        var layout = BuildLayout(card.Bounds);
        GumUiChrome.DrawFrame(gumUi, parent, card.Bounds, GetFrameStyle(card));
        DrawTitle(gumUi, parent, layout.TitleBounds, card.Title);

        if (card.Root is null)
        {
            GumUiText.AddCentered(
                gumUi,
                parent,
                layout.PreviewBounds,
                "Unavailable",
                EmptyPreviewTextColor,
                GumTextStyle.Small);
            return null;
        }

        ResearchTreeUiRenderer.DrawCardPreview(gumUi, parent, session, layout.PreviewBounds, card.Root, config);
        if (!config.EnableNodeSelection)
        {
            return null;
        }

        var hoveredNode = ResearchTreeUiRenderer.TryGetHoveredCardNode(
            card.Root,
            layout.PreviewBounds,
            pointerPoint,
            config,
            out var hoveredCenter);
        if (hoveredNode is not null)
        {
            var treeLayout = CalculateTreeLayout(card.Root, card.Bounds, config);
            ResearchTreeUiRenderer.DrawCardNodeHover(gumUi, parent, hoveredCenter, treeLayout.Radius);
        }

        return hoveredNode;
    }

    private static GumUiFrameStyle GetFrameStyle(ResearchTreeCardData card)
    {
        if (card.IsSelected)
        {
            return new GumUiFrameStyle(CardSelectedFill, CardSelectedBorder, 2, 14);
        }

        return card.IsHovered
            ? new GumUiFrameStyle(CardHoverFill, CardHoverBorder, 2, 14)
            : new GumUiFrameStyle(CardFill, CardBorder, 2, 14);
    }

    private static void DrawTitle(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        Rectangle bounds,
        string title)
    {
        GumUiText.AddFittedCentered(
            gumUi,
            parent,
            bounds,
            title,
            CardTitleColor,
            GumTextStyle.Small);
    }
}

internal readonly record struct ResearchTreeCardLayout(
    Rectangle Bounds,
    Rectangle TitleBounds,
    Rectangle SubtitleBounds,
    Rectangle PreviewBounds);
