using System.Globalization;

namespace MinimalActorSystem.Testing;

/// <summary>
/// Вспомогательные методы для работы с датами и временем.
/// Используется только в тестах и отладке.
/// </summary>
public static class DateTimeUtils
{
    /// <summary>
    /// Смещение локального времени от UTC. По умолчанию +3 (московское время).
    /// Может быть изменено для других часовых поясов.
    /// </summary>
    public static TimeSpan LocalOffset { get; set; } = TimeSpan.FromHours(3);

    /// <summary>
    /// Преобразует строку вида "dd.MM.yyyy HH:mm:ss" в UTC.
    /// Входная строка интерпретируется как локальное время с учётом <see cref="LocalOffset"/>.
    /// </summary>
    /// <param name="s">Строка в формате "dd.MM.yyyy HH:mm:ss".</param>
    /// <returns>UTC-время. Kind = <see cref="DateTimeKind.Utc"/>.</returns>
    public static DateTime AsUtc(this string s)
    {
        // Парсим как локальное время. AssumeLocal устанавливает Kind = Local,
        // но само значение остаётся "как в строке" — 03:00:00 останется 03:00:00.
        var local = DateTime.ParseExact(s, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
        // local.Kind == DateTimeKind.Local
        // local.TimeOfDay == указанное в строке время (без вычитания offset)

        // Вычитаем наш кастомный offset: 03:00:00 - 03:00 = 00:00:00
        // Теперь это UTC-значение, но Kind унаследован от local (Local).
        var utcValue = local.Subtract(LocalOffset);
        // utcValue.Kind == DateTimeKind.Local (унаследован от local)
        // utcValue.TimeOfDay == 00:00:00 (UTC)

        // Создаём новый DateTime с тем же значением ticks, но Kind = Utc.
        // Конструктор не выполняет конвертацию — просто устанавливает флаг.
        return new DateTime(utcValue.Ticks, DateTimeKind.Utc);
        // result.Kind == DateTimeKind.Utc
        // result.TimeOfDay == 00:00:00
    }

    /// <summary>
    /// Преобразует строку вида "dd.MM.yyyy HH:mm:ss" в локальное время.
    /// </summary>
    /// <param name="s">Строка в формате "dd.MM.yyyy HH:mm:ss".</param>
    /// <returns>Локальное время. Kind = <see cref="DateTimeKind.Local"/>.</returns>
    public static DateTime AsLocal(this string s)
    {
        // Парсим как локальное время. Значение "как в строке", Kind = Local.
        var local = DateTime.ParseExact(s, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
        // local.Kind == DateTimeKind.Local
        // local.TimeOfDay == указанное в строке время
        return local;
    }

    /// <summary>
    /// Конвертирует UTC-время в локальное с учётом <see cref="LocalOffset"/>.
    /// </summary>
    /// <param name="utc">UTC-время. Kind = <see cref="DateTimeKind.Utc"/>.</param>
    /// <returns>Локальное время. Kind = <see cref="DateTimeKind.Local"/>.</returns>
    public static DateTime ConvertToLocal(this DateTime utc)
    {
        // Добавляем кастомный offset к значению в UTC.
        // Конструктор устанавливает Kind = Local без конвертации.
        return new DateTime(utc.Add(LocalOffset).Ticks, DateTimeKind.Local);
        // result.Kind == DateTimeKind.Local
        // result.TimeOfDay == utc.TimeOfDay + LocalOffset
    }

    /// <summary>
    /// Конвертирует локальное время в UTC с учётом <see cref="LocalOffset"/>.
    /// </summary>
    /// <param name="local">Локальное время. Kind = <see cref="DateTimeKind.Local"/>.</param>
    /// <returns>UTC-время. Kind = <see cref="DateTimeKind.Utc"/>.</returns>
    public static DateTime ConvertToUtc(this DateTime local)
    {
        // Вычитаем кастомный offset из локального значения.
        // Конструктор устанавливает Kind = Utc без конвертации.
        return new DateTime(local.Subtract(LocalOffset).Ticks, DateTimeKind.Utc);
        // result.Kind == DateTimeKind.Utc
        // result.TimeOfDay == local.TimeOfDay - LocalOffset
    }
}
