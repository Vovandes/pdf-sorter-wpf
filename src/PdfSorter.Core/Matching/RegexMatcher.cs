using System.Text.RegularExpressions;

namespace PdfSorter.Core.Matching;

/// <summary>
/// Общие правила работы с регулярными выражениями: единые опции, таймаут
/// (защита от «катастрофического» перебора) и извлечение значения для <c>{match}</c>.
/// </summary>
public static class RegexMatcher
{
    /// <summary>Максимальное время одной проверки выражения.</summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(500);

    public static RegexOptions GetOptions(bool ignoreCase) =>
        RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);

    /// <summary>Ищет совпадение. Статические методы Regex кэшируют разобранные выражения.</summary>
    public static Match Match(string input, string pattern, bool ignoreCase) =>
        Regex.Match(input, pattern, GetOptions(ignoreCase), MatchTimeout);

    /// <summary>Возвращает текст ошибки, если выражение некорректно, иначе <c>null</c>.</summary>
    public static string? Validate(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "выражение не задано";
        }

        try
        {
            _ = new Regex(pattern, RegexOptions.None, MatchTimeout);
            return null;
        }
        catch (ArgumentException ex)
        {
            return $"некорректное регулярное выражение: {ex.Message}";
        }
    }

    /// <summary>
    /// Значение для подстановки <c>{match}</c>: первая сработавшая группа, если она есть,
    /// иначе всё совпадение целиком.
    /// </summary>
    public static string ExtractValue(Match match)
    {
        for (var i = 1; i < match.Groups.Count; i++)
        {
            if (match.Groups[i].Success)
            {
                return match.Groups[i].Value.Trim();
            }
        }

        return match.Value.Trim();
    }
}
