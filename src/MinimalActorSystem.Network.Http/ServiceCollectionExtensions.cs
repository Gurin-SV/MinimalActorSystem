using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MinimalActorSystem.Network.Http;

/// <summary>
/// Методы расширения для регистрации HTTP компонентов в DI контейнере.
/// </summary>
/// <remarks>
/// Extension methods for registering HTTP components in DI container.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует HTTP транспорт и клиент роутера в DI контейнере.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="routerAddresses">Список адресов роутеров.</param>
    /// <returns>Коллекция сервисов для цепочки вызовов.</returns>
    /// <remarks>
    /// Registers HTTP transport and router client in DI container.
    /// </remarks>
    public static IServiceCollection AddHttpNetworkComponents(
        this IServiceCollection services,
        params string[] routerAddresses)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (routerAddresses == null || routerAddresses.Length == 0)
            throw new ArgumentException("At least one router address is required", nameof(routerAddresses));

        services.AddHttpClient();

        services.AddSingleton<IRouterClient>(sp =>
        {
            IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            ILogger<HttpRouterClient> logger = sp.GetRequiredService<ILogger<HttpRouterClient>>();
            return new HttpRouterClient(httpClientFactory, logger, routerAddresses);
        });

        services.AddSingleton<INetworkTransport>(sp =>
        {
            IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            ILogger<HttpNetworkTransport> logger = sp.GetRequiredService<ILogger<HttpNetworkTransport>>();
            return new HttpNetworkTransport(httpClientFactory, logger);
        });

        return services;
    }

    /// <summary>
    /// Регистрирует HTTP транспорт в DI контейнере (без клиента роутера).
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <returns>Коллекция сервисов для цепочки вызовов.</returns>
    /// <remarks>
    /// Registers HTTP transport only in DI container (without router client).
    /// </remarks>
    public static IServiceCollection AddHttpNetworkTransport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient();

        services.AddSingleton<INetworkTransport>(sp =>
        {
            IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            ILogger<HttpNetworkTransport> logger = sp.GetRequiredService<ILogger<HttpNetworkTransport>>();
            return new HttpNetworkTransport(httpClientFactory, logger);
        });

        return services;
    }

    /// <summary>
    /// Регистрирует клиент роутера в DI контейнере.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="routerAddresses">Список адресов роутеров.</param>
    /// <returns>Коллекция сервисов для цепочки вызовов.</returns>
    /// <remarks>
    /// Registers router client only in DI container.
    /// </remarks>
    public static IServiceCollection AddHttpRouterClient(
        this IServiceCollection services,
        params string[] routerAddresses)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (routerAddresses == null || routerAddresses.Length == 0)
            throw new ArgumentException("At least one router address is required", nameof(routerAddresses));

        services.AddHttpClient();

        services.AddSingleton<IRouterClient>(sp =>
        {
            IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            ILogger<HttpRouterClient> logger = sp.GetRequiredService<ILogger<HttpRouterClient>>();
            return new HttpRouterClient(httpClientFactory, logger, routerAddresses);
        });

        return services;
    }

    /// <summary>
    /// Регистрирует HTTP транспорт и клиент роутера с пользовательской конфигурацией HTTP клиента.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="routerAddresses">Список адресов роутеров.</param>
    /// <param name="configureHttpClient">Действие для настройки HTTP клиента.</param>
    /// <returns>Коллекция сервисов для цепочки вызовов.</returns>
    /// <remarks>
    /// Registers HTTP transport and router client with custom HTTP client configuration.
    /// </remarks>
    public static IServiceCollection AddHttpNetworkComponents(
        this IServiceCollection services,
        string[] routerAddresses,
        Action<HttpClient> configureHttpClient)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureHttpClient);

        if (routerAddresses == null || routerAddresses.Length == 0)
            throw new ArgumentException("At least one router address is required", nameof(routerAddresses));

        services.AddHttpClient("NetworkClient", configureHttpClient);

        services.AddSingleton<IRouterClient>(sp =>
        {
            IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            ILogger<HttpRouterClient> logger = sp.GetRequiredService<ILogger<HttpRouterClient>>();
            return new HttpRouterClient(httpClientFactory, logger, routerAddresses);
        });

        services.AddSingleton<INetworkTransport>(sp =>
        {
            IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            ILogger<HttpNetworkTransport> logger = sp.GetRequiredService<ILogger<HttpNetworkTransport>>();
            return new HttpNetworkTransport(httpClientFactory, logger);
        });

        return services;
    }
}
