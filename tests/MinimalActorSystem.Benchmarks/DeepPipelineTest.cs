using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MinimalActorSystem.Benchmarks;

public sealed class DeepPipelineTest : IBenchmarkTest
{
    public string Name => "Глубокий конвейер";
    public string Description => "Меньше пар, но больше сообщений на пару. "
        + "Снижена конкуренция за пул потоков. "
        + "Проверяет гипотезу о накладных расходах на переключение контекстов.";

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

    private sealed class ConsumerActor(IActorSystem system, Guid uid, string name, DeepPipelineTest.ResponseMessage responseTemplate) : Actor(system, uid, name)
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

    public async Task RunAsync(Meter meter)
    {
        const int pairs = 100;
        const int messagesPerPair = 150_000;

        // Прикладные метрики
        var requestsCounter = meter.CreateCounter<long>("pipeline.requests_processed", description: "Total requests processed");
        var requestFailedCounter = meter.CreateCounter<long>("pipeline.request_failed", description: "Failed request deliveries");
        var responseFailedCounter = meter.CreateCounter<long>("pipeline.response_failed", description: "Failed response deliveries");

        var monitor = new ResourceMonitor();
        Console.WriteLine($"Конфигурация: {pairs} пар, {messagesPerPair:N0} сообщений на пару");
        Console.WriteLine($"Всего акторов: {pairs * 2}, всего сообщений: {(long)pairs * messagesPerPair * 2:N0}");

        var system = new ActorSystem(new Settings())
        {
            Metrics = new SystemMetrics(meter)
        };

        var allDone = new TaskCompletionSource<bool>();
        var counter = new Counter { Value = pairs };

        var requestTemplates = new RequestMessage[pairs];
        var responseTemplates = new ResponseMessage[pairs];
        var consumers = new List<ConsumerActor>(pairs);
        var producers = new List<ProducerActor>(pairs);

        Console.WriteLine($"\nСоздаю {pairs} получателей...");
        var swCreation = Stopwatch.StartNew();

        for (int i = 0; i < pairs; i++)
        {
            var consumerUid = Guid.NewGuid();
            var producerUid = Guid.NewGuid();

            requestTemplates[i] = new RequestMessage(producerUid, consumerUid);
            responseTemplates[i] = new ResponseMessage(consumerUid, producerUid);

            var consumer = new ConsumerActor(system, consumerUid, $"consumer-{i}", responseTemplates[i]);
            system.RegisterActor(consumer);
            consumers.Add(consumer);
        }

        Console.WriteLine($"Создаю и запускаю {pairs} производителей...");

        for (int i = 0; i < pairs; i++)
        {
            var consumerUid = consumers[i].Uid;
            var producerUid = requestTemplates[i].Sender;

            var producer = new ProducerActor(system, producerUid, $"producer-{i}",
                consumerUid, messagesPerPair, counter, allDone, requestTemplates[i]);
            system.RegisterActor(producer);
            producer.Start();
            producers.Add(producer);
        }

        swCreation.Stop();
        monitor.Snapshot(out long memAfterCreate, out long peakAfterCreate, out int threadsAfterCreate, out int pendingAfterCreate, out long allocAfterCreate);
        monitor.PrintStats("После создания и запуска", memAfterCreate, peakAfterCreate, threadsAfterCreate, pendingAfterCreate, allocAfterCreate);
        Console.WriteLine($"  Время создания: {swCreation.ElapsedMilliseconds} мс");

        Console.WriteLine("\nОжидание завершения...");
        var swWork = Stopwatch.StartNew();
        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(120));
        swWork.Stop();

        monitor.Snapshot(out long memAfterWork, out long peakAfterWork, out int threadsAfterWork, out int pendingAfterWork, out long allocAfterWork);
        monitor.PrintStats("После нагрузки", memAfterWork, peakAfterWork, threadsAfterWork, pendingAfterWork, allocAfterWork);

        long totalMessages = (long)pairs * messagesPerPair * 2;
        double totalUs = swWork.Elapsed.TotalMicroseconds;
        int totalProducerFailed = producers.Sum(p => p.SendFailed);
        int totalConsumerFailed = consumers.Sum(c => c.SendFailed);
        long totalProcessed = consumers.Sum(c => (long)c.Processed);

        // Прикладные метрики
        requestsCounter.Add(totalProcessed);
        requestFailedCounter.Add(totalProducerFailed);
        responseFailedCounter.Add(totalConsumerFailed);

        Console.WriteLine();
        Console.WriteLine("=== Результаты производительности ===");
        Console.WriteLine($"  Сообщений всего:      {totalMessages:N0}");
        Console.WriteLine($"  Обработано запросов:  {totalProcessed:N0}");
        Console.WriteLine($"  Недоставлено запрос:  {totalProducerFailed}");
        Console.WriteLine($"  Недоставлено ответ:   {totalConsumerFailed}");
        Console.WriteLine($"  Время работы:         {swWork.ElapsedMilliseconds:N0} мс");
        Console.WriteLine($"  На сообщение:         {totalUs / totalMessages:F2} мкс");
        Console.WriteLine($"  Сообщений в секунду:  {totalMessages / (swWork.ElapsedMilliseconds / 1000.0):N0}");

        Console.WriteLine("\nЗавершение системы...");
        var swShutdown = Stopwatch.StartNew();
        system.Shutdown();
        await system.WaitForShutdownAsync();
        swShutdown.Stop();

        monitor.Snapshot(out long memAfterShutdown, out long peakFinal, out int threadsAfterShutdown, out int pendingAfterShutdown, out long allocAfterShutdown);
        monitor.PrintStats("После завершения", memAfterShutdown, peakFinal, threadsAfterShutdown, pendingAfterShutdown, allocAfterShutdown);
        Console.WriteLine($"  Время завершения: {swShutdown.ElapsedMilliseconds} мс");
    }
}
