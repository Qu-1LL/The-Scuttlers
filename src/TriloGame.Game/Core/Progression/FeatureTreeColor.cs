namespace TriloGame.Game.Core.Progression;

// Rendering-agnostic RGB value for authored feature tree identity colors.
public readonly record struct FeatureTreeColor(byte R, byte G, byte B)
{
    public string ToHex()
    {
        return $"{R:x2}{G:x2}{B:x2}";
    }
}
