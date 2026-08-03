using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

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

    public ReadOnlySpan<Particle> ActiveParticles => _particles.AsSpan(0, _activeCount);

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

    public void Update(GameTime gameTime, Cave? cave)
    {
        Update((float)gameTime.ElapsedGameTime.TotalSeconds, cave);
    }

    public void Update(float elapsedSeconds)
    {
        Update(elapsedSeconds, cave: null);
    }

    public void Update(float elapsedSeconds, Cave? cave)
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
            var previousPosition = particle.Position;
            particle.Update(clampedElapsed);
            if (particle.IsAlive)
            {
                if (cave is not null && particle.UseTileCollision)
                {
                    ResolveTileCollision(cave, ref particle, previousPosition);
                }

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
        Draw(context.SpriteBatch, context.Camera, blendMode, context.WhitePixel);
    }

    public void Draw(SpriteBatch spriteBatch, CameraController camera, ParticleBlendMode blendMode = ParticleBlendMode.Alpha)
    {
        Draw(spriteBatch, camera, blendMode, shadowTexture: null);
    }

    private void Draw(SpriteBatch spriteBatch, CameraController camera, ParticleBlendMode blendMode, Texture2D? shadowTexture)
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

            DrawShadow(spriteBatch, camera, shadowTexture, in particle);

            spriteBatch.Draw(
                particle.Texture,
                camera.WorldToScreen(particle.GetDrawPosition()),
                particle.SourceRectangle,
                color,
                particle.Rotation,
                particle.Origin,
                scale * camera.CurrentScale,
                SpriteEffects.None,
                particle.LayerDepth);
        }
    }

    private static void ResolveTileCollision(Cave cave, ref Particle particle, Vector2 previousPosition)
    {
        if (!IsBlockedParticleTile(cave, particle.Position))
        {
            return;
        }

        var xOnly = new Vector2(particle.Position.X, previousPosition.Y);
        var yOnly = new Vector2(previousPosition.X, particle.Position.Y);
        var xOpen = !IsBlockedParticleTile(cave, xOnly);
        var yOpen = !IsBlockedParticleTile(cave, yOnly);

        if (xOpen && (!yOpen || MathF.Abs(particle.Velocity.X) >= MathF.Abs(particle.Velocity.Y)))
        {
            particle.Position = xOnly;
            particle.Velocity = new Vector2(particle.Velocity.X * 0.72f, -particle.Velocity.Y * 0.35f);
            return;
        }

        if (yOpen)
        {
            particle.Position = yOnly;
            particle.Velocity = new Vector2(-particle.Velocity.X * 0.35f, particle.Velocity.Y * 0.72f);
            return;
        }

        particle.Position = previousPosition;
        particle.Velocity *= -0.35f;
    }

    private static bool IsBlockedParticleTile(Cave cave, Vector2 position)
    {
        var point = new GridPoint(
            (int)MathF.Floor((position.X + TileConstants.TileHalfSize) / TileConstants.TileSize),
            (int)MathF.Floor((position.Y + TileConstants.TileHalfSize) / TileConstants.TileSize));
        var tile = cave.GetTile(point);
        return tile is null || !tile.CreatureFits();
    }

    private static void DrawShadow(SpriteBatch spriteBatch, CameraController camera, Texture2D? shadowTexture, in Particle particle)
    {
        if (!particle.DrawShadow ||
            !particle.UseVisualHeight ||
            particle.Height <= 0f ||
            shadowTexture is null)
        {
            return;
        }

        var age = MathHelper.Clamp(particle.AgeSeconds / particle.LifetimeSeconds, 0f, 1f);
        var heightFade = MathHelper.Clamp(1f - (particle.Height / 36f), 0.25f, 1f);
        var alpha = (byte)MathF.Round(72f * heightFade * (1f - (age * 0.65f)));
        if (alpha == 0)
        {
            return;
        }

        var screenPosition = camera.WorldToScreen(particle.Position);
        var scale = MathF.Max(0.2f, particle.GetDrawScale()) * camera.CurrentScale;
        var width = Math.Max(1, (int)MathF.Round(7f * scale * heightFade));
        var height = Math.Max(1, (int)MathF.Round(3f * scale * heightFade));
        var destination = new Rectangle(
            (int)MathF.Round(screenPosition.X - (width * 0.5f)),
            (int)MathF.Round(screenPosition.Y - (height * 0.5f)),
            width,
            height);

        spriteBatch.Draw(shadowTexture, destination, null, new Color((byte)0, (byte)0, (byte)0, alpha), 0f, Vector2.Zero, SpriteEffects.None, particle.LayerDepth);
    }
}
