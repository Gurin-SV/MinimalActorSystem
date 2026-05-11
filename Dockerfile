# Сборка
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем nuget.config и пакет
COPY nuget.config .
COPY build/packages/ ./build/packages/

# Копируем проект бенчмарков
COPY MinimalActorSystem.Benchmarks/ ./MinimalActorSystem.Benchmarks/

# Восстанавливаем и публикуем
WORKDIR /src/MinimalActorSystem.Benchmarks
RUN dotnet restore
RUN dotnet publish -c Release -o /app

# Запуск
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "MinimalActorSystem.Benchmarks.dll"]
