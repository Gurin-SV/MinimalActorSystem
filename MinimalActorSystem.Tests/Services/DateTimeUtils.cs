using System.Globalization;

namespace MinimalActorSystem.Tests;

/// <summary>
/// Вспомогательные методы для работы с датами и временем.
/// Используется только в тестах и отладке.
/// </summary>
/// <remarks>
/// DateTime helper methods. Used only in tests and debugging.
/// </remarks>
public static class DateTimeUtils
{
    /// <summary>
    /// Смещение локального времени от UTC. По умолчанию +3 (московское время).
    /// Может быть изменено для других часовых поясов.
    /// </summary>
    /// <remarks>
    /// Local time offset from UTC. Default is +3 (Moscow time).
    /// </remarks>
    public static TimeSpan LocalOffset { get; set; } = TimeSpan.FromHours(3);

    /// <summary>
    /// Преобразует строку вида "dd.MM.yyyy HH:mm:ss" в UTC.
    /// Входная строка интерпретируется как локальное время с учётом <see cref="LocalOffset"/>.
    /// </summary>
    /// <param name="s">Строка в формате "dd.MM.yyyy HH:mm:ss".</param>
    /// <returns>UTC-время. Kind = <see cref="DateTimeKind.Utc"/>.</returns>
    /// <remarks>
    /// Parses a string as local time (using LocalOffset) and converts to UTC.
    /// </remarks>
    public static DateTime AsUtc(this string s)
    {
        var local = DateTime.ParseExact(s, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
        var utcValue = local.Subtract(LocalOffset);
        return new DateTime(utcValue.Ticks, DateTimeKind.Utc);
    }

    /// <summary>
    /// Преобразует строку вида "dd.MM.yyyy HH:mm:ss" в локальное время.
    /// </summary>
    /// <param name="s">Строка в формате "dd.MM.yyyy HH:mm:ss".</param>
    /// <returns>Локальное время. Kind = <see cref="DateTimeKind.Local"/>.</returns>
    public static DateTime AsLocal(this string s)
    {
        return DateTime.ParseExact(s, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
    }

    /// <summary>
    /// Конвертирует UTC-время в локальное с учётом <see cref="LocalOffset"/>.
    /// </summary>
    /// <param name="utc">UTC-время. Kind = <see cref="DateTimeKind.Utc"/>.</param>
    /// <returns>Локальное время. Kind = <see cref="DateTimeKind.Local"/>.</returns>
    public static DateTime ConvertToLocal(this DateTime utc)
    {
        return new DateTime(utc.Add(LocalOffset).Ticks, DateTimeKind.Local);
    }

    /// <summary>
    /// Конвертирует локальное время в UTC с учётом <see cref="LocalOffset"/>.
    /// </summary>
    /// <param name="local">Локальное время. Kind = <see cref="DateTimeKind.Local"/>.</param>
    /// <returns>UTC-время. Kind = <see cref="DateTimeKind.Utc"/>.</returns>
    public static DateTime ConvertToUtc(this DateTime local)
    {
        return new DateTime(local.Subtract(LocalOffset).Ticks, DateTimeKind.Utc);
    }
}
