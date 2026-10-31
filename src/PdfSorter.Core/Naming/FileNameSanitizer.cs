using System.Text;
using PdfSorter.Core.Text;

namespace PdfSorter.Core.Naming;

/// <summary>
/// Приведение имён к допустимому для Windows виду. Набор запрещённых символов фиксированный
/// (не зависит от ОС, на которой запущен код), чтобы результат был предсказуемым.
/// </summary>
public static class FileNameSanitizer
{
    /// <summary>Чем заменяются недопустимые символы.</summary>
    public const char Replacement = '_';

    /// <summary>Максимальная длина имени файла вместе с расширением.</summary>
    public const int MaxFileNameLength = 180;

    private static readonly HashSet<char> InvalidChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Очищает один элемент пути (имя папки или имя файла без расширения): заменяет запрещённые
    /// и управляющие символы, схлопывает пробелы, убирает точки и пробелы в конце,
    /// обезвреживает <c>.</c>/<c>..</c> и зарезервированные имена (<c>CON</c>, <c>NUL</c>, …).
    /// </summary>
    public static string SanitizeSegment(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsControl(ch) || InvalidChars.Contains(ch) ? Replacement : ch);
        }

        var result = TextNormalizer.CollapseWhitespace(builder.ToString()).TrimEnd('.', ' ');
        if (result.Length == 0)
        {
            return Replacement.ToString();
        }

        var stem = result.Split('.')[0];
        return ReservedNames.Contains(stem) ? Replacement + result : result;
    }

    /// <summary>Очищает имя файла с расширением и ограничивает его длину, сохраняя расширение.</summary>
    public static string SanitizeFileName(string fileName)
    {
        var extension = GetExtension(fileName);
        var stem = SanitizeSegment(fileName[..^extension.Length]);
        extension = extension.Length > 0 ? "." + SanitizeSegment(extension[1..]) : string.Empty;

        var maxStemLength = MaxFileNameLength - extension.Length;
        if (stem.Length > maxStemLength)
        {
            stem = SanitizeSegment(stem[..maxStemLength]);
        }

        return stem + extension;
    }

    // Path.GetExtension зависит от разделителей текущей ОС, поэтому расширение ищем сами.
    private static string GetExtension(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        if (dot < 0 || dot == fileName.Length - 1)
        {
            return string.Empty;
        }

        var extension = fileName[dot..];
        return extension.IndexOfAny(['/', '\\', ' ']) >= 0 ? string.Empty : extension;
    }
}
