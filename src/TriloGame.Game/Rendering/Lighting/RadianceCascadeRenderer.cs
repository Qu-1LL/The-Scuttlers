using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;

namespace TriloGame.Game.Rendering.Lighting;

// World footprint of the entity occluder mask. Everything the ray march needs to turn a world
// position into a mask texel, with no reference to the screen - see GetOccluderMaskLayout.
public readonly record struct OccluderMaskLayout(Point Size, Vector2 WorldOrigin, Vector2 TexelsPerWorld)
{
    public Vector2 WorldSpan => new(Size.X / TexelsPerWorld.X, Size.Y / TexelsPerWorld.Y);

    // How far past a given screen footprint this mask reaches, in world units, on the tighter axis.
    // Must stay at or above ShortShadowTiles for the ray march's bounds test to be unreachable by any
    // caster that could still contribute.
    public float GetWorldMargin(Vector2 screenWorldSpan)
    {
        var span = WorldSpan;
        return MathF.Min((span.X - screenWorldSpan.X) * 0.5f, (span.Y - screenWorldSpan.Y) * 0.5f);
    }
}

public sealed class RadianceCascadeRenderer : IDisposable
{
    private const float LightBufferScale = 0.5f;

    // There is deliberately NO temporal accumulation here any more.
    //
    // It existed to average out "lattice shimmer" - but once every cascade is snapped to its own
    // world grid there is nothing left to average: measured over a 240-frame capture, the field is
    // bit-identical (0.000%) from frame to frame both while stationary and while panning. A filter
    // with nothing to filter is not free, though. It actively obstructed diagnosis for a long time,
    // because it converted the one real defect - a single-frame 80%-of-mean step when the lattice
    // stepped - into a ten-frame exponential decay at (1 - blend) per frame. That is why this
    // presented as continuous "stuttering" rather than as a discrete event every 3.2 tiles, and why
    // frame-to-frame pixel comparisons never localised it.
    //
    // If a genuinely stochastic input is ever added (jittered sampling, stochastic shadow taps), a
    // temporal filter can come back - but it should be added to hide sampling noise that is known to
    // exist, never to hide instability whose source has not been found.
    private float _animationSeconds;
    // Each cascade's probe lattice, as an absolute world origin. Indexed by cascade - see
    // UpdateProbeLattice for why these are not one shared value.
    //
    // These used to be accompanied by per-cascade OFFSETS - residuals against the camera's own
    // origin - because the shader derived a probe's world position from its packed-pixel index and
    // needed the residual to correct it. It now receives the origin itself and adds whole probe
    // spacings to it, so there is no residual and no second copy of the conversion to keep in step.
    private Vector2[] _latticeWorldOrigins = [];

    // Cascade 0's nominal probe spacing in world units, and each cascade's actual spacing - which is
    // the nominal doubling per level, widened where a cascade has to cover its own merge look-ahead.
    private Vector2 _probeWorldSpacing;
    private Vector2[] _cascadeProbeWorldSpacings = [];
    // World footprint of the entity occluder mask this frame - see GetOccluderMaskLayout.
    private OccluderMaskLayout _maskLayout;

    // Cascade 0's lattice, which is the one the composite reads and the one the temporal
    // reprojection tracks.
    private Vector2 LatticeWorldOrigin => _latticeWorldOrigins.Length > 0 ? _latticeWorldOrigins[0] : Vector2.Zero;


    private readonly GraphicsDevice _graphicsDevice;
    private readonly Effect _effect;
    private readonly LightingRenderTargets _targets;
    private readonly LightingTileGrid _tileGrid;
    private readonly LightingSourceCollector _sourceCollector = new();
    private readonly List<OreLightEmitter> _oreEmitters = [];
    private readonly List<BuildingLightEmitter> _buildingEmitters = [];
    private readonly List<Tile> _visibleTiles = [];
    // The visible tiles the water surface covers, padding included. See
    // WorldSceneRenderer.CollectWaterSurfaceTiles.
    private readonly List<Tile> _waterSurfaceTiles = [];
    private readonly OreLightColorPalette _colorPalette = new();
    private readonly BuildingOccluderCoverage _occluderCoverage = new();
    private readonly Texture2D _oreLightTexture;
    private readonly Texture2D _waterNoiseTexture;

    // Additive, but RGB only. The water silhouette lives in the mask's ALPHA and the disturbance
    // discs are added into its colour channels afterwards; a plain BlendState.Additive would
    // accumulate into alpha as well and dissolve the silhouette it is being drawn on top of.
    private static readonly BlendState AdditiveColorOnly = new()
    {
        ColorSourceBlend = Blend.SourceAlpha,
        ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
        ColorWriteChannels = ColorWriteChannels.Red | ColorWriteChannels.Green | ColorWriteChannels.Blue
    };

    public RadianceCascadeRenderer(GraphicsDevice graphicsDevice, Effect effect)
    {
        _graphicsDevice = graphicsDevice;
        _effect = effect;
        _targets = new LightingRenderTargets(graphicsDevice);
        _tileGrid = new LightingTileGrid(graphicsDevice);
        _oreLightTexture = CreateOreLightTexture(64);
        _waterNoiseTexture = CreateWaterNoiseTexture(256);
    }

    public LightingDebugMode DebugMode { get; private set; }

    // ---- Lighting-field capture (diagnostics) -------------------------------------------------
    //
    // Writes the raw lighting field, plus the exact camera and lattice state that produced it, for a
    // run of consecutive frames. Video can only get within about 2:1 of the compression noise floor,
    // which is not enough to tell a genuinely unstable probe from an h.264 artefact. This records the
    // float values directly and records the camera origin alongside them, so a fixed WORLD position
    // can be followed from frame to frame and its radiance compared against itself - no motion
    // estimation, no codec, no guessing.
    //
    // The run is split into three phases so that each source of change is measured against the others
    // rather than confounded with them:
    //
    //   0 STILL - camera fixed. Anything that moves here is the scene (creatures, pulsing ore), not us.
    //   1 PAN   - camera translating at a fixed world rate, zoom held.
    //   2 ZOOM  - camera fixed, stepping through the zoom ladder and back down again.
    //
    // The zoom phase revisits every rung on the way back, which is what makes DETERMINISM testable:
    // the same rung with the same camera must produce a bit-identical field, and if it does not, the
    // problem is genuine non-determinism rather than a sampling-density change. That distinction
    // cannot be made by eye, and it decides which half of the system to look in.
    private int _captureFramesRemaining;
    private int _captureStillFrames;
    private int _capturePanFrames;
    private int _captureZoomFrames;
    private BinaryWriter? _captureWriter;

    public int CaptureFramesRemaining => _captureFramesRemaining;

    // 0 = still, 1 = pan, 2 = zoom. Frames already written, counted forward.
    public int CaptureFrameIndex =>
        _captureStillFrames + _capturePanFrames + _captureZoomFrames - _captureFramesRemaining;

    public int CapturePhase
    {
        get
        {
            var index = CaptureFrameIndex;
            if (index < _captureStillFrames)
            {
                return 0;
            }

            return index < _captureStillFrames + _capturePanFrames ? 1 : 2;
        }
    }

    // Which frame of the zoom phase this is, or -1 when not in it.
    //
    // The _captureFramesRemaining guard is load-bearing, not defensive: with no capture running every
    // counter is zero, so CaptureFrameIndex is 0 and CapturePhase's "past the pan phase" test is
    // trivially true - it reports phase 2 forever. Without this check the caller then drives the
    // camera to a capture zoom rung on every frame of normal play, which pins the zoom.
    public int CaptureZoomFrame =>
        _captureFramesRemaining > 0 && CapturePhase == 2
            ? CaptureFrameIndex - _captureStillFrames - _capturePanFrames
            : -1;

    public bool CaptureIsPanningPhase => _captureFramesRemaining > 0 && CapturePhase == 1;

    public string? BeginFieldCapture(int stillFrames, int panFrames, int zoomFrames)
    {
        if (_captureWriter is not null || _targets.LightingField is null)
        {
            return null;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "lightfield-capture.bin");
        _captureWriter = new BinaryWriter(File.Create(path));
        _captureWriter.Write(new[] { 'T', 'R', 'L', 'F' });
        _captureWriter.Write(2);
        _captureWriter.Write(_targets.LightingField.Width);
        _captureWriter.Write(_targets.LightingField.Height);
        _captureWriter.Write(stillFrames + panFrames + zoomFrames);
        _captureWriter.Write(stillFrames);
        _captureWriter.Write(panFrames);
        _captureWriter.Write(zoomFrames);
        _captureStillFrames = stillFrames;
        _capturePanFrames = panFrames;
        _captureZoomFrames = zoomFrames;
        _captureFramesRemaining = stillFrames + panFrames + zoomFrames;
        return path;
    }

    private void CaptureLightingField(RenderingContext context)
    {
        if (_captureWriter is null)
        {
            return;
        }

        var field = GetLitField() as RenderTarget2D;
        if (field is null)
        {
            return;
        }

        var count = field.Width * field.Height;
        var rgb = new float[count * 3];
        if (field.Format == SurfaceFormat.HalfVector4)
        {
            var data = new HalfVector4[count];
            field.GetData(data);
            for (var i = 0; i < count; i++)
            {
                var v = data[i].ToVector4();
                rgb[(i * 3) + 0] = v.X;
                rgb[(i * 3) + 1] = v.Y;
                rgb[(i * 3) + 2] = v.Z;
            }
        }
        else
        {
            var data = new Color[count];
            field.GetData(data);
            for (var i = 0; i < count; i++)
            {
                rgb[(i * 3) + 0] = data[i].R / 255f;
                rgb[(i * 3) + 1] = data[i].G / 255f;
                rgb[(i * 3) + 2] = data[i].B / 255f;
            }
        }

        var camera = context.Camera;
        _captureWriter.Write(camera.CameraOrigin.X);
        _captureWriter.Write(camera.CameraOrigin.Y);
        _captureWriter.Write(camera.CurrentScale);
        _captureWriter.Write(LatticeWorldOrigin.X);
        _captureWriter.Write(LatticeWorldOrigin.Y);
        // Cascade 0's probe spacing, which is what a field texel spans. Replaces the lattice offset
        // the format used to record; the offset no longer exists, and the spacing is what an analysis
        // actually needs to convert a world delta into texels.
        _captureWriter.Write(_probeWorldSpacing.X);
        _captureWriter.Write(_probeWorldSpacing.Y);
        _captureWriter.Write(CapturePhase);
        foreach (var f in rgb)
        {
            _captureWriter.Write(f);
        }

        if (--_captureFramesRemaining <= 0)
        {
            _captureWriter.Dispose();
            _captureWriter = null;
        }
    }

    // Ore light colours are derived from the ore sprites themselves, so they must be registered
    // once the sprite atlas is loaded.
    public void RegisterOreLightColors(SpriteFactory sprites)
    {
        foreach (var ore in OreType.GetOres())
        {
            if (sprites.TryGet(ore.Name, out var texture))
            {
                _colorPalette.Register(ore.Name, texture);
            }
        }
    }

    // Per-tile sprite coverage for occluding buildings, measured once from the atlas for the same
    // reason the palette is: it reads pixels back off the GPU, which cannot happen per frame.
    //
    // Takes prototypes rather than live buildings because coverage depends only on the texture and
    // the footprint - both fixed per building type - and the atlas is loaded long before any
    // building exists in a cave.
    public void RegisterBuildingOccluders(SpriteFactory sprites, IEnumerable<Building> prototypes)
    {
        foreach (var building in prototypes)
        {
            if (LightingTileClassifier.IsBuildingOccluder(building) &&
                sprites.TryGet(building.TextureKey, out var texture))
            {
                _occluderCoverage.Register(building.TextureKey, texture, building.Size);
            }
        }
    }

    // Cycle render diagnostics through only the cascades allocated for the current viewport.
    public void CycleDebugMode()
    {
        if (DebugMode == LightingDebugMode.Cascade5 && _targets.Layout.CascadeCount < 6)
        {
            DebugMode = LightingDebugMode.LitWorld;
            return;
        }

        if (DebugMode is >= LightingDebugMode.Cascade0 and <= LightingDebugMode.Cascade5)
        {
            var nextCascade = GetCascadeIndex(DebugMode) + 1;
            DebugMode = nextCascade < _targets.Layout.CascadeCount
                ? (LightingDebugMode)((int)LightingDebugMode.Cascade0 + nextCascade)
                : LightingDebugMode.LitWorld;
            return;
        }

        DebugMode = DebugMode == LightingDebugMode.LightingField
            ? LightingDebugMode.Cascade0
            : DebugMode + 1;
    }

    public void RenderWorld(
        RenderingContext context,
        WorldSceneRenderer worldRenderer,
        GameSession session,
        WorldSpriteEffectSystem spriteEffects,
        Point viewportSize,
        bool showFullMapVisibility,
        bool showCombatDebug,
        float interpolationAlpha)
    {
        var cave = session.Cave;
        if (cave is null)
        {
            return;
        }

        _targets.EnsureSize(viewportSize);

        // Shared animation clock, used by the composite to phase the water ripple.
        _animationSeconds = spriteEffects.ElapsedSeconds;
        UpdateProbeLattice(context.Camera, viewportSize);

        var scene = _targets.Scene!;
        var waterScene = _targets.WaterScene!;
        var entityOccluder = _targets.EntityOccluder!;
        var emissive = _targets.Emissive!;

        WorldSceneRenderer.CollectVisibleTiles(
            cave,
            context.Camera,
            viewportSize,
            showFullMapVisibility,
            _visibleTiles);
        WorldSceneRenderer.CollectWaterSurfaceTiles(cave, _visibleTiles, _waterSurfaceTiles);

        // Screen-space consumers only: the drawn halo sprites and the creature contact shadows. The
        // ray march does NOT read this list - the tile grid resolves emission itself, across the
        // full lighting footprint, so a deposit off the edge of the screen still lights what is on
        // it. See LightingTileGrid.Update.
        _sourceCollector.CollectOreEmitters(_visibleTiles, spriteEffects, _oreEmitters, _colorPalette);
        _sourceCollector.CollectBuildingEmitters(
            cave,
            showFullMapVisibility,
            spriteEffects.ElapsedSeconds,
            _buildingEmitters);
        _tileGrid.Update(
            cave,
            context.Camera,
            viewportSize,
            showFullMapVisibility,
            spriteEffects,
            _colorPalette,
            CalculateLightRangeTiles(_targets.Layout, BaseIntervalSpacing),
            _occluderCoverage);

        // The layer under the floor. Kept separate so the composite can shade the surface and then
        // cover it with the scene, which is what makes water read as being below the floor rather
        // than as a tile beside it - see WorldSceneRenderer.DrawWaterSceneLayer.
        _graphicsDevice.SetRenderTarget(waterScene);
        _graphicsDevice.Clear(Color.Transparent);
        context.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
        worldRenderer.DrawWaterSceneLayer(context, _waterSurfaceTiles, spriteEffects);
        context.SpriteBatch.End();

        _graphicsDevice.SetRenderTarget(scene);
        _graphicsDevice.Clear(Color.Transparent);
        context.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
        worldRenderer.DrawWorldLayer(
            context,
            session,
            spriteEffects,
            showFullMapVisibility,
            showCombatDebug,
            interpolationAlpha,
            _visibleTiles,
            _oreEmitters,
            _tileGrid);
        context.SpriteBatch.End();

        if (DebugMode == LightingDebugMode.SceneOnly)
        {
            Composite(context, viewportSize, scene, waterScene, entityOccluder, emissive);
            return;
        }

        // No tall-occluder mask pass any more: full-height blockers are resolved from the world-space
        // tile grid inside the ray march (see SampleRaySegment), which is both stable under camera
        // motion and one whole full-screen rasterisation cheaper per frame.
        DrawEntityOccluderMask(context, worldRenderer, cave, entityOccluder, interpolationAlpha);
        DrawWaterMask(context, worldRenderer, cave, _targets.WaterMask!, interpolationAlpha);
        DrawEmissiveMask(context, worldRenderer, emissive);
        RunCascades(context, viewportSize);
        ReduceLightingField(context.SpriteBatch);
        CaptureLightingField(context);
        Composite(context, viewportSize, scene, waterScene, entityOccluder, emissive);
    }

    public void Dispose()
    {
        _targets.Dispose();
        _tileGrid.Dispose();
        _oreLightTexture.Dispose();
        _waterNoiseTexture.Dispose();
        _effect.Dispose();
        GC.SuppressFinalize(this);
    }

    // World distance between adjacent cascade-0 probes, and therefore the world footprint of one
    // lighting-field texel. This is the single conversion between a probe index and a world
    // distance, so the lattice snap and the history reprojection both have to go through it.
    //
    // Above the default zoom GetZoomFactor cancels the division by CameraScale exactly and this is
    // constant - that is what SetCascadeParameters' scaling buys. Below it the factor is clamped to
    // 1 (see GetZoomFactor for why it must be), so the spacing is fixed in SCREEN pixels there and
    // grows in world terms as the camera pulls back, which is the ordinary behaviour of a
    // screen-space probe lattice. Either way it is derived, never assumed: computing it from the
    // ACTUAL light-buffer size rather than the nominal LightBufferScale keeps it exact on an odd
    // viewport, where CalculateLightSize rounds up and the real ratio is not quite 2.
    internal static Vector2 GetProbeWorldSpacing(Point viewportSize, Point lightSize, float cameraScale)
    {
        var safeScale = MathF.Max(0.000001f, cameraScale);
        var nominal = new Vector2(
            OreLightSettings.BaseProbeSpacing * SourcePixelToViewportPixels(viewportSize.X, lightSize.X),
            OreLightSettings.BaseProbeSpacing * SourcePixelToViewportPixels(viewportSize.Y, lightSize.Y));

        // Quantise the spacing to a power-of-two multiple of its value at the reference zoom, and
        // round COARSER, never finer.
        //
        // Screen-relative spacing means the world grid depends on the camera scale, so in principle
        // every zoom rung has a different grid and every zoom re-seats every probe. Snapping the
        // spacing to powers of two collapses that: the zoom ladder steps by 4/3, so several
        // consecutive rungs land on the same quantised spacing and share ONE world grid - zooming
        // between them moves no probe at all. When a step does cross a boundary the spacing exactly
        // doubles or halves, so the new grid is nested in the old one and half the probes keep their
        // world positions rather than all of them moving.
        //
        // At this viewport the eleven rungs collapse to six distinct grids. Rounding up rather than
        // to nearest is what keeps the lattice covering the screen: rounding to nearest picks a finer
        // spacing at some rungs, and since the probe COUNT is fixed by the packed layout, finer means
        // a lattice smaller than the view it has to light.
        var reference = nominal / GameConstants.DefaultCameraScale;
        var desired = nominal / safeScale;
        var steps = MathF.Ceiling(MathF.Log2(MathF.Max(0.000001f, desired.X / reference.X)));
        var factor = MathF.Pow(2f, steps);
        return reference * factor;
    }

    // The world point every cascade's lattice is centred on: the middle of the view.
    //
    // This deliberately does NOT reference the viewport size or the visible world span. The lattice is
    // centred on the screen either way - ViewCenter is exactly viewport/2, so
    // screenTopLeft + screenSpan/2 is the camera origin - but the two formulations behave completely
    // differently under zoom.
    //
    // Anchoring at the screen's top-left and subtracting a (latticeSpan - screenSpan)/2 margin makes
    // the origin depend on screenSpan, which changes at EVERY zoom rung. Measured at 1080p, rungs 0
    // and +1 share a probe spacing of 102.4 but their margins are 409.6 and 1945.6 - so the snapped
    // origin moved about fifteen probes on a zoom step that was supposed to move nothing. That is the
    // jolt visible in the LightingField debug view when zooming: the field is drawn in its own space
    // and should sit still, and it does not.
    //
    // Written as a centre, the screenSpan terms cancel before they are ever floored, so the origin
    // depends only on the camera position and the spacing. Two rungs sharing a spacing then share an
    // exact world grid, which is what the quantisation was for.
    internal static Vector2 GetLatticeAnchorWorld(
        Vector2 cameraOrigin,
        Vector2 shakeOffset,
        float cameraScale)
    {
        return cameraOrigin - (shakeOffset / MathF.Max(0.000001f, cameraScale));
    }

    // Snaps DOWNWARD, and the direction is the whole point.
    //
    // Rounding to nearest puts the snapped origin up to half a spacing on EITHER side of the point it
    // is derived from. The positive half is unusable: it places the lattice origin after the camera's
    // own origin, so the top-left of the screen falls outside the probe grid entirely and
    // GetLightingFieldUv resolves to a negative UV that the clamped sampler answers with a single
    // edge texel, smeared across the whole uncovered band. Measured on a 1920x1080 capture: a 224 px
    // band down the left of the screen, ramping to zero over 32 frames of panning and snapping back
    // to 224 on the next lattice step. Because the band's colour is just whichever probe sits on the
    // lattice edge, it reads as an arbitrary strip of full brightness or full darkness rather than as
    // anything lighting-shaped.
    //
    // Flooring makes the offset one-sided - always in [-spacing, 0] - so the screen origin is never
    // ahead of the lattice origin and that band cannot exist. LightingCascadeLayout.Create carries the
    // matching full-block slack on the far edge.
    internal static Vector2 SnapToLattice(Vector2 worldPoint, Vector2 spacing)
    {
        return new Vector2(
            MathF.Floor(worldPoint.X / spacing.X) * spacing.X,
            MathF.Floor(worldPoint.Y / spacing.Y) * spacing.Y);
    }

    // (GetMergeLookAheadWorld is gone. It measured how far past a probe the merge reached into the
    // next cascade - a question that stopped existing when the merge moved to sampling at the probe's
    // own position. Nothing reaches ahead any more, so nothing needs the distance.)

    // World distance between adjacent probes of a given cascade: the base spacing doubled per level.
    //
    // This briefly carried a coverage floor that widened the coarse cascades so they could span the
    // screen PLUS the distance a merge reached into them. That requirement came entirely from the
    // merge sampling at the ray's far endpoint; sampling at the probe's own position instead (see
    // CascadeMergePixel) puts the lookup inside the lattice by construction, so the floor is not just
    // unnecessary but harmful - it was coarsening the top cascade by up to 6x for nothing.
    //
    // Every cascade spans the same world extent by construction: probe count halves exactly as
    // spacing doubles, so count_k * spacing_k is the same at every level. A probe of cascade k is
    // therefore inside cascade k+1's lattice everywhere except within a spacing of the edge, which is
    // what CascadeMergePixel's edge fade covers.
    internal static Vector2 GetCascadeProbeWorldSpacing(Vector2 probeWorldSpacing, int cascadeIndex)
    {
        return probeWorldSpacing * (1 << Math.Max(0, cascadeIndex));
    }

    // (GetHigherOriginSourceOffset is gone. It existed to reconcile two cascades' origins inside a
    // source-pixel space that no longer exists: the merge is handed the higher cascade's own world
    // origin and spacing, so it indexes that cascade directly and the reconciliation falls out.)

    private static float SourcePixelToViewportPixels(int viewportExtent, int lightExtent)
    {
        return Math.Max(1, viewportExtent) / (float)Math.Max(1, lightExtent);
    }

    // Pins the probe lattice to a fixed world grid whose spacing is one cascade-0 probe, so that
    // moving the camera never changes which world point a given probe samples - it only changes
    // which probe index that world point falls on. Everything downstream (ray marching, occlusion,
    // the reprojection in AccumulateLightingField) depends on that being exactly true.
    //
    // The subtlety, and what this previously got wrong, is WHICH world point has to be snapped. It
    // used to snap CameraOrigin alone, but a probe's world position is not CameraOrigin plus a
    // multiple of the spacing - SourcePixelToWorld also subtracts the view centre and the shake and
    // then divides the whole thing by CameraScale:
    //
    //     probeWorld(i) = CameraOrigin + (halfSourcePixel - ViewCenter - Shake) / CameraScale
    //                     + (i + 0.5) * spacing
    //
    // Only the last term is zoom-invariant. The bracketed term is a large screen-space distance
    // divided by the live scale, so its remainder modulo the spacing is different at every rung of
    // the zoom ladder - at this viewport it lands exactly on the grid at the default rung and 0.75
    // of a probe off it one rung in. Snapping CameraOrigin therefore pinned the lattice against
    // panning but left it free to slide up to half a probe on every zoom step and continuously
    // under screen shake, which is what showed up as light and shadow shifting off the tiles when
    // zooming even after the camera had settled.
    //
    // Snapping the assembled origin instead makes probeWorld(i) an exact multiple of the spacing
    // plus a fixed half-probe phase, at every zoom, with or without shake. The residual is handed to
    // the shader as a world offset applied to probe positions; GetLightingFieldUv undoes the same
    // offset, in world space, when reading the field back.
    //
    // EACH CASCADE GETS ITS OWN ORIGIN, and that is what stops the lattice stepping being visible.
    //
    // A single shared origin has to be snapped to the COARSEST cascade's spacing, because cascade k's
    // probes only land back on positions they already occupied when the origin moves a whole multiple
    // of spacing * 2^k. At this viewport that made one step 3.2 TILES - and the step is not a no-op
    // the way it looks, because it also teleports the screen 16 probe columns sideways INSIDE the
    // lattice. Measured over a 240-frame capture: the field was bit-identical (0.000%) on every
    // frame between steps and jumped 28% of mean radiance on the step itself, decaying back at
    // exactly the history blend rate over the following ten frames. Three steps in a 10.5-tile pan,
    // one every 3.2 tiles, at every zoom rung - which is the stutter, and why it was equally present
    // zoomed in and zoomed out.
    //
    // Snapping each cascade to its own spacing removes the coupling: cascade 0 steps by 0.2 tiles,
    // cascade 4 by 3.2, but each steps by exactly ONE OF ITS OWN probes, so every cascade re-seats
    // onto positions it already occupied. The screen's position inside cascade 0's lattice - which is
    // the one the composite reads - now moves by a single texel instead of sixteen.
    //
    // The cost is that adjacent cascades no longer share an origin, so the merge is handed the higher
    // cascade's origin and spacing directly rather than a position in the lower cascade's space.
    private void UpdateProbeLattice(CameraController camera, Point viewportSize)
    {
        var lightSize = new Point(_targets.LightWidth, _targets.LightHeight);

        // The lattice is built at the camera's SETTLED scale, never its live one.
        //
        // CurrentScale glides continuously for ~200ms after every zoom input. Feeding that into the
        // probe spacing meant the spacing - and therefore the world grid every cascade is snapped to -
        // changed on every frame of the glide, so every probe re-marched from a new world position
        // twelve or so times per zoom. Nothing downstream could be stable through that.
        //
        // TargetScale jumps once, at the moment the input is received, and then holds. Combined with
        // the power-of-two quantisation in GetProbeWorldSpacing, most zoom steps do not change the
        // spacing at all and the lattice simply does not move; the ones that do change it move onto a
        // nested grid, once, rather than continuously.
        //
        // This does NOT desynchronise the light from the geometry. Probe positions are world
        // quantities and the composite maps screen to world with the live CurrentScale, so the field
        // stays glued to the tiles throughout the glide - only its sampling density is briefly that of
        // the rung being left rather than the one being entered.
        var latticeScale = camera.TargetScale;
        _probeWorldSpacing = GetProbeWorldSpacing(viewportSize, lightSize, latticeScale);
        var anchor = GetLatticeAnchorWorld(camera.CameraOrigin, camera.ShakeOffset, latticeScale);

        var layout = _targets.Layout;
        var cascadeCount = layout.CascadeCount;
        if (_latticeWorldOrigins.Length != cascadeCount)
        {
            _latticeWorldOrigins = new Vector2[cascadeCount];
            _cascadeProbeWorldSpacings = new Vector2[cascadeCount];
        }

        for (var index = 0; index < cascadeCount; index++)
        {
            var cascadeSpacing = GetCascadeProbeWorldSpacing(_probeWorldSpacing, index);
            _cascadeProbeWorldSpacings[index] = cascadeSpacing;

            // Step back from the view's centre by half the lattice, then floor onto the spacing.
            //
            // The half-lattice puts the surplus extent on both sides. There is no longer a half-probe
            // bias on top: probe 0 now sits exactly ON the origin (see GetProbeWorld), so flooring
            // already guarantees it lands at or before the point it is derived from. The bias existed
            // only to compensate for the half-probe phase, and went with it.
            //
            // Every term is a function of the camera position and the spacing, so panning moves the
            // origin in whole spacings and zooming within a shared spacing does not move it at all.
            var count = layout.GetProbeCount(index);
            var latticeSpan = new Vector2(count.X * cascadeSpacing.X, count.Y * cascadeSpacing.Y);

            _latticeWorldOrigins[index] = SnapToLattice(
                anchor - (latticeSpan * 0.5f),
                cascadeSpacing);
        }

        _maskLayout = GetOccluderMaskLayout(
            new Point(_targets.OccluderMaskWidth, _targets.OccluderMaskHeight),
            camera.CameraOrigin,
            camera.ViewCenter,
            camera.ShakeOffset,
            camera.CurrentScale,
            viewportSize);
    }

    // The entity occluder mask's size, in texels. Fixed for a given viewport - deliberately NOT a
    // function of the live camera scale, because it would then be reallocated on every frame of every
    // zoom glide. Sized for the tightest zoom so the world margin below can only ever be larger than
    // it needs to be.
    internal static Point GetOccluderMaskSize(Point viewportSize)
    {
        var marginTexels = 2f * OreLightSettings.ShortShadowTiles * TileConstants.TileSize
            * GameConstants.MaxScale * LightBufferScale;
        // Two extra texels on top of the nominal margin. GetOccluderMaskLayout snaps the origin
        // DOWNWARD to a whole texel, which slides the whole footprint back by up to one texel and so
        // takes that much off the far edge; the strict upper bound in IsInsideMask needs the other.
        // Without them the far corner lands at 1803.86 of 1803 at the tightest zoom - i.e. the margin
        // is correct on average and short in the worst case, which is the only case that matters.
        const float snapSlackTexels = 2f;
        return new Point(
            (int)MathF.Ceiling((viewportSize.X * LightBufferScale) + marginTexels + snapSlackTexels),
            (int)MathF.Ceiling((viewportSize.Y * LightBufferScale) + marginTexels + snapSlackTexels));
    }

    // Where the entity occluder mask sits in the world, and how many of its texels one world unit
    // spans. This is the whole of item 2: the mask must not have a SCREEN-shaped edge.
    //
    // It used to be exactly the size of the light buffer, i.e. exactly the screen, and the ray march
    // bounds-tested every occlusion sample against it. Rays reach ~26 tiles while the screen at high
    // zoom is barely 4, so whether a creature occluded a ray depended on where the camera happened to
    // be pointing - and the boundary swept across the world as you panned. Worse, the bounds test
    // `continue`d, which shrank the DENOMINATOR of the coverage average: two of five samples landing
    // on a creature read as 1.0 coverage instead of 0.4, so a sample crossing the screen edge changed
    // the answer even where the mask did have data.
    //
    // The mask only ever needs to reach ShortShadowTiles beyond the view, because shortFade takes a
    // short caster's contribution to exactly zero past that distance - so a caster further out than
    // this margin cannot affect any on-screen pixel no matter where the camera is. Covering that
    // margin makes the bounds test unreachable for anything that could contribute, which is what lets
    // the denominator go back to being the full sample count.
    internal static OccluderMaskLayout GetOccluderMaskLayout(
        Point maskSize,
        Vector2 cameraOrigin,
        Vector2 viewCenter,
        Vector2 shakeOffset,
        float cameraScale,
        Point viewportSize)
    {
        var scale = MathF.Max(0.000001f, cameraScale);
        var texelsPerWorld = new Vector2(scale * LightBufferScale, scale * LightBufferScale);
        var texelWorld = new Vector2(1f / texelsPerWorld.X, 1f / texelsPerWorld.Y);
        var maskWorldSpan = new Vector2(maskSize.X / texelsPerWorld.X, maskSize.Y / texelsPerWorld.Y);
        var screenWorldSpan = new Vector2(viewportSize.X / scale, viewportSize.Y / scale);
        var screenTopLeftWorld = cameraOrigin + ((-viewCenter - shakeOffset) / scale);

        // Centre the screen in the mask, then snap to a whole mask texel. Snapping is what keeps a
        // point-sampled silhouette edge from flipping between blocking and clear as the camera slides
        // a fraction of a texel; the residual is under one texel either way, unlike the probe
        // lattice's, which is why these two offsets must stay separate quantities.
        var margin = (maskWorldSpan - screenWorldSpan) * 0.5f;
        var origin = SnapToLattice(screenTopLeftWorld - margin, texelWorld);
        return new OccluderMaskLayout(maskSize, origin, texelsPerWorld);
    }

    // Maps Camera.WorldToScreen's output into the mask's own texel space.
    //
    //   mask = (world - MaskWorldOrigin) * scale * LightBufferScale
    //   screen = ViewCenter + Shake + (world - CameraOrigin) * scale
    //
    // Eliminating `world` gives a plain scale-then-translate, which is what SpriteBatch wants. When
    // the mask origin happens to be the screen's own top-left the translation falls out to zero and
    // this reduces to the bare CreateScale(LightBufferScale) it replaced.
    private Matrix GetOccluderMaskTransform(RenderingContext context)
    {
        var camera = context.Camera;
        var translation =
            (((camera.CameraOrigin - _maskLayout.WorldOrigin) * camera.CurrentScale)
                - camera.ViewCenter - camera.ShakeOffset) * LightBufferScale;
        return Matrix.CreateScale(LightBufferScale)
            * Matrix.CreateTranslation(translation.X, translation.Y, 0f);
    }

    private void DrawEntityOccluderMask(
        RenderingContext context,
        WorldSceneRenderer worldRenderer,
        Cave cave,
        RenderTarget2D target,
        float interpolationAlpha)
    {
        _graphicsDevice.SetRenderTarget(target);
        _graphicsDevice.Clear(Color.Transparent);
        context.SpriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            blendState: BlendState.AlphaBlend,
            transformMatrix: GetOccluderMaskTransform(context));
        worldRenderer.DrawEntityOccluderLayer(context, cave, interpolationAlpha);
        context.SpriteBatch.End();
    }

    // Two passes into one target, carrying two unrelated things in different channels.
    //
    // ALPHA is the water silhouette, which the composite uses to decide what is water at all and
    // whose edge doubles as the shoreline mask. RGB is local disturbance - a radial falloff per
    // moving source, which the shader turns into expanding rings by feeding it to a sine (see
    // GetWaterDisturbance). Packing them together is what avoids both a second render target and a
    // uniform array capping how many sources can exist.
    private void DrawWaterMask(
        RenderingContext context,
        WorldSceneRenderer worldRenderer,
        Cave cave,
        RenderTarget2D target,
        float interpolationAlpha)
    {
        _graphicsDevice.SetRenderTarget(target);
        _graphicsDevice.Clear(Color.Transparent);
        context.SpriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            blendState: BlendState.AlphaBlend,
            transformMatrix: Matrix.CreateScale(LightBufferScale));
        worldRenderer.DrawWaterMaskLayer(context, _waterSurfaceTiles);
        context.SpriteBatch.End();

        context.SpriteBatch.Begin(
            samplerState: SamplerState.LinearClamp,
            blendState: AdditiveColorOnly,
            transformMatrix: Matrix.CreateScale(LightBufferScale));
        worldRenderer.DrawWaterDisturbanceLayer(context, cave, _oreLightTexture, interpolationAlpha);
        context.SpriteBatch.End();
    }

    private void DrawEmissiveMask(
        RenderingContext context,
        WorldSceneRenderer worldRenderer,
        RenderTarget2D target)
    {
        _graphicsDevice.SetRenderTarget(target);
        _graphicsDevice.Clear(Color.Transparent);
        context.SpriteBatch.Begin(
            blendState: BlendState.Additive,
            samplerState: SamplerState.LinearClamp,
            transformMatrix: Matrix.CreateScale(LightBufferScale));
        worldRenderer.DrawEmissiveLayer(context, _oreEmitters, _oreLightTexture, _buildingEmitters);
        context.SpriteBatch.End();
    }

    // Generate every packed cascade independently before merging from the far field inward.
    private void RunCascades(RenderingContext context, Point viewportSize)
    {
        var layout = _targets.Layout;
        var entityOccluder = _targets.EntityOccluder!;
        var emissive = _targets.Emissive!;
        for (var index = 0; index < layout.CascadeCount; index++)
        {
            var target = _targets.Cascades[index];
            _graphicsDevice.SetRenderTarget(target);
            _graphicsDevice.Clear(Color.Transparent);
            _effect.CurrentTechnique = _effect.Techniques["RadianceCascade"];
            SetParameter("Texture", _tileGrid.Texture!);
            SetParameter("TileGridTexture", _tileGrid.Texture!);
            SetParameter("TileEmissionColorTexture", _tileGrid.EmissionColorTexture!);
            SetParameter("EmissiveTexture", emissive);
            SetCascadeParameters(context, viewportSize, layout, index);
            SetParameter("HasHigherCascade", 0f);
            // The raw cascade samples the tile grid through the shared Texture parameter.
            // DrawFullscreen binds its source texture immediately before the draw, so the
            // tile grid must also be the SpriteBatch source here; the emissive mask remains
            // available through EmissiveTexture for the independently collected sources.
            DrawFullscreen(context.SpriteBatch, _tileGrid.Texture!, target.Width, target.Height, BlendState.Opaque, _effect);
        }

        for (var index = layout.CascadeCount - 2; index >= 0; index--)
        {
            var lower = _targets.Cascades[index];
            var higher = _targets.Cascades[index + 1];
            var scratch = _targets.CascadeScratch!;
            _graphicsDevice.SetRenderTarget(scratch);
            _graphicsDevice.Clear(Color.Transparent);
            _effect.CurrentTechnique = _effect.Techniques["CascadeMerge"];
            SetParameter("Texture", lower);
            SetParameter("PreviousCascadeTexture", higher);
            SetMergeParameters(layout, index);
            SetParameter("HigherCascadeResolution", new Vector2(higher.Width, higher.Height));
            SetParameter("HigherRayDimension", (float)layout.GetRayDimension(index + 1));
            // The higher cascade's own world grid. Handing the merge these directly is what removed
            // the origin-reconciliation term: it indexes that cascade in that cascade's own space.
            SetParameter("HigherProbeWorldOrigin", _latticeWorldOrigins[index + 1]);
            SetParameter("HigherProbeWorldSpacing", _cascadeProbeWorldSpacings[index + 1]);
            var higherProbeCount = layout.GetProbeCount(index + 1);
            SetParameter("HigherProbeCount", new Vector2(higherProbeCount.X, higherProbeCount.Y));
            SetParameter("HasHigherCascade", 1f);
            DrawFullscreen(context.SpriteBatch, lower, scratch.Width, scratch.Height, BlendState.Opaque, _effect);
            _targets.SwapCascadeWithScratch(index);
        }
    }

    private void ReduceLightingField(SpriteBatch spriteBatch)
    {
        var layout = _targets.Layout;
        var field = _targets.LightingField!;
        var cascade0 = _targets.Cascades[0];
        _graphicsDevice.SetRenderTarget(field);
        _graphicsDevice.Clear(Color.Transparent);
        _effect.CurrentTechnique = _effect.Techniques["LightingField"];
        SetParameter("Texture", cascade0);
        SetParameter("CascadeResolution", new Vector2(cascade0.Width, cascade0.Height));
        SetParameter("LightingFieldResolution", new Vector2(field.Width, field.Height));
        SetParameter("RayDimension", (float)layout.BaseRayDimension);
        DrawFullscreen(spriteBatch, cascade0, field.Width, field.Height, BlendState.Opaque, _effect);
    }

    private void Composite(
        RenderingContext context,
        Point viewportSize,
        RenderTarget2D scene,
        RenderTarget2D waterScene,
        RenderTarget2D entityOccluder,
        RenderTarget2D emissive)
    {
        _graphicsDevice.SetRenderTarget(null);
        if (DebugMode == LightingDebugMode.SceneOnly)
        {
            DrawFullscreen(context.SpriteBatch, waterScene, viewportSize.X, viewportSize.Y, BlendState.AlphaBlend, null);
            DrawFullscreen(context.SpriteBatch, scene, viewportSize.X, viewportSize.Y, BlendState.AlphaBlend, null);
            return;
        }

        if (DebugMode == LightingDebugMode.TileGrid)
        {
            _effect.CurrentTechnique = _effect.Techniques["TileGridDebug"];
            SetTileGridParameters(context, viewportSize);
            SetParameter("TileGridTexture", _tileGrid.Texture!);
            DrawFullscreen(context.SpriteBatch, _tileGrid.Texture!, viewportSize.X, viewportSize.Y, BlendState.Opaque, _effect);
            return;
        }

        if (DebugMode == LightingDebugMode.EntityOccluder)
        {
            DrawFullscreen(context.SpriteBatch, entityOccluder, viewportSize.X, viewportSize.Y, BlendState.Opaque, null);
            return;
        }

        if (DebugMode == LightingDebugMode.EmissiveMask)
        {
            DrawFullscreen(context.SpriteBatch, emissive, viewportSize.X, viewportSize.Y, BlendState.Opaque, null);
            return;
        }

        if (DebugMode == LightingDebugMode.LightingField)
        {
            DrawFullscreen(context.SpriteBatch, GetLitField(), viewportSize.X, viewportSize.Y, BlendState.Opaque, null);
            return;
        }

        if (DebugMode is >= LightingDebugMode.Cascade0 and <= LightingDebugMode.Cascade5)
        {
            var cascadeIndex = GetCascadeIndex(DebugMode);
            if (cascadeIndex < _targets.Cascades.Length)
            {
                DrawFullscreen(
                    context.SpriteBatch,
                    _targets.Cascades[cascadeIndex],
                    viewportSize.X,
                    viewportSize.Y,
                    BlendState.Opaque,
                    null);
                return;
            }
        }

        if (DebugMode == LightingDebugMode.AmbientOnly)
        {
            DrawAmbientScene(context.SpriteBatch, waterScene, viewportSize);
            DrawAmbientScene(context.SpriteBatch, scene, viewportSize);
            return;
        }

        // Keep the captured scene as the base layer, then add only the lighting contribution.
        // This preserves the parallax background wherever the scene target is transparent.
        //
        // Two draws, water first, because the water layer is BELOW the floor: alpha-blending the
        // scene over it reproduces on screen exactly the stack the world is - background, then pool,
        // then floor. The lighting pass below rebuilds the same order per pixel from the scene's
        // alpha, so the lit and the ambient halves of the image agree about what is on top.
        DrawAmbientScene(context.SpriteBatch, waterScene, viewportSize);
        DrawAmbientScene(context.SpriteBatch, scene, viewportSize);
        _effect.CurrentTechnique = _effect.Techniques["LightingAdd"];
        SetParameter("Texture", scene);
        SetParameter("WaterSceneTexture", waterScene);
        SetParameter("LightingTexture", GetLitField());
        SetParameter("EmissiveTexture", emissive);
        SetParameter("LightingEnabled", 1f);
        SetParameter("LightGain", OreLightSettings.LightGain);
        SetParameter("LitContribution", OreLightSettings.LitContribution);
        SetParameter("WaterMaskTexture", _targets.WaterMask!);
        SetParameter("WaterNoiseTexture", _waterNoiseTexture);
        SetParameter("WaterSheenStrength", OreLightSettings.WaterSheenStrength);
        SetParameter("ElapsedSeconds", _animationSeconds);
        SetParameter("WaterWaveStrength", OreLightSettings.WaterWaveStrength);
        SetParameter("WaterWaveSpeed", OreLightSettings.WaterWaveSpeed);
        SetParameter("WaterNoiseWarpTiles", OreLightSettings.WaterNoiseWarpTiles);
        SetParameter("WaterDistortionUv", OreLightSettings.WaterDistortionUv);
        SetParameter("WaterReflectionTiles", OreLightSettings.WaterReflectionTiles);
        SetParameter("WaterDiffuseStrength", OreLightSettings.WaterDiffuseStrength);
        SetParameter("WaterSpecularStrength", OreLightSettings.WaterSpecularStrength);
        SetParameter("WaterSpecularPower", OreLightSettings.WaterSpecularPower);
        SetParameter("WaterLightBands", OreLightSettings.WaterLightBands);
        SetParameter("WaterRippleRingFrequency", OreLightSettings.WaterRippleRingFrequency);
        SetParameter("WaterRippleRingSpeed", OreLightSettings.WaterRippleRingSpeed);
        SetParameter("WaterFresnelFloor", OreLightSettings.WaterFresnelFloor);
        SetParameter("WaterFresnelScale", OreLightSettings.WaterFresnelScale);
        SetParameter("WaterFresnelPower", OreLightSettings.WaterFresnelPower);
        SetParameter("WaterRefractionLoss", OreLightSettings.WaterRefractionLoss);
        SetParameter("WaterAlbedoLow", OreLightSettings.WaterAlbedoLow);
        SetParameter("WaterAlbedoHigh", OreLightSettings.WaterAlbedoHigh);
        SetTileGridParameters(context, viewportSize);
        // The field is a reduction of cascade 0, so the composite locates it on cascade 0's world
        // grid. Two uniforms, no UV scale, no zoom factor: field texel i holds the probe at
        // origin + (i + 0.5) * spacing, so a world position inverts straight back to a texel.
        SetParameter("ProbeWorldOrigin", LatticeWorldOrigin);
        // Cascade 0's actual spacing. Nothing merges into cascade 0, so it is never widened and this
        // equals the nominal base spacing - but read it from the same array the ray march used, so
        // the two cannot drift apart if that ever stops being true.
        SetParameter("ProbeWorldSpacing", _cascadeProbeWorldSpacings[0]);
        SetParameter("LightingFieldResolution", new Vector2(_targets.LightingField!.Width, _targets.LightingField!.Height));
        DrawFullscreen(context.SpriteBatch, scene, viewportSize.X, viewportSize.Y, BlendState.Additive, _effect);
    }

    // The reduced field, with no temporal stage between it and the screen - see the note on
    // _animationSeconds for why accumulation was removed. This is also what the capture records, so
    // a capture now shows exactly what the ray march produced rather than a filtered version of it.
    private Texture2D GetLitField()
    {
        return _targets.LightingField!;
    }

    // (CalculateLightReachTiles is gone. It converted a pixel reach into tiles at a given zoom, which
    // is a question that can no longer be asked: the reach IS a tile count, authored once in
    // OreLightSettings.LightReachTiles and derived from nowhere.)

    // The range the falloff runs over: the hierarchy's reach, capped so one deposit cannot tint half
    // the map when the reach grows at wide zoom.
    //
    // The cap is what keeps the cost of the widened intervals bounded. Nothing past this distance
    // contributes any light, so it - not the raw reach - is what the tile grid has to cover and what
    // the ray march can stop at.
    internal static float CalculateLightRangeTiles(LightingCascadeLayout layout, float probeWorldSpacing)
    {
        return MathF.Min(
            OreLightSettings.OreLightRangeTiles,
            layout.GetTotalReachWorld(probeWorldSpacing) / TileConstants.TileSize);
    }

    private void DrawAmbientScene(SpriteBatch spriteBatch, Texture2D scene, Point viewportSize)
    {
        DrawFullscreen(
            spriteBatch,
            scene,
            viewportSize.X,
            viewportSize.Y,
            BlendState.AlphaBlend,
            null,
            new Color(OreLightSettings.Ambient, OreLightSettings.Ambient, OreLightSettings.Ambient, 1f));
    }

    // GetZoomFactor is gone.
    //
    // It existed to stop the cascade hierarchy's reach in TILES shrinking as the camera zoomed in -
    // a real problem, but one caused entirely by the intervals being authored in light-buffer pixels.
    // Because the same factor also scaled probe SPACING (the two were the same quantity), fixing
    // reach dragged the lattice with it: the probe lattice grew to several times the screen, only
    // 2.7% of its probes landed on screen at the tightest zoom, and every downstream consumer had to
    // divide the factor back out again (GetLightingFieldUvScale). Removing it is not a deletion of
    // the fix - reach is now authored directly in world units (OreLightSettings.LightReachTiles), so
    // the thing the factor was compensating for cannot happen.
    //
    // Two consequences worth stating plainly. Probe spacing is once again a fixed number of SCREEN
    // pixels, so the lattice spans almost exactly the packed texture and essentially all of its
    // probes are on screen - at the tightest zoom that is roughly 4x finer lighting for the same
    // cost, because the wasted overhang is gone. And the clamp at 1, which existed to stop the
    // lattice shrinking below the screen when zoomed out, is no longer needed for the same reason.

    private void SetCascadeParameters(
        RenderingContext context,
        Point viewportSize,
        LightingCascadeLayout layout,
        int cascadeIndex)
    {
        SetParameter("CascadeResolution", new Vector2(_targets.Cascades[cascadeIndex].Width, _targets.Cascades[cascadeIndex].Height));
        SetParameter("RayDimension", (float)layout.GetRayDimension(cascadeIndex));
        // The whole of a cascade's geometry, in world units and nothing else.
        SetParameter("ProbeWorldOrigin", _latticeWorldOrigins[cascadeIndex]);
        SetParameter("ProbeWorldSpacing", _cascadeProbeWorldSpacings[cascadeIndex]);
        var baseSpacing = BaseIntervalSpacing;
        SetParameter("IntervalWorldOrigin", layout.GetIntervalWorldOrigin(cascadeIndex, baseSpacing));
        SetParameter("IntervalWorldLength", layout.GetIntervalWorldLength(cascadeIndex, baseSpacing));
        SetParameter("LightRangeTiles", CalculateLightRangeTiles(layout, baseSpacing));
        SetParameter("LightFadeFraction", OreLightSettings.LightFadeFraction);
        SetParameter("NearFieldFloor", OreLightSettings.NearFieldFloor);
        SetParameter("NearFieldRampFraction", OreLightSettings.NearFieldRampFraction);
        SetParameter("OreLightColor", OreLightSettings.SharedOreLightColor.ToVector3());
        SetTileGridParameters(context, viewportSize);
    }

    // CascadeMergePixel only reads these; the rest of SetCascadeParameters targets globals unused by
    // the CascadeMerge technique, which crashes EffectParameter.SetValue on the OpenGL backend when
    // the compiled shader has no storage for them.
    private void SetMergeParameters(LightingCascadeLayout layout, int cascadeIndex)
    {
        SetParameter("CascadeResolution", new Vector2(_targets.Cascades[cascadeIndex].Width, _targets.Cascades[cascadeIndex].Height));
        SetParameter("RayDimension", (float)layout.GetRayDimension(cascadeIndex));
        SetParameter("ProbeWorldOrigin", _latticeWorldOrigins[cascadeIndex]);
        SetParameter("ProbeWorldSpacing", _cascadeProbeWorldSpacings[cascadeIndex]);
        SetParameter("IntervalWorldOrigin", layout.GetIntervalWorldOrigin(cascadeIndex, BaseIntervalSpacing));
        SetParameter("IntervalWorldLength", layout.GetIntervalWorldLength(cascadeIndex, BaseIntervalSpacing));
    }

    // The spacing the interval scale is derived from: cascade 0's, taken on whichever axis is coarser
    // so the "spacing must not exceed the interval" requirement holds on both.
    private float BaseIntervalSpacing => MathF.Max(_probeWorldSpacing.X, _probeWorldSpacing.Y);

    private void SetTileGridParameters(RenderingContext context, Point viewportSize)
    {
        SetParameter("ViewportResolution", new Vector2(viewportSize.X, viewportSize.Y));
        // Must be the camera's TRUE origin, never a snapped/quantised one. The scene colour
        // target and the entity occluder mask are both rasterised with Camera.WorldToScreen,
        // and the resulting lighting field is composited back over that scene in screen space.
        // Feeding the shader a different origin than the one geometry was drawn with displaces
        // all light and shadow from the geometry by the snap residual - a displacement that
        // slides as the camera pans (reads as jitter) and covers a different fraction of a tile
        // at each zoom level (reads as light drifting off the tiles when zooming).
        SetParameter("CameraOrigin", context.Camera.CameraOrigin);
        // ProbeWorldOffset is deliberately NOT set here: it is per-cascade now, and this method is
        // shared by the cascade passes and the composite. Each caller sets its own.
        SetParameter("MaskWorldOrigin", _maskLayout.WorldOrigin);
        SetParameter("MaskTexelsPerWorld", _maskLayout.TexelsPerWorld);
        SetParameter("MaskResolution", new Vector2(_maskLayout.Size.X, _maskLayout.Size.Y));
        SetParameter("CameraViewCenter", context.Camera.ViewCenter);
        SetParameter("CameraShakeOffset", context.Camera.ShakeOffset);
        SetParameter("CameraScale", context.Camera.CurrentScale);
        SetParameter("TileGridOrigin", new Vector2(_tileGrid.Layout.Origin.X, _tileGrid.Layout.Origin.Y));
        SetParameter("TileGridResolution", new Vector2(_tileGrid.Layout.Width, _tileGrid.Layout.Height));
        SetParameter("TileSize", TileConstants.TileSize);
    }

    private void DrawFullscreen(
        SpriteBatch spriteBatch,
        Texture2D texture,
        int width,
        int height,
        BlendState blendState,
        Effect? effect = null,
        Color? color = null)
    {
        if (effect is not null)
        {
            if (_effect.Parameters["Texture"] is { } textureParameter)
            {
                textureParameter.SetValue(texture);
            }

            // Unlike SpriteBatch's built-in SpriteEffect, a custom Effect never gets its
            // MatrixTransform populated automatically; without this every vertex collapses
            // to the origin and the draw silently rasterizes nothing.
            if (_effect.Parameters["MatrixTransform"] is { } matrixParameter)
            {
                var projection = Matrix.CreateOrthographicOffCenter(0, width, height, 0, 0, 1);
                matrixParameter.SetValue(projection);
            }
        }

        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            blendState,
            SamplerState.LinearClamp,
            depthStencilState: null,
            rasterizerState: null,
            effect);
        spriteBatch.Draw(texture, new Rectangle(0, 0, width, height), color ?? Color.White);
        spriteBatch.End();
    }

    private void SetParameter(string name, Texture2D value)
    {
        if (_effect.Parameters[name] is { } parameter)
        {
            parameter.SetValue(value);
        }
    }

    private void SetParameter(string name, Vector2 value)
    {
        if (_effect.Parameters[name] is { } parameter)
        {
            parameter.SetValue(value);
        }
    }

    private void SetParameter(string name, Vector3 value)
    {
        if (_effect.Parameters[name] is { } parameter)
        {
            parameter.SetValue(value);
        }
    }

    private void SetParameter(string name, float value)
    {
        if (_effect.Parameters[name] is { } parameter)
        {
            parameter.SetValue(value);
        }
    }

    private static int GetCascadeIndex(LightingDebugMode mode)
    {
        return (int)mode - (int)LightingDebugMode.Cascade0;
    }

    // Tileable value noise for the water surface warp.
    //
    // Generated rather than authored as an asset because it MUST tile: the shader scrolls it
    // continuously in two directions, so any seam would sweep across every lake once per wrap.
    // Building it from wrapping lattices makes that a property of the construction rather than
    // something an artist has to get right.
    //
    // Three octaves, smoothstep-interpolated, on lattices whose sizes share no common factor.
    //
    // Two octaves on 8 and 16 was too little structure and, worse, 16 is a multiple of 8 - so the
    // two lined up on the coarse lattice's boundaries and reinforced its grid instead of hiding it.
    // 5, 11 and 23 are mutually coprime, so no two octaves share a cell edge anywhere in the
    // texture. That is what stops the noise itself carrying a visible period into the water.
    internal static Color[] CreateWaterNoiseData(int size)
    {
        var data = new Color[size * size];
        // Fixed seeds, and deliberately NOT RandomUtil: drawing from the simulation's stream here
        // would shift world generation depending on whether the renderer had initialised yet.
        var coarse = CreateNoiseLattice(5, 1337u);
        var medium = CreateNoiseLattice(11, 7919u);
        var fine = CreateNoiseLattice(23, 104729u);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var u = x / (float)size;
                var v = y / (float)size;
                var value = (SampleNoiseLattice(coarse, 5, u, v) * 0.52f)
                    + (SampleNoiseLattice(medium, 11, u, v) * 0.31f)
                    + (SampleNoiseLattice(fine, 23, u, v) * 0.17f);
                value = Math.Clamp(value, 0f, 1f);
                data[(y * size) + x] = new Color(value, value, value, 1f);
            }
        }

        return data;
    }

    // xorshift rather than System.Random so the texture is identical on every platform and run -
    // a surface that differed between machines would make any visual comparison worthless.
    private static float[] CreateNoiseLattice(int extent, uint seed)
    {
        var values = new float[extent * extent];
        var state = seed;
        for (var index = 0; index < values.Length; index++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            values[index] = (state & 0xFFFFFF) / (float)0xFFFFFF;
        }

        return values;
    }

    // Bilinear with a smoothstep on the weights, wrapping at the lattice edge. The wrap is what
    // makes the result tile.
    private static float SampleNoiseLattice(float[] values, int extent, float u, float v)
    {
        var x = u * extent;
        var y = v * extent;
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var fx = x - x0;
        var fy = y - y0;
        fx = fx * fx * (3f - (2f * fx));
        fy = fy * fy * (3f - (2f * fy));
        var x1 = ((x0 + 1) % extent + extent) % extent;
        var y1 = ((y0 + 1) % extent + extent) % extent;
        x0 = ((x0 % extent) + extent) % extent;
        y0 = ((y0 % extent) + extent) % extent;
        return MathHelper.Lerp(
            MathHelper.Lerp(values[(y0 * extent) + x0], values[(y0 * extent) + x1], fx),
            MathHelper.Lerp(values[(y1 * extent) + x0], values[(y1 * extent) + x1], fx),
            fy);
    }

    private Texture2D CreateWaterNoiseTexture(int size)
    {
        var texture = new Texture2D(_graphicsDevice, size, size, false, SurfaceFormat.Color);
        texture.SetData(CreateWaterNoiseData(size));
        return texture;
    }

    private Texture2D CreateOreLightTexture(int size)
    {
        var texture = new Texture2D(_graphicsDevice, size, size, false, SurfaceFormat.Color);
        var data = new Color[size * size];
        var center = (size - 1) / 2f;
        var radius = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x - center) / radius;
                var dy = (y - center) / radius;
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                var falloff = Math.Clamp(1f - distance, 0f, 1f);
                falloff *= falloff;
                data[(y * size) + x] = new Color(falloff, falloff, falloff, falloff);
            }
        }

        texture.SetData(data);
        return texture;
    }
}
