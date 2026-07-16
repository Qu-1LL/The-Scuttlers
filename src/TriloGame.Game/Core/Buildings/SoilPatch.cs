using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.Buildings;

public sealed class SoilPatch : Building, IBuildPlacementDragTarget
{
    public static readonly GridPoint DefaultSize = new(2, 2);
    private static readonly InteractionZoneDefinition[] WorkZones =
    [
        new(
            "Soil work area",
            InteractionZonePurpose.Work,
            GridPoint.Zero,
            DefaultSize,
            [new GridPoint(0, 0), new GridPoint(1, 0), new GridPoint(0, 1), new GridPoint(1, 1)])
    ];
    private readonly SoilTile[] _soilTiles;

    public SoilPatch(GameSession session)
        : base("Soil Patch", DefaultSize, [[1, 1], [1, 1]], session, false)
    {
        _soilTiles =
        [
            new SoilTile(this, new GridPoint(0, 0)),
            new SoilTile(this, new GridPoint(1, 0)),
            new SoilTile(this, new GridPoint(0, 1)),
            new SoilTile(this, new GridPoint(1, 1))
        ];
        SoilArea = new SoilArea(session);
        SoilArea.AddSoilPatch(this);
        IgnoredByAnts = true;
        Recipe = [ResourceRequirement.ForCategory(ResourceCategory.Organic, 5)];
        Description = "A 2x2 patch of passable soil. Each tile grows its planted crop independently.";
        TextureKey = "SoilTile_0";
    }

    public override bool MaintainsNavigationField => false;

    public override BuildingNavigationSeedMode NavigationSeedMode => BuildingNavigationSeedMode.None;

    public override BuildingNavigationMaintenanceMode NavigationFieldMaintenanceMode => BuildingNavigationMaintenanceMode.None;

    public Ranch? Ranch
    {
        get
        {
            for (var index = 0; index < _soilTiles.Length; index++)
            {
                if (_soilTiles[index].Ranch is not null)
                {
                    return _soilTiles[index].Ranch;
                }
            }

            return null;
        }
    }

    public IReadOnlyList<SoilTile> SoilTiles => _soilTiles;

    public SoilArea? SoilArea { get; internal set; }

    public BuildPlacementDragKind DragPlacementKind => BuildPlacementDragKind.FootprintGrid;

    protected override IReadOnlyList<InteractionZoneDefinition> GetInteractionZoneDefinitions() => WorkZones;

    public GridPoint DragPlacementStep => DefaultSize;

    public override int Tick(World.Cave cave)
    {
        return Tick(RandomUtil.Shared, Session.TickCount % 10);
    }

    internal int Tick(Random random, int currentTickMod)
    {
        var advancedTiles = 0;
        for (var index = 0; index < _soilTiles.Length; index++)
        {
            advancedTiles += _soilTiles[index].Tick(random, currentTickMod);
        }

        return advancedTiles;
    }

    public bool TryGetLocalOffset(GridPoint worldLocation, out GridPoint localOffset)
    {
        localOffset = GridPoint.Zero;
        if (Location is not { } location)
        {
            return false;
        }

        var offset = new GridPoint(worldLocation.X - location.X, worldLocation.Y - location.Y);
        if (!IsValidLocalOffset(offset))
        {
            return false;
        }

        localOffset = offset;
        return true;
    }

    public SoilTile? GetSoilTile(GridPoint localOffset)
    {
        return TryGetSoilTileIndex(localOffset, out var index)
            ? _soilTiles[index]
            : null;
    }

    public int Harvest(GridPoint localOffset)
    {
        if (!TryGetSoilTileIndex(localOffset, out var index))
        {
            return 0;
        }

        return _soilTiles[index].Harvest();
    }

    public int HarvestAtWorldTile(GridPoint worldLocation)
    {
        return TryGetLocalOffset(worldLocation, out var localOffset)
            ? Harvest(localOffset)
            : 0;
    }

    internal void SetGrowthConstant(GridPoint localOffset, double value)
    {
        if (!TryGetSoilTileIndex(localOffset, out var index))
        {
            return;
        }

        _soilTiles[index].SetGrowthConstant(value);
    }

    internal void SetAllGrowthConstants(double value)
    {
        for (var index = 0; index < _soilTiles.Length; index++)
        {
            _soilTiles[index].SetGrowthConstant(value);
        }
    }

    internal void SetReturnedAlgaeAmount(GridPoint localOffset, int amount)
    {
        if (!TryGetSoilTileIndex(localOffset, out var index))
        {
            return;
        }

        _soilTiles[index].SetReturnedAlgaeAmount(amount);
    }

    internal void SetAllReturnedAlgaeAmounts(int amount)
    {
        for (var index = 0; index < _soilTiles.Length; index++)
        {
            _soilTiles[index].SetReturnedAlgaeAmount(amount);
        }
    }

    internal void SetGrowthLevel(GridPoint localOffset, int level)
    {
        if (!TryGetSoilTileIndex(localOffset, out var index))
        {
            return;
        }

        _soilTiles[index].SetGrowthLevel(level);
    }

    internal void SetAllGrowthLevels(int level)
    {
        for (var index = 0; index < _soilTiles.Length; index++)
        {
            _soilTiles[index].SetGrowthLevel(level);
        }
    }

    internal void SetPlantedResource(GridPoint localOffset, GrowableResourceType? resourceType)
    {
        if (!TryGetSoilTileIndex(localOffset, out var index))
        {
            return;
        }

        _soilTiles[index].SetPlantedResource(resourceType);
    }

    internal void SetAllPlantedResources(GrowableResourceType? resourceType)
    {
        for (var index = 0; index < _soilTiles.Length; index++)
        {
            _soilTiles[index].SetPlantedResource(resourceType);
        }
    }

    private static bool IsValidLocalOffset(GridPoint localOffset)
    {
        return localOffset.X >= 0 &&
               localOffset.X < 2 &&
               localOffset.Y >= 0 &&
               localOffset.Y < 2;
    }

    private static bool TryGetSoilTileIndex(GridPoint localOffset, out int index)
    {
        index = -1;
        if (!IsValidLocalOffset(localOffset))
        {
            return false;
        }

        index = localOffset.Y * 2 + localOffset.X;
        return true;
    }
}
