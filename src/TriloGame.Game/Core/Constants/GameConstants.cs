using TriloGame.Game.Core.Economy;

namespace TriloGame.Game.Core.Constants;

public static class GameConstants
{
    public const double TickSpeedSlow = 500d;
    public const double TickSpeedNormal = 250d;
    public const double TickSpeedFast = 100d;
    public const double TickSpeedFastest = 50d;
    public const double GameTimePerSimulationTickMs = TickSpeedFast;
    public const double RoundGraceDurationMs = 3d * 60d * 1000d;
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

    public const float DefaultCameraScale = 80f / TileConstants.TileSize;
    // Zoom is a quantised ladder of DefaultCameraScale * ZoomStepRatio^step rather than a live
    // scale the wheel keeps multiplying. Repeated multiplication drifts off the ladder and the
    // clamp at either end swallowed steps, so zooming out and back in never returned to the level
    // you started from. The step index makes every level exact and the sequence reversible.
    public const float ZoomStepRatio = 4f / 3f;
    public const int MaxZoomSteps = 5;
    // The outermost rungs, written as exact ZoomStepRatio^MaxZoomSteps ratios (4^5 / 3^5). The
    // limits have to land ON the ladder: bounds that fall between rungs make the last step short,
    // and a short step is not undone by a step in the opposite direction.
    public const float MinScale = DefaultCameraScale * (243f / 1024f);
    public const float MaxScale = DefaultCameraScale * (1024f / 243f);
    // Approach rate of the eased zoom, in e-folds per second. High enough that a wheel notch feels
    // immediate, low enough that the scale moves continuously instead of snapping - a snap resets
    // the lighting's temporal history in one frame, which is what made each notch flash.
    public const float ZoomApproachRatePerSecond = 18f;
    public const float KeyboardPanSpeedPixelsPerSecond = 800f;
    public const float DragThresholdPixels = 10f;
    public const double DoubleClickThresholdMs = 300d;

    public const int MinOreYield = 15;
    public const int MaxOreYield = 50;
    public const int DarkestOreYield = 5;
    public const float MaxOreDarkness = 0.3f;
    public const int CaveCrystalMinCount = 6;
    public const int CaveCrystalMaxCount = 28;
    public const int CaveCrystalTileDivisor = 180;
    public const int CaveCrystalHitsRequired = 3;
    public const int CaveFloorHoleProtectedRadius = 8;
    // Water bodies. Sized to read as lakes rather than puddles: each cluster is a cellular blob
    // up to CaveFloorHoleMaxClusterSize on a side, and the tile budget decides how much floods.
    public const int CaveFloorHoleMinTileCount = 120;
    public const int CaveFloorHoleMaxTileCount = 340;
    public const int CaveFloorHoleTileDivisor = 40;
    public const int CaveFloorHoleMinClusterSize = 7;
    // Structural floor of the cellular mask, independent of the tuning above: the generator forces
    // a 3x3 core, so a mask smaller than that cannot be built. Kept separate because tying the
    // guard to MinClusterSize made raising that tuning value reject legitimate 3x3..6x6 masks.
    public const int CaveFloorHoleMinimumShapeSize = 3;
    public const int CaveFloorHoleMaxClusterSize = 14;
    public const int CaveFloorHoleCellularPasses = 2;
    // Higher fill chance stops the cellular pass eroding large blobs into scattered specks.
    public const double CaveFloorHoleInitialFillChance = 0.58d;
    public const float CaveBackgroundParallaxFactor = 0.16f;
    public const float CaveBackgroundScaleMultiplier = 1.3f;
    public const int OreHitsPerYield = 3;
    public const int MinOreHitsPerYield = OreHitsPerYield;
    public const int MaxOreHitsPerYield = OreHitsPerYield;
    public const int TrilobiteCarryCapacity = 5;
    public const int TrilobiteStarterTraitCount = 0;
    public const int ExplosiveTraitBlastRadius = 3;
    public const float ExplosiveTraitScreenShakeIntensity = 1f;
    public const int AlgaeHarvestYield = 5;
    public const int WallHitsRequired = 10;
    public const int WallDropAmount = 5;
    public const int WallDropCarryAmount = 1;
    public static readonly ResourceName WallMineResourceType = ResourceName.Sandstone;
    public const int WallMineResourceAmount = 1;
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
