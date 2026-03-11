namespace ObjLoader.Systems.Physics;

internal sealed class PhysicsThreadPool : IDisposable
{
    private readonly Thread[] _workers;
    private readonly ManualResetEventSlim[] _workerGates;
    private readonly ManualResetEventSlim _completionGate;
    private readonly int _workerCount;
    private volatile bool _disposed;

    private Action<int>? _currentWork;
    private int _totalItems;
    private int _nextItem;
    private int _completedWorkers;

    public PhysicsThreadPool()
    {
        _workerCount = Math.Max(1, Environment.ProcessorCount - 1);
        _workers = new Thread[_workerCount];
        _workerGates = new ManualResetEventSlim[_workerCount];
        _completionGate = new ManualResetEventSlim(false);

        for (int i = 0; i < _workerCount; i++)
        {
            _workerGates[i] = new ManualResetEventSlim(false);
            int workerIndex = i;
            _workers[i] = new Thread(() => WorkerLoop(workerIndex))
            {
                IsBackground = true,
                Name = $"PhysicsWorker_{i}",
                Priority = ThreadPriority.AboveNormal
            };
            _workers[i].Start();
        }
    }

    public void Dispatch(int itemCount, Action<int> work)
    {
        if (itemCount <= 0 || _disposed) return;

        _currentWork = work;
        _totalItems = itemCount;
        _nextItem = 0;
        _completedWorkers = 0;
        _completionGate.Reset();

        int workersToUse = Math.Min(_workerCount, itemCount);

        for (int i = 0; i < workersToUse; i++)
        {
            _workerGates[i].Set();
        }

        int item;
        while ((item = Interlocked.Increment(ref _nextItem) - 1) < _totalItems)
        {
            work(item);
        }

        if (Interlocked.Add(ref _completedWorkers, 1) < workersToUse + 1)
        {
            _completionGate.Wait();
        }

        _currentWork = null;
    }

    private void WorkerLoop(int workerIndex)
    {
        var gate = _workerGates[workerIndex];

        while (!_disposed)
        {
            gate.Wait();
            if (_disposed) break;
            gate.Reset();

            var work = _currentWork;
            if (work == null) continue;

            int item;
            while ((item = Interlocked.Increment(ref _nextItem) - 1) < _totalItems)
            {
                work(item);
            }

            int workersToUse = Math.Min(_workerCount, _totalItems);
            if (Interlocked.Add(ref _completedWorkers, 1) >= workersToUse + 1)
            {
                _completionGate.Set();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        for (int i = 0; i < _workerCount; i++)
        {
            _workerGates[i].Set();
        }

        for (int i = 0; i < _workerCount; i++)
        {
            _workers[i].Join(500);
        }

        for (int i = 0; i < _workerCount; i++)
        {
            _workerGates[i].Dispose();
        }
        _completionGate.Dispose();
    }
}