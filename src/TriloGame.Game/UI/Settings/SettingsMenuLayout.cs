using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Settings;

public static class SettingsMenuLayout
{
    public const int VolumeStep = 5;
    public const int TopHudButtonWidth = 132;
    public const int TopHudButtonHeight = 44;
    public const int TopHudButtonGap = 12;

    public static Rectangle GetSettingsButtonBounds(Point viewport)
    {
        return GetTopHudButtonBounds(viewport, 0);
    }

    public static Rectangle GetTopHudButtonBounds(Point viewport, int index)
    {
        var safeIndex = Math.Max(0, index);
        return new Rectangle(
            18 + ((TopHudButtonWidth + TopHudButtonGap) * safeIndex),
            18,
            TopHudButtonWidth,
            TopHudButtonHeight);
    }

    public static Rectangle GetPanelBounds(Point viewport)
    {
        return GetPanelBounds(viewport, includeQuitToMainMenu: true);
    }

    public static Rectangle GetPanelBounds(Point viewport, bool includeQuitToMainMenu)
    {
        var width = Math.Min(420, Math.Max(320, viewport.X - 56));
        var height = includeQuitToMainMenu ? 334 : 284;
        return new Rectangle((viewport.X - width) / 2, (viewport.Y - height) / 2, width, height);
    }

    public static Rectangle GetCloseButtonBounds(Rectangle panelBounds)
    {
        return new Rectangle(panelBounds.Right - 50, panelBounds.Y + 14, 34, 34);
    }

    public static Rectangle GetBackButtonBounds(Rectangle panelBounds)
    {
        return new Rectangle(panelBounds.Right - 58, panelBounds.Bottom - 54, 42, 32);
    }

    public static Rectangle GetVolumeValueBounds(Rectangle panelBounds)
    {
        return new Rectangle(panelBounds.X + 24, panelBounds.Y + 62, panelBounds.Width - 48, 30);
    }

    public static Rectangle GetVolumeBarBounds(Rectangle panelBounds)
    {
        return new Rectangle(panelBounds.X + 88, panelBounds.Y + 112, panelBounds.Width - 176, 18);
    }

    public static Rectangle GetVolumeDownButtonBounds(Rectangle panelBounds)
    {
        var bar = GetVolumeBarBounds(panelBounds);
        return new Rectangle(panelBounds.X + 22, bar.Y - 11, 40, 40);
    }

    public static Rectangle GetVolumeUpButtonBounds(Rectangle panelBounds)
    {
        var bar = GetVolumeBarBounds(panelBounds);
        return new Rectangle(panelBounds.Right - 62, bar.Y - 11, 40, 40);
    }

    public static Rectangle GetReturnToMainMenuButtonBounds(Rectangle panelBounds)
    {
        return new Rectangle(panelBounds.X + 24, panelBounds.Y + 204, panelBounds.Width - 48, 38);
    }

    public static Rectangle GetTrilodexButtonBounds(Rectangle panelBounds)
    {
        return new Rectangle(panelBounds.X + 24, panelBounds.Y + 156, panelBounds.Width - 48, 38);
    }

    public static Rectangle GetQuitToMainMenuButtonBounds(Rectangle panelBounds)
    {
        return new Rectangle(panelBounds.X + 24, panelBounds.Y + 252, panelBounds.Width - 48, 38);
    }

    public static Rectangle GetDismissHintBounds(Rectangle panelBounds)
    {
        return new Rectangle(panelBounds.X + 24, panelBounds.Bottom - 28, panelBounds.Width - 48, 18);
    }

    public static int GetSnappedVolumeFromBar(Rectangle barBounds, int pointerX)
    {
        if (barBounds.Width <= 1)
        {
            return 0;
        }

        var ratio = Math.Clamp((pointerX - barBounds.Left) / (float)barBounds.Width, 0f, 1f);
        var raw = (int)MathF.Round(ratio * 100f);
        return Math.Clamp((int)MathF.Round(raw / (float)VolumeStep) * VolumeStep, 0, 100);
    }

    public static Rectangle GetVolumeFillBounds(Rectangle barBounds, int volumePercent)
    {
        var width = Math.Max(0, (int)MathF.Round(barBounds.Width * (Math.Clamp(volumePercent, 0, 100) / 100f)));
        return new Rectangle(barBounds.X, barBounds.Y, width, barBounds.Height);
    }
}
