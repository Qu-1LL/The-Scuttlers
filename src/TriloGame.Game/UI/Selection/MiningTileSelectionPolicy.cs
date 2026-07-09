using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.World;

namespace TriloGame.Game.UI.Selection;

public static class MiningTileSelectionPolicy
{
    public static bool CanSelect(Cave? cave, Tile? tile)
    {
        return cave is not null &&
               tile is not null &&
               cave.IsTileRevealed(tile) &&
               Building.IsMineableType(tile.Base);
    }
}
