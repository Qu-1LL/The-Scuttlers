using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.World;

internal static class GeneratedTileSpriteRotation
{
    private const int QuarterTurnCount = 4;
    private const int OreSalt = 0x2F6E2B1;

    public static void AssignOreRotation(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        tile.SetOreRotationQuarterTurns(ChooseQuarterTurns(tile.Coordinates, tile.Base, OreSalt));
    }

    public static byte NormalizeQuarterTurns(int quarterTurns)
    {
        var normalized = quarterTurns % QuarterTurnCount;
        if (normalized < 0)
        {
            normalized += QuarterTurnCount;
        }

        return (byte)normalized;
    }

    private static byte ChooseQuarterTurns(GridPoint coordinates, string identity, int salt)
    {
        unchecked
        {
            var hash = salt;
            hash = (hash * 397) ^ coordinates.X;
            hash = (hash * 397) ^ coordinates.Y;

            for (var index = 0; index < identity.Length; index++)
            {
                hash = (hash * 397) ^ identity[index];
            }

            return (byte)(hash & (QuarterTurnCount - 1));
        }
    }
}
