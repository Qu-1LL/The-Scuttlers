using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TriloGame.Game.Rendering.Particles;

public sealed class ParticleSystem
{
    private readonly Particle[] _particles;
    private int _activeCount;

    public ParticleSystem(int maxParticles = 2048)
    {
        if (maxParticles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxParticles));
        }

        _particles = new Particle[maxParticles];
    }

    public int ActiveCount => _activeCount;

    public int MaxParticles => _particles.Length;

    public bool HasActiveParticles => _activeCount > 0;

    public bool TryAdd(in Particle particle)
    {
        if (_activeCount >= _particles.Length)
        {
            return false;
        }

        _particles[_activeCount] = particle;
        _activeCount++;
        return true;
    }

    public void Clear()
    {
        _activeCount = 0;
    }

    public void Update(GameTime gameTime)
    {
        Update((float)gameTime.ElapsedGameTime.TotalSeconds);
    }

    public void Update(float elapsedSeconds)
    {
        if (_activeCount == 0 || elapsedSeconds <= 0f)
        {
            return;
        }

        var clampedElapsed = MathF.Min(elapsedSeconds, 0.25f);
        var index = 0;
        while (index < _activeCount)
        {
            ref var particle = ref _particles[index];
            particle.Update(clampedElapsed);
            if (particle.IsAlive)
            {
                index++;
                continue;
            }

            _activeCount--;
            _particles[index] = _particles[_activeCount];
        }
    }

    public bool HasParticlesWithBlendMode(ParticleBlendMode blendMode)
    {
        for (var index = 0; index < _activeCount; index++)
        {
            if (_particles[index].BlendMode == blendMode)
            {
                return true;
            }
        }

        return false;
    }

    public void Draw(RenderingContext context, ParticleBlendMode blendMode = ParticleBlendMode.Alpha)
    {
        Draw(context.SpriteBatch, context.Camera, blendMode);
    }

    public void Draw(SpriteBatch spriteBatch, CameraController camera, ParticleBlendMode blendMode = ParticleBlendMode.Alpha)
    {
        for (var index = 0; index < _activeCount; index++)
        {
            ref readonly var particle = ref _particles[index];
            if (particle.BlendMode != blendMode || particle.Texture is null)
            {
                continue;
            }

            var color = particle.GetDrawColor();
            if (color.A == 0)
            {
                continue;
            }

            var scale = particle.GetDrawScale();
            if (scale <= 0f)
            {
                continue;
            }

            spriteBatch.Draw(
                particle.Texture,
                camera.WorldToScreen(particle.Position),
                particle.SourceRectangle,
                color,
                particle.Rotation,
                particle.Origin,
                scale * camera.CurrentScale,
                SpriteEffects.None,
                0f);
        }
    }
}
