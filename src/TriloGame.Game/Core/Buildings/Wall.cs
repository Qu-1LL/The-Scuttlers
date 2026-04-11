using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public sealed class Wall : Building
{
    private static readonly int[][] DefaultOpenMap = [[1]];
    private const string TopDirection = "top";
    private const string RightDirection = "right";
    private const string BottomDirection = "bottom";
    private const string LeftDirection = "left";
    private readonly Dictionary<string, bool> _connections = new(StringComparer.Ordinal)
    {
        [TopDirection] = false,
        [RightDirection] = false,
        [BottomDirection] = false,
        [LeftDirection] = false
    };

    public Wall(GameSession session, WallType? wallType = null)
        : base((wallType ?? WallType.Default).Name, new GridPoint(1, 1), DefaultOpenMap, session, false)
    {
        Type = wallType ?? WallType.Default;
        TextureKey = Type.NoConnectionSprite;
        MaxHealth = Type.Health;
        Health = MaxHealth;
        Recipe = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Sandstone"] = 5
        };
        Description = "A defensive wall segment that trilobites can traverse while enemies must break through it.";
    }

    public WallType Type { get; }

    public IReadOnlyDictionary<string, bool> Connections => _connections;

    public override void OnBuilt(World.Cave cave)
    {
        base.OnBuilt(cave);
        RebuildConnectionsFromNeighbors();
        UpdateNeighborConnections(true);
        UpdateConnectionVisual();
    }

    public override void CleanupBeforeRemoval(object? source = null)
    {
        UpdateNeighborConnections(false);
        ClearConnections();
        UpdateConnectionVisual();
        base.CleanupBeforeRemoval(source);
    }

    internal void RefreshConnectionVisual()
    {
        RebuildConnectionsFromNeighbors();
        UpdateConnectionVisual();
    }

    private void SetConnection(string direction, bool connected)
    {
        if (!_connections.ContainsKey(direction) || _connections[direction] == connected)
        {
            return;
        }

        _connections[direction] = connected;
        UpdateConnectionVisual();
    }

    private void RebuildConnectionsFromNeighbors()
    {
        if (TileArray.Count == 0)
        {
            ClearConnections();
            return;
        }

        ClearConnections();
        foreach (var neighbor in TileArray[0].Neighbors)
        {
            if (neighbor.Built is not Wall || !TryResolveDirections(TileArray[0], neighbor, out var myDirection, out _))
            {
                continue;
            }

            _connections[myDirection] = true;
        }
    }

    private void UpdateNeighborConnections(bool connected)
    {
        if (TileArray.Count == 0)
        {
            return;
        }

        foreach (var neighbor in TileArray[0].Neighbors)
        {
            if (neighbor.Built is Wall wall &&
                !ReferenceEquals(wall, this) &&
                TryResolveDirections(TileArray[0], neighbor, out _, out var neighborDirection))
            {
                wall.SetConnection(neighborDirection, connected);
            }
        }
    }

    private void ClearConnections()
    {
        _connections[TopDirection] = false;
        _connections[RightDirection] = false;
        _connections[BottomDirection] = false;
        _connections[LeftDirection] = false;
    }

    private void UpdateConnectionVisual()
    {
        var connectionMask = 0;
        if (_connections[TopDirection])
        {
            connectionMask |= 1;
        }

        if (_connections[RightDirection])
        {
            connectionMask |= 2;
        }

        if (_connections[BottomDirection])
        {
            connectionMask |= 4;
        }

        if (_connections[LeftDirection])
        {
            connectionMask |= 8;
        }

        var (textureKey, rotationTurns) = Type.ResolveAppearance(connectionMask);
        TextureKey = textureKey;
        SetDisplayRotationTurns(rotationTurns);
    }

    private static bool TryResolveDirections(World.Tile sourceTile, World.Tile neighborTile, out string myDirection, out string neighborDirection)
    {
        var deltaX = neighborTile.Coordinates.X - sourceTile.Coordinates.X;
        var deltaY = neighborTile.Coordinates.Y - sourceTile.Coordinates.Y;
        if (deltaX == 0 && deltaY == -1)
        {
            myDirection = TopDirection;
            neighborDirection = BottomDirection;
            return true;
        }

        if (deltaX == 1 && deltaY == 0)
        {
            myDirection = RightDirection;
            neighborDirection = LeftDirection;
            return true;
        }

        if (deltaX == 0 && deltaY == 1)
        {
            myDirection = BottomDirection;
            neighborDirection = TopDirection;
            return true;
        }

        if (deltaX == -1 && deltaY == 0)
        {
            myDirection = LeftDirection;
            neighborDirection = RightDirection;
            return true;
        }

        myDirection = string.Empty;
        neighborDirection = string.Empty;
        return false;
    }
}
