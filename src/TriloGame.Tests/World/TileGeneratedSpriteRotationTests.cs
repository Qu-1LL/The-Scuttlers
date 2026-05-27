using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.World;

namespace TriloGame.Tests.World;

public sealed class TileGeneratedSpriteRotationTests
{
    [Fact]
    public void SetOreRotationQuarterTurns_NormalizesIntoQuarterTurnRange()
    {
        var tile = new Tile(0, "0,0");

        tile.SetOreRotationQuarterTurns(-1);
        Assert.Equal(3, tile.OreRotationQuarterTurns);

        tile.SetOreRotationQuarterTurns(6);
        Assert.Equal(2, tile.OreRotationQuarterTurns);
    }

    [Fact]
    public void ClearingOreState_AlsoClearsStoredOreRotation()
    {
        var tile = new Tile(0, "0,0");
        tile.SetBase(OreType.LUMENITE.Name);
        tile.ConfigureOre(2, 1);
        tile.SetOreRotationQuarterTurns(2);

        tile.ClearResourceState();
        Assert.Equal(0, tile.OreRotationQuarterTurns);

        tile.SetOreRotationQuarterTurns(1);
        tile.SetBase("empty");
        Assert.Equal(0, tile.OreRotationQuarterTurns);
    }
}
