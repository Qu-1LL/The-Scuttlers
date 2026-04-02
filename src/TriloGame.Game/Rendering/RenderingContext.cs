using Microsoft.Xna.Framework.Graphics;

namespace TriloGame.Game.Rendering;

public sealed class RenderingContext
{
    public required SpriteBatch SpriteBatch { get; init; }

    public required SpriteFont UiFont { get; init; }

    public required SpriteFont SmallFont { get; init; }

    public required SpriteFont DebugFont { get; init; }

    public required Texture2D WhitePixel { get; init; }

    public required SpriteFactory Sprites { get; init; }

    public required CameraController Camera { get; init; }
}
