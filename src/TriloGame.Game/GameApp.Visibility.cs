using TriloGame.Game.Core.World;

namespace TriloGame.Game;

public sealed partial class GameApp
{
    // Full-map visibility stays render-only so the real reveal set keeps updating underneath.
    private IEnumerable<Tile> GetMapVisibleTiles(Cave cave)
    {
        return _showFullMapVisibility ? cave.GetTiles() : cave.GetRevealedTiles();
    }

    private bool IsMapTileVisible(Cave cave, Tile? tile)
    {
        return tile is not null && (_showFullMapVisibility || cave.IsTileRevealed(tile));
    }
}
