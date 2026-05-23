11.05.2026 13:23:26,43
============================================================
Test 1: Sequential Ping-Pong
------------------------------------------------------------
Ping/pong pairs exchange messages sequentially. Only 2 actors are active at any time. Measures the speed of a sequential message chain.
============================================================

Configuration: 3000 pairs, 5000 messages per pair
Total actors: 6000, total messages: 30,000,000

Creating 6000 actors...

--- After creation ---
  Current memory:     35.6 MB
  Peak memory:        35.6 MB
  Allocated so far:   8.8 MB
  Thread pool threads: 0
  Total threads:      8
  Max pool threads:   0
  Creation time: 15 ms

Sending start messages to 3000 actors...
Waiting for completion...

--- After load ---
  Current memory:     54.5 MB
  Peak memory:        54.5 MB
  Allocated so far:   2755.9 MB
  Thread pool threads: 1
  Total threads:      31
  Max pool threads:   1

=== Performance Results ===
  Total messages:       30,000,000
  Ping undelivered:     0
  Pong undelivered:     0
  Execution time:       12,659 ms
  Time per message:     0.42 μs
  Messages per second:  2,369,855

Shutting down system...

--- After shutdown ---
  Current memory:     55.8 MB
  Peak memory:        55.8 MB
  Allocated so far:   2759.7 MB
  Thread pool threads: 1
  Total threads:      31
  Max pool threads:   1
  Shutdown time: 223 ms

Forcing garbage collection...

--- Garbage collection ---
  Gen0 collections: 313
  Gen1 collections: 155
  Gen2 collections: 5
  Total allocated: 2760.5 MB

Test 1 completed

============================================================
Test 2: Parallel Pipeline of Independent Pairs
------------------------------------------------------------
All pairs work simultaneously. Producer waits for response before next request. All actors are active, all cores are loaded.
============================================================

Configuration: 3000 pairs, 5000 messages per pair
Total actors: 6000, total messages: 30,000,000

Creating 3000 receivers...
Creating and starting 3000 producers...

--- After creation and startup ---
  Current memory:     49.9 MB
  Peak memory:        49.9 MB
  Allocated so far:   9.1 MB
  Thread pool threads: 12
  Total threads:      31
  Max pool threads:   12
  Creation time: 9 ms

Waiting for completion...

--- After load ---
  Current memory:     55.4 MB
  Peak memory:        55.4 MB
  Allocated so far:   2755.4 MB
  Thread pool threads: 1
  Total threads:      31
  Max pool threads:   12

=== Performance Results ===
  Total messages:       30,000,000
  Requests processed:   15,000,000
  Request undelivered:  0
  Response undelivered: 0
  Execution time:       13,456 ms
  Time per message:     0.45 μs
  Messages per second:  2,229,489

Shutting down system...

--- After shutdown ---
  Current memory:     55.6 MB
  Peak memory:        55.6 MB
  Allocated so far:   2759.4 MB
  Thread pool threads: 1
  Total threads:      31
  Max pool threads:   12
  Shutdown time: 180 ms

Forcing garbage collection...

--- Garbage collection ---
  Gen0 collections: 626
  Gen1 collections: 309
  Gen2 collections: 9
  Total allocated: 5519.9 MB

Test 2 completed

============================================================
Test 3: Deep Pipeline
------------------------------------------------------------
Fewer pairs, but more messages per pair. Reduced thread pool contention. Tests the hypothesis about context switching overhead.
============================================================

Configuration: 100 pairs, 150,000 messages per pair
Total actors: 200, total messages: 30,000,000

Creating 100 receivers...
Creating and starting 100 producers...

--- After creation and startup ---
  Current memory:     41.7 MB
  Peak memory:        41.7 MB
  Allocated so far:   1.1 MB
  Thread pool threads: 12
  Total threads:      31
  Max pool threads:   12
  Creation time: 0 ms

Waiting for completion...

--- After load ---
  Current memory:     46.8 MB
  Peak memory:        46.8 MB
  Allocated so far:   2746.9 MB
  Thread pool threads: 1
  Total threads:      28
  Max pool threads:   12

=== Performance Results ===
  Total messages:       30,000,000
  Requests processed:   15,000,000
  Request undelivered:  0
  Response undelivered: 0
  Execution time:       10,173 ms
  Time per message:     0.34 μs
  Messages per second:  2,948,983

Shutting down system...

--- After shutdown ---
  Current memory:     46.8 MB
  Peak memory:        46.8 MB
  Allocated so far:   2747.0 MB
  Thread pool threads: 1
  Total threads:      28
  Max pool threads:   12
  Shutdown time: 0 ms

Forcing garbage collection...

--- Garbage collection ---
  Gen0 collections: 937
  Gen1 collections: 313
  Gen2 collections: 12
  Total allocated: 8266.8 MB

Test 3 completed

All tests completed.