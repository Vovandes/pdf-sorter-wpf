using PdfSorter.Core.Rules;

namespace PdfSorter.Core.Planning;

/// <summary>Что и куда сортировать.</summary>
public sealed record SortJob
{
    public required string SourceFolder { get; init; }

    /// <summary>Обрабатывать ли файлы во вложенных папках исходной папки.</summary>
    public bool IncludeSubfolders { get; init; }

    public required string TargetFolder { get; init; }

    public required RuleSet RuleSet { get; init; }
}
