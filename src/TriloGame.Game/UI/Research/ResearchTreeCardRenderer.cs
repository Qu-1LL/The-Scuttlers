using Microsoft.Xna.Framework;
using MonoGameGum.GueDeriving;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Research;

internal static class ResearchTreeCardRenderer
{
    public const int PreferredCardHeight = 190;

    private const int HorizontalPadding = 12;
    private const int PreviewHorizontalPadding = 10;
    private const int TitleTopPadding = 8;
    private const int TitleHeight = 20;
    private const int SubtitleTopPadding = 30;
    private const int SubtitleHeight = 18;
    private const int PreviewTopPadding = 54;
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
            new Rectangle(
                bounds.X + HorizontalPadding,
                bounds.Y + SubtitleTopPadding,
                Math.Max(0, bounds.Width - (HorizontalPadding * 2)),
                SubtitleHeight),
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
        return ResearchTreeUiRenderer.DrawTreeEntryCard(gumUi, session, card, config, pointerPoint);
    }

    public static ResearchTreeViewNode? Draw(
        GumUiRenderer gumUi,
        ContainerRuntime? parent,
        GameSession session,
        ResearchTreeCardData card,
        ResearchTreeRenderConfig config,
        Point pointerPoint)
    {
        return ResearchTreeUiRenderer.DrawTreeEntryCard(gumUi, parent, session, card, config, pointerPoint);
    }
}

internal readonly record struct ResearchTreeCardLayout(
    Rectangle Bounds,
    Rectangle TitleBounds,
    Rectangle SubtitleBounds,
    Rectangle PreviewBounds);
