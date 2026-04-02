using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public class Building
{
    public Building(string name, GridPoint size, int[][] openMap, GameSession session, bool hasStation)
    {
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
        Selectable = true;
    }

    public string Name { get; }

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

    public World.Cave? Cave { get; set; }

    public BfsField BfsField { get; set; }

    public Dictionary<string, int>? Recipe { get; protected set; }

    public bool Selectable { get; protected set; }

    public int DisplayRotationTurns { get; protected set; }

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

    public void SetDisplayRotationTurns(int turns)
    {
        DisplayRotationTurns = ((turns % 4) + 4) % 4;
    }

    public virtual Dictionary<string, int>? GetRecipe()
    {
        return Recipe is null ? null : new Dictionary<string, int>(Recipe, StringComparer.Ordinal);
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
    }

    public virtual bool RemoveFromGame(object? source = null)
    {
        return Cave?.RemoveBuilding(this, source) ?? true;
    }

    public virtual void OnBuilt(World.Cave cave)
    {
    }

    public virtual int Tick(World.Cave cave)
    {
        return 0;
    }

    protected static int[][] CloneOpenMap(int[][] openMap)
    {
        return openMap.Select(row => row.ToArray()).ToArray();
    }

    public static bool IsMineableType(string tileType)
    {
        return string.Equals(tileType, "wall", StringComparison.Ordinal) ||
               Economy.OreType.GetOres().Any(ore => string.Equals(ore.Name, tileType, StringComparison.Ordinal));
    }
}
