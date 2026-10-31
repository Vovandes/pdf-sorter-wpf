namespace PdfSorter.Core.Planning;

public enum PlanItemStatus
{
    /// <summary>Файл будет перемещён или скопирован.</summary>
    Ready,

    /// <summary>Файл не трогаем: не подошло ни одно правило или он уже на месте.</summary>
    Skipped,

    /// <summary>Файл не удалось обработать (битый PDF, нет доступа и т.п.).</summary>
    Error,
}

/// <summary>Строка плана: исходный файл → новый путь.</summary>
public sealed record PlanItem
{
    public required string SourcePath { get; init; }

    public string? DestinationPath { get; init; }

    /// <summary>Название сработавшего правила.</summary>
    public string? RuleName { get; init; }

    public PlanItemStatus Status { get; init; }

    /// <summary>Пояснение для пропущенных и ошибочных строк.</summary>
    public string? Message { get; init; }
}
