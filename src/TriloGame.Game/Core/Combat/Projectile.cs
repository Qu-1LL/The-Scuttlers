using TriloGame.Game.Core.Constants;

namespace TriloGame.Game.Core.Combat;

public sealed class Projectile
{
    public Projectile(string name, string spriteKey, int damage, float spriteScale, float travelPixelsPerTick)
    {
        Name = name;
        SpriteKey = spriteKey;
        Damage = damage;
        SpriteScale = spriteScale;
        TravelPixelsPerTick = travelPixelsPerTick;
    }

    public string Name { get; }

    public string SpriteKey { get; }

    public int Damage { get; }

    public float SpriteScale { get; }

    public float TravelPixelsPerTick { get; }
}

public static class ProjectileCatalog
{
    public static Projectile Rock { get; } = new("Rock", "Rock", 10, 0.5f, TileConstants.TileSize * 2f);
}
