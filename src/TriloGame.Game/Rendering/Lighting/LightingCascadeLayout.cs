using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;

namespace TriloGame.Game.Rendering.Lighting;

public readonly record struct LightingCascadeLayout(
    int CascadeCount,
    Point PackedSize,
    Point LightingFieldSize,
    int BaseProbeSpacing,
    int BaseRayDimension)
{
    public static LightingCascadeLayout Create(Point lightSize)
    {
        var width = Math.Max(1, lightSize.X);
        var height = Math.Max(1, lightSize.Y);
        var diagonal = MathF.Sqrt((width * width) + (height * height));
        var cascadeCount = CalculateCascadeCount(diagonal, OreLightSettings.CascadeIntervalTexels);
        var largestScale = 1 << (cascadeCount - 1);
        var alignment = OreLightSettings.BaseProbeSpacing * largestScale;
        // A FULL alignment block of slack on top of the light buffer, not half of one.
        //
        // Each cascade's lattice is snapped DOWNWARD to its own probe spacing (SnapToLattice uses
        // Floor), so its origin sits between zero and one full spacing before the camera's own
        // origin - never after it. That one-sided guarantee is what keeps the top-left of the screen
        // inside the probe grid; the cost is that the far edge has to absorb the whole spacing rather
        // than half of it, and the coarsest cascade's spacing is `alignment` source pixels.
        //
        // Half a block was sized for the old symmetric (Round) snap, which put the offset at
        // +/- half a quantum. The positive half of that range is the bug: it slid the lattice origin
        // PAST the screen origin, so the leftmost columns of the screen had no probe outside them and
        // the clamped sampler smeared one edge texel across the band - measured at 224 px of 1920,
        // sawtoothing to zero and back on every lattice step. Whatever that edge probe happened to
        // hold is what the band showed, which is why it read as arbitrarily full-bright or full-dark.
        var packedWidth = RoundUp(width + alignment, alignment);
        var packedHeight = RoundUp(height + alignment, alignment);

        return new LightingCascadeLayout(
            cascadeCount,
            new Point(packedWidth, packedHeight),
            new Point(
                packedWidth / OreLightSettings.BaseProbeSpacing,
                packedHeight / OreLightSettings.BaseProbeSpacing),
            OreLightSettings.BaseProbeSpacing,
            OreLightSettings.BaseRayDimension);
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

    // Cascade k's ray interval, in WORLD units.
    //
    // Interval lengths quadruple per cascade, so the hierarchy's total reach is base * (4^N - 1) / 3.
    // Inverting that gives the base from an authored reach, which is one of the two things setting
    // the base has to satisfy. The other is a hard requirement of the hierarchy itself:
    //
    //     PROBE SPACING MUST NOT EXCEED CASCADE 0'S INTERVAL.
    //
    // A probe gathers light only along its own interval. If its neighbours are further away than that
    // interval is long, light that falls between probes is gathered by nobody and is simply lost.
    // Measured over a fixed world region, cascade 0 at 1.6 tiles of spacing against a 0.30 tile
    // interval lost 77% of the scene's light at the most zoomed-out rung.
    //
    // Spacing is screen-relative, so it varies 32x across the zoom ladder while an authored reach does
    // not - which is what broke the requirement at the zoomed-out end. Taking the MAXIMUM of the two
    // satisfies both: the authored floor holds while zoomed in, so the reach stays where it was tuned
    // (this is what stops the old "light range shrinks as you zoom in" bug returning), and when zoomed
    // out the intervals grow with the spacing so the hierarchy stays valid. The reach then grows in
    // tiles at the far end, which is the right direction anyway - at the widest rung you can see 101
    // tiles, so a 25.6 tile range was never going to look like anything but isolated dots.
    //
    // The old pixel-based layout satisfied this automatically, because intervals and spacing were the
    // same quantity. It paid for that with a reach that shrank as you zoomed in. The max() keeps the
    // half that worked and floors out the half that did not.
    public static float GetBaseIntervalWorld(int cascadeCount, float probeWorldSpacing)
    {
        var levels = Math.Max(1, cascadeCount);
        var total = OreLightSettings.LightReachTiles * TileConstants.TileSize;
        var authoredFloor = total * 3f / (MathF.Pow(4f, levels) - 1f);
        return MathF.Max(authoredFloor, probeWorldSpacing);
    }

    public float GetIntervalWorldOrigin(int cascadeIndex, float probeWorldSpacing)
    {
        return GetBaseIntervalWorld(CascadeCount, probeWorldSpacing) * (MathF.Pow(4f, cascadeIndex) - 1f) / 3f;
    }

    public float GetIntervalWorldLength(int cascadeIndex, float probeWorldSpacing)
    {
        return GetBaseIntervalWorld(CascadeCount, probeWorldSpacing) * MathF.Pow(4f, cascadeIndex);
    }

    // How far the merged hierarchy can actually see, in world units: the far end of the top cascade's
    // interval. Nothing past this contributes, so the distance falloff has to finish inside it.
    //
    // Constant while zoomed in, where the authored floor sets the base; growing with the spacing once
    // zoomed out far enough that the spacing takes over. See GetBaseIntervalWorld.
    public float GetTotalReachWorld(float probeWorldSpacing)
    {
        var topCascade = Math.Max(0, CascadeCount - 1);
        return GetIntervalWorldOrigin(topCascade, probeWorldSpacing)
            + GetIntervalWorldLength(topCascade, probeWorldSpacing);
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
