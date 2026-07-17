using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public class Building
{
    private readonly List<World.Tile> _projectedTiles = [];
    private readonly List<InteractionZone> _interactionZones = [];

    public Building(string name, GridPoint size, int[][] openMap, GameSession session, bool hasStation)
    {
        Id = session.AllocateWorldObjectId();
        Name = name;
        Size = size;
        DisplayBaseSize = size;
        OpenMap = CloneOpenMap(openMap);
        Session = session;
        HasStation = hasStation;
        TileArray = [];
        Description = string.Empty;
        BfsField = new BfsField(name, "building", null, this);
        Health = 100;
        MaxHealth = 100;
        IgnoredByAnts = false;
        Selectable = true;
    }

    public string Name { get; }

    public int Id { get; }

    public GridPoint Size { get; protected set; }

    public GridPoint DisplayBaseSize { get; protected set; }

    public int[][] OpenMap { get; protected set; }

    public GameSession Session { get; }

    public List<World.Tile> TileArray { get; set; }

    public string Description { get; protected set; }

    public string TextureKey { get; protected set; } = string.Empty;

    public bool HasStation { get; }

    public GridPoint? Location { get; set; }

    public int Health { get; protected set; }

    public int MaxHealth { get; protected set; }

    public bool IgnoredByAnts { get; protected set; }

    public World.Cave? Cave { get; set; }

    public BfsField BfsField { get; set; }

    public List<ResourceRequirement>? Recipe { get; protected set; }

    public List<ResourceRequirement>? ConstructionCost { get; protected set; }

    public bool Selectable { get; protected set; }

    public int DisplayRotationTurns { get; protected set; }

    public IReadOnlyList<World.Tile> ProjectedTiles => _projectedTiles;

    public IReadOnlyList<InteractionZone> InteractionZones => _interactionZones;

    public bool TryGetInteractionZone(InteractionZonePurpose purpose, out InteractionZone zone)
    {
        for (var index = 0; index < _interactionZones.Count; index++)
        {
            if (_interactionZones[index].Purpose == purpose)
            {
                zone = _interactionZones[index];
                return true;
            }
        }

        zone = null!;
        return false;
    }

    public bool OwnsInteractionZone(InteractionZone zone)
    {
        for (var index = 0; index < _interactionZones.Count; index++)
        {
            if (ReferenceEquals(_interactionZones[index], zone))
            {
                return true;
            }
        }

        return false;
    }

    public virtual int ProjectionRadius => 0;

    public virtual int[][] RotateMap()
    {
        var rotated = new int[Size.X][];
        for (var column = 0; column < Size.X; column++)
        {
            rotated[column] = new int[Size.Y];
            var targetIndex = 0;
            for (var row = Size.Y - 1; row >= 0; row--)
            {
                rotated[column][targetIndex] = OpenMap[row][column];
                targetIndex++;
            }
        }

        OpenMap = rotated;
        Size = new GridPoint(Size.Y, Size.X);
        return OpenMap;
    }

    public virtual GridPoint GetCenter()
    {
        var location = Location ?? GridPoint.Zero;
        return new GridPoint(location.X + (Size.X / 2), location.Y + (Size.Y / 2));
    }

    public virtual GridPoint GetDisplayPivotBaseSize() => DisplayBaseSize;

    public int GetDisplayRotationTurns() => ((DisplayRotationTurns % 4) + 4) % 4;

    public WorldRectangle GetWorldBounds()
    {
        if (TileArray.Count > 0)
        {
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;
            for (var index = 0; index < TileArray.Count; index++)
            {
                var point = TileArray[index].Coordinates;
                minX = System.Math.Min(minX, point.X);
                minY = System.Math.Min(minY, point.Y);
                maxX = System.Math.Max(maxX, point.X);
                maxY = System.Math.Max(maxY, point.Y);
            }

            return new WorldRectangle(
                (minX * WorldUnits.UnitsPerTile) - WorldUnits.UnitsPerHalfTile,
                (minY * WorldUnits.UnitsPerTile) - WorldUnits.UnitsPerHalfTile,
                (maxX - minX + 1) * WorldUnits.UnitsPerTile,
                (maxY - minY + 1) * WorldUnits.UnitsPerTile);
        }

        var location = Location ?? GridPoint.Zero;
        return new WorldRectangle(
            (location.X * WorldUnits.UnitsPerTile) - WorldUnits.UnitsPerHalfTile,
            (location.Y * WorldUnits.UnitsPerTile) - WorldUnits.UnitsPerHalfTile,
            Size.X * WorldUnits.UnitsPerTile,
            Size.Y * WorldUnits.UnitsPerTile);
    }

    public void SetDisplayRotationTurns(int turns)
    {
        DisplayRotationTurns = ((turns % 4) + 4) % 4;
    }

    public virtual IReadOnlyList<ResourceRequirement>? GetRecipe()
    {
        return Recipe is null ? null : [.. Recipe];
    }

    public virtual IReadOnlyList<ResourceRequirement>? GetConstructionCost()
    {
        return ConstructionCost is null ? null : [.. ConstructionCost];
    }

    public virtual bool CanBeSelected() => Selectable;

    public bool MarkBfsFieldDirty(IEnumerable<string>? tileKeys = null)
    {
        return BfsField.MarkDirty(tileKeys ?? [], [], []);
    }

    public virtual int RestoreHealth()
    {
        Health = MaxHealth;
        return Health;
    }

    public virtual int TakeDamage(int amount, object? source = null)
    {
        if (amount <= 0 || Health <= 0)
        {
            return 0;
        }

        var applied = System.Math.Min(Health, amount);
        Health -= applied;
        if (Health <= 0)
        {
            Health = 0;
            RemoveFromGame(source);
        }

        return applied;
    }

    public virtual void CleanupBeforeRemoval(object? source = null)
    {
        ClearInteractionZones();
        ClearProjectedTiles();
    }

    public virtual bool RemoveFromGame(object? source = null)
    {
        return Cave?.RemoveBuilding(this, source) ?? true;
    }

    public virtual void OnBuilt(World.Cave cave)
    {
        RefreshProjectedTiles(cave);
        RefreshInteractionZones();
    }

    protected virtual IReadOnlyList<InteractionZoneDefinition> GetInteractionZoneDefinitions() => [];

    protected void RefreshInteractionZones()
    {
        ClearInteractionZones();
        if (Location is null)
        {
            return;
        }

        var definitions = GetInteractionZoneDefinitions();
        for (var index = 0; index < definitions.Count; index++)
        {
            _interactionZones.Add(CreateInteractionZone(index, definitions[index]));
        }
    }

    private InteractionZone CreateInteractionZone(int zoneIndex, InteractionZoneDefinition definition)
    {
        var transformedCells = new GridPoint[definition.Size.X * definition.Size.Y];
        var cellIndex = 0;
        for (var y = 0; y < definition.Size.Y; y++)
        {
            for (var x = 0; x < definition.Size.X; x++)
            {
                transformedCells[cellIndex++] = TransformLocalCell(new GridPoint(
                    definition.Origin.X + x,
                    definition.Origin.Y + y));
            }
        }

        var minX = transformedCells.Min(point => point.X);
        var minY = transformedCells.Min(point => point.Y);
        var maxX = transformedCells.Max(point => point.X);
        var maxY = transformedCells.Max(point => point.Y);
        var location = Location!.Value;
        var bounds = new WorldRectangle(
            ((location.X + minX) * WorldUnits.UnitsPerTile) - WorldUnits.UnitsPerHalfTile,
            ((location.Y + minY) * WorldUnits.UnitsPerTile) - WorldUnits.UnitsPerHalfTile,
            (maxX - minX + 1) * WorldUnits.UnitsPerTile,
            (maxY - minY + 1) * WorldUnits.UnitsPerTile);

        var slots = new WorldPoint[definition.Slots.Count];
        for (var index = 0; index < definition.Slots.Count; index++)
        {
            var transformed = TransformLocalCell(definition.Slots[index]);
            slots[index] = WorldPoint.FromGridPoint(new GridPoint(
                location.X + transformed.X,
                location.Y + transformed.Y));
        }

        return new InteractionZone(
            checked((Id * 100) + zoneIndex),
            this,
            definition.Name,
            definition.Purpose,
            bounds,
            slots);
    }

    private GridPoint TransformLocalCell(GridPoint point)
    {
        var width = DisplayBaseSize.X;
        var height = DisplayBaseSize.Y;
        return GetDisplayRotationTurns() switch
        {
            1 => new GridPoint(height - 1 - point.Y, point.X),
            2 => new GridPoint(width - 1 - point.X, height - 1 - point.Y),
            3 => new GridPoint(point.Y, width - 1 - point.X),
            _ => point
        };
    }

    private void ClearInteractionZones()
    {
        for (var index = 0; index < _interactionZones.Count; index++)
        {
            _interactionZones[index].ClearReservations();
        }

        _interactionZones.Clear();
    }

    public virtual int Tick(World.Cave cave)
    {
        return 0;
    }

    public virtual void TargetInRadius(Creature creature)
    {
    }

    public virtual void TargetNoLongerInRadius(Creature creature)
    {
    }

    public virtual void TrackedCreatureDied(Creature creature)
    {
    }

    protected void TrackCreature(Creature? creature)
    {
        if (creature is null)
        {
            return;
        }

        creature.AddTrackedBy(this);
    }

    protected void UntrackCreature(Creature? creature)
    {
        if (creature is null)
        {
            return;
        }

        creature.RemoveTrackedBy(this);
    }

    protected void RefreshProjectedTiles(World.Cave cave)
    {
        ClearProjectedTiles();
        if (ProjectionRadius <= 0 || Location is null)
        {
            return;
        }

        var center = GetCenter();
        var radiusSquared = ProjectionRadius * ProjectionRadius;
        for (var dx = -ProjectionRadius; dx <= ProjectionRadius; dx++)
        {
            for (var dy = -ProjectionRadius; dy <= ProjectionRadius; dy++)
            {
                var projectedLocation = new GridPoint(center.X + dx, center.Y + dy);
                if (GridPoint.SquaredDistance(center, projectedLocation) > radiusSquared)
                {
                    continue;
                }

                var tile = cave.GetTile(projectedLocation);
                if (tile is null || !tile.AddProjection(this))
                {
                    continue;
                }

                _projectedTiles.Add(tile);
            }
        }
    }

    protected void ClearProjectedTiles()
    {
        for (var index = _projectedTiles.Count - 1; index >= 0; index--)
        {
            _projectedTiles[index].RemoveProjection(this);
        }

        _projectedTiles.Clear();
    }

    protected static int[][] CloneOpenMap(int[][] openMap)
    {
        return openMap.Select(row => row.ToArray()).ToArray();
    }

    public static bool IsMineableType(string tileType)
    {
        return string.Equals(tileType, "wall", StringComparison.Ordinal) ||
               string.Equals(tileType, World.Tile.CaveCrystalBase, StringComparison.Ordinal) ||
               Economy.OreType.GetOres().Any(ore => string.Equals(ore.Name, tileType, StringComparison.Ordinal));
    }
}
