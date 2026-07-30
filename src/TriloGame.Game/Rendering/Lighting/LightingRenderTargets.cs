using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TriloGame.Game.Rendering.Lighting;

public sealed class LightingRenderTargets : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private int _sceneWidth;
    private int _sceneHeight;

    public LightingRenderTargets(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    public RenderTarget2D? Scene { get; private set; }

    // Short casters (creatures): occlude only close behind themselves.
    public RenderTarget2D? EntityOccluder { get; private set; }

    // Full-height casters (walls, radars, solid rock): occlude at any distance.
    public RenderTarget2D? TallOccluder { get; private set; }

    public RenderTarget2D? Emissive { get; private set; }

    // Silhouette of open water, used to give it a specular sheen in the composite.
    public RenderTarget2D? WaterMask { get; private set; }

    public RenderTarget2D? LightingField { get; private set; }

    // Previous frame's accumulated field, swapped with LightingField each frame.
    public RenderTarget2D? LightingHistory { get; private set; }

    public RenderTarget2D? LightingAccumulated { get; private set; }

    public RenderTarget2D[] Cascades { get; private set; } = [];

    public LightingCascadeLayout Layout { get; private set; }

    public int LightWidth { get; private set; }

    public int LightHeight { get; private set; }

    public static Point CalculateLightSize(Point viewportSize)
    {
        return new Point(
            Math.Max(1, (Math.Max(1, viewportSize.X) + 1) / 2),
            Math.Max(1, (Math.Max(1, viewportSize.Y) + 1) / 2));
    }

    // Recreate all camera-space buffers together so no pass can use a stale viewport size.
    // Returns true when targets were reallocated, so callers know any temporal history they were
    // accumulating into them is now undefined and must be discarded.
    public bool EnsureSize(Point viewportSize)
    {
        var sceneWidth = Math.Max(1, viewportSize.X);
        var sceneHeight = Math.Max(1, viewportSize.Y);
        var lightSize = CalculateLightSize(viewportSize);
        var lightWidth = lightSize.X;
        var lightHeight = lightSize.Y;
        var layout = LightingCascadeLayout.Create(lightSize);
        if (Scene is not null &&
            _sceneWidth == sceneWidth &&
            _sceneHeight == sceneHeight &&
            LightWidth == lightWidth &&
            LightHeight == lightHeight &&
            Layout == layout)
        {
            return false;
        }

        DisposeTargets();
        _sceneWidth = sceneWidth;
        _sceneHeight = sceneHeight;
        LightWidth = lightWidth;
        LightHeight = lightHeight;
        Scene = CreateTarget(sceneWidth, sceneHeight, highPrecision: false);
        EntityOccluder = CreateTarget(lightWidth, lightHeight, highPrecision: false);
        TallOccluder = CreateTarget(lightWidth, lightHeight, highPrecision: false);
        WaterMask = CreateTarget(lightWidth, lightHeight, highPrecision: false);
        Emissive = CreateTarget(lightWidth, lightHeight, highPrecision: true);
        LightingField = CreateTarget(layout.LightingFieldSize.X, layout.LightingFieldSize.Y, highPrecision: true);
        LightingAccumulated = CreateTarget(layout.LightingFieldSize.X, layout.LightingFieldSize.Y, highPrecision: true);
        LightingHistory = CreateTarget(layout.LightingFieldSize.X, layout.LightingFieldSize.Y, highPrecision: true);
        Cascades = new RenderTarget2D[layout.CascadeCount];
        for (var index = 0; index < Cascades.Length; index++)
        {
            Cascades[index] = CreateTarget(layout.PackedSize.X, layout.PackedSize.Y, highPrecision: true);
        }

        CascadeScratch = CreateTarget(layout.PackedSize.X, layout.PackedSize.Y, highPrecision: true);
        Layout = layout;
        return true;
    }

    public void Dispose()
    {
        DisposeTargets();
        GC.SuppressFinalize(this);
    }

    private RenderTarget2D CreateTarget(int width, int height, bool highPrecision)
    {
        if (highPrecision)
        {
            try
            {
                return CreateTarget(width, height, SurfaceFormat.HalfVector4);
            }
            catch (InvalidOperationException)
            {
                // Reach devices may not expose half-float render targets; use the portable format.
            }
            catch (NotSupportedException)
            {
                // DesktopGL drivers can reject optional formats even when the shader is supported.
            }
        }

        return CreateTarget(width, height, SurfaceFormat.Color);
    }

    private RenderTarget2D CreateTarget(int width, int height, SurfaceFormat format)
    {
        return new RenderTarget2D(
            _graphicsDevice,
            width,
            height,
            mipMap: false,
            format,
            DepthFormat.None,
            preferredMultiSampleCount: 0,
            RenderTargetUsage.DiscardContents);
    }

    internal void SwapAccumulatedWithHistory()
    {
        (LightingAccumulated, LightingHistory) = (LightingHistory, LightingAccumulated);
    }

    internal void SwapCascadeWithScratch(int cascadeIndex)
    {
        var scratch = CascadeScratch ?? throw new InvalidOperationException("Cascade scratch target is not initialized.");
        var current = Cascades[cascadeIndex];
        Cascades[cascadeIndex] = scratch;
        CascadeScratch = current;
    }

    private void DisposeTargets()
    {
        Scene?.Dispose();
        EntityOccluder?.Dispose();
        TallOccluder?.Dispose();
        WaterMask?.Dispose();
        Emissive?.Dispose();
        LightingField?.Dispose();
        LightingAccumulated?.Dispose();
        LightingHistory?.Dispose();
        for (var index = 0; index < Cascades.Length; index++)
        {
            Cascades[index].Dispose();
        }

        CascadeScratch?.Dispose();

        Scene = null;
        EntityOccluder = null;
        TallOccluder = null;
        WaterMask = null;
        Emissive = null;
        LightingField = null;
        LightingAccumulated = null;
        LightingHistory = null;
        Cascades = [];
        CascadeScratch = null;
    }

    internal RenderTarget2D? CascadeScratch { get; private set; }
}
