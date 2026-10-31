namespace PdfSorter.Core.Naming;

/// <summary>Подбор свободного имени при конфликте: <c>Договор.pdf</c> → <c>Договор (2).pdf</c> → <c>Договор (3).pdf</c>.</summary>
public static class UniquePathResolver
{
    /// <summary>Пояснение для плана и журнала, когда к имени пришлось добавить номер.</summary>
    public const string ConflictMessage = "добавлен номер (имя занято)";

    private const int MaxAttempts = 9999;

    /// <param name="path">Желаемый путь.</param>
    /// <param name="isTaken">Возвращает <c>true</c>, если путь занят (на диске или уже в плане).</param>
    public static string Resolve(string path, Func<string, bool> isTaken)
    {
        if (!isTaken(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var index = 2; index <= MaxAttempts; index++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({index}){extension}");
            if (!isTaken(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"Не удалось подобрать свободное имя для «{path}».");
    }
}
