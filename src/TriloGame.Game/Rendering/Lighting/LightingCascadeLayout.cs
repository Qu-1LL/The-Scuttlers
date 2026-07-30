using Microsoft.Xna.Framework;

namespace TriloGame.Game.Rendering.Lighting;

public readonly record struct LightingCascadeLayout(
    int CascadeCount,
    Point PackedSize,
    Point LightingFieldSize,
    int BaseProbeSpacing,
    int BaseRayDimension,
    float IntervalLength)
{
    public static LightingCascadeLayout Create(Point lightSize)
    {
        var width = Math.Max(1, lightSize.X);
        var height = Math.Max(1, lightSize.Y);
        var diagonal = MathF.Sqrt((width * width) + (height * height));
        var cascadeCount = CalculateCascadeCount(diagonal, OreLightSettings.CascadeIntervalTexels);
        var largestScale = 1 << (cascadeCount - 1);
        var alignment = OreLightSettings.BaseProbeSpacing * largestScale;
        var packedWidth = RoundUp(width, alignment);
        var packedHeight = RoundUp(height, alignment);

        return new LightingCascadeLayout(
            cascadeCount,
            new Point(packedWidth, packedHeight),
            new Point(
                packedWidth / OreLightSettings.BaseProbeSpacing,
                packedHeight / OreLightSettings.BaseProbeSpacing),
            OreLightSettings.BaseProbeSpacing,
            OreLightSettings.BaseRayDimension,
            OreLightSettings.CascadeIntervalTexels);
    }

    public static int CalculateCascadeCount(float diagonal, float intervalLength)
    {
        var safeDiagonal = MathF.Max(1f, diagonal);
        var safeInterval = MathF.Max(1f, intervalLength);
        var uncapped = (int)MathF.Ceiling(MathF.Log(safeDiagonal / safeInterval, 4f));
        return Math.Clamp(uncapped, OreLightSettings.MinCascadeCount, OreLightSettings.MaxCascadeCount);
    }

    public int GetProbeSpacing(int cascadeIndex) => BaseProbeSpacing * (1 << cascadeIndex);

    public int GetRayDimension(int cascadeIndex) => BaseRayDimension * (1 << cascadeIndex);

    public Point GetProbeCount(int cascadeIndex)
    {
        var rayDimension = GetRayDimension(cascadeIndex);
        return new Point(PackedSize.X / rayDimension, PackedSize.Y / rayDimension);
    }

    public float GetIntervalOrigin(int cascadeIndex)
    {
        var scale = MathF.Pow(4f, cascadeIndex);
        return IntervalLength * (1f - scale) / (1f - 4f);
    }

    public float GetIntervalLength(int cascadeIndex) => IntervalLength * MathF.Pow(4f, cascadeIndex);

    // Screen UV must be multiplied by this before sampling the lighting field, because the
    // field spans PackedSize source pixels while the screen only spans lightSize of them.
    // Without it the whole radiance field is stretched relative to the geometry it lights.
    public Vector2 GetLightingFieldUvScale(Point lightSize)
    {
        return new Vector2(
            lightSize.X / (float)PackedSize.X,
            lightSize.Y / (float)PackedSize.Y);
    }

    public int GetRayCount(int cascadeIndex)
    {
        var rayDimension = GetRayDimension(cascadeIndex);
        return rayDimension * rayDimension;
    }

    // Mirrors GetRayDirection in RadianceCascade.fx. A ray's identity is this 1D angular index;
    // the rayDimension x rayDimension texture arrangement is only storage for it.
    public static float GetRayAngle(int rayIndex, int rayCount)
    {
        return (rayIndex + 0.5f) / rayCount * MathF.Tau;
    }

    // Mirrors GetHigherRayOffset in RadianceCascade.fx. Ray count quadruples per cascade, so
    // the four finer directions covering parent angular index i are exactly 4i..4i+3. Kept here
    // (and covered by tests) because getting this mapping wrong does not fail loudly - it just
    // makes the merge read radiance from the wrong direction uniformly across the whole map,
    // which looks like a single global light direction rather than an obvious glitch.
    public static int GetHigherRayIndex(int rayIndex, int childOffset)
    {
        return (rayIndex * 4) + childOffset;
    }

    // Storage coordinates of a higher-cascade ray index, given that cascade's row width.
    public static Point GetHigherRayOffset(int higherRayIndex, int higherRayDimension)
    {
        return new Point(
            higherRayIndex % higherRayDimension,
            higherRayIndex / higherRayDimension);
    }

    private static int RoundUp(int value, int alignment)
    {
        return ((value + alignment - 1) / alignment) * alignment;
    }
}
