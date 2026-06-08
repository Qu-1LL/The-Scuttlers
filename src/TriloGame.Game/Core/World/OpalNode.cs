using TriloGame.Game.Core.Constants;

namespace TriloGame.Game.Core.World;

public sealed class OpalNode
{
    private int _initialGraceTicksRemaining = GameConstants.OpalInitialGraceTicks;

    public OpalNode(string tileKey)
    {
        TileKey = tileKey;
        RemainingYield = GameConstants.OpalYield;
    }

    public string TileKey { get; }

    public int RemainingYield { get; private set; }

    public int TicksSinceLastMine { get; private set; }

    public bool IsDepleted => RemainingYield <= 0;

    public bool BlocksNaturalAntHoleSpawns()
    {
        return _initialGraceTicksRemaining > 0;
    }

    public void Tick()
    {
        if (IsDepleted)
        {
            return;
        }

        if (_initialGraceTicksRemaining > 0)
        {
            _initialGraceTicksRemaining--;
            return;
        }

        TicksSinceLastMine++;
    }

    public bool ApplyMineHit()
    {
        if (IsDepleted)
        {
            return false;
        }

        RemainingYield = Math.Max(0, RemainingYield - 1);
        TicksSinceLastMine = 0;
        return true;
    }

    public float GetWarningProgress()
    {
        if (_initialGraceTicksRemaining > 0)
        {
            return 0f;
        }

        if (TicksSinceLastMine < GameConstants.OpalDormantTicks)
        {
            return 0f;
        }

        var elapsed = TicksSinceLastMine - GameConstants.OpalDormantTicks;
        return Math.Clamp(elapsed / (float)GameConstants.OpalWarningTicks, 0f, 1f);
    }

    public float GetRedness()
    {
        return GetWarningProgress() * GameConstants.OpalMaxRedness;
    }

    public int GetAntHoleSpawnChanceDenominator()
    {
        if (BlocksNaturalAntHoleSpawns())
        {
            return GameConstants.AntHoleBaseSpawnChanceDenominator;
        }

        if (TicksSinceLastMine < GameConstants.OpalDormantTicks)
        {
            return GameConstants.AntHoleBaseSpawnChanceDenominator;
        }

        var progress = GetWarningProgress();
        var maxReduction = GameConstants.AntHoleBaseSpawnChanceDenominator - 1;
        var reduction = Math.Max(1, (int)MathF.Ceiling(progress * maxReduction));
        return Math.Max(1, GameConstants.AntHoleBaseSpawnChanceDenominator - reduction);
    }
}
