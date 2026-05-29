using System.Diagnostics;

namespace MinimalActorSystem.Benchmarks;

public sealed class DeepPipelineTest : IBenchmarkTest
{
    public string Name => Localization.DeepPipelineName;
    public string Description => Localization.DeepPipelineDesc;

    private class Counter { public int Value; }

    private sealed class RequestMessage(Guid sender, Guid receiver) : Letter(sender, receiver);
    private sealed class ResponseMessage(Guid sender, Guid receiver) : Letter(sender, receiver);

    private sealed class ProducerActor(IActorSystem system, Guid uid, string name,
        Guid consumerUid, int messagesToSend, DeepPipelineTest.Counter counter,
        TaskCompletionSource<bool> done, DeepPipelineTest.RequestMessage requestTemplate) : Actor(system, uid, name)
    {
        private readonly Guid _consumerUid = consumerUid;
        private readonly int _messagesToSend = messagesToSend;
        private readonly Counter _counter = counter;
        private readonly TaskCompletionSource<bool> _done = done;
        private readonly RequestMessage _requestTemplate = requestTemplate;
        private int _sent;
        private int _responses;
        private int _sendFailed;

        public int SendFailed => _sendFailed;

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is ResponseMessage)
            {
                _responses++;

                if (_responses >= _messagesToSend)
                {
                    int remaining = Interlocked.Decrement(ref _counter.Value);
                    if (remaining == 0)
                        _done.TrySetResult(true);
                }
                else if (_sent < _messagesToSend)
                {
                    SendRequest();
                }
            }
            return default;
        }

        public void Start()
        {
            SendRequest();
        }

        private void SendRequest()
        {
            _requestTemplate.Receiver = _consumerUid;
            if (!System.Send(_requestTemplate))
                Interlocked.Increment(ref _sendFailed);
            _sent++;
        }
    }

    private sealed class ConsumerActor(IActorSystem system, Guid uid, string name,
        DeepPipelineTest.ResponseMessage responseTemplate)
        : Actor(system, uid, name)
    {
        private readonly ResponseMessage _responseTemplate = responseTemplate;
        private int _processed;
        private int _sendFailed;

        public int Processed => _processed;
        public int SendFailed => _sendFailed;

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is RequestMessage request)
            {
                _processed++;
                _responseTemplate.Receiver = request.Sender;
                if (!System.Send(_responseTemplate))
                    Interlocked.Increment(ref _sendFailed);
            }
            return default;
        }
    }

    public async Task RunAsync()
    {
        const int pairs = 100;
        const int messagesPerPair = 150_000;

        ResourceMonitor monitor = new();
        Console.WriteLine(Localization.ConfigFormat(pairs, messagesPerPair));
        Console.WriteLine(Localization.TotalActorsFormat(pairs * 2, (long)pairs * messagesPerPair * 2));

        ActorSystem system = new(new Settings());

        TaskCompletionSource<bool> allDone = new();
        Counter counter = new() { Value = pairs };

        RequestMessage[] requestTemplates = new RequestMessage[pairs];
        ResponseMessage[] responseTemplates = new ResponseMessage[pairs];
        List<ConsumerActor> consumers = new(pairs);
        List<ProducerActor> producers = new(pairs);

        Console.WriteLine($"\n{Localization.CreatingConsumers}");
        Stopwatch swCreation = Stopwatch.StartNew();

        for (int i = 0; i < pairs; i++)
        {
            Guid consumerUid = Guid.NewGuid();
            Guid producerUid = Guid.NewGuid();

            requestTemplates[i] = new RequestMessage(producerUid, consumerUid);
            responseTemplates[i] = new ResponseMessage(consumerUid, producerUid);

            ConsumerActor consumer = new(system, consumerUid, $"consumer-{i}", responseTemplates[i]);
            system.RegisterActor(consumer);
            consumers.Add(consumer);
        }

        Console.WriteLine($"\n{Localization.CreatingProducers}");

        for (int i = 0; i < pairs; i++)
        {
            Guid consumerUid = consumers[i].Uid;
            Guid producerUid = requestTemplates[i].Sender;

            ProducerActor producer = new(system, producerUid, $"producer-{i}",
                consumerUid, messagesPerPair, counter, allDone, requestTemplates[i]);
            system.RegisterActor(producer);
            producer.Start();
            producers.Add(producer);
        }

        swCreation.Stop();
        monitor.Snapshot(out long memAfterCreate, out long peakAfterCreate, out int threadsAfterCreate,
            out int pendingAfterCreate, out long allocAfterCreate);
        monitor.PrintStats(Localization.AfterCreation, memAfterCreate, peakAfterCreate, threadsAfterCreate,
            pendingAfterCreate, allocAfterCreate);
        Console.WriteLine($"  {Localization.CreationTime}: {swCreation.ElapsedMilliseconds} {Localization.MilliSeconds}");

        Console.WriteLine($"\n{Localization.WaitingCompletion}");
        Stopwatch swWork = Stopwatch.StartNew();
        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(120));
        swWork.Stop();

        monitor.Snapshot(out long memAfterWork, out long peakAfterWork, out int threadsAfterWork,
            out int pendingAfterWork, out long allocAfterWork);
        monitor.PrintStats(Localization.AfterLoad, memAfterWork, peakAfterWork, threadsAfterWork,
            pendingAfterWork, allocAfterWork);

        long totalMessages = (long)pairs * messagesPerPair * 2;
        double totalUs = swWork.Elapsed.TotalMicroseconds;
        int totalProducerFailed = producers.Sum(p => p.SendFailed);
        int totalConsumerFailed = consumers.Sum(c => c.SendFailed);
        long totalProcessed = consumers.Sum(c => (long)c.Processed);

        Console.WriteLine();
        Console.WriteLine("=== " + Localization.Results + " ===");
        Console.WriteLine($"  {Localization.MessagesTotal}: {totalMessages:N0}");
        Console.WriteLine($"  {Localization.ProcessedRequests}: {totalProcessed:N0}");
        Console.WriteLine($"  {Localization.FailedRequests}: {totalProducerFailed}");
        Console.WriteLine($"  {Localization.FailedResponses}: {totalConsumerFailed}");
        Console.WriteLine($"  {Localization.WorkTime}: {swWork.ElapsedMilliseconds:N0} {Localization.MilliSeconds}");
        Console.WriteLine($"  {Localization.TimePerMessage}: {totalUs / totalMessages:F2} {Localization.MicroSeconds}");
        Console.WriteLine($"  {Localization.MessagesPerSecond}: {totalMessages / (swWork.ElapsedMilliseconds / 1000.0):N0}");

        Console.WriteLine($"\n{Localization.ShuttingDown}");
        Stopwatch swShutdown = Stopwatch.StartNew();
        system.Shutdown();
        await system.WaitForShutdownAsync();
        swShutdown.Stop();

        monitor.Snapshot(out long memAfterShutdown, out long peakFinal, out int threadsAfterShutdown,
            out int pendingAfterShutdown, out long allocAfterShutdown);
        monitor.PrintStats(Localization.AfterShutdown, memAfterShutdown, peakFinal, threadsAfterShutdown,
            pendingAfterShutdown, allocAfterShutdown);
        Console.WriteLine($"  {Localization.ShutdownTime}: {swShutdown.ElapsedMilliseconds} {Localization.MilliSeconds}");
    }
}
