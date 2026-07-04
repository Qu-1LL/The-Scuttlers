using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Game.Core.World;

internal sealed class CaveGenerator
{
    private const int SizeMult = 30;
    private const int HoleLimit = 10;
    private const double DegradeLimit = 2.75;
    private const double DegradeDeviation = 0.7;
    private const int CavernCount = 25;
    private const int Radius = 20;
    private const int OreMult = 300;
    private const int OreDist = 8;

    public void Generate(Cave cave)
    {
        FillCircle(cave, 0, 0, Radius);

        var origins = new List<GridPoint> { GridPoint.Zero };
        var successfulCaverns = 0;
        while (successfulCaverns < CavernCount)
        {
            var parent = origins[RandomUtil.NextInt(origins.Count)];
            var t = RandomUtil.NextDouble();
            var xOffset = (Radius * 2d * t) + (Radius * RandomUtil.NextDouble());
            var yOffset = (Radius * 2d * (1d - t)) + (Radius * RandomUtil.NextDouble());

            var candidateX = (int)Math.Floor(parent.X + xOffset);
            var candidateY = (int)Math.Floor(parent.Y + yOffset);

            if (RandomUtil.NextDouble() > 0.5d)
            {
                candidateX = -candidateX;
            }

            if (RandomUtil.NextDouble() > 0.5d)
            {
                candidateY = -candidateY;
            }

            var tooClose = false;
            foreach (var origin in origins)
            {
                var dx = candidateX - origin.X;
                var dy = candidateY - origin.Y;
                if ((dx * dx) + (dy * dy) <= Radius * Radius)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
            {
                continue;
            }

            var newOrigin = new GridPoint(candidateX, candidateY);
            origins.Add(newOrigin);
            var newRadius = (int)Math.Floor((0.5d + RandomUtil.NextDouble()) * Radius);
            FillCircle(cave, newOrigin.X, newOrigin.Y, newRadius);
            successfulCaverns++;
        }

        RemoveInteriorHoles(cave);
        DegradeCave(cave);
        RemoveIsolatedTiles(cave);
        MarkBoundaryWalls(cave);
        RemoveFullyBuriedWalls(cave);
        FillOres(cave);
        GenerateFloorHoles(cave);
        PlaceCaveCrystals(cave);
    }

    private static void RemoveInteriorHoles(Cave cave)
    {
        var protectedCenterRadius = Radius / 2;
        var holeBreakThreshold = (Radius * HoleLimit) + (CavernCount * HoleLimit);
        var tileKeys = RandomUtil.Shuffle(GetTileKeys(cave));
        var removedHoleCount = 0;
        foreach (var tileKey in tileKeys)
        {
            var tile = cave.GetTile(tileKey)!;
            var coords = GridPoint.Parse(tileKey);
            if (tile.Neighbors.Count == 4 &&
                ((coords.X * coords.X) + (coords.Y * coords.Y) > protectedCenterRadius * protectedCenterRadius))
            {
                cave.RemoveTile(tileKey);
                removedHoleCount++;
            }

            if (removedHoleCount > holeBreakThreshold)
            {
                break;
            }
        }
    }

    private static void DegradeCave(Cave cave)
    {
        for (var index = 0; index < 2d + ((double)Radius / SizeMult) + ((double)Radius / CavernCount); index++)
        {
            DegradeCaveOnce(cave);
        }
    }

    private static void DegradeCaveOnce(Cave cave)
    {
        var tileKeys = RandomUtil.Shuffle(GetTileKeys(cave));
        foreach (var tileKey in tileKeys)
        {
            var tile = cave.GetTile(tileKey)!;
            var neighborCount = tile.Neighbors.Count;
            var sample = RandomUtil.NextNormal(neighborCount, DegradeDeviation);
            if (neighborCount < 4 && sample < DegradeLimit)
            {
                cave.RemoveTile(tileKey);
            }
        }
    }

    private static void RemoveIsolatedTiles(Cave cave)
    {
        foreach (var tileKey in GetTileKeys(cave))
        {
            if (cave.GetTile(tileKey)?.Neighbors.Count == 0)
            {
                cave.RemoveTile(tileKey);
            }
        }
    }

    private static void MarkBoundaryWalls(Cave cave)
    {
        foreach (var tileKey in GetTileKeys(cave))
        {
            var tile = cave.GetTile(tileKey)!;
            if (tile.Neighbors.Count < 4)
            {
                tile.SetBase("wall");
                tile.CreatureCanFit = false;
                tile.ConfigureWall(GameConstants.WallHitsRequired);
            }
        }
    }

    private static void RemoveFullyBuriedWalls(Cave cave)
    {
        foreach (var tileKey in GetTileKeys(cave))
        {
            var tile = cave.GetTile(tileKey)!;
            if (tile.Base != "wall")
            {
                continue;
            }

            var willDelete = tile.Neighbors.All(neighbor => neighbor.Base != "empty");
            if (willDelete)
            {
                cave.RemoveTile(tileKey);
            }
        }
    }

    private static void FillOres(Cave cave)
    {
        TryPlaceGuaranteedOre(cave, -8, 9, OreType.CHITINSTONE.Name);
        TryPlaceGuaranteedOre(cave, -6, 7, OreType.LUMENITE.Name);
        TryPlaceGuaranteedOre(cave, -6, 7, OreType.MYCOCORE.Name);

        var oreCount = 0;
        foreach (var ore in OreType.GetOres())
        {
            var count = 0;
            foreach (var tile in RandomUtil.Shuffle(cave.GetTiles()))
            {
                var lower = Math.Abs(RandomUtil.NextNormal(3d * CavernCount * oreCount, CavernCount * (OreType.GetOres().Count - oreCount)) / OreDist);
                var upper = Math.Abs(RandomUtil.NextNormal(3d * CavernCount * (oreCount + 3), 2d * CavernCount * (OreType.GetOres().Count - oreCount)) / OreDist);
                var coords = GridPoint.Parse(tile.Key);
                var vector = GetDistance(coords.X, coords.Y, 0, 0);
                if (vector > lower && vector < upper && tile.Base == "empty")
                {
                    ConfigureGeneratedOreTile(tile, ore.Name);
                    var veinCount = 0;
                    var roll = RandomUtil.NextDouble();
                    while (roll < 0.85d && veinCount <= 2 + (OreType.GetOres().Count - oreCount))
                    {
                        var neighbor = tile.GetRandomNeighbor();
                        var brokenCount = 0;
                        while (neighbor is not null && neighbor.Base != "empty" && brokenCount < 4)
                        {
                            neighbor = neighbor.GetRandomNeighbor();
                            brokenCount++;
                        }

                        if (neighbor is not null && brokenCount < 4)
                        {
                            ConfigureGeneratedOreTile(neighbor, ore.Name);
                        }

                        roll = RandomUtil.NextDouble();
                        veinCount++;
                    }

                    count++;
                }

                if (count >= (CavernCount / 5d) + (CavernCount * Radius * (OreType.GetOres().Count - oreCount)) / (double)OreMult)
                {
                    break;
                }
            }

            oreCount++;
        }
    }

    private static bool TryPlaceGuaranteedOre(Cave cave, int min, int maxExclusive, string ore)
    {
        for (var attempt = 0; attempt < 500; attempt++)
        {
            var x = RandomUtil.NextInt(min, maxExclusive);
            var y = RandomUtil.NextInt(min, maxExclusive);
            var tile = cave.GetTile(new GridPoint(x, y));
            if (tile is not null && tile.Base == "empty")
            {
                ConfigureGeneratedOreTile(tile, ore);
                return true;
            }
        }

        return false;
    }

    private static void ConfigureGeneratedOreTile(Tile tile, string oreName)
    {
        tile.SetBase(oreName);
        tile.ConfigureOre(
            RandomUtil.NextInt(GameConstants.MinOreYield, GameConstants.MaxOreYield + 1),
            RandomUtil.NextInt(GameConstants.MinOreHitsPerYield, GameConstants.MaxOreHitsPerYield + 1));
        GeneratedTileSpriteRotation.AssignOreRotation(tile);
    }

    private static void GenerateFloorHoles(Cave cave)
    {
        var candidates = new List<Tile>();
        foreach (var tile in cave.GetTiles())
        {
            if (!CanBecomeFloorHole(tile))
            {
                continue;
            }

            candidates.Add(tile);
        }

        if (candidates.Count == 0)
        {
            return;
        }

        var targetHoleTiles = Math.Clamp(
            candidates.Count / GameConstants.CaveFloorHoleTileDivisor,
            GameConstants.CaveFloorHoleMinTileCount,
            GameConstants.CaveFloorHoleMaxTileCount);
        var placedHoles = 0;
        var usedKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var seed in RandomUtil.Shuffle(candidates))
        {
            if (placedHoles >= targetHoleTiles || usedKeys.Contains(seed.Key))
            {
                continue;
            }

            var width = RandomUtil.NextInt(
                GameConstants.CaveFloorHoleMinClusterSize,
                GameConstants.CaveFloorHoleMaxClusterSize + 1);
            var height = RandomUtil.NextInt(
                GameConstants.CaveFloorHoleMinClusterSize,
                GameConstants.CaveFloorHoleMaxClusterSize + 1);
            var shape = BuildCellularFloorHoleShape(width, height);
            var origin = new GridPoint(
                seed.Coordinates.X - (width / 2),
                seed.Coordinates.Y - (height / 2));
            if (!TryResolveFloorHoleShapeTiles(cave, shape, origin, usedKeys, out var shapeTiles))
            {
                continue;
            }

            foreach (var tile in shapeTiles)
            {
                tile.SetFloorCover(false);
                usedKeys.Add(tile.Key);
                placedHoles++;
            }
        }
    }

    internal static bool[,] BuildCellularFloorHoleShape(int width, int height)
    {
        if (width < GameConstants.CaveFloorHoleMinClusterSize || height < GameConstants.CaveFloorHoleMinClusterSize)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Floor-hole cellular masks must be at least 3x3.");
        }

        var cells = new bool[width, height];
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                cells[x, y] = RandomUtil.NextDouble() <= GameConstants.CaveFloorHoleInitialFillChance;
            }
        }

        ForceMinimumFloorHoleCore(cells);
        for (var pass = 0; pass < GameConstants.CaveFloorHoleCellularPasses; pass++)
        {
            cells = SmoothFloorHoleCells(cells);
            ForceMinimumFloorHoleCore(cells);
        }

        return KeepConnectedFloorHoleCells(cells);
    }

    private static bool[,] SmoothFloorHoleCells(bool[,] cells)
    {
        var width = cells.GetLength(0);
        var height = cells.GetLength(1);
        var smoothed = new bool[width, height];
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var aliveNeighbors = CountAliveNeighbors(cells, x, y);
                smoothed[x, y] = aliveNeighbors >= 5 || (cells[x, y] && aliveNeighbors >= 4);
            }
        }

        return smoothed;
    }

    private static void ForceMinimumFloorHoleCore(bool[,] cells)
    {
        var centerX = cells.GetLength(0) / 2;
        var centerY = cells.GetLength(1) / 2;
        for (var x = centerX - 1; x <= centerX + 1; x++)
        {
            for (var y = centerY - 1; y <= centerY + 1; y++)
            {
                cells[x, y] = true;
            }
        }
    }

    private static bool[,] KeepConnectedFloorHoleCells(bool[,] cells)
    {
        var width = cells.GetLength(0);
        var height = cells.GetLength(1);
        var connected = new bool[width, height];
        var center = new GridPoint(width / 2, height / 2);
        var queue = new Queue<GridPoint>();
        connected[center.X, center.Y] = true;
        queue.Enqueue(center);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            TryEnqueueConnectedCell(cells, connected, queue, current.X - 1, current.Y);
            TryEnqueueConnectedCell(cells, connected, queue, current.X + 1, current.Y);
            TryEnqueueConnectedCell(cells, connected, queue, current.X, current.Y - 1);
            TryEnqueueConnectedCell(cells, connected, queue, current.X, current.Y + 1);
        }

        ForceMinimumFloorHoleCore(connected);
        return connected;
    }

    private static void TryEnqueueConnectedCell(bool[,] cells, bool[,] connected, Queue<GridPoint> queue, int x, int y)
    {
        if (x < 0 ||
            y < 0 ||
            x >= cells.GetLength(0) ||
            y >= cells.GetLength(1) ||
            !cells[x, y] ||
            connected[x, y])
        {
            return;
        }

        connected[x, y] = true;
        queue.Enqueue(new GridPoint(x, y));
    }

    private static int CountAliveNeighbors(bool[,] cells, int x, int y)
    {
        var count = 0;
        for (var offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (var offsetY = -1; offsetY <= 1; offsetY++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                var sampleX = x + offsetX;
                var sampleY = y + offsetY;
                if (sampleX < 0 ||
                    sampleY < 0 ||
                    sampleX >= cells.GetLength(0) ||
                    sampleY >= cells.GetLength(1))
                {
                    continue;
                }

                if (cells[sampleX, sampleY])
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static bool TryResolveFloorHoleShapeTiles(
        Cave cave,
        bool[,] shape,
        GridPoint origin,
        ISet<string> usedKeys,
        out IReadOnlyList<Tile> tiles)
    {
        var resolvedTiles = new List<Tile>();
        for (var x = 0; x < shape.GetLength(0); x++)
        {
            for (var y = 0; y < shape.GetLength(1); y++)
            {
                if (!shape[x, y])
                {
                    continue;
                }

                var tile = cave.GetTile(new GridPoint(origin.X + x, origin.Y + y));
                if (tile is null || usedKeys.Contains(tile.Key) || !CanBecomeFloorHole(tile))
                {
                    tiles = [];
                    return false;
                }

                resolvedTiles.Add(tile);
            }
        }

        tiles = resolvedTiles;
        return resolvedTiles.Count >= GameConstants.CaveFloorHoleMinClusterSize * GameConstants.CaveFloorHoleMinClusterSize;
    }

    private static bool CanBecomeFloorHole(Tile tile)
    {
        return string.Equals(tile.Base, "empty", StringComparison.Ordinal) &&
            tile.CreatureFits() &&
            tile.HasFloorCover &&
            GridPoint.ManhattanDistance(tile.Coordinates, GridPoint.Zero) >= GameConstants.CaveFloorHoleProtectedRadius;
    }

    private static void PlaceCaveCrystals(Cave cave)
    {
        var candidates = new List<Tile>();
        foreach (var tile in cave.GetTiles())
        {
            if (string.Equals(tile.Base, "empty", StringComparison.Ordinal) &&
                tile.CreatureFits() &&
                tile.HasFloorCover)
            {
                candidates.Add(tile);
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        var targetCount = Math.Clamp(
            candidates.Count / GameConstants.CaveCrystalTileDivisor,
            GameConstants.CaveCrystalMinCount,
            GameConstants.CaveCrystalMaxCount);
        var placed = 0;
        foreach (var tile in RandomUtil.Shuffle(candidates))
        {
            var neighborHasCrystal = false;
            foreach (var neighbor in tile.Neighbors)
            {
                if (neighbor.IsCaveCrystal())
                {
                    neighborHasCrystal = true;
                    break;
                }
            }

            if (neighborHasCrystal)
            {
                continue;
            }

            ConfigureGeneratedCaveCrystalTile(tile);
            placed++;
            if (placed >= targetCount)
            {
                break;
            }
        }
    }

    private static void ConfigureGeneratedCaveCrystalTile(Tile tile)
    {
        tile.SetBase(Tile.CaveCrystalBase);
        tile.CreatureCanFit = false;
        tile.ConfigureCaveCrystal(GameConstants.CaveCrystalHitsRequired);
        GeneratedTileSpriteRotation.AssignCaveCrystalRotation(tile);
    }

    private static void FillCircle(Cave cave, int originX, int originY, int radius)
    {
        for (var x = originX - radius; x <= originX + radius; x++)
        {
            for (var y = originY - radius; y <= originY + radius; y++)
            {
                if (!IsInCircle(x, y, originX, originY, radius))
                {
                    continue;
                }

                var tilePoint = new GridPoint(x, y);
                cave.AddTile(tilePoint.ToString());
                if (cave.GetTile(new GridPoint(x - 1, y)) is not null)
                {
                    cave.AddEdge(tilePoint.ToString(), new GridPoint(x - 1, y).ToString());
                }

                if (cave.GetTile(new GridPoint(x, y - 1)) is not null)
                {
                    cave.AddEdge(tilePoint.ToString(), new GridPoint(x, y - 1).ToString());
                }
            }
        }
    }

    private static string[] GetTileKeys(Cave cave)
    {
        var tiles = cave.GetTiles();
        var keys = new string[tiles.Count];
        for (var index = 0; index < tiles.Count; index++)
        {
            keys[index] = tiles[index].Key;
        }

        return keys;
    }

    private static bool IsInCircle(int x, int y, int cx, int cy, int radius)
    {
        var dx = x - cx;
        var dy = y - cy;
        return (dx * dx) + (dy * dy) <= radius * radius;
    }

    private static double GetDistance(int x, int y, int cx, int cy)
    {
        var dx = x - cx;
        var dy = y - cy;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
