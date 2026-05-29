using System.Globalization;

namespace MinimalActorSystem.Benchmarks;

public class Program
{
    public static async Task Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        // Определяем язык: из аргумента командной строки или запросом в консоли
        string? lang = null;

        if (args.Length > 0 && (args[0] == "ru" || args[0] == "en"))
        {
            lang = args[0];
        }
        else
        {
            Console.Write("Select language (ru/en): ");
            lang = Console.ReadLine()?.ToLower();
        }

        Localization.SetLanguage(lang ?? "en");
        Console.WriteLine();
        IBenchmarkTest[] tests =
        [
            new SequentialPingPongTest(),
            new DeepPipelineTest(),
        ];

        for (int i = 0; i < tests.Length; i++)
        {
            IBenchmarkTest test = tests[i];

            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"{Localization.Test} {i + 1}: {test.Name}");
            Console.WriteLine(new string('-', 60));
            Console.WriteLine(test.Description);
            Console.WriteLine(new string('=', 60));
            Console.WriteLine();

            // Сборщик метрик для консольного вывода
            Dictionary<string, long> measurements = [];
            Dictionary<string, int> observableValues = [];

            // Запуск теста
            await test.RunAsync();

            Console.WriteLine();
            foreach (KeyValuePair<string, long> kvp in measurements.Where(m => m.Key.Contains('.')
                && !m.Key.StartsWith("messages")
                && !m.Key.StartsWith("actors")).OrderBy(m => m.Key))
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }

            Console.WriteLine();
            Console.WriteLine(Localization.ForceGC);
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);

            ResourceMonitor.PrintGcStats();
            Console.WriteLine();
            Console.WriteLine($"{Localization.TestCompleted} {i + 1}");
            Console.WriteLine();
        }

        Console.WriteLine(Localization.AllTestsCompleted);
    }
}
