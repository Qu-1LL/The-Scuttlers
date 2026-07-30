using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;

namespace TriloGame.Game.Rendering.Lighting;

public sealed class RadianceCascadeRenderer : IDisposable
{
    private const float LightBufferScale = 0.5f;
    // Weight given to the newest frame when accumulating. Low enough to suppress lattice
    // shimmer, high enough that moving lights and creatures do not visibly trail.
    private const float HistoryBlendWeight = 0.35f;

    private Vector2 _previousCameraOrigin;
    private float _previousCameraScale;
    private bool _hasLightingHistory;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly Effect _effect;
    private readonly LightingRenderTargets _targets;
    private readonly LightingTileGrid _tileGrid;
    private readonly LightingSourceCollector _sourceCollector = new();
    private readonly List<OreLightEmitter> _oreEmitters = [];
    private readonly List<Tile> _visibleTiles = [];
    private readonly List<Tile> _occludingTiles = [];
    private readonly OreLightColorPalette _colorPalette = new();
    private readonly Texture2D _oreLightTexture;

    public RadianceCascadeRenderer(GraphicsDevice graphicsDevice, Effect effect)
    {
        _graphicsDevice = graphicsDevice;
        _effect = effect;
        _targets = new LightingRenderTargets(graphicsDevice);
        _tileGrid = new LightingTileGrid(graphicsDevice);
        _oreLightTexture = CreateOreLightTexture(64);
    }

    public LightingDebugMode DebugMode { get; private set; }

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

        if (_targets.EnsureSize(viewportSize))
        {
            // Freshly allocated targets contain no usable history.
            _hasLightingHistory = false;
        }

        var scene = _targets.Scene!;
        var entityOccluder = _targets.EntityOccluder!;
        var emissive = _targets.Emissive!;

        WorldSceneRenderer.CollectVisibleTiles(
            cave,
            context.Camera,
            viewportSize,
            showFullMapVisibility,
            _visibleTiles);
        _sourceCollector.CollectOreEmitters(_visibleTiles, spriteEffects, _oreEmitters, _colorPalette);
        _tileGrid.Update(
            cave,
            context.Camera,
            viewportSize,
            showFullMapVisibility,
            _oreEmitters);

        // Solid casters are rasterised into the occluder mask so shadows follow their sprites.
        _occludingTiles.Clear();
        for (var index = 0; index < _visibleTiles.Count; index++)
        {
            if (LightingTileClassifier.BlocksLight(_visibleTiles[index]))
            {
                _occludingTiles.Add(_visibleTiles[index]);
            }
        }

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
            _visibleTiles);
        context.SpriteBatch.End();

        if (DebugMode == LightingDebugMode.SceneOnly)
        {
            Composite(context, viewportSize, scene, entityOccluder, emissive);
            return;
        }

        DrawEntityOccluderMask(context, worldRenderer, cave, entityOccluder, interpolationAlpha);
        DrawTallOccluderMask(context, worldRenderer, _targets.TallOccluder!);
        DrawWaterMask(context, worldRenderer, _targets.WaterMask!);
        DrawEmissiveMask(context, worldRenderer, emissive);
        RunCascades(context, viewportSize);
        ReduceLightingField(context.SpriteBatch);
        AccumulateLightingField(context, viewportSize);
        Composite(context, viewportSize, scene, entityOccluder, emissive);
    }

    public void Dispose()
    {
        _targets.Dispose();
        _tileGrid.Dispose();
        _oreLightTexture.Dispose();
        _effect.Dispose();
        GC.SuppressFinalize(this);
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
            transformMatrix: Matrix.CreateScale(LightBufferScale));
        worldRenderer.DrawEntityOccluderLayer(context, cave, interpolationAlpha);
        context.SpriteBatch.End();
    }

    private void DrawTallOccluderMask(
        RenderingContext context,
        WorldSceneRenderer worldRenderer,
        RenderTarget2D target)
    {
        _graphicsDevice.SetRenderTarget(target);
        _graphicsDevice.Clear(Color.Transparent);
        context.SpriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            blendState: BlendState.AlphaBlend,
            transformMatrix: Matrix.CreateScale(LightBufferScale));
        worldRenderer.DrawTallOccluderLayer(context, _occludingTiles);
        context.SpriteBatch.End();
    }

    private void DrawWaterMask(
        RenderingContext context,
        WorldSceneRenderer worldRenderer,
        RenderTarget2D target)
    {
        _graphicsDevice.SetRenderTarget(target);
        _graphicsDevice.Clear(Color.Transparent);
        context.SpriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            blendState: BlendState.AlphaBlend,
            transformMatrix: Matrix.CreateScale(LightBufferScale));
        worldRenderer.DrawWaterMaskLayer(context, _visibleTiles);
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
        worldRenderer.DrawEmissiveLayer(context, _oreEmitters, _oreLightTexture);
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
            SetParameter("EntityOccluderTexture", entityOccluder);
            SetParameter("TallOccluderTexture", _targets.TallOccluder!);
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
            SetParameter("HigherProbeSpacing", (float)layout.GetProbeSpacing(index + 1));
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

    // Blend this frame's field with the previous one, reprojected by the camera's motion, to
    // average out the aliasing that the screen-space probe lattice produces while panning.
    private void AccumulateLightingField(RenderingContext context, Point viewportSize)
    {
        var field = _targets.LightingField!;
        var accumulated = _targets.LightingAccumulated!;
        var history = _targets.LightingHistory!;
        var camera = context.Camera;
        var uvScale = GetLightingFieldUvScale();

        // Where a world point that is now at screen p was on screen last frame:
        //   p_prev = p + (origin_now - origin_prev) * scale
        // Converted into field UV, which spans PackedSize source pixels (screen / 2 per pixel).
        var screenDelta = (camera.CameraOrigin - _previousCameraOrigin) * camera.CurrentScale;
        var historyUvOffset = new Vector2(
            screenDelta.X / viewportSize.X * uvScale.X,
            screenDelta.Y / viewportSize.Y * uvScale.Y);

        // A zoom change rescales the lattice, so last frame's field cannot be reprojected by a
        // simple offset - take the new frame whole rather than smear a mismatched history.
        var zoomChanged = MathF.Abs(camera.CurrentScale - _previousCameraScale) > 0.000001f;
        var blend = !_hasLightingHistory || zoomChanged ? 1f : HistoryBlendWeight;

        _graphicsDevice.SetRenderTarget(accumulated);
        _graphicsDevice.Clear(Color.Transparent);
        _effect.CurrentTechnique = _effect.Techniques["LightingAccumulate"];
        SetParameter("Texture", field);
        SetParameter("PreviousCascadeTexture", history);
        SetParameter("HistoryUvOffset", historyUvOffset);
        SetParameter("HistoryBlend", blend);
        DrawFullscreen(context.SpriteBatch, field, accumulated.Width, accumulated.Height, BlendState.Opaque, _effect);

        _targets.SwapAccumulatedWithHistory();
        _previousCameraOrigin = camera.CameraOrigin;
        _previousCameraScale = camera.CurrentScale;
        _hasLightingHistory = true;
    }

    private void Composite(
        RenderingContext context,
        Point viewportSize,
        RenderTarget2D scene,
        RenderTarget2D entityOccluder,
        RenderTarget2D emissive)
    {
        _graphicsDevice.SetRenderTarget(null);
        if (DebugMode == LightingDebugMode.SceneOnly)
        {
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
            DrawAmbientScene(context.SpriteBatch, scene, viewportSize);
            return;
        }

        // Keep the captured scene as the base layer, then add only the lighting contribution.
        // This preserves the parallax background wherever the scene target is transparent.
        DrawAmbientScene(context.SpriteBatch, scene, viewportSize);
        _effect.CurrentTechnique = _effect.Techniques["LightingAdd"];
        SetParameter("Texture", scene);
        SetParameter("LightingTexture", GetLitField());
        SetParameter("EmissiveTexture", emissive);
        SetParameter("LightingEnabled", 1f);
        SetParameter("LightingFieldUvScale", GetLightingFieldUvScale());
        SetParameter("WaterMaskTexture", _targets.WaterMask!);
        SetParameter("WaterSheenStrength", OreLightSettings.WaterSheenStrength);
        DrawFullscreen(context.SpriteBatch, scene, viewportSize.X, viewportSize.Y, BlendState.Additive, _effect);
    }

    // AccumulateLightingField swaps its result into LightingHistory, so that is the field the
    // composite must read. Falls back to the raw reduced field if accumulation has not run
    // (SceneOnly and other debug paths return before it).
    private Texture2D GetLitField()
    {
        return _hasLightingHistory
            ? _targets.LightingHistory!
            : _targets.LightingField!;
    }

    private Vector2 GetLightingFieldUvScale()
    {
        return _targets.Layout.GetLightingFieldUvScale(
            new Point(_targets.LightWidth, _targets.LightHeight));
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

    private void SetCascadeParameters(
        RenderingContext context,
        Point viewportSize,
        LightingCascadeLayout layout,
        int cascadeIndex)
    {
        SetParameter("SourceResolution", new Vector2(_targets.LightWidth, _targets.LightHeight));
        SetParameter("CascadeResolution", new Vector2(_targets.Cascades[cascadeIndex].Width, _targets.Cascades[cascadeIndex].Height));
        SetParameter("RayDimension", (float)layout.GetRayDimension(cascadeIndex));
        SetParameter("ProbeSpacing", (float)layout.GetProbeSpacing(cascadeIndex));
        SetParameter("IntervalOrigin", layout.GetIntervalOrigin(cascadeIndex));
        SetParameter("IntervalLength", layout.GetIntervalLength(cascadeIndex));
        SetParameter("RaySteps", OreLightSettings.CascadeRaySteps);
        SetParameter("LightRangeTiles", OreLightSettings.OreLightRangeTiles);
        SetParameter("ShortShadowTiles", OreLightSettings.ShortShadowTiles);
        SetParameter("WallTransmission", OreLightSettings.WallTransmission);
        SetParameter("OreLightColor", OreLightSettings.SharedOreLightColor.ToVector3());
        SetTileGridParameters(context, viewportSize);
    }

    // CascadeMergePixel only reads these five parameters; the rest of SetCascadeParameters
    // targets globals unused by the CascadeMerge technique, which crashes EffectParameter.SetValue
    // on the OpenGL backend when the compiled shader has no storage for them.
    private void SetMergeParameters(LightingCascadeLayout layout, int cascadeIndex)
    {
        SetParameter("CascadeResolution", new Vector2(_targets.Cascades[cascadeIndex].Width, _targets.Cascades[cascadeIndex].Height));
        SetParameter("RayDimension", (float)layout.GetRayDimension(cascadeIndex));
        SetParameter("ProbeSpacing", (float)layout.GetProbeSpacing(cascadeIndex));
        SetParameter("IntervalOrigin", layout.GetIntervalOrigin(cascadeIndex));
        SetParameter("IntervalLength", layout.GetIntervalLength(cascadeIndex));
    }

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
