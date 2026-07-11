using Microsoft.Xna.Framework;

namespace TriloGame.Game.Rendering;

public readonly record struct AlphaPulseEffect(float MinAlpha, float MaxAlpha, float CycleSeconds, float PhaseOffsetSeconds = 0f);

public sealed class WorldSpriteEffectSystem
{
    private readonly Dictionary<string, AlphaPulseEffect> _alphaPulseEffects = new(StringComparer.Ordinal);
    private float _elapsedSeconds;

    public void RegisterAlphaPulse(string textureKey, AlphaPulseEffect effect)
    {
        if (string.IsNullOrWhiteSpace(textureKey))
        {
            return;
        }

        _alphaPulseEffects[textureKey] = effect;
    }

    public void Update(GameTime gameTime)
    {
        var elapsedSeconds = MathF.Min(0.1f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        if (elapsedSeconds <= 0f)
        {
            return;
        }

        _elapsedSeconds += elapsedSeconds;
        if (_elapsedSeconds >= 3600f)
        {
            _elapsedSeconds %= 3600f;
        }
    }

    public Color ApplyColor(string textureKey, Color baseColor, float phaseOffsetSeconds = 0f)
    {
        if (!_alphaPulseEffects.TryGetValue(textureKey, out var effect))
        {
            return baseColor;
        }

        var color = baseColor.ToVector4();
        color.W *= SampleAlphaMultiplier(effect, phaseOffsetSeconds);
        return new Color(color);
    }

    private float SampleAlphaMultiplier(AlphaPulseEffect effect, float phaseOffsetSeconds)
    {
        var minAlpha = Math.Clamp(effect.MinAlpha, 0f, 1f);
        var maxAlpha = Math.Clamp(effect.MaxAlpha, minAlpha, 1f);
        var cycleSeconds = Math.Max(0.01f, effect.CycleSeconds);
        var phase = ((_elapsedSeconds + effect.PhaseOffsetSeconds + phaseOffsetSeconds) / cycleSeconds) * MathF.Tau;
        var normalized = (MathF.Sin(phase) * 0.5f) + 0.5f;
        return MathHelper.Lerp(minAlpha, maxAlpha, normalized);
    }
}
