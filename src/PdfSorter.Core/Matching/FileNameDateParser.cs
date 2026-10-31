using System.Text.RegularExpressions;

namespace PdfSorter.Core.Matching;

/// <summary>
/// Извлекает дату из имени файла. Поддерживаются форматы <c>2025-03-14</c>, <c>2025_03_14</c>, <c>2025.03.14</c>
/// и <c>14.03.2025</c>, <c>14-03-2025</c>, <c>14_03_2025</c>. Берётся первая корректная дата.
/// </summary>
public static partial class FileNameDateParser
{
    public static DateTime? TryParse(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        foreach (Match match in DatePattern().Matches(fileName))
        {
            var (year, month, day) = match.Groups["y1"].Success
                ? (match.Groups["y1"].Value, match.Groups["m1"].Value, match.Groups["d1"].Value)
                : (match.Groups["y2"].Value, match.Groups["m2"].Value, match.Groups["d2"].Value);

            if (TryCreate(int.Parse(year), int.Parse(month), int.Parse(day), out var date))
            {
                return date;
            }
        }

        return null;
    }

    private static bool TryCreate(int year, int month, int day, out DateTime date)
    {
        date = default;
        if (year < 1900 || year > 2100 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        date = new DateTime(year, month, day);
        return true;
    }

    // Разделитель внутри одной даты должен быть одинаковым (\k<s1>), а сама дата — не частью более длинного числа.
    [GeneratedRegex(
        @"(?<![0-9])(?:(?<y1>[0-9]{4})(?<s1>[-_.])(?<m1>[0-9]{2})\k<s1>(?<d1>[0-9]{2})|(?<d2>[0-9]{2})(?<s2>[-_.])(?<m2>[0-9]{2})\k<s2>(?<y2>[0-9]{4}))(?![0-9])",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex DatePattern();
}
