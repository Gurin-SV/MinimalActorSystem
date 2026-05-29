namespace MinimalActorSystem.Benchmarks;

/// <summary>
/// Локализация текстов для бенчмарков. Поддерживает русский и английский языки.
/// </summary>
/// <summary>
/// Localization for benchmarks. Supports Russian and English.
/// </summary>
public static class Localization
{
    private static string _lang = "en";

    /// <summary>
    /// Устанавливает язык: "ru" или "en".
    /// </summary>
    /// <summary>
    /// Sets language: "ru" or "en".
    /// </summary>
    public static void SetLanguage(string lang) => _lang = lang == "ru" ? "ru" : "en";

    private static string T(string ru, string en) => _lang == "ru" ? ru : en;

    // --- Общие фразы ---
    public static string Test => T("Тест", "Test");
    public static string Config => T("Конфигурация", "Configuration");
    public static string TotalActors => T("Всего акторов", "Total actors");
    public static string TotalMessages => T("всего сообщений", "total messages");
    public static string CreatingActors => T("Создаю акторов...", "Creating actors...");
    public static string AfterCreation => T("После создания", "After creation");
    public static string AfterLoad => T("После нагрузки", "After load");
    public static string AfterShutdown => T("После завершения", "After shutdown");
    public static string WaitingCompletion => T("Ожидание завершения...", "Waiting for completion...");
    public static string ShuttingDown => T("Завершение системы...", "Shutting down system...");
    public static string ShutdownTime => T("Время завершения", "Shutdown time");
    public static string CreationTime => T("Время создания", "Creation time");
    public static string WorkTime => T("Время работы", "Work time");
    public static string MessagesTotal => T("Сообщений всего", "Total messages");
    public static string MessagesPerSecond => T("Сообщений в секунду", "Messages per second");
    public static string TimePerMessage => T("На сообщение", "Per message");
    public static string ForceGC => T("Принудительная сборка мусора...", "Forcing garbage collection...");
    public static string TestCompleted => T("Тест завершён", "Test completed");
    public static string AllTestsCompleted => T("Все тесты завершены", "All tests completed");
    public static string MilliSeconds => T("мс", "ms");
    public static string MicroSeconds => T("мкс", "μs");
    public static string ActorsLower => T("акторам", "actors");
    public static string Results => T("Результаты производительности", "Performance results");

    // --- Метрики ---
    public static string SystemMetrics => T("Системные метрики", "System metrics");
    public static string MemoryCurrent => T("Память текущая", "Current memory");
    public static string MemoryPeak => T("Память пиковая", "Peak memory");
    public static string AllocatedSinceStart => T("Выделено с начала", "Allocated since start");
    public static string ThreadPoolThreads => T("Потоков в пуле", "Thread pool threads");
    public static string TotalThreads => T("Всего потоков", "Total threads");
    public static string MaxPoolThreads => T("Макс потоков пула", "Max pool threads");
    public static string GarbageCollection => T("Сборка мусора", "Garbage collection");
    public static string Collections => T("сборок", "collections");
    public static string TotalAllocated => T("Всего выделено", "Total allocated");

    // --- SequentialPingPongTest ---
    public static string SequentialPingPongName => T("Последовательный Ping-Pong", "Sequential Ping-Pong");
    public static string SequentialPingPongDesc => T(
        "Пары ping/pong обмениваются сообщениями последовательно. В любой момент активны только 2 актора.",
        "Ping/pong pairs exchange messages sequentially. Only 2 actors are active at any time.");
    public static string Pairs => T("пар", "pairs");
    public static string MessagesPerPair => T("сообщений на пару", "messages per pair");
    public static string SendingStartMessages => T("Отправляю стартовые сообщения", "Sending start messages");
    public static string FailedPing => T("Недоставлено ping", "Failed ping");
    public static string FailedPong => T("Недоставлено pong", "Failed pong");

    // --- DeepPipelineTest ---
    public static string DeepPipelineName => T("Глубокий конвейер", "Deep Pipeline");
    public static string DeepPipelineDesc => T(
        "Меньше пар, но больше сообщений на пару. Снижена конкуренция за пул потоков.",
        "Fewer pairs but more messages per pair. Reduced thread pool contention.");
    public static string ProducersConsumers => T("производителей/потребителей", "producers/consumers");
    public static string MessagesPerProducer => T("сообщений на производителя", "messages per producer");
    public static string CreatingConsumers => T("Создаю получателей...", "Creating consumers...");
    public static string CreatingProducers => T("Создаю и запускаю производителей...", "Creating and starting producers...");
    public static string ProcessedRequests => T("Обработано запросов", "Processed requests");
    public static string FailedRequests => T("Недоставлено запрос", "Failed requests");
    public static string FailedResponses => T("Недоставлено ответ", "Failed responses");

    // --- ResourceMonitor ---
    public static string MemoryMB => T("MB", "MB");

    // --- Форматирование с параметрами ---
    public static string ConfigFormat(int pairs, int messagesPerPair) =>
        _lang == "ru"
            ? $"{Config}: {pairs} пар, {messagesPerPair:N0} сообщений на пару"
            : $"{Config}: {pairs} pairs, {messagesPerPair:N0} messages per pair";

    public static string TotalActorsFormat(int count, long totalMessages) =>
        _lang == "ru"
            ? $"{TotalActors}: {count}, {TotalMessages}: {totalMessages:N0}"
            : $"{TotalActors}: {count}, {totalMessages:N0} {TotalMessages}";

    public static string ResultsFormat(long totalMessages, long processed, long failed, double timeMs, double throughput) =>
        _lang == "ru"
            ? $"""
               {MessagesTotal}:      {totalMessages:N0}
               Обработано:           {processed:N0}
               Недоставлено:         {failed}
               {WorkTime}:           {timeMs:N0} мс
               {MessagesPerSecond}:  {throughput:N0}
               """
            : $"""
               {MessagesTotal}:      {totalMessages:N0}
               Processed:            {processed:N0}
               Failed:               {failed}
               {WorkTime}:           {timeMs:N0} ms
               {MessagesPerSecond}:  {throughput:N0}
               """;
}
