namespace TriloGame.Game.Core.World;

public sealed class Tile
{
    public const string CaveCrystalBase = "CaveCrystal";

    private readonly HashSet<Tile> _adjacent = [];
    private readonly Dictionary<string, int> _droppedResources = new(StringComparer.Ordinal);
    private readonly List<Buildings.Building> _projections = [];

    public Tile(int id, string key)
    {
        Id = id;
        Key = key;
        Coordinates = Shared.Math.GridPoint.Parse(key);
        Base = "empty";
        HasFloorCover = true;
        CreatureCanFit = true;
    }

    public int Id { get; }

    public string Key { get; }

    public Shared.Math.GridPoint Coordinates { get; }

    public string Base { get; private set; }

    public TileDecoration Decoration { get; private set; }

    public bool HasFloorCover { get; private set; }

    public byte OreRotationQuarterTurns { get; private set; }

    public int ResourceYield { get; private set; }

    public int HitsPerYield { get; private set; }

    public int HitsRemaining { get; private set; }

    public BiomeRegion? Biome { get; private set; }

    public string? BiomeName => Biome?.Name;

    public Buildings.Building? Built { get; private set; }

    public bool CreatureCanFit { get; set; }

    public IReadOnlyCollection<Tile> Neighbors => _adjacent;

    public IReadOnlyDictionary<string, int> DroppedResources => _droppedResources;

    public IReadOnlyList<Buildings.Building> Projections => _projections;

    public void AddNeighbor(Tile tile)
    {
        if (tile == this)
        {
            return;
        }

        if (_adjacent.Add(tile))
        {
            tile._adjacent.Add(this);
        }
    }

    public void RemoveNeighbor(Tile tile)
    {
        if (_adjacent.Remove(tile))
        {
            tile._adjacent.Remove(this);
        }
    }

    public void SetBase(string tileBase)
    {
        Base = tileBase;
        ClearOreRotation();
        if (!string.Equals(tileBase, "empty", StringComparison.Ordinal))
        {
            ClearDecoration();
        }

        if (!IsResourcelessBreakableBase(tileBase))
        {
            HitsRemaining = 0;
        }

        if (string.Equals(tileBase, "empty", StringComparison.Ordinal))
        {
            ClearResourceState();
        }
    }

    public void ConfigureOre(int yield, int hitsPerYield)
    {
        ResourceYield = Math.Max(0, yield);
        HitsPerYield = Math.Max(1, hitsPerYield);
        HitsRemaining = ResourceYield > 0 ? HitsPerYield : 0;
    }

    public void ConfigureWall(int hitsRequired)
    {
        ClearResourceState();
        HitsRemaining = Math.Max(1, hitsRequired);
    }

    public void ConfigureCaveCrystal(int hitsRequired)
    {
        ClearResourceState();
        HitsRemaining = Math.Max(1, hitsRequired);
    }

    public void ClearResourceState()
    {
        ResourceYield = 0;
        HitsPerYield = 0;
        HitsRemaining = 0;
        ClearOreRotation();
    }

    public void SetDecoration(TileDecoration decoration)
    {
        Decoration = decoration;
    }

    public void SetFloorCover(bool hasFloorCover)
    {
        HasFloorCover = hasFloorCover;
    }

    public void SetOreRotationQuarterTurns(int quarterTurns)
    {
        OreRotationQuarterTurns = GeneratedTileSpriteRotation.NormalizeQuarterTurns(quarterTurns);
    }

    public void ClearOreRotation()
    {
        OreRotationQuarterTurns = 0;
    }

    public void ClearDecoration()
    {
        Decoration = TileDecoration.None;
    }

    public bool IsOreTile() => ResourceYield > 0 && HitsPerYield > 0 && !string.Equals(Base, "wall", StringComparison.Ordinal);

    public bool IsCaveCrystal() => string.Equals(Base, CaveCrystalBase, StringComparison.Ordinal);

    // A gap in the cave floor, which is rendered as water beneath the floor level. Creatures
    // cannot enter one (CreatureFits already requires HasFloorCover), and a building placed over
    // the gap bridges it, so a covered gap is no longer open water.
    public bool IsWater() =>
        !HasFloorCover &&
        Built is null &&
        string.Equals(Base, "empty", StringComparison.Ordinal);

    public static bool IsResourcelessBreakableBase(string tileBase)
    {
        return string.Equals(tileBase, "wall", StringComparison.Ordinal) ||
               string.Equals(tileBase, CaveCrystalBase, StringComparison.Ordinal);
    }

    public bool ApplyOreMineHit(out bool depleted)
    {
        depleted = false;
        if (!IsOreTile() || HitsRemaining <= 0)
        {
            return false;
        }

        HitsRemaining--;
        if (HitsRemaining > 0)
        {
            return false;
        }

        ResourceYield = Math.Max(0, ResourceYield - 1);
        depleted = ResourceYield <= 0;
        if (!depleted)
        {
            HitsRemaining = HitsPerYield;
        }

        return true;
    }

    public bool ApplyWallMineHit()
    {
        if (!string.Equals(Base, "wall", StringComparison.Ordinal) || HitsRemaining <= 0)
        {
            return false;
        }

        HitsRemaining--;
        return HitsRemaining <= 0;
    }

    public bool ApplyCaveCrystalMineHit()
    {
        if (!IsCaveCrystal() || HitsRemaining <= 0)
        {
            return false;
        }

        HitsRemaining--;
        return HitsRemaining <= 0;
    }

    public int GetDroppedResourceCount(string resourceType)
    {
        return string.IsNullOrWhiteSpace(resourceType) ? 0 : _droppedResources.GetValueOrDefault(resourceType, 0);
    }

    public int AddDroppedResource(string resourceType, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceType) || amount <= 0)
        {
            return 0;
        }

        _droppedResources[resourceType] = GetDroppedResourceCount(resourceType) + amount;
        return amount;
    }

    public int TakeDroppedResource(string resourceType, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceType) || amount <= 0)
        {
            return 0;
        }

        var available = GetDroppedResourceCount(resourceType);
        var taken = Math.Min(available, amount);
        if (taken <= 0)
        {
            return 0;
        }

        var remaining = available - taken;
        if (remaining <= 0)
        {
            _droppedResources.Remove(resourceType);
        }
        else
        {
            _droppedResources[resourceType] = remaining;
        }

        return taken;
    }

    public void SetBuilt(Buildings.Building? building)
    {
        Built = building;
    }

    public bool CreatureFits() => CreatureCanFit && HasFloorCover;

    internal void SetBiome(BiomeRegion? biome)
    {
        Biome = biome;
    }

    public bool EnemyFits() => CreatureCanFit && HasFloorCover && Built is not Buildings.Wall;

    public bool CreatureFits(Entities.Creature creature)
    {
        return creature is Entities.Enemy ? EnemyFits() : CreatureFits();
    }

    public bool AddProjection(Buildings.Building building)
    {
        for (var index = 0; index < _projections.Count; index++)
        {
            if (ReferenceEquals(_projections[index], building))
            {
                return false;
            }
        }

        _projections.Add(building);
        return true;
    }

    public bool RemoveProjection(Buildings.Building building)
    {
        for (var index = 0; index < _projections.Count; index++)
        {
            if (!ReferenceEquals(_projections[index], building))
            {
                continue;
            }

            _projections.RemoveAt(index);
            return true;
        }

        return false;
    }

    public Tile? GetRandomNeighbor()
    {
        if (_adjacent.Count == 0)
        {
            return null;
        }

        var index = Shared.Utilities.RandomUtil.NextInt(_adjacent.Count);
        return _adjacent.ElementAt(index);
    }
}
