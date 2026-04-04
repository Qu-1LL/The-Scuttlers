using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Pathfinding;

public readonly record struct MiningPostOwnership(MiningPost? Post, int Distance)
{
    public bool IsOwned => Post is not null && Distance != int.MaxValue;
}

internal readonly record struct MiningPostAdjacencyEdge(MiningPost Left, MiningPost Right);

public sealed class MiningPostOwnershipField
{
    private MiningPost?[] _owners = [];
    private MiningPost?[] _seedOwners = [];
    private int[] _distances = [];
    private bool[] _covered = [];
    private bool[] _nextCovered = [];
    private bool[] _queued = [];
    private readonly Queue<int> _queue = [];
    private readonly Dictionary<MiningPost, int> _ownerOrder = [];
    private HashSet<MiningPostAdjacencyEdge>?[] _tileAdjacencyEdges = [];
    private readonly Dictionary<MiningPostAdjacencyEdge, int> _adjacencySupportCounts = [];
    private readonly Dictionary<MiningPost, HashSet<MiningPost>> _adjacency = [];
    private bool _ownershipCacheDirty = true;
    private int _coverageCount;

    public MiningPostOwnershipField(Cave? cave = null)
    {
        Cave = cave;
        OwnershipField = new Dictionary<string, MiningPostOwnership>(StringComparer.Ordinal);
        UpdatedTiles = new HashSet<string>(StringComparer.Ordinal);
        UpdatedBuildings = [];
        EnsureCapacity(cave?.TileCapacity ?? 0);
    }

    public Cave? Cave { get; private set; }

    public Dictionary<string, MiningPostOwnership> OwnershipField { get; private set; }

    public bool Updated { get; private set; }

    public HashSet<string> UpdatedTiles { get; }

    public HashSet<Building> UpdatedBuildings { get; }

    public void SetCave(Cave? cave)
    {
        Cave = cave;
        EnsureCapacity(cave?.TileCapacity ?? 0);
        _ownershipCacheDirty = true;
    }

    public bool IsUpdated() => Updated;

    public bool ClearUpdates()
    {
        Updated = true;
        UpdatedTiles.Clear();
        UpdatedBuildings.Clear();
        return Updated;
    }

    public bool MarkTilesDirty(IEnumerable<string>? tileKeys)
    {
        Updated = false;
        foreach (var tileKey in tileKeys ?? [])
        {
            if (!string.IsNullOrWhiteSpace(tileKey))
            {
                UpdatedTiles.Add(tileKey);
            }
        }

        return Updated;
    }

    public bool MarkBuildingsDirty(IEnumerable<Building>? buildings)
    {
        Updated = false;
        foreach (var building in buildings ?? [])
        {
            UpdatedBuildings.Add(building);
        }

        return Updated;
    }

    public bool MarkDirty(IEnumerable<string>? tileKeys = null, IEnumerable<Building>? buildings = null)
    {
        Updated = false;
        MarkTilesDirty(tileKeys);
        MarkBuildingsDirty(buildings);
        return Updated;
    }

    public IReadOnlyDictionary<string, MiningPostOwnership> GetOwnershipField(bool refresh = true)
    {
        if (refresh)
        {
            Refresh();
        }

        if (!_ownershipCacheDirty)
        {
            return OwnershipField;
        }

        var field = new Dictionary<string, MiningPostOwnership>(Math.Max(0, _coverageCount), StringComparer.Ordinal);
        if (Cave is not null)
        {
            foreach (var tile in Cave.GetTiles())
            {
                if (!_covered[tile.Id])
                {
                    continue;
                }

                field[tile.Key] = new MiningPostOwnership(_owners[tile.Id], _distances[tile.Id]);
            }
        }

        OwnershipField = field;
        _ownershipCacheDirty = false;
        return OwnershipField;
    }

    public MiningPostOwnership GetOwnership(GridPoint location, bool refresh = true)
    {
        if (refresh)
        {
            Refresh();
        }

        var tile = GetTile(location.ToString());
        if (tile is null || !_covered[tile.Id])
        {
            return new MiningPostOwnership(null, int.MaxValue);
        }

        return new MiningPostOwnership(_owners[tile.Id], _distances[tile.Id]);
    }

    public MiningPost? GetOwner(GridPoint location, bool refresh = true)
    {
        return GetOwnership(location, refresh).Post;
    }

    public int GetDistance(GridPoint location, bool refresh = true)
    {
        return GetOwnership(location, refresh).Distance;
    }

    public IReadOnlyCollection<MiningPost> GetAdjacentPosts(MiningPost post, bool refresh = true)
    {
        if (refresh)
        {
            Refresh();
        }

        return _adjacency.TryGetValue(post, out var neighbors)
            ? OrderPosts(neighbors)
            : Array.Empty<MiningPost>();
    }

    public IReadOnlyDictionary<MiningPost, IReadOnlyCollection<MiningPost>> GetAdjacencyGraph(bool refresh = true)
    {
        if (refresh)
        {
            Refresh();
        }

        var graph = new Dictionary<MiningPost, IReadOnlyCollection<MiningPost>>();
        foreach (var post in OrderPosts(_adjacency.Keys))
        {
            graph[post] = GetAdjacentPosts(post, false);
        }

        return graph;
    }

    private void EnsureCapacity(int requiredCapacity)
    {
        if (requiredCapacity <= _distances.Length)
        {
            return;
        }

        var oldLength = _distances.Length;
        var newLength = Math.Max(requiredCapacity, Math.Max(8, oldLength * 2));

        Array.Resize(ref _owners, newLength);
        Array.Resize(ref _seedOwners, newLength);
        Array.Resize(ref _distances, newLength);
        Array.Fill(_distances, int.MaxValue, oldLength, newLength - oldLength);
        Array.Resize(ref _covered, newLength);
        Array.Resize(ref _nextCovered, newLength);
        Array.Resize(ref _queued, newLength);
        Array.Resize(ref _tileAdjacencyEdges, newLength);
    }

    private Tile? GetTile(string? tileKey)
    {
        if (string.IsNullOrWhiteSpace(tileKey) || Cave is null)
        {
            return null;
        }

        return Cave.GetTile(tileKey);
    }

    private static bool IsActiveMiningPost(MiningPost post)
    {
        return post.Location is not null && post.TileArray.Count > 0;
    }

    private static string GetOwnerLocationKey(MiningPost post)
    {
        return post.Location?.ToString() ?? string.Empty;
    }

    private void RefreshCoverageState(bool resetValues)
    {
        if (Cave is null)
        {
            _coverageCount = 0;
            Array.Clear(_covered, 0, _covered.Length);
            Array.Clear(_nextCovered, 0, _nextCovered.Length);
            return;
        }

        EnsureCapacity(Cave.TileCapacity);
        Array.Clear(_nextCovered, 0, _nextCovered.Length);

        foreach (var tile in Cave.GetReachableTiles())
        {
            if (tile.CreatureFits())
            {
                _nextCovered[tile.Id] = true;
            }
        }

        _coverageCount = 0;
        foreach (var tile in Cave.GetTiles())
        {
            var tileId = tile.Id;
            var shouldCover = _nextCovered[tileId];
            _covered[tileId] = shouldCover;
            if (shouldCover)
            {
                _coverageCount++;
                if (resetValues)
                {
                    _owners[tileId] = null;
                    _distances[tileId] = int.MaxValue;
                }
            }
            else
            {
                _owners[tileId] = null;
                _seedOwners[tileId] = null;
                _distances[tileId] = int.MaxValue;
            }
        }

        _ownershipCacheDirty = true;
    }

    private void RebuildOwnerOrdering()
    {
        _ownerOrder.Clear();
        if (Cave is null)
        {
            return;
        }

        // Equal-distance ties are resolved by the mining post's top-left tile key.
        // Those location keys are unique and stable for a placed post, so ownership
        // cannot oscillate between ticks just because refresh order changes.
        var orderedPosts = Cave.GetMiningPosts()
            .Where(IsActiveMiningPost)
            .OrderBy(GetOwnerLocationKey, StringComparer.Ordinal)
            .ToArray();

        for (var index = 0; index < orderedPosts.Length; index++)
        {
            _ownerOrder[orderedPosts[index]] = index;
        }
    }

    private int CompareOwners(MiningPost? left, MiningPost? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return 1;
        }

        if (right is null)
        {
            return -1;
        }

        var leftOrder = _ownerOrder.GetValueOrDefault(left, int.MaxValue);
        var rightOrder = _ownerOrder.GetValueOrDefault(right, int.MaxValue);
        if (leftOrder != rightOrder)
        {
            return leftOrder.CompareTo(rightOrder);
        }

        var keyCompare = string.CompareOrdinal(GetOwnerLocationKey(left), GetOwnerLocationKey(right));
        if (keyCompare != 0)
        {
            return keyCompare;
        }

        return string.CompareOrdinal(left.Name, right.Name);
    }

    private MiningPost[] OrderPosts(IEnumerable<MiningPost> posts)
    {
        return posts
            .OrderBy(post => _ownerOrder.GetValueOrDefault(post, int.MaxValue))
            .ThenBy(GetOwnerLocationKey, StringComparer.Ordinal)
            .ToArray();
    }

    private void AssignSeedOwner(int tileId, MiningPost owner)
    {
        if (_seedOwners[tileId] is null || CompareOwners(owner, _seedOwners[tileId]) < 0)
        {
            _seedOwners[tileId] = owner;
        }
    }

    private void RefreshSeedOwners()
    {
        Array.Clear(_seedOwners, 0, _seedOwners.Length);
        if (Cave is null)
        {
            return;
        }

        foreach (var post in OrderPosts(Cave.GetMiningPosts().Where(IsActiveMiningPost)))
        {
            var addedSeed = false;
            foreach (var tile in post.TileArray)
            {
                if (!_covered[tile.Id] || !tile.CreatureFits())
                {
                    continue;
                }

                AssignSeedOwner(tile.Id, post);
                addedSeed = true;
            }

            if (addedSeed)
            {
                continue;
            }

            foreach (var tile in post.TileArray)
            {
                foreach (var neighbor in tile.Neighbors)
                {
                    if (!_covered[neighbor.Id] || !neighbor.CreatureFits())
                    {
                        continue;
                    }

                    AssignSeedOwner(neighbor.Id, post);
                }
            }
        }
    }

    private bool IsBetterCandidate(MiningPost? candidateOwner, int candidateDistance, MiningPost? bestOwner, int bestDistance)
    {
        if (candidateOwner is null || candidateDistance == int.MaxValue)
        {
            return false;
        }

        if (bestOwner is null || bestDistance == int.MaxValue)
        {
            return true;
        }

        if (candidateDistance != bestDistance)
        {
            return candidateDistance < bestDistance;
        }

        return CompareOwners(candidateOwner, bestOwner) < 0;
    }

    private MiningPostOwnership ComputeOwnership(Tile tile)
    {
        if (!_covered[tile.Id])
        {
            return new MiningPostOwnership(null, int.MaxValue);
        }

        var bestOwner = _seedOwners[tile.Id];
        var bestDistance = bestOwner is null ? int.MaxValue : 0;

        foreach (var neighbor in tile.Neighbors)
        {
            if (!_covered[neighbor.Id])
            {
                continue;
            }

            var neighborOwner = _owners[neighbor.Id];
            var neighborDistance = _distances[neighbor.Id];
            if (!IsBetterCandidate(neighborOwner, neighborDistance + 1, bestOwner, bestDistance))
            {
                continue;
            }

            bestOwner = neighborOwner;
            bestDistance = neighborDistance + 1;
        }

        return new MiningPostOwnership(bestOwner, bestDistance);
    }

    private void EnqueueTile(Tile tile)
    {
        if (!_covered[tile.Id] || _queued[tile.Id])
        {
            return;
        }

        _queued[tile.Id] = true;
        _queue.Enqueue(tile.Id);
    }

    private void AddDirtyTile(Tile tile, ISet<int> adjacencyImpactedTiles)
    {
        EnqueueTile(tile);
        adjacencyImpactedTiles.Add(tile.Id);

        foreach (var neighbor in tile.Neighbors)
        {
            EnqueueTile(neighbor);
            adjacencyImpactedTiles.Add(neighbor.Id);
        }
    }

    private void RemoveTileAdjacencyContributions(int tileId)
    {
        var contributions = _tileAdjacencyEdges[tileId];
        if (contributions is null)
        {
            return;
        }

        foreach (var edge in contributions)
        {
            if (!_adjacencySupportCounts.TryGetValue(edge, out var count))
            {
                continue;
            }

            if (count <= 1)
            {
                _adjacencySupportCounts.Remove(edge);
            }
            else
            {
                _adjacencySupportCounts[edge] = count - 1;
            }
        }

        _tileAdjacencyEdges[tileId] = null;
    }

    private MiningPostAdjacencyEdge CreateEdge(MiningPost left, MiningPost right)
    {
        return CompareOwners(left, right) <= 0
            ? new MiningPostAdjacencyEdge(left, right)
            : new MiningPostAdjacencyEdge(right, left);
    }

    private void RefreshTileAdjacencyContributions(Tile tile)
    {
        RemoveTileAdjacencyContributions(tile.Id);

        if (!_covered[tile.Id])
        {
            return;
        }

        var owner = _owners[tile.Id];
        if (owner is null)
        {
            return;
        }

        HashSet<MiningPostAdjacencyEdge>? contributions = null;
        foreach (var neighbor in tile.Neighbors)
        {
            if (tile.Id >= neighbor.Id || !_covered[neighbor.Id])
            {
                continue;
            }

            var neighborOwner = _owners[neighbor.Id];
            if (neighborOwner is null || ReferenceEquals(owner, neighborOwner))
            {
                continue;
            }

            contributions ??= [];
            var edge = CreateEdge(owner, neighborOwner);
            if (!contributions.Add(edge))
            {
                continue;
            }

            _adjacencySupportCounts[edge] = _adjacencySupportCounts.GetValueOrDefault(edge, 0) + 1;
        }

        _tileAdjacencyEdges[tile.Id] = contributions;
    }

    private void RebuildAdjacencyLookup()
    {
        _adjacency.Clear();
        if (Cave is null)
        {
            return;
        }

        var activePosts = Cave.GetMiningPosts()
            .Where(IsActiveMiningPost)
            .ToHashSet();

        foreach (var post in activePosts)
        {
            _adjacency[post] = [];
        }

        foreach (var edge in _adjacencySupportCounts.Keys.ToArray())
        {
            if (!activePosts.Contains(edge.Left) || !activePosts.Contains(edge.Right))
            {
                _adjacencySupportCounts.Remove(edge);
                continue;
            }

            _adjacency[edge.Left].Add(edge.Right);
            _adjacency[edge.Right].Add(edge.Left);
        }
    }

    private void RebuildAdjacencyGraph()
    {
        Array.Clear(_tileAdjacencyEdges, 0, _tileAdjacencyEdges.Length);
        _adjacencySupportCounts.Clear();

        if (Cave is not null)
        {
            foreach (var tile in Cave.GetTiles())
            {
                RefreshTileAdjacencyContributions(tile);
            }
        }

        RebuildAdjacencyLookup();
    }

    private void RefreshAdjacencyGraph(IEnumerable<int> impactedTileIds)
    {
        if (Cave is null)
        {
            _adjacency.Clear();
            _adjacencySupportCounts.Clear();
            return;
        }

        foreach (var tileId in impactedTileIds.Distinct())
        {
            var tile = Cave.GetTileById(tileId);
            if (tile is null)
            {
                RemoveTileAdjacencyContributions(tileId);
                continue;
            }

            RefreshTileAdjacencyContributions(tile);
        }

        RebuildAdjacencyLookup();
    }

    private MiningPostOwnershipField CommitCurrentState()
    {
        _ownershipCacheDirty = true;
        ClearUpdates();
        return this;
    }

    public MiningPostOwnershipField Rebuild()
    {
        if (Cave is null)
        {
            OwnershipField = new Dictionary<string, MiningPostOwnership>(StringComparer.Ordinal);
            _ownershipCacheDirty = false;
            _coverageCount = 0;
            _adjacency.Clear();
            _adjacencySupportCounts.Clear();
            Array.Clear(_tileAdjacencyEdges, 0, _tileAdjacencyEdges.Length);
            ClearUpdates();
            return this;
        }

        RefreshCoverageState(resetValues: true);
        RebuildOwnerOrdering();
        RefreshSeedOwners();
        Array.Clear(_queued, 0, _queued.Length);
        _queue.Clear();

        foreach (var tile in Cave.GetTiles())
        {
            var seedOwner = _seedOwners[tile.Id];
            if (seedOwner is null || !_covered[tile.Id])
            {
                continue;
            }

            _owners[tile.Id] = seedOwner;
            _distances[tile.Id] = 0;
            EnqueueTile(tile);
        }

        while (_queue.Count > 0)
        {
            var currentId = _queue.Dequeue();
            _queued[currentId] = false;

            var currentTile = Cave.GetTileById(currentId);
            var currentOwner = currentTile is null ? null : _owners[currentId];
            if (currentTile is null || currentOwner is null)
            {
                continue;
            }

            var currentDistance = _distances[currentId];
            foreach (var neighbor in currentTile.Neighbors)
            {
                if (!_covered[neighbor.Id])
                {
                    continue;
                }

                var candidateDistance = currentDistance + 1;
                if (!IsBetterCandidate(currentOwner, candidateDistance, _owners[neighbor.Id], _distances[neighbor.Id]))
                {
                    continue;
                }

                _owners[neighbor.Id] = currentOwner;
                _distances[neighbor.Id] = candidateDistance;
                EnqueueTile(neighbor);
            }
        }

        RebuildAdjacencyGraph();
        return CommitCurrentState();
    }

    public MiningPostOwnershipField Rebalance(IEnumerable<string>? dirtyKeys = null)
    {
        if (Cave is null || _coverageCount == 0)
        {
            return Rebuild();
        }

        RefreshCoverageState(resetValues: false);
        RebuildOwnerOrdering();
        RefreshSeedOwners();
        Array.Clear(_queued, 0, _queued.Length);
        _queue.Clear();

        var hasDirty = false;
        var adjacencyImpactedTiles = new HashSet<int>();

        foreach (var dirtyKey in dirtyKeys ?? UpdatedTiles)
        {
            var tile = GetTile(dirtyKey);
            if (tile is null)
            {
                continue;
            }

            hasDirty = true;
            AddDirtyTile(tile, adjacencyImpactedTiles);
        }

        if (!hasDirty)
        {
            return Rebuild();
        }

        while (_queue.Count > 0)
        {
            var currentId = _queue.Dequeue();
            _queued[currentId] = false;

            var currentTile = Cave.GetTileById(currentId);
            if (currentTile is null)
            {
                continue;
            }

            var nextOwnership = ComputeOwnership(currentTile);
            var currentOwner = _owners[currentId];
            var currentDistance = _distances[currentId];
            if (ReferenceEquals(currentOwner, nextOwnership.Post) && currentDistance == nextOwnership.Distance)
            {
                continue;
            }

            _owners[currentId] = nextOwnership.Post;
            _distances[currentId] = nextOwnership.Distance;
            adjacencyImpactedTiles.Add(currentId);

            foreach (var neighbor in currentTile.Neighbors)
            {
                EnqueueTile(neighbor);
                adjacencyImpactedTiles.Add(neighbor.Id);
            }
        }

        RefreshAdjacencyGraph(adjacencyImpactedTiles);
        return CommitCurrentState();
    }

    public MiningPostOwnershipField Refresh()
    {
        if (_coverageCount == 0)
        {
            return Rebuild();
        }

        if (IsUpdated())
        {
            GetOwnershipField(false);
            return this;
        }

        return UpdatedTiles.Count == 0 ? Rebuild() : Rebalance();
    }
}
