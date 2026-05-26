# Actor System Benchmark Results

**Date:** 2026-05-11 13:23:26

---

## Test 1: Sequential Ping-Pong

*Ping/pong pairs exchange messages sequentially. Only 2 actors are active at any time. Measures the speed of a sequential message chain.*

| Metric | Value |
|--------|-------|
| **Configuration** | 3000 pairs, 5000 messages per pair |
| **Total actors** | 6000 |
| **Total messages** | 30,000,000 |

### Memory & Threads

| Stage | Current Memory | Peak Memory | Allocated | Thread Pool | Total Threads | Max Pool |
|-------|---------------|-------------|-----------|-------------|---------------|----------|
| After creation | 35.6 MB | 35.6 MB | 8.8 MB | 0 | 8 | 0 |
| After load | 54.5 MB | 54.5 MB | 2755.9 MB | 1 | 31 | 1 |
| After shutdown | 55.8 MB | 55.8 MB | 2759.7 MB | 1 | 31 | 1 |

### Performance Results

| Metric | Value |
|--------|-------|
| Total messages | 30,000,000 |
| Ping undelivered | 0 |
| Pong undelivered | 0 |
| Execution time | 12,659 ms |
| **Time per message** | **0.42 μs** |
| **Messages per second** | **2,369,855** |

### Timing

| Operation | Time |
|-----------|------|
| Creation | 15 ms |
| Shutdown | 223 ms |

### Garbage Collection

| Generation | Collections |
|------------|-------------|
| Gen0 | 313 |
| Gen1 | 155 |
| Gen2 | 5 |
| **Total allocated** | **2760.5 MB** |

---

## Test 2: Parallel Pipeline of Independent Pairs

*All pairs work simultaneously. Producer waits for response before next request. All actors are active, all cores are loaded.*

| Metric | Value |
|--------|-------|
| **Configuration** | 3000 pairs, 5000 messages per pair |
| **Total actors** | 6000 |
| **Total messages** | 30,000,000 |

### Memory & Threads

| Stage | Current Memory | Peak Memory | Allocated | Thread Pool | Total Threads | Max Pool |
|-------|---------------|-------------|-----------|-------------|---------------|----------|
| After creation & startup | 49.9 MB | 49.9 MB | 9.1 MB | 12 | 31 | 12 |
| After load | 55.4 MB | 55.4 MB | 2755.4 MB | 1 | 31 | 12 |
| After shutdown | 55.6 MB | 55.6 MB | 2759.4 MB | 1 | 31 | 12 |

### Performance Results

| Metric | Value |
|--------|-------|
| Total messages | 30,000,000 |
| Requests processed | 15,000,000 |
| Request undelivered | 0 |
| Response undelivered | 0 |
| Execution time | 13,456 ms |
| **Time per message** | **0.45 μs** |
| **Messages per second** | **2,229,489** |

### Timing

| Operation | Time |
|-----------|------|
| Creation | 9 ms |
| Shutdown | 180 ms |

### Garbage Collection

| Generation | Collections |
|------------|-------------|
| Gen0 | 626 |
| Gen1 | 309 |
| Gen2 | 9 |
| **Total allocated** | **5519.9 MB** |

---

## Test 3: Deep Pipeline

*Fewer pairs, but more messages per pair. Reduced thread pool contention. Tests the hypothesis about context switching overhead.*

| Metric | Value |
|--------|-------|
| **Configuration** | 100 pairs, 150,000 messages per pair |
| **Total actors** | 200 |
| **Total messages** | 30,000,000 |

### Memory & Threads

| Stage | Current Memory | Peak Memory | Allocated | Thread Pool | Total Threads | Max Pool |
|-------|---------------|-------------|-----------|-------------|---------------|----------|
| After creation & startup | 41.7 MB | 41.7 MB | 1.1 MB | 12 | 31 | 12 |
| After load | 46.8 MB | 46.8 MB | 2746.9 MB | 1 | 28 | 12 |
| After shutdown | 46.8 MB | 46.8 MB | 2747.0 MB | 1 | 28 | 12 |

### Performance Results

| Metric | Value |
|--------|-------|
| Total messages | 30,000,000 |
| Requests processed | 15,000,000 |
| Request undelivered | 0 |
| Response undelivered | 0 |
| Execution time | 10,173 ms |
| **Time per message** | **0.34 μs** |
| **Messages per second** | **2,948,983** |

### Timing

| Operation | Time |
|-----------|------|
| Creation | 0 ms |
| Shutdown | 0 ms |

### Garbage Collection

| Generation | Collections |
|------------|-------------|
| Gen0 | 937 |
| Gen1 | 313 |
| Gen2 | 12 |
| **Total allocated** | **8266.8 MB** |

---

## Summary

| Test | Configuration | Messages/sec | Time per message | Total time |
|------|---------------|--------------|-----------------|------------|
| **Test 1** | 3000 pairs × 5000 msgs | 2,369,855 | 0.42 μs | 12,659 ms |
| **Test 2** | 3000 pairs × 5000 msgs (parallel) | 2,229,489 | 0.45 μs | 13,456 ms |
| **Test 3** | 100 pairs × 150,000 msgs | **2,948,983** | **0.34 μs** | **10,173 ms** |

### Key Observations

- **Best performance:** Test 3 (Deep Pipeline) with **~2.95 million messages/second**
- **Minimum latency:** **0.34 μs** per message
- **No message loss** in any test (0 undelivered)
- **Lower thread contention** improves performance significantly (Test 3 vs Test 2)
