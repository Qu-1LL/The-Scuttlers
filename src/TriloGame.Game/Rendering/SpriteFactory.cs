using Microsoft.Xna.Framework.Graphics;

namespace TriloGame.Game.Rendering;

public sealed class SpriteFactory
{
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);

    public void Register(string key, Texture2D texture)
    {
        _textures[key] = texture;
    }

    public Texture2D this[string key] => _textures[key];

    public Texture2D Get(string key) => _textures[key];

    public bool TryGet(string key, out Texture2D texture)
    {
        return _textures.TryGetValue(key, out texture!);
    }
}
