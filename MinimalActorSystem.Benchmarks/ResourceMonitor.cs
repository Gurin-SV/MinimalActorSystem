using System.Diagnostics;

namespace MinimalActorSystem.Benchmarks;

public sealed class ResourceMonitor
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly long _totalAllocated;
    private long _peakMemory;
    private int _maxThreads;

    public ResourceMonitor()
    {
        _process.Refresh();
        _peakMemory = _process.WorkingSet64;
        GC.Collect(2, GCCollectionMode.Forced, true);
        _totalAllocated = GC.GetTotalAllocatedBytes(true);
    }

    public void Snapshot(out long currentMemory, out long peakMemory, out int threadCount, out int pendingThreads, out long allocatedBytes)
    {
        _process.Refresh();
        currentMemory = _process.WorkingSet64;
        if (currentMemory > _peakMemory)
            _peakMemory = currentMemory;
        peakMemory = _peakMemory;
        ThreadPool.GetAvailableThreads(out var availableWorker, out _);
        ThreadPool.GetMaxThreads(out var maxWorker, out _);
        threadCount = maxWorker - availableWorker;
        pendingThreads = _process.Threads.Count;
        if (threadCount > _maxThreads)
            _maxThreads = threadCount;

        allocatedBytes = GC.GetTotalAllocatedBytes(false) - _totalAllocated;
    }

    public void PrintStats(string label, long currentMemory, long peakMemory, int threadCount, int pendingThreads, long allocatedBytes)
    {
        Console.WriteLine();
        Console.WriteLine($"--- {label} ---");
        Console.WriteLine($"  Память текущая:     {currentMemory / 1024.0 / 1024.0:F1} MB");
        Console.WriteLine($"  Память пиковая:     {peakMemory / 1024.0 / 1024.0:F1} MB");
        Console.WriteLine($"  Выделено с начала:  {allocatedBytes / 1024.0 / 1024.0:F1} MB");
        Console.WriteLine($"  Потоков в пуле:     {threadCount}");
        Console.WriteLine($"  Всего потоков:      {pendingThreads}");
        Console.WriteLine($"  Макс потоков пула:  {_maxThreads}");
    }

    public static void PrintGcStats()
    {
        Console.WriteLine();
        Console.WriteLine("--- Сборка мусора ---");
        for (int i = 0; i <= GC.MaxGeneration; i++)
        {
            Console.WriteLine($"  Gen{i}: {GC.CollectionCount(i)} сборок");
        }
        Console.WriteLine($"  Всего выделено: {GC.GetTotalAllocatedBytes(false) / 1024.0 / 1024.0:F1} MB");
    }
}
