using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Constants;

namespace TriloGame.Game.Rendering;

internal readonly record struct HealthBarLayout(Vector2 Position, Vector2 Size, float Border, float Fraction)
{
    internal const float Width = TileConstants.TileSize * 0.75f;
    internal const float Height = TileConstants.TileSize / 16f;
    internal const float Gap = TileConstants.TileSize / 16f;
    private const float BorderWidth = TileConstants.TileSize / 64f;

    public Vector2 FillPosition => Position + new Vector2(Border);

    public Vector2 TrackSize => Size - new Vector2(Border * 2f);

    public Vector2 FillSize => new(TrackSize.X * Fraction, TrackSize.Y);

    internal static bool IsDamaged(IHealth entity) =>
        entity.MaxHealth > 0 && entity.Health > 0 && entity.Health < entity.MaxHealth;

    // Read live health every frame and scale the entire bar with the tile, including its gap and border.
    internal static bool TryCreate(IHealth entity, Vector2 worldTopCenter, CameraController camera, out HealthBarLayout layout)
    {
        layout = default;
        if (!IsDamaged(entity))
        {
            return false;
        }

        var worldPosition = worldTopCenter - new Vector2(Width / 2f, Gap + Height);
        layout = new HealthBarLayout(
            camera.WorldToScreen(worldPosition),
            new Vector2(Width, Height) * camera.CurrentScale,
            BorderWidth * camera.CurrentScale,
            entity.Health / (float)entity.MaxHealth);
        return true;
    }

    // Cull the bar itself so an entity just below the viewport can still show its health above it.
    internal bool IntersectsViewport(Point viewportSize) =>
        Position.X < viewportSize.X && Position.Y < viewportSize.Y &&
        Position.X + Size.X > 0f && Position.Y + Size.Y > 0f;
}
