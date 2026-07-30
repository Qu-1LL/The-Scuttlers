using Microsoft.Xna.Framework;

namespace TriloGame.Game.Rendering;

public readonly record struct AlphaPulseEffect(float MinAlpha, float MaxAlpha, float CycleSeconds, float PhaseOffsetSeconds = 0f);

// A looping flip-book of texture keys. Registered under an animation name (e.g. "Water") and
// resolved to whichever frame is current, so callers draw a normal sprite and never track time.
public sealed record TileAnimationEffect(IReadOnlyList<string> FrameTextureKeys, float FrameSeconds);

public sealed class WorldSpriteEffectSystem
{
    private readonly Dictionary<string, AlphaPulseEffect> _alphaPulseEffects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TileAnimationEffect> _tileAnimations = new(StringComparer.Ordinal);
    private float _elapsedSeconds;

    public void RegisterAlphaPulse(string textureKey, AlphaPulseEffect effect)
    {
        if (string.IsNullOrWhiteSpace(textureKey))
        {
            return;
        }

        _alphaPulseEffects[textureKey] = effect;
    }

    public void RegisterTileAnimation(string animationKey, TileAnimationEffect effect)
    {
        if (string.IsNullOrWhiteSpace(animationKey) || effect.FrameTextureKeys.Count == 0)
        {
            return;
        }

        _tileAnimations[animationKey] = effect;
    }

    public bool HasTileAnimation(string animationKey)
    {
        return _tileAnimations.ContainsKey(animationKey);
    }

    // Current frame for an animation. phaseOffsetSeconds lets each tile start at a different point
    // in the loop so a large water body ripples unevenly instead of flipping in lockstep. Returns
    // the animation key itself when nothing is registered, so an unregistered animation degrades to
    // "just draw a texture with this name" rather than throwing or drawing nothing.
    public string GetAnimatedTextureKey(string animationKey, float phaseOffsetSeconds = 0f)
    {
        if (!_tileAnimations.TryGetValue(animationKey, out var effect))
        {
            return animationKey;
        }

        var frameSeconds = MathF.Max(0.01f, effect.FrameSeconds);
        var frameCount = effect.FrameTextureKeys.Count;
        var totalSeconds = _elapsedSeconds + phaseOffsetSeconds;
        // Floor toward negative infinity so a negative phase offset still lands on a valid frame.
        var rawFrame = (int)MathF.Floor(totalSeconds / frameSeconds);
        var frameIndex = ((rawFrame % frameCount) + frameCount) % frameCount;
        return effect.FrameTextureKeys[frameIndex];
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
        var color = baseColor.ToVector4();
        color.W *= GetAlphaMultiplier(textureKey, phaseOffsetSeconds);
        return new Color(color);
    }

    public float GetAlphaMultiplier(string textureKey, float phaseOffsetSeconds = 0f)
    {
        if (!_alphaPulseEffects.TryGetValue(textureKey, out var effect))
        {
            return 1f;
        }

        return SampleAlphaMultiplier(effect, phaseOffsetSeconds);
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
