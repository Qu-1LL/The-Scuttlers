using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using TriloGame.Game.Rendering;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Settings;

public sealed class SettingsMenuRenderer
{
    private static readonly GumUiFrameStyle PanelFrameStyle = new(new Color(8, 19, 29, 247), new Color(77, 122, 140), 3, 16);
    private static readonly GumUiFrameStyle TopHudOpenFrameStyle = new(new Color(27, 65, 88), new Color(163, 217, 235), 2, 14);
    private static readonly GumUiButtonStyle TopHudButtonStyle = new(
        new GumUiFrameStyle(new Color(16, 38, 54), new Color(54, 88, 107), 2, 14),
        new GumUiFrameStyle(new Color(22, 50, 71), new Color(125, 179, 196), 2, 14),
        Color.White,
        GumTextStyle.Small);
    private static readonly GumUiButtonStyle ChromeButtonStyle = new(
        new GumUiFrameStyle(new Color(22, 44, 60), new Color(110, 149, 167), 2, 10),
        new GumUiFrameStyle(new Color(36, 64, 82), new Color(188, 221, 234), 2, 10),
        Color.White);
    private static readonly GumUiButtonStyle TrilodexButtonStyle = new(
        new GumUiFrameStyle(new Color(152, 125, 74), new Color(233, 201, 143), 2, 12),
        new GumUiFrameStyle(new Color(180, 147, 92), new Color(255, 229, 170), 2, 12),
        new Color(18, 26, 34),
        GumTextStyle.Small);
    private static readonly GumUiButtonStyle ReturnToMenuButtonStyle = new(
        new GumUiFrameStyle(new Color(61, 92, 76), new Color(129, 170, 149), 2, 12),
        new GumUiFrameStyle(new Color(82, 113, 96), new Color(185, 230, 204), 2, 12),
        Color.White,
        GumTextStyle.Small);

    public void Draw(
        GumUiRenderer gumUiRenderer,
        RenderingContext rendering,
        Point viewport,
        Point pointer,
        bool isOpen,
        bool isMainMenuOpen,
        int volumePercent)
    {
        if (!isMainMenuOpen)
        {
            DrawTopHudButton(gumUiRenderer, viewport, pointer, isOpen);
        }

        if (!isOpen)
        {
            return;
        }

        DrawPanel(gumUiRenderer, rendering, viewport, pointer, isMainMenuOpen, volumePercent);
    }

    private static void DrawTopHudButton(GumUiRenderer gumUiRenderer, Point viewport, Point pointer, bool isOpen)
    {
        var buttonBounds = SettingsMenuLayout.GetSettingsButtonBounds(viewport);
        var buttonHovered = buttonBounds.Contains(pointer);
        GumUiChrome.DrawFrame(gumUiRenderer, buttonBounds, isOpen ? TopHudOpenFrameStyle : (buttonHovered ? TopHudButtonStyle.HoverFrame : TopHudButtonStyle.NormalFrame));
        DrawGearIcon(gumUiRenderer, new Rectangle(buttonBounds.X + 12, buttonBounds.Y + 10, 24, 24), Color.White);
        GumUiText.AddFittedCentered(
            gumUiRenderer,
            new Rectangle(buttonBounds.X + 40, buttonBounds.Y, buttonBounds.Width - 46, buttonBounds.Height),
            "Settings",
            Color.White,
            GumTextStyle.Small);
    }

    private static void DrawPanel(
        GumUiRenderer gumUiRenderer,
        RenderingContext rendering,
        Point viewport,
        Point pointer,
        bool isMainMenuOpen,
        int volumePercent)
    {
        var includeQuitToMainMenu = !isMainMenuOpen;
        var panelBounds = SettingsMenuLayout.GetPanelBounds(viewport, includeQuitToMainMenu);
        var closeBounds = SettingsMenuLayout.GetCloseButtonBounds(panelBounds);
        var backBounds = SettingsMenuLayout.GetBackButtonBounds(panelBounds);
        var titleBounds = new Rectangle(panelBounds.X + 20, panelBounds.Y + 16, panelBounds.Width - 40, 26);
        var valueBounds = SettingsMenuLayout.GetVolumeValueBounds(panelBounds);
        var volumeDownBounds = SettingsMenuLayout.GetVolumeDownButtonBounds(panelBounds);
        var volumeUpBounds = SettingsMenuLayout.GetVolumeUpButtonBounds(panelBounds);
        var volumeBarBounds = SettingsMenuLayout.GetVolumeBarBounds(panelBounds);
        var volumeFillBounds = SettingsMenuLayout.GetVolumeFillBounds(volumeBarBounds, volumePercent);
        var trilodexBounds = SettingsMenuLayout.GetTrilodexButtonBounds(panelBounds);
        var returnBounds = includeQuitToMainMenu
            ? SettingsMenuLayout.GetReturnToMainMenuButtonBounds(panelBounds)
            : Rectangle.Empty;

        gumUiRenderer.AddFilledRectangle(new Rectangle(0, 0, viewport.X, viewport.Y), new Color(0, 0, 0, isMainMenuOpen ? 180 : 96));
        GumUiChrome.DrawFrame(gumUiRenderer, panelBounds, PanelFrameStyle);

        DrawChromeButton(gumUiRenderer, closeBounds, closeBounds.Contains(pointer), "X", GumTextStyle.Small);
        GumUiText.AddFittedCentered(gumUiRenderer, titleBounds, "Settings", Color.White, GumTextStyle.Ui);
        GumUiText.AddFittedCentered(gumUiRenderer, valueBounds, $"Volume: {Math.Clamp(volumePercent, 0, 100)}%", new Color(216, 232, 239), GumTextStyle.Small);

        DrawChromeButton(gumUiRenderer, volumeDownBounds, volumeDownBounds.Contains(pointer), "-", GumTextStyle.Ui);
        DrawChromeButton(gumUiRenderer, volumeUpBounds, volumeUpBounds.Contains(pointer), "+", GumTextStyle.Ui);
        DrawVolumeBar(gumUiRenderer, volumeBarBounds, volumeFillBounds, volumeBarBounds.Contains(pointer));
        DrawTrilodexButton(gumUiRenderer, trilodexBounds, trilodexBounds.Contains(pointer));

        if (includeQuitToMainMenu)
        {
            DrawReturnToMainMenuButton(gumUiRenderer, returnBounds, returnBounds.Contains(pointer));
        }

        DrawBackButton(gumUiRenderer, rendering, backBounds, backBounds.Contains(pointer));
    }

    private static void DrawChromeButton(GumUiRenderer gumUiRenderer, Rectangle bounds, bool hovered, string label, GumTextStyle textStyle)
    {
        GumUiChrome.DrawButton(gumUiRenderer, bounds, label, hovered, ChromeButtonStyle with { TextStyle = textStyle });
    }

    private static void DrawVolumeBar(GumUiRenderer gumUiRenderer, Rectangle bounds, Rectangle fillBounds, bool hovered)
    {
        gumUiRenderer.AddRoundedFrame(
            bounds,
            hovered ? new Color(14, 29, 41) : new Color(10, 22, 32),
            hovered ? new Color(159, 209, 224) : new Color(74, 114, 132),
            2,
            12);
        gumUiRenderer.AddRoundedRectangle(fillBounds, new Color(143, 205, 226), 10);
    }

    private static void DrawTrilodexButton(GumUiRenderer gumUiRenderer, Rectangle bounds, bool hovered)
    {
        GumUiChrome.DrawButton(gumUiRenderer, bounds, "Trilodex", hovered, TrilodexButtonStyle);
    }

    private static void DrawReturnToMainMenuButton(GumUiRenderer gumUiRenderer, Rectangle bounds, bool hovered)
    {
        GumUiChrome.DrawButton(gumUiRenderer, bounds, "Return To Main Menu", hovered, ReturnToMenuButtonStyle);
    }

    private static void DrawBackButton(GumUiRenderer gumUiRenderer, RenderingContext rendering, Rectangle bounds, bool hovered)
    {
        gumUiRenderer.AddRoundedFrame(
            bounds,
            hovered ? new Color(32, 61, 80) : new Color(20, 43, 58),
            hovered ? new Color(180, 219, 233) : new Color(107, 151, 169),
            2,
            12);
        if (!rendering.Sprites.TryGet("BackArrow", out var backArrowTexture))
        {
            return;
        }

        gumUiRenderer.AddSprite(
            new Rectangle(bounds.X + 9, bounds.Y + 7, Math.Max(0, bounds.Width - 18), Math.Max(0, bounds.Height - 14)),
            backArrowTexture,
            Color.White);
    }

    private static void DrawGearIcon(GumUiRenderer gumUiRenderer, Rectangle bounds, Color color)
    {
        var iconSize = Math.Min(bounds.Width, bounds.Height);
        if (iconSize <= 0)
        {
            return;
        }

        var centerSize = Math.Max(8, iconSize / 2);
        var toothThickness = Math.Max(2, iconSize / 8);
        var toothLength = Math.Max(3, iconSize / 6);
        var centerBounds = new Rectangle(
            bounds.Center.X - (centerSize / 2),
            bounds.Center.Y - (centerSize / 2),
            centerSize,
            centerSize);
        gumUiRenderer.AddFilledRectangle(centerBounds, color);

        gumUiRenderer.AddFilledRectangle(new Rectangle(centerBounds.Center.X - (toothThickness / 2), bounds.Y, toothThickness, toothLength), color);
        gumUiRenderer.AddFilledRectangle(new Rectangle(centerBounds.Center.X - (toothThickness / 2), bounds.Bottom - toothLength, toothThickness, toothLength), color);
        gumUiRenderer.AddFilledRectangle(new Rectangle(bounds.X, centerBounds.Center.Y - (toothThickness / 2), toothLength, toothThickness), color);
        gumUiRenderer.AddFilledRectangle(new Rectangle(bounds.Right - toothLength, centerBounds.Center.Y - (toothThickness / 2), toothLength, toothThickness), color);

        var diagonalTooth = Math.Max(3, toothThickness + 1);
        gumUiRenderer.AddFilledRectangle(new Rectangle(bounds.X + toothThickness, bounds.Y + toothThickness, diagonalTooth, diagonalTooth), color);
        gumUiRenderer.AddFilledRectangle(new Rectangle(bounds.Right - toothThickness - diagonalTooth, bounds.Y + toothThickness, diagonalTooth, diagonalTooth), color);
        gumUiRenderer.AddFilledRectangle(new Rectangle(bounds.X + toothThickness, bounds.Bottom - toothThickness - diagonalTooth, diagonalTooth, diagonalTooth), color);
        gumUiRenderer.AddFilledRectangle(new Rectangle(bounds.Right - toothThickness - diagonalTooth, bounds.Bottom - toothThickness - diagonalTooth, diagonalTooth, diagonalTooth), color);
    }
}
