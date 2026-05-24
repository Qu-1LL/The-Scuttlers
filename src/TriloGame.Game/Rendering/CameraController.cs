using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Rendering;

public sealed class CameraController
{
    private const float ShakeSeedX = 17.137f;
    private const float ShakeSeedY = 5.713f;
    private float _shakeTrauma;
    private float _shakeNoiseTime;
    private Vector2 _shakeOffset;

    public float CurrentScale { get; set; } = GameConstants.DefaultCameraScale;

    public Vector2 CameraOrigin { get; private set; }

    public Vector2 ViewCenter { get; private set; }

    public Vector2 ShakeOffset => _shakeOffset;

    public float ShakeTrauma => _shakeTrauma;

    public void SetViewport(int width, int height)
    {
        ViewCenter = new Vector2(width / 2f, height / 2f);
    }

    public void SetOrigin(Vector2 origin)
    {
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
        CameraOrigin -= new Vector2(dx, dy) * (1f / CurrentScale);
    }

    public void Update(GameTime gameTime)
    {
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
}
