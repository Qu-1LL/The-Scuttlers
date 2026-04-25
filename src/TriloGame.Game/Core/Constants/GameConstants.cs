namespace TriloGame.Game.Core.Constants;

public static class GameConstants
{
    public const double TickSpeedSlow = 500d;
    public const double TickSpeedNormal = 250d;
    public const double TickSpeedFast = 100d;
    public const double TickSpeedFastest = 50d;
    public const double GameTimePerSimulationTickMs = TickSpeedFast;
    public const double RoundDurationMs = 5d * 60d * 1000d;
    public const double RoundZeroGraceDurationMs = 2d * 60d * 1000d;
    public const double RoundSpawnWindowDurationMs = 30d * 1000d;
    public const int RoundBaseAntCount = 5;
    public const int RoundAntGrowthPerRound = 3;
    public const int RoundMinAntHolesPerSpawnEvent = 1;
    public const int RoundMaxAntHolesPerSpawnEvent = 3;
    public const int RoundSingleAntSpawnMaxRound = 5;
    public const int RoundAntHoleMinDistanceFromQueen = 30;
    public const int RoundAntHoleMaxDistanceFromQueen = 50;
    public const float ExplosionShakeMaxPixels = 20f;
    public const float ExplosionShakeFrequencyHz = 18f;
    public const float ExplosionShakeDecayPerSecond = 2.75f;

    public const float MinScale = 0.1f;
    public const float MaxScale = 2.5f;
    public const float KeyboardPanSpeedPixelsPerSecond = 800f;
    public const float DragThresholdPixels = 10f;
    public const double DoubleClickThresholdMs = 300d;

    public const int MinOreYield = 15;
    public const int MaxOreYield = 50;
    public const int DarkestOreYield = 5;
    public const float MaxOreDarkness = 0.3f;
    public const int MinOreHitsPerYield = 1;
    public const int MaxOreHitsPerYield = 5;
    public const int TrilobiteCarryCapacity = 5;
    public const int TrilobiteStarterTraitCount = 0;
    public const int ExplosiveTraitBlastRadius = 3;
    public const float ExplosiveTraitScreenShakeIntensity = 1f;
    public const int AlgaeHarvestYield = 5;
    public const int WallHitsRequired = 3;
    public const int WallDropAmount = 5;
    public const int WallDropCarryAmount = 1;
    public const float WallDropSpriteScale = 0.125f;
    public const int WorkerEnemyFleeRadius = 3;
    public const int AntHoleBaseSpawnChanceDenominator = 500;
    public const int AntHoleMinSpawnDistanceFromQueen = 15;
    public const int QueenEnemySpawnExclusionRadius = 10;
    public const int MinAmbientAntSpawnCount = 1;
    public const int MaxAmbientAntSpawnCount = 1;
    public const int MaxAmbientAntCount = 24;
    public const int AntHoleSpawnRadius = 2;
    public const int TurretProjectionRadius = 10;
    public const int TurretMaxHealth = 100;
}
