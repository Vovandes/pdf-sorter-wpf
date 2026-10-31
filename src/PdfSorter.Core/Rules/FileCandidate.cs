using PdfSorter.Core.Matching;

namespace PdfSorter.Core.Rules;

/// <summary>
/// Файл-кандидат, который проверяют правила. Дата из имени и текст первой страницы вычисляются лениво:
/// PDF открывается только если до него дошло правило по тексту, и не больше одного раза.
/// </summary>
public sealed class FileCandidate
{
    private readonly Func<string> _readFirstPageText;
    private readonly Lazy<DateTime?> _dateFromName;
    private string? _firstPageText;

    public FileCandidate(string fullPath, DateTime lastWriteTime, Func<string> readFirstPageText)
    {
        FullPath = fullPath;
        FileName = Path.GetFileName(fullPath);
        NameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
        Extension = Path.GetExtension(fullPath).TrimStart('.');
        LastWriteTime = lastWriteTime;
        _readFirstPageText = readFirstPageText;
        _dateFromName = new Lazy<DateTime?>(() => FileNameDateParser.TryParse(NameWithoutExtension));
    }

    public string FullPath { get; }

    /// <summary>Имя с расширением.</summary>
    public string FileName { get; }

    public string NameWithoutExtension { get; }

    /// <summary>Расширение без точки.</summary>
    public string Extension { get; }

    public DateTime LastWriteTime { get; }

    /// <summary>Дата из имени файла (<c>2025-03-14</c>, <c>14.03.2025</c>) или <c>null</c>.</summary>
    public DateTime? DateFromName => _dateFromName.Value;

    /// <summary>Читался ли уже текст PDF.</summary>
    public bool IsTextLoaded => _firstPageText is not null;

    /// <summary>Текст первой страницы. Ошибки чтения пробрасываются вызывающему коду.</summary>
    public string GetFirstPageText() => _firstPageText ??= _readFirstPageText();
}
