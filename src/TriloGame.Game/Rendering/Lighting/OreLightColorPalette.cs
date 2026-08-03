using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TriloGame.Game.Rendering.Lighting;

// Derives each ore's light colour from its own sprite so the emitted light matches the deposit
// the player is looking at, and stays correct if the art changes. A plain average of the sprite
// washes out to grey rock, so opaque pixels are weighted by saturation to recover the ore's
// characteristic hue, then renormalised to a bright light colour.
public sealed class OreLightColorPalette
{
    private readonly Dictionary<string, Color> _colors = new(StringComparer.Ordinal);

    public void Register(string oreName, Texture2D texture)
    {
        _colors[oreName] = ExtractLightColor(texture);
    }

    public Color GetLightColor(string oreName)
    {
        return _colors.TryGetValue(oreName, out var color) ? color : Color.White;
    }

    public static Color ExtractLightColor(Texture2D texture)
    {
        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        return ExtractLightColor(pixels);
    }

    // An ore sprite is mostly surrounding rock with a minority of coloured vein pixels. Averaging
    // everything (even saturation-weighted) lets the rock dominate and every ore comes out the
    // same blue-grey. So: measure the mean saturation first, then average only the pixels above
    // it - that isolates the veins from the rock and recovers each ore's real hue.
    public static Color ExtractLightColor(IReadOnlyList<Color> pixels)
    {
        var opaqueCount = 0;
        var saturationSum = 0f;
        for (var i = 0; i < pixels.Count; i++)
        {
            if (pixels[i].A < 128)
            {
                continue;
            }

            saturationSum += GetSaturation(pixels[i]);
            opaqueCount++;
        }

        if (opaqueCount == 0)
        {
            return Color.White;
        }

        var meanSaturation = saturationSum / opaqueCount;
        var accumulated = Vector3.Zero;
        var accumulatedWeight = 0f;
        for (var i = 0; i < pixels.Count; i++)
        {
            var pixel = pixels[i];
            if (pixel.A < 128)
            {
                continue;
            }

            var saturation = GetSaturation(pixel);
            if (saturation < meanSaturation)
            {
                continue;
            }

            // Weight by saturation again so the most vividly coloured vein pixels lead.
            var weight = saturation + 0.01f;
            accumulated += new Vector3(pixel.R / 255f, pixel.G / 255f, pixel.B / 255f) * weight;
            accumulatedWeight += weight;
        }

        if (accumulatedWeight <= 0f)
        {
            return Color.White;
        }

        return Normalize(accumulated / accumulatedWeight);
    }

    private static float GetSaturation(Color pixel)
    {
        var r = pixel.R / 255f;
        var g = pixel.G / 255f;
        var b = pixel.B / 255f;
        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        return max <= 0f ? 0f : (max - min) / max;
    }

    // Scale the hue up so the brightest channel is saturated: light should carry the ore's
    // colour at full strength, with the cascade's own falloff providing the dimming.
    private static Color Normalize(Vector3 color)
    {
        var peak = MathF.Max(color.X, MathF.Max(color.Y, color.Z));
        if (peak <= 0.0001f)
        {
            return Color.White;
        }

        // Only a small lift toward white: enough that a very deep ore still reads as light,
        // little enough that the ore's hue stays recognisable.
        var scaled = Vector3.Lerp(color / peak, Vector3.One, 0.12f);
        return new Color(scaled.X, scaled.Y, scaled.Z, 1f);
    }
}
