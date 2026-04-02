using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Settings;

public static class SettingsMenuLayout
{
    public const int VolumeStep = 5;

    public static Rectangle GetSettingsButtonBounds(Point viewport)
    {
        return new Rectangle(18, 18, 132, 44);
    }

    public static Rectangle GetPanelBounds(Point viewport)
    {
        var width = Math.Min(348, Math.Max(280, viewport.X - 36));
        var height = 184;
        var button = GetSettingsButtonBounds(viewport);
        return new Rectangle(button.X, button.Bottom + 12, width, height);
    }

    public static Rectangle GetVolumeValueBounds(Rectangle panelBounds)
    {
        return new Rectangle(panelBounds.X + 22, panelBounds.Y + 52, panelBounds.Width - 44, 30);
    }

    public static Rectangle GetVolumeBarBounds(Rectangle panelBounds)
    {
        return new Rectangle(panelBounds.X + 74, panelBounds.Y + 102, panelBounds.Width - 148, 18);
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

    public static Rectangle GetDismissHintBounds(Rectangle panelBounds)
    {
        return new Rectangle(panelBounds.X + 22, panelBounds.Bottom - 34, panelBounds.Width - 44, 20);
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
