using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Rendering;
using TriloGame.Game.Rendering.Particles;
using TriloGame.Game.Runtime.Automation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.State;

namespace TriloGame.Game;

public sealed partial class GameApp
{
    private readonly ParticleSystem _worldParticleSystem = new(maxParticles: 4096);
    private readonly ParticleSpraySettings _defaultAdjacentTileSpraySettings = new()
    {
        ParticlesPerTile = 7,
        MinLifetimeSeconds = 1.05f,
        MaxLifetimeSeconds = 1.8f,
        MinSpeed = 6f,
        MaxSpeed = 18f,
        DriftAmount = 9f,
        Drag = 4.6f,
        SpawnJitterPixels = 8f,
        StartScale = 0.55f,
        EndScale = 1.05f,
        StartColor = new Color(214, 245, 222, 214),
        EndColor = new Color(150, 178, 160, 0),
        FadeOutFraction = 0.42f,
        DirectionalSpreadRadians = MathHelper.ToRadians(20f),
        MinRotationSpeed = -0.3f,
        MaxRotationSpeed = 0.3f,
        BlendMode = ParticleBlendMode.Alpha
    };
    private readonly ParticleSpraySettings _deathMistSettings = new()
    {
        ParticlesPerTile = 5,
        MinLifetimeSeconds = 1.4f,
        MaxLifetimeSeconds = 2.2f,
        MinSpeed = 1f,
        MaxSpeed = 5f,
        DriftAmount = 3f,
        Drag = 3.2f,
        SpawnJitterPixels = 18f,
        StartScale = 0.7f,
        EndScale = 1.25f,
        StartColor = new Color(232, 255, 242, 210),
        EndColor = new Color(180, 214, 192, 0),
        FadeOutFraction = 0.5f,
        DirectionalSpreadRadians = MathHelper.ToRadians(180f),
        MinRotationSpeed = -0.18f,
        MaxRotationSpeed = 0.18f,
        BlendMode = ParticleBlendMode.Alpha
    };

    private ParticleEmitter? _worldParticleEmitter;
    private Texture2D? _defaultParticleTexture;
    private Texture2D[] _deathMistTextures = [];
    private readonly HashSet<int> _miningParticleHurtboxIds = [];
    private readonly List<int> _miningParticleHurtboxIdsToRemove = [];

    private ParticleEmitter WorldParticleEmitter => _worldParticleEmitter ??= new ParticleEmitter(_worldParticleSystem);

    private void InitializeWorldParticles()
    {
        _defaultParticleTexture = CreateDefaultParticleTexture(GraphicsDevice);
        _deathMistTextures =
        [
            Content.Load<Texture2D>("Textures/HealingMist1"),
            Content.Load<Texture2D>("Textures/HealingMist2"),
            Content.Load<Texture2D>("Textures/HealingMist3")
        ];
    }

    private void UpdateWorldParticles(GameTime gameTime)
    {
        _worldParticleSystem.Update(gameTime, _session.Cave);
    }

    private void DrawWorldParticles()
    {
        _worldParticleSystem.Draw(_rendering, ParticleBlendMode.Alpha);
    }

    private void ClearWorldParticles()
    {
        _worldParticleSystem.Clear();
        _miningParticleHurtboxIds.Clear();
        _miningParticleHurtboxIdsToRemove.Clear();
    }

    private void EmitMiningHitboxParticles(GameSession session)
    {
        var active = session.Mining.Active;
        for (var index = 0; index < active.Count; index++)
        {
            var hurtbox = active[index];
            if (hurtbox.TileKey is null ||
                !_miningParticleHurtboxIds.Add(hurtbox.Id))
            {
                continue;
            }

            EmitMiningHitboxParticles(session, hurtbox);
        }

        _miningParticleHurtboxIdsToRemove.Clear();
        foreach (var id in _miningParticleHurtboxIds)
        {
            if (!HasActiveMiningHurtbox(active, id))
            {
                _miningParticleHurtboxIdsToRemove.Add(id);
            }
        }

        for (var index = 0; index < _miningParticleHurtboxIdsToRemove.Count; index++)
        {
            _miningParticleHurtboxIds.Remove(_miningParticleHurtboxIdsToRemove[index]);
        }
    }

    private void EmitMiningHitboxParticles(GameSession session, MiningStrike hurtbox)
    {
        if (session.Cave?.GetTile(hurtbox.TileKey) is not { } tile ||
            !TryGetMiningParticleTexture(tile, out var texture))
        {
            return;
        }

        var impact = ToParticleWorldPosition(hurtbox.Center);
        var source = ToParticleWorldPosition(hurtbox.Source.Position);
        WorldParticleEmitter.EmitMiningParticles(
            new MiningParticleEmissionRequest
            {
                Texture = texture,
                TextureSourceBounds = new Rectangle(0, 0, texture.Width, texture.Height),
                WorldBounds = GetMiningHitParticleBounds(tile, source, impact),
                ImpactPosition = impact,
                Mode = MiningParticleEmissionMode.Hit,
                Tint = Color.White
            });
    }

    private bool TryGetMiningParticleTexture(Tile tile, out Texture2D texture)
    {
        var textureKey = WorldSceneRenderer.GetTileOverlayTextureKey(tile);
        if (textureKey is null && string.Equals(tile.Base, "wall", StringComparison.Ordinal))
        {
            textureKey = "wall";
        }

        if (textureKey is not null && _rendering.Sprites.TryGet(textureKey, out texture!))
        {
            return true;
        }

        texture = null!;
        return false;
    }

    private static ParticleWorldBounds GetMiningHitParticleBounds(Tile tile, Vector2 source, Vector2 impact)
    {
        var spawnCenter = tile.IsOreTile()
            ? impact
            : Vector2.Lerp(source, impact, 0.5f);

        if (!tile.IsOreTile())
        {
            var awayFromImpact = source - impact;
            if (awayFromImpact.LengthSquared() > 0.0001f)
            {
                awayFromImpact.Normalize();
                spawnCenter += awayFromImpact;
            }
        }

        return ParticleWorldBounds.Centered(spawnCenter, 24f, 24f);
    }

    private static Vector2 ToParticleWorldPosition(WorldPoint point)
    {
        return new Vector2(
            point.X / (float)WorldUnits.UnitsPerPixel,
            point.Y / (float)WorldUnits.UnitsPerPixel);
    }

    private static bool HasActiveMiningHurtbox(IReadOnlyList<MiningStrike> active, int id)
    {
        for (var index = 0; index < active.Count; index++)
        {
            var hurtbox = active[index];
            if (hurtbox.Id == id)
            {
                return true;
            }
        }

        return false;
    }

    private int EmitAdjacentTileSpray(GridPoint sourceTile)
    {
        if (_defaultParticleTexture is null)
        {
            return 0;
        }

        return WorldParticleEmitter.EmitAroundAdjacentTiles(
            new Point(sourceTile.X, sourceTile.Y),
            TileConstants.TileSize,
            _defaultParticleTexture,
            _defaultAdjacentTileSpraySettings);
    }

    private void EmitDeathMist(DeathMistRequest request)
    {
        if (_deathMistTextures.Length == 0)
        {
            return;
        }

        for (var dx = -request.Radius; dx <= request.Radius; dx++)
        {
            var maxDy = request.Radius - Math.Abs(dx);
            for (var dy = -maxDy; dy <= maxDy; dy++)
            {
                var tileCenter = new Vector2(
                    (request.OriginTile.X + dx) * TileConstants.TileSize,
                    (request.OriginTile.Y + dy) * TileConstants.TileSize);
                var texture = _deathMistTextures[Rendering.Particles.RenderingRandom.NextInt(_deathMistTextures.Length)];
                WorldParticleEmitter.EmitBurst(tileCenter, Vector2.Zero, texture, _deathMistSettings, _deathMistSettings.ParticlesPerTile);
            }
        }
    }

    private static Texture2D CreateDefaultParticleTexture(GraphicsDevice graphicsDevice)
    {
        const int size = 18;
        var texture = new Texture2D(graphicsDevice, size, size);
        var data = new Color[size * size];
        var center = (size - 1) * 0.5f;
        var radius = center;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                var normalized = MathHelper.Clamp(distance / radius, 0f, 1f);
                var alpha = 1f - normalized;
                alpha *= alpha;
                data[(y * size) + x] = new Color((byte)255, (byte)255, (byte)255, (byte)MathF.Round(alpha * 255f));
            }
        }

        texture.SetData(data);
        return texture;
    }
}
