using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Rendering;

public sealed class CameraController
{
    private const float ShakeSeedX = 17.137f;
    private const float ShakeSeedY = 5.713f;
    // Outermost rungs of the zoom ladder. GameConstants.MinScale/MaxScale are defined as exactly
    // these rungs, so the ladder reaches the supported range without ever landing between rungs.
    private const int MaxZoomStep = GameConstants.MaxZoomSteps;
    private const int MinZoomStep = -GameConstants.MaxZoomSteps;

    private float _shakeTrauma;
    private float _shakeNoiseTime;
    private Vector2 _shakeOffset;
    private int _zoomStep;
    private float _currentScale = GameConstants.DefaultCameraScale;
    private float _targetScale = GameConstants.DefaultCameraScale;

    // The scale the view is drawn at this frame. Eases toward TargetScale so a wheel notch is a
    // short glide rather than a jump; assigning it directly (tests, debug tooling) takes effect
    // immediately, cancels any glide in progress, and re-derives the nearest ladder rung so a later
    // notch continues from where the camera actually is instead of from a stale step index.
    public float CurrentScale
    {
        get => _currentScale;
        set
        {
            _currentScale = value;
            _targetScale = value;
            _zoomStep = GetNearestZoomStep(value);
        }
    }

    // Where the eased zoom is heading. Exposed so callers can tell a settled camera from one that
    // is still gliding.
    public float TargetScale => _targetScale;

    public int ZoomStep => _zoomStep;

    public bool IsZooming => MathF.Abs(_targetScale - _currentScale) > 0.000001f;

    public Vector2 CameraOrigin { get; private set; }

    public Vector2 ParallaxScreenOffset { get; private set; }

    public Vector2 ViewCenter { get; private set; }

    public Vector2 ShakeOffset => _shakeOffset;

    public float ShakeTrauma => _shakeTrauma;

    public void SetViewport(int width, int height)
    {
        ViewCenter = new Vector2(width / 2f, height / 2f);
    }

    public void SetOrigin(Vector2 origin)
    {
        ParallaxScreenOffset += (CameraOrigin - origin) * CurrentScale;
        CameraOrigin = origin;
    }

    public void HandleViewportResize(int oldWidth, int oldHeight, int newWidth, int newHeight)
    {
        var oldCenter = new Vector2(oldWidth / 2f, oldHeight / 2f);
        var newCenter = new Vector2(newWidth / 2f, newHeight / 2f);
        CameraOrigin += (oldCenter - newCenter) * (1f / CurrentScale);
        ViewCenter = newCenter;
    }

    public void PanByScreenDelta(float dx, float dy)
    {
        var screenDelta = new Vector2(dx, dy);
        CameraOrigin -= screenDelta * (1f / CurrentScale);
        ParallaxScreenOffset += screenDelta;
    }

    // Move the zoom by whole ladder rungs. Only the target moves; Update glides the live scale to
    // it, so the view never teleports and the lighting keeps a reprojectable history.
    public void ZoomBySteps(int steps)
    {
        if (steps == 0)
        {
            return;
        }

        _zoomStep = Math.Clamp(_zoomStep + steps, MinZoomStep, MaxZoomStep);
        _targetScale = GetScaleForZoomStep(_zoomStep);
    }

    // Snap back to the default rung with no glide. Used when a new game hands the camera a fresh
    // viewpoint, where easing from the previous session's zoom would only be a distraction.
    public void ResetZoom()
    {
        _zoomStep = 0;
        _targetScale = GameConstants.DefaultCameraScale;
        _currentScale = GameConstants.DefaultCameraScale;
    }

    public static float GetScaleForZoomStep(int step)
    {
        var clampedStep = Math.Clamp(step, MinZoomStep, MaxZoomStep);
        return GameConstants.DefaultCameraScale * MathF.Pow(GameConstants.ZoomStepRatio, clampedStep);
    }

    public void Update(GameTime gameTime)
    {
        UpdateZoom(gameTime);
        var elapsedSeconds = MathF.Min(0.1f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        if (_shakeTrauma <= 0f || elapsedSeconds <= 0f)
        {
            _shakeTrauma = MathF.Max(0f, _shakeTrauma);
            _shakeOffset = Vector2.Zero;
            return;
        }

        _shakeNoiseTime += elapsedSeconds * GameConstants.ExplosionShakeFrequencyHz;
        var amplitude = GameConstants.ExplosionShakeMaxPixels * _shakeTrauma * _shakeTrauma;
        var offset = new Vector2(
            PerlinNoise.Sample(_shakeNoiseTime + ShakeSeedX, ShakeSeedY),
            PerlinNoise.Sample(ShakeSeedX, _shakeNoiseTime + ShakeSeedY)) * amplitude;
        if (offset.LengthSquared() > amplitude * amplitude && amplitude > 0f)
        {
            offset.Normalize();
            offset *= amplitude;
        }

        _shakeOffset = offset;
        _shakeTrauma = MathF.Max(0f, _shakeTrauma - (GameConstants.ExplosionShakeDecayPerSecond * elapsedSeconds));
        if (_shakeTrauma <= 0f)
        {
            _shakeOffset = Vector2.Zero;
        }
    }

    // Eased in LOG space: zoom is multiplicative, so a constant fraction of the remaining ratio per
    // second is what reads as an even glide. Interpolating the scale linearly instead makes zooming
    // out crawl and zooming in lurch, because the same scale delta covers a different ratio at each
    // end of the ladder.
    private void UpdateZoom(GameTime gameTime)
    {
        if (!IsZooming)
        {
            _currentScale = _targetScale;
            return;
        }

        var elapsedSeconds = MathF.Min(0.1f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        if (elapsedSeconds <= 0f)
        {
            return;
        }

        var approach = 1f - MathF.Exp(-GameConstants.ZoomApproachRatePerSecond * elapsedSeconds);
        var remainingRatio = _targetScale / _currentScale;
        _currentScale *= MathF.Pow(remainingRatio, approach);

        // Land exactly on the rung once the remainder is sub-perceptual. Without this the scale
        // creeps toward the target forever, and a never-settling scale means the lighting can never
        // treat the camera as stationary.
        if (MathF.Abs(_targetScale - _currentScale) <= _targetScale * 0.0005f)
        {
            _currentScale = _targetScale;
        }
    }

    // Nearest rung to an arbitrary scale, so a directly assigned scale still rejoins the ladder.
    private static int GetNearestZoomStep(float scale)
    {
        var safeScale = MathF.Max(0.000001f, scale);
        var step = MathF.Log(safeScale / GameConstants.DefaultCameraScale) / MathF.Log(GameConstants.ZoomStepRatio);
        return Math.Clamp((int)MathF.Round(step), MinZoomStep, MaxZoomStep);
    }

    public void TriggerExplosionShake(float intensity = 1f)
    {
        if (intensity <= 0f)
        {
            return;
        }

        _shakeTrauma = MathHelper.Clamp(_shakeTrauma + intensity, 0f, 1f);
    }

    public void ClearShake()
    {
        _shakeTrauma = 0f;
        _shakeNoiseTime = 0f;
        _shakeOffset = Vector2.Zero;
    }

    public Vector2 WorldToScreen(Vector2 world)
    {
        return ViewCenter + _shakeOffset + ((world - CameraOrigin) * CurrentScale);
    }

    public Vector2 ScreenToWorld(Point screen)
    {
        return CameraOrigin + ((screen.ToVector2() - ViewCenter - _shakeOffset) * (1f / CurrentScale));
    }

    // Expose the current world-space view corners so renderers can cull by the actual camera footprint.
    public void GetVisibleWorldBounds(Point viewportSize, out Vector2 topLeft, out Vector2 bottomRight)
    {
        topLeft = ScreenToWorld(Point.Zero);
        bottomRight = ScreenToWorld(viewportSize);
    }
}
