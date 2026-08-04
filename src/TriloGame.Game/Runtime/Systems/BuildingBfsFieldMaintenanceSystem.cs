using System.Collections.Concurrent;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;

namespace TriloGame.Game.Runtime.Systems;

// Owns the only background thread used for per-building traversal maintenance. The worker sees
// value snapshots only; all Cave/building access stays on the main thread in command creation and
// result publication.
public sealed class BuildingBfsFieldMaintenanceSystem : IBuildingNavigationFieldService, IDisposable
{
    private abstract record WorkerCommand(long SessionGeneration);

    private sealed record FullTopologyCommand(
        long SessionGeneration,
        BuildingNavigationTopologySnapshot Topology,
        IReadOnlyList<int> DirtyTileIds) : WorkerCommand(SessionGeneration);

    private sealed record TopologyDeltaCommand(
        long SessionGeneration,
        BuildingNavigationTopologyDelta Delta) : WorkerCommand(SessionGeneration);

    private sealed record ResetCommand(long SessionGeneration) : WorkerCommand(SessionGeneration);

    private sealed record ShutdownCommand() : WorkerCommand(long.MinValue);

    private sealed record CompletedField(long SessionGeneration, BuildingNavigationFieldSnapshot Snapshot);

    private sealed class TileMirror
    {
        public TileMirror(int id, string key, bool passable, bool reachable, IReadOnlyList<int> neighbors)
        {
            Id = id;
            Key = key;
            Passable = passable;
            Reachable = reachable;
            Neighbors = neighbors.ToArray();
        }

        public int Id { get; }

        public string Key { get; }

        public bool Passable { get; }

        public bool Reachable { get; }

        public int[] Neighbors { get; }
    }

    private sealed class WorkerTopology
    {
        private TileMirror?[] _tiles = [];
        private readonly Dictionary<string, int> _tileIdsByKey = new(StringComparer.Ordinal);

        public int Capacity => _tiles.Length;

        public TileMirror? GetTile(int id)
        {
            return id >= 0 && id < _tiles.Length ? _tiles[id] : null;
        }

        public void Apply(BuildingNavigationTopologySnapshot snapshot)
        {
            if (_tiles.Length < snapshot.TileCapacity)
            {
                Array.Resize(ref _tiles, snapshot.TileCapacity);
            }

            Array.Clear(_tiles, 0, _tiles.Length);
            _tileIdsByKey.Clear();
            foreach (var tile in snapshot.Tiles)
            {
                if (tile.Id < 0 || tile.Id >= _tiles.Length)
                {
                    continue;
                }

                _tiles[tile.Id] = new TileMirror(
                    tile.Id,
                    tile.Key,
                    tile.PassableForCreatures,
                    tile.Reachable,
                    tile.NeighborIds);
                _tileIdsByKey[tile.Key] = tile.Id;
            }
        }

        // Apply only changed tile records while retaining the rest of the worker-owned graph.
        public void Apply(BuildingNavigationTopologyDelta delta)
        {
            if (_tiles.Length < delta.TileCapacity)
            {
                Array.Resize(ref _tiles, delta.TileCapacity);
            }

            for (var index = 0; index < delta.RemovedTileKeys.Count; index++)
            {
                var key = delta.RemovedTileKeys[index];
                if (_tileIdsByKey.TryGetValue(key, out var removedId))
                {
                    if (removedId >= 0 && removedId < _tiles.Length)
                    {
                        _tiles[removedId] = null;
                    }

                    _tileIdsByKey.Remove(key);
                }
            }

            for (var index = 0; index < delta.TileUpdates.Count; index++)
            {
                var tile = delta.TileUpdates[index];
                if (tile.Id < 0 || tile.Id >= _tiles.Length)
                {
                    continue;
                }

                _tiles[tile.Id] = new TileMirror(
                    tile.Id,
                    tile.Key,
                    tile.PassableForCreatures,
                    tile.Reachable,
                    tile.NeighborIds);
                _tileIdsByKey[tile.Key] = tile.Id;
            }
        }
    }

    private sealed class WorkerFieldState
    {
        public WorkerFieldState(BuildingNavigationBuildingSnapshot description, int capacity)
        {
            RuntimeId = description.RuntimeId;
            PlacementOrder = description.PlacementOrder;
            SeedMode = description.SeedMode;
            Footprint = description.FootprintTileIds.ToHashSet();
            Seeds = description.SeedTileIds.ToHashSet();
            Distance = CreateDistanceBuffer(capacity);
            NextStep = CreateNextStepBuffer(capacity);
            PendingDirty = [];
            ChangedTiles = [];
            NeedsPublish = true;
        }

        public int RuntimeId { get; }

        public int PlacementOrder { get; set; }

        public BuildingNavigationSeedMode SeedMode { get; set; }

        public HashSet<int> Footprint { get; private set; }

        public HashSet<int> Seeds { get; private set; }

        public int[] Distance { get; private set; }

        public int[] NextStep { get; private set; }

        public HashSet<int> PendingDirty { get; }

        public HashSet<int> ChangedTiles { get; }

        public bool NeedsPublish { get; set; }

        public bool NeedsFullNextStepRebuild { get; set; } = true;

        public long Generation { get; set; }

        public long TopologyVersion { get; set; }

        public long ReachabilityVersion { get; set; }

        public void UpdateDescription(BuildingNavigationBuildingSnapshot description, int capacity)
        {
            var footprintChanged = !Footprint.SetEquals(description.FootprintTileIds);
            var seedsChanged = !Seeds.SetEquals(description.SeedTileIds);
            var modeChanged = SeedMode != description.SeedMode;
            PlacementOrder = description.PlacementOrder;
            SeedMode = description.SeedMode;
            Footprint = description.FootprintTileIds.ToHashSet();
            Seeds = description.SeedTileIds.ToHashSet();
            EnsureCapacity(capacity);
            if (footprintChanged || seedsChanged || modeChanged)
            {
                NeedsPublish = true;
            }
        }

        public void EnsureCapacity(int capacity)
        {
            if (Distance.Length >= capacity)
            {
                return;
            }

            var oldLength = Distance.Length;
            var distance = Distance;
            Array.Resize(ref distance, capacity);
            Array.Fill(distance, int.MaxValue, oldLength, capacity - oldLength);
            Distance = distance;
            var nextStep = NextStep;
            Array.Resize(ref nextStep, capacity);
            Array.Fill(nextStep, -1, oldLength, capacity - oldLength);
            NextStep = nextStep;
        }

        private static int[] CreateDistanceBuffer(int capacity)
        {
            var values = new int[capacity];
            Array.Fill(values, int.MaxValue);
            return values;
        }

        private static int[] CreateNextStepBuffer(int capacity)
        {
            var values = new int[capacity];
            Array.Fill(values, -1);
            return values;
        }
    }

    private readonly BlockingCollection<WorkerCommand> _commands = new();
    private readonly ConcurrentQueue<CompletedField> _completed = new();
    private readonly AutoResetEvent _completedSignal = new(false);
    private readonly Thread _workerThread;
    private readonly object _sessionGate = new();
    private readonly Dictionary<int, BuildingNavigationFieldSnapshot> _published = [];
    private GameSession? _session;
    private Cave? _cave;
    private long _sessionGeneration;
    private bool _disposed;

    private int _asyncJobsProcessed;
    private int _incrementalRepairsProcessed;
    private int _fallbackRebuildCount;
    private int _discardedStaleResults;
    private int _inheritedScaffoldFieldCount;

    public BuildingBfsFieldMaintenanceSystem()
    {
        _workerThread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "Building BFS Field Maintenance"
        };
        _workerThread.Start();
    }

    public bool IsAttached
    {
        get
        {
            lock (_sessionGate)
            {
                return _session is not null;
            }
        }
    }

    public int AsyncJobsProcessed => Volatile.Read(ref _asyncJobsProcessed);

    public int IncrementalRepairsProcessed => Volatile.Read(ref _incrementalRepairsProcessed);

    public int FallbackRebuildCount => Volatile.Read(ref _fallbackRebuildCount);

    public int DiscardedStaleResults => Volatile.Read(ref _discardedStaleResults);

    public int InheritedScaffoldFieldCount => Volatile.Read(ref _inheritedScaffoldFieldCount);

    public bool WaitForPublishedResult(TimeSpan timeout)
    {
        return _completedSignal.WaitOne(timeout);
    }

    public void Attach(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Detach();

        var cave = session.Cave ?? throw new InvalidOperationException("A session must have a cave before navigation maintenance can attach.");
        lock (_sessionGate)
        {
            _sessionGeneration++;
            _session = session;
            _cave = cave;
            _published.Clear();
            session.BuildingNavigationFieldService = this;
            cave.NavigationTopologyChanged += HandleTopologyChanged;
            EnqueueInitialTopology(cave);
        }
    }

    public void Detach()
    {
        GameSession? session;
        Cave? cave;
        long generation;
        lock (_sessionGate)
        {
            session = _session;
            cave = _cave;
            if (session is null)
            {
                return;
            }

            generation = ++_sessionGeneration;
            _session = null;
            _cave = null;
            _published.Clear();
            cave!.NavigationTopologyChanged -= HandleTopologyChanged;
            if (ReferenceEquals(session.BuildingNavigationFieldService, this))
            {
                session.BuildingNavigationFieldService = null;
            }
        }

        _commands.Add(new ResetCommand(generation));
    }

    // Main-thread pump point. It never waits for the worker and only publishes results for the
    // currently attached session and still-live building instance.
    public int PumpCompleted()
    {
        var publishedCount = 0;
        GameSession? session;
        Cave? cave;
        long generation;
        lock (_sessionGate)
        {
            session = _session;
            cave = _cave;
            generation = _sessionGeneration;
        }

        if (session is null || cave is null)
        {
            while (_completed.TryDequeue(out _))
            {
                Interlocked.Increment(ref _discardedStaleResults);
            }

            return 0;
        }

        while (_completed.TryDequeue(out var completed))
        {
            if (completed.SessionGeneration != generation)
            {
                Interlocked.Increment(ref _discardedStaleResults);
                continue;
            }

            Building? building = null;
            var buildings = cave.GetBuildingList();
            for (var index = 0; index < buildings.Count; index++)
            {
                if (buildings[index].RuntimeId == completed.Snapshot.BuildingRuntimeId)
                {
                    building = buildings[index];
                    break;
                }
            }

            if (building is null ||
                !building.MaintainsNavigationField ||
                building.NavigationFieldMaintenanceMode != BuildingNavigationMaintenanceMode.Asynchronous)
            {
                Interlocked.Increment(ref _discardedStaleResults);
                continue;
            }

            _published[building.RuntimeId] = completed.Snapshot;
            building.PublishNavigationField(completed.Snapshot);
            publishedCount++;
        }

        return publishedCount;
    }

    public IReadOnlyDictionary<int, BuildingNavigationFieldSnapshot> GetPublishedSnapshots()
    {
        lock (_sessionGate)
        {
            return new Dictionary<int, BuildingNavigationFieldSnapshot>(_published);
        }
    }

    public void Dispose()
    {
        lock (_sessionGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        Detach();
        _commands.Add(new ShutdownCommand());
        _workerThread.Join();
        _completedSignal.Dispose();
        _commands.Dispose();
    }

    private void HandleTopologyChanged(BuildingNavigationTopologyChange change)
    {
        lock (_sessionGate)
        {
            if (_session is null || _cave is null || _disposed)
            {
                return;
            }

            var delta = _cave.CreateBuildingNavigationTopologyDelta(
                change.DirtyTileKeys,
                change.StructuralChange);
            _commands.Add(new TopologyDeltaCommand(_sessionGeneration, delta));
        }
    }

    private void EnqueueInitialTopology(Cave cave)
    {
        var topology = cave.CreateBuildingNavigationTopologySnapshot();
        var dirtyIds = topology.Tiles.Select(tile => tile.Id).ToArray();

        _commands.Add(new FullTopologyCommand(_sessionGeneration, topology, dirtyIds));
    }

    private void WorkerLoop()
    {
        var topology = new WorkerTopology();
        var states = new Dictionary<int, WorkerFieldState>();
        var activeGeneration = 0L;

        foreach (var firstCommand in _commands.GetConsumingEnumerable())
        {
            if (firstCommand is ShutdownCommand)
            {
                return;
            }

            var batch = new List<WorkerCommand> { firstCommand };
            while (_commands.TryTake(out var nextCommand))
            {
                if (nextCommand is ShutdownCommand)
                {
                    return;
                }

                batch.Add(nextCommand);
            }

            foreach (var command in batch)
            {
                if (command.SessionGeneration < activeGeneration)
                {
                    continue;
                }

                if (command.SessionGeneration > activeGeneration)
                {
                    activeGeneration = command.SessionGeneration;
                    states.Clear();
                }

                switch (command)
                {
                    case ResetCommand:
                        states.Clear();
                        break;
                    case FullTopologyCommand fullTopologyCommand:
                        ApplyFullTopologyCommand(topology, states, fullTopologyCommand);
                        break;
                    case TopologyDeltaCommand topologyDeltaCommand:
                        ApplyTopologyDeltaCommand(topology, states, topologyDeltaCommand);
                        break;
                }
            }

            var orderedStates = states.Values.OrderBy(state => state.PlacementOrder).ThenBy(state => state.RuntimeId).ToArray();
            for (var index = 0; index < orderedStates.Length; index++)
            {
                if (_commands.Count > 0)
                {
                    break;
                }

                ProcessField(topology, orderedStates[index], activeGeneration);
            }
        }
    }

    private void ApplyFullTopologyCommand(
        WorkerTopology topology,
        Dictionary<int, WorkerFieldState> states,
        FullTopologyCommand command)
    {
        topology.Apply(command.Topology);
        ApplyBuildingDescriptions(
            topology,
            states,
            command.Topology.Buildings,
            command.DirtyTileIds,
            command.Topology.TopologyVersion,
            command.Topology.ReachabilityVersion);
    }

    private void ApplyTopologyDeltaCommand(
        WorkerTopology topology,
        Dictionary<int, WorkerFieldState> states,
        TopologyDeltaCommand command)
    {
        var delta = command.Delta;
        topology.Apply(delta);
        if (delta.HasBuildingChanges)
        {
            ApplyBuildingDescriptions(
                topology,
                states,
                delta.BuildingUpdates,
                delta.DirtyTileIds,
                delta.TopologyVersion,
                delta.ReachabilityVersion);
            return;
        }

        foreach (var state in states.Values)
        {
            state.EnsureCapacity(topology.Capacity);
            state.TopologyVersion = delta.TopologyVersion;
            state.ReachabilityVersion = delta.ReachabilityVersion;
            for (var dirtyIndex = 0; dirtyIndex < delta.DirtyTileIds.Count; dirtyIndex++)
            {
                AddDirtyTileAndNeighbors(topology, state, delta.DirtyTileIds[dirtyIndex]);
            }
        }
    }

    private void ApplyBuildingDescriptions(
        WorkerTopology topology,
        Dictionary<int, WorkerFieldState> states,
        IReadOnlyList<BuildingNavigationBuildingSnapshot> activeDescriptions,
        IReadOnlyList<int> dirtyTileIds,
        long topologyVersion,
        long reachabilityVersion)
    {
        var activeIds = new HashSet<int>();
        for (var index = 0; index < activeDescriptions.Count; index++)
        {
            var description = activeDescriptions[index];
            activeIds.Add(description.RuntimeId);
            if (!states.TryGetValue(description.RuntimeId, out var state))
            {
                state = new WorkerFieldState(description, topology.Capacity);
                state.TopologyVersion = topologyVersion;
                state.ReachabilityVersion = reachabilityVersion;
                states[description.RuntimeId] = state;
                var inherited = TryAdoptInheritedField(state, description, topology);
                if (!inherited)
                {
                    BuildInitialField(topology, state);
                }

                if (inherited)
                {
                    for (var dirtyIndex = 0; dirtyIndex < dirtyTileIds.Count; dirtyIndex++)
                    {
                        AddDirtyTileAndNeighbors(topology, state, dirtyTileIds[dirtyIndex]);
                    }
                }

                continue;
            }

            var oldFootprint = state.Footprint.ToArray();
            var oldSeeds = state.Seeds.ToArray();
            var oldMode = state.SeedMode;
            state.UpdateDescription(description, topology.Capacity);
            if (oldMode != state.SeedMode || !state.Footprint.SetEquals(oldFootprint) || !state.Seeds.SetEquals(oldSeeds))
            {
                AddRepairRing(topology, state, state.Footprint);
                AddRepairRing(topology, state, state.Seeds);
            }

            for (var dirtyIndex = 0; dirtyIndex < dirtyTileIds.Count; dirtyIndex++)
            {
                AddDirtyTileAndNeighbors(topology, state, dirtyTileIds[dirtyIndex]);
            }

            state.TopologyVersion = topologyVersion;
            state.ReachabilityVersion = reachabilityVersion;
        }

        foreach (var stateId in states.Keys.ToArray())
        {
            if (!activeIds.Contains(stateId))
            {
                states.Remove(stateId);
            }
        }
    }

    private bool TryAdoptInheritedField(
        WorkerFieldState state,
        BuildingNavigationBuildingSnapshot description,
        WorkerTopology topology)
    {
        var inherited = description.InheritedField;
        if (inherited is null || !inherited.HasMatchingFootprint(CreateComparableSnapshot(description)))
        {
            if (inherited is not null)
            {
                Interlocked.Increment(ref _fallbackRebuildCount);
            }

            return false;
        }

        state.EnsureCapacity(topology.Capacity);
        Array.Fill(state.Distance, int.MaxValue);
        Array.Fill(state.NextStep, -1);
        for (var index = 0; index < inherited.DistanceByTileId.Count && index < state.Distance.Length; index++)
        {
            state.Distance[index] = inherited.DistanceByTileId[index];
        }

        for (var index = 0; index < inherited.NextStepTileIdByTileId.Count && index < state.NextStep.Length; index++)
        {
            state.NextStep[index] = inherited.NextStepTileIdByTileId[index];
        }

        state.NeedsFullNextStepRebuild = false;
        state.NeedsPublish = true;
        state.TopologyVersion = inherited.TopologyVersion;
        state.ReachabilityVersion = inherited.ReachabilityVersion;
        Interlocked.Increment(ref _inheritedScaffoldFieldCount);

        if (inherited.SeedMode != description.SeedMode ||
            !inherited.SeedTileIds.SequenceEqual(description.SeedTileIds))
        {
            AddRepairRing(topology, state, state.Footprint);
            AddRepairRing(topology, state, state.Seeds);
            Interlocked.Increment(ref _incrementalRepairsProcessed);
        }

        return true;
    }

    private static BuildingNavigationFieldSnapshot CreateComparableSnapshot(BuildingNavigationBuildingSnapshot description)
    {
        return new BuildingNavigationFieldSnapshot(
            description.RuntimeId,
            0,
            0,
            0,
            description.SeedMode,
            [],
            [],
            [],
            description.FootprintTileIds,
            description.SeedTileIds);
    }

    private void ProcessField(WorkerTopology topology, WorkerFieldState state, long sessionGeneration)
    {
        if (!state.NeedsPublish && state.PendingDirty.Count == 0)
        {
            return;
        }

        if (state.PendingDirty.Count > 0)
        {
            RepairIncrementally(topology, state);
            Interlocked.Increment(ref _incrementalRepairsProcessed);
        }

        // A state can receive another topology command before its first publish. Build every
        // successor in that case; an incremental refresh only covers tiles whose distance changed.
        if (state.NeedsFullNextStepRebuild)
        {
            RebuildNextSteps(topology, state);
            state.NeedsFullNextStepRebuild = false;
        }
        else if (state.PendingDirty.Count > 0)
        {
            RefreshNextStepsIncrementally(topology, state);
        }
        state.Generation++;
        state.NeedsPublish = false;
        state.PendingDirty.Clear();
        var passable = new bool[state.Distance.Length];
        for (var tileId = 0; tileId < passable.Length; tileId++)
        {
            passable[tileId] = IsTraversable(topology, tileId);
        }

        var snapshot = new BuildingNavigationFieldSnapshot(
            state.RuntimeId,
            state.Generation,
            state.TopologyVersion,
            state.ReachabilityVersion,
            state.SeedMode,
            state.Distance,
            state.NextStep,
            passable,
            state.Footprint.OrderBy(id => id).ToArray(),
            state.Seeds.OrderBy(id => id).ToArray());
        _completed.Enqueue(new CompletedField(sessionGeneration, snapshot));
        _completedSignal.Set();
        Interlocked.Increment(ref _asyncJobsProcessed);
    }

    private static void BuildInitialField(WorkerTopology topology, WorkerFieldState state)
    {
        Array.Fill(state.Distance, int.MaxValue);
        Array.Fill(state.NextStep, -1);
        var queue = new Queue<int>();
        foreach (var seedId in state.Seeds.OrderBy(id => id))
        {
            if (!IsTraversable(topology, seedId) || state.Distance[seedId] == 0)
            {
                continue;
            }

            state.Distance[seedId] = 0;
            queue.Enqueue(seedId);
        }

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var currentValue = state.Distance[currentId];
            var tile = topology.GetTile(currentId);
            if (tile is null)
            {
                continue;
            }

            for (var index = 0; index < tile.Neighbors.Length; index++)
            {
                var neighborId = tile.Neighbors[index];
                if (!IsTraversable(topology, neighborId) || currentValue == int.MaxValue)
                {
                    continue;
                }

                var nextValue = currentValue + 1;
                if (nextValue >= state.Distance[neighborId])
                {
                    continue;
                }

                state.Distance[neighborId] = nextValue;
                queue.Enqueue(neighborId);
            }
        }

        state.NeedsPublish = true;
    }

    private static void RepairIncrementally(WorkerTopology topology, WorkerFieldState state)
    {
        var touched = state.PendingDirty.ToArray();
        state.ChangedTiles.Clear();
        var invalidationQueue = new Queue<int>();
        var queued = new HashSet<int>();
        for (var index = 0; index < touched.Length; index++)
        {
            Enqueue(invalidationQueue, queued, touched[index]);
            var tile = topology.GetTile(touched[index]);
            if (tile is null)
            {
                continue;
            }

            for (var neighborIndex = 0; neighborIndex < tile.Neighbors.Length; neighborIndex++)
            {
                Enqueue(invalidationQueue, queued, tile.Neighbors[neighborIndex]);
            }
        }

        // Invalidate first so a closed chokepoint cannot preserve an optimistic cyclic distance.
        while (invalidationQueue.Count > 0)
        {
            var tileId = invalidationQueue.Dequeue();
            var tile = topology.GetTile(tileId);
            if (tile is null || tileId >= state.Distance.Length || IsCurrentDistanceValid(topology, state, tileId))
            {
                continue;
            }

            if (state.Distance[tileId] == int.MaxValue)
            {
                continue;
            }

            state.Distance[tileId] = int.MaxValue;
            state.ChangedTiles.Add(tileId);
            for (var index = 0; index < tile.Neighbors.Length; index++)
            {
                Enqueue(invalidationQueue, queued, tile.Neighbors[index]);
            }
        }

        var repairQueue = new Queue<int>();
        queued.Clear();
        for (var index = 0; index < touched.Length; index++)
        {
            Enqueue(repairQueue, queued, touched[index]);
            var tile = topology.GetTile(touched[index]);
            if (tile is null)
            {
                continue;
            }

            for (var neighborIndex = 0; neighborIndex < tile.Neighbors.Length; neighborIndex++)
            {
                Enqueue(repairQueue, queued, tile.Neighbors[neighborIndex]);
            }
        }

        while (repairQueue.Count > 0)
        {
            var tileId = repairQueue.Dequeue();
            if (tileId < 0 || tileId >= state.Distance.Length)
            {
                continue;
            }

            var candidate = ComputeCandidate(topology, state, tileId);
            if (candidate >= state.Distance[tileId])
            {
                continue;
            }

            state.Distance[tileId] = candidate;
            state.ChangedTiles.Add(tileId);
            var tile = topology.GetTile(tileId);
            if (tile is null)
            {
                continue;
            }

            for (var index = 0; index < tile.Neighbors.Length; index++)
            {
                Enqueue(repairQueue, queued, tile.Neighbors[index]);
            }
        }
    }

    private static bool IsCurrentDistanceValid(WorkerTopology topology, WorkerFieldState state, int tileId)
    {
        var current = state.Distance[tileId];
        if (!IsTraversable(topology, tileId))
        {
            return current == int.MaxValue;
        }

        if (state.Seeds.Contains(tileId))
        {
            return current == 0;
        }

        if (current == int.MaxValue)
        {
            return true;
        }

        var tile = topology.GetTile(tileId);
        if (tile is null)
        {
            return false;
        }

        var requiredNeighborValue = current - 1;
        for (var index = 0; index < tile.Neighbors.Length; index++)
        {
            var neighborId = tile.Neighbors[index];
            if (IsTraversable(topology, neighborId) && state.Distance[neighborId] == requiredNeighborValue)
            {
                return true;
            }
        }

        return false;
    }

    private static int ComputeCandidate(WorkerTopology topology, WorkerFieldState state, int tileId)
    {
        if (!IsTraversable(topology, tileId))
        {
            return int.MaxValue;
        }

        if (state.Seeds.Contains(tileId))
        {
            return 0;
        }

        var tile = topology.GetTile(tileId);
        if (tile is null)
        {
            return int.MaxValue;
        }

        var best = int.MaxValue;
        for (var index = 0; index < tile.Neighbors.Length; index++)
        {
            var neighborId = tile.Neighbors[index];
            if (!IsTraversable(topology, neighborId))
            {
                continue;
            }

            var neighborValue = state.Distance[neighborId];
            if (neighborValue < best)
            {
                best = neighborValue;
            }
        }

        return best == int.MaxValue ? int.MaxValue : best + 1;
    }

    private static void RebuildNextSteps(WorkerTopology topology, WorkerFieldState state)
    {
        Array.Fill(state.NextStep, -1);
        for (var tileId = 0; tileId < state.Distance.Length; tileId++)
        {
            var currentValue = state.Distance[tileId];
            if (!IsTraversable(topology, tileId) || currentValue == int.MaxValue || currentValue == 0)
            {
                continue;
            }

            var tile = topology.GetTile(tileId);
            if (tile is null)
            {
                continue;
            }

            var bestId = -1;
            var bestValue = currentValue;
            for (var index = 0; index < tile.Neighbors.Length; index++)
            {
                var neighborId = tile.Neighbors[index];
                if (!IsTraversable(topology, neighborId))
                {
                    continue;
                }

                var neighborValue = state.Distance[neighborId];
                if (neighborValue >= bestValue || neighborValue == int.MaxValue)
                {
                    continue;
                }

                if (bestId < 0 || neighborValue < bestValue ||
                    string.CompareOrdinal(topology.GetTile(neighborId)!.Key, topology.GetTile(bestId)!.Key) < 0)
                {
                    bestId = neighborId;
                    bestValue = neighborValue;
                }
            }

            state.NextStep[tileId] = bestId;
        }
    }

    // Only changed tiles and their immediate predecessors need new successors after an incremental repair.
    private static void RefreshNextStepsIncrementally(WorkerTopology topology, WorkerFieldState state)
    {
        var affected = new HashSet<int>();
        foreach (var changedTileId in state.ChangedTiles)
        {
            affected.Add(changedTileId);
            var tile = topology.GetTile(changedTileId);
            if (tile is null)
            {
                continue;
            }

            for (var index = 0; index < tile.Neighbors.Length; index++)
            {
                affected.Add(tile.Neighbors[index]);
            }
        }

        foreach (var tileId in affected)
        {
            RefreshNextStep(topology, state, tileId);
        }
    }

    private static void RefreshNextStep(WorkerTopology topology, WorkerFieldState state, int tileId)
    {
        if (tileId < 0 || tileId >= state.Distance.Length)
        {
            return;
        }

        var currentValue = state.Distance[tileId];
        if (!IsTraversable(topology, tileId) || currentValue == int.MaxValue || currentValue == 0)
        {
            state.NextStep[tileId] = -1;
            return;
        }

        var tile = topology.GetTile(tileId);
        if (tile is null)
        {
            state.NextStep[tileId] = -1;
            return;
        }

        var bestId = -1;
        var bestValue = currentValue;
        for (var index = 0; index < tile.Neighbors.Length; index++)
        {
            var neighborId = tile.Neighbors[index];
            if (!IsTraversable(topology, neighborId))
            {
                continue;
            }

            var neighborValue = state.Distance[neighborId];
            if (neighborValue >= bestValue || neighborValue == int.MaxValue)
            {
                continue;
            }

            if (bestId < 0 || neighborValue < bestValue ||
                string.CompareOrdinal(topology.GetTile(neighborId)!.Key, topology.GetTile(bestId)!.Key) < 0)
            {
                bestId = neighborId;
                bestValue = neighborValue;
            }
        }

        state.NextStep[tileId] = bestId;
    }

    private static bool IsTraversable(WorkerTopology topology, int tileId)
    {
        var tile = topology.GetTile(tileId);
        return tile is not null && tile.Passable && tile.Reachable;
    }

    private static void AddRepairRing(WorkerTopology topology, WorkerFieldState state, IEnumerable<int> ids)
    {
        foreach (var id in ids)
        {
            AddDirtyTileAndNeighbors(topology, state, id);
        }
    }

    private static void AddDirtyTileAndNeighbors(WorkerTopology topology, WorkerFieldState state, int tileId)
    {
        if (tileId < 0)
        {
            return;
        }

        state.PendingDirty.Add(tileId);
        var tile = topology.GetTile(tileId);
        if (tile is null)
        {
            return;
        }

        for (var index = 0; index < tile.Neighbors.Length; index++)
        {
            state.PendingDirty.Add(tile.Neighbors[index]);
        }
    }

    private static void Enqueue(Queue<int> queue, ISet<int> queued, int tileId)
    {
        if (tileId >= 0 && queued.Add(tileId))
        {
            queue.Enqueue(tileId);
        }
    }
}
