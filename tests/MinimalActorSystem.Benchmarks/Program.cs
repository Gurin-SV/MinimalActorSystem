using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace MinimalActorSystem.Benchmarks;

public class Program
{
    public static async Task Main(string[] _)
    {
        using var meter = new Meter("MinimalActorSystem");

        // Экспорт метрик в OpenTelemetry Collector
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("MinimalActorSystem")
            .AddOtlpExporter()
            .Build();

        var tests = new IBenchmarkTest[]
        {
            new SequentialPingPongTest(),
            new DeepPipelineTest(),
        };

        for (int i = 0; i < tests.Length; i++)
        {
            var test = tests[i];

            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"Тест {i + 1}: {test.Name}");
            Console.WriteLine(new string('-', 60));
            Console.WriteLine(test.Description);
            Console.WriteLine(new string('=', 60));
            Console.WriteLine();

            // Сборщик метрик для консольного вывода
            var measurements = new Dictionary<string, long>();
            var observableValues = new Dictionary<string, int>();

            using var listener = new MeterListener();

            listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "MinimalActorSystem")
                {
                    switch (instrument)
                    {
                        case Counter<long>:
                        case Histogram<long>:
                        case Counter<int>:
                        case Histogram<int>:
                            listener.EnableMeasurementEvents(instrument, instrument);
                            break;
                        case ObservableGauge<int>:
                        case ObservableCounter<int>:
                            listener.EnableMeasurementEvents(instrument, instrument);
                            listener.RecordObservableInstruments();
                            break;
                    }
                }
            };

            listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                var key = instrument.Name;
                measurements.TryGetValue(key, out var existing);
                measurements[key] = existing + measurement;
            });

            listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
            {
                observableValues[instrument.Name] = measurement;
            });

            listener.Start();

            // Запуск теста
            await test.RunAsync(meter);

            // Даём время на доставку метрик
            Thread.Sleep(100);
            listener.RecordObservableInstruments();
            Thread.Sleep(100);

            // Вывод метрик в консоль
            Console.WriteLine();
            Console.WriteLine("--- Системные метрики ---");
            PrintMetric("messages.sent", measurements);
            PrintMetric("messages.dropped", measurements);
            PrintMetric("actors.created", measurements);
            PrintMetric("actors.destroyed", measurements);
            PrintGauge("actors.active", observableValues);

            Console.WriteLine();
            Console.WriteLine("--- Прикладные метрики ---");
            foreach (var kvp in measurements.Where(m => m.Key.Contains(".")
                && !m.Key.StartsWith("messages")
                && !m.Key.StartsWith("actors")).OrderBy(m => m.Key))
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }

            Console.WriteLine();
            Console.WriteLine("Принудительная сборка мусора...");
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);

            ResourceMonitor.PrintGcStats();
            Console.WriteLine();
            Console.WriteLine($"Тест {i + 1} завершён");
            Console.WriteLine();
        }

        // Сброс метрик в Collector перед завершением
        meterProvider.ForceFlush();
        Thread.Sleep(1000);

        Console.WriteLine("Все тесты завершены.");
    }

    private static void PrintMetric(string name, Dictionary<string, long> measurements)
    {
        measurements.TryGetValue(name, out var value);
        Console.WriteLine($"  {name}: {value}");
    }

    private static void PrintGauge(string name, Dictionary<string, int> values)
    {
        values.TryGetValue(name, out var value);
        Console.WriteLine($"  {name}: {value}");
    }
}
