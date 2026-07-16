using System.Threading;
using TriloGame.Game.Core.Pathfinding;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;

namespace TriloGame.Game.Runtime.Systems;

// Owns the dedicated worker that computes detached building-field replacements.
public sealed class BuildingBfsFieldMaintenanceSystem : IDisposable
{
    private readonly object _workerGate = new();
    private readonly AutoResetEvent _workSignal = new(false);
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Thread _workerThread;
    private BuildingBfsMaintenanceBatch? _queuedBatch;
    private BuildingBfsMaintenanceBatchResult? _completedResult;
    private bool _workerBusy;
    private bool _paused = true;

    public BuildingBfsFieldMaintenanceSystem()
    {
        _workerThread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "Building BFS Maintenance"
        };
        _workerThread.Start();
    }

    // Publish completed work only after the simulation has reached a safe tick boundary.
    public void Update(GameSession session, bool isPaused = false)
    {
        var cave = session.Cave;
        if (cave is null)
        {
            return;
        }

        cave.EnableAsyncBuildingBfsMaintenance();

        BuildingBfsMaintenanceBatch? batchToQueue = null;
        var signalWorker = false;
        BuildingBfsMaintenanceBatchResult? completedResult = null;
        lock (_workerGate)
        {
            completedResult = _completedResult;
            _completedResult = null;
            if (completedResult is not null)
            {
                _workerBusy = false;
            }

            _paused = isPaused;
        }

        if (completedResult is not null)
        {
            var completedBatch = TakeCompletedBatch();
            if (completedBatch is not null)
            {
                cave.PublishBuildingBfsMaintenanceBatch(completedBatch, completedResult);
            }
        }

        lock (_workerGate)
        {
            if (!_paused && _queuedBatch is not null)
            {
                signalWorker = true;
            }
            else if (!_paused && !_workerBusy)
            {
                batchToQueue = cave.TakeBuildingBfsMaintenanceBatch();
                if (batchToQueue is not null)
                {
                    _queuedBatch = batchToQueue;
                    _workerBusy = true;
                    signalWorker = true;
                }
            }
        }

        if (signalWorker)
        {
            _workSignal.Set();
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _workSignal.Set();
        _workerThread.Join(TimeSpan.FromSeconds(1));
        _workSignal.Dispose();
        _cancellation.Dispose();
    }

    private BuildingBfsMaintenanceBatch? TakeCompletedBatch()
    {
        lock (_workerGate)
        {
            var batch = _completedBatchForPublish;
            _completedBatchForPublish = null;
            return batch;
        }
    }

    private BuildingBfsMaintenanceBatch? _completedBatchForPublish;

    private void WorkerLoop()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            _workSignal.WaitOne(1);

            BuildingBfsMaintenanceBatch? batch;
            lock (_workerGate)
            {
                if (_paused || _queuedBatch is null)
                {
                    continue;
                }

                batch = _queuedBatch;
                _queuedBatch = null;
            }

            BuildingBfsMaintenanceBatchResult result;
            try
            {
                result = BuildingBfsFieldMaintenance.ComputeBatch(batch);
            }
            catch
            {
                lock (_workerGate)
                {
                    _queuedBatch = batch;
                    _paused = true;
                }

                continue;
            }

            lock (_workerGate)
            {
                _completedBatchForPublish = batch;
                _completedResult = result;
            }
        }
    }
}
