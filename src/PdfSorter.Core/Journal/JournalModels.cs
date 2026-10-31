using System.Text.Json.Serialization;

namespace PdfSorter.Core.Journal;

/// <summary>Перемещать файлы или копировать.</summary>
public enum FileOperationMode
{
    Move,
    Copy,
}

public enum OperationStatus
{
    /// <summary>Файл перемещён или скопирован.</summary>
    Done,

    /// <summary>Пробный прогон: операция только записана в журнал.</summary>
    DryRun,

    /// <summary>Файл пропущен ещё на этапе плана.</summary>
    Skipped,

    /// <summary>Операция не удалась (ошибка плана или файловой системы).</summary>
    Failed,

    /// <summary>Не выполнено, потому что пакет был остановлен пользователем.</summary>
    Cancelled,
}

/// <summary>Одна операция над файлом.</summary>
public sealed class JournalOperation
{
    public string Source { get; set; } = string.Empty;

    public string? Destination { get; set; }

    public FileOperationMode Action { get; set; }

    public string? Rule { get; set; }

    public OperationStatus Status { get; set; }

    public string? Message { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Операция откачена командой «Отменить последнюю операцию».</summary>
    public bool Reverted { get; set; }

    /// <summary>Почему не удалось откатить операцию (если не удалось).</summary>
    public string? RevertError { get; set; }
}

/// <summary>Итоги пакета — пишутся в журнал для удобства чтения человеком.</summary>
public sealed record JournalSummary(int Total, int Done, int DryRun, int Skipped, int Failed, int Cancelled, int Reverted);

/// <summary>Пакет операций — один запуск «Выполнить» или «Пробный прогон». Хранится в отдельном JSON-файле.</summary>
public sealed class JournalBatch
{
    public string Id { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public FileOperationMode Mode { get; set; }

    public bool IsDryRun { get; set; }

    public bool WasCancelled { get; set; }

    public string SourceFolder { get; set; } = string.Empty;

    public string TargetFolder { get; set; } = string.Empty;

    /// <summary>Когда пакет был полностью отменён; <c>null</c> — не отменялся.</summary>
    public DateTimeOffset? UndoneAt { get; set; }

    /// <summary>Итоги — вычисляются из операций и пишутся в файл перед ними, чтобы журнал было удобно читать.</summary>
    public JournalSummary Summary => new(
        Operations.Count,
        Operations.Count(o => o.Status == OperationStatus.Done),
        Operations.Count(o => o.Status == OperationStatus.DryRun),
        Operations.Count(o => o.Status == OperationStatus.Skipped),
        Operations.Count(o => o.Status == OperationStatus.Failed),
        Operations.Count(o => o.Status == OperationStatus.Cancelled),
        Operations.Count(o => o.Reverted));

    /// <summary>Папки, созданные пакетом, в порядке создания (родитель раньше вложенной).</summary>
    public List<string> CreatedDirectories { get; set; } = [];

    public List<JournalOperation> Operations { get; set; } = [];

    /// <summary>Есть ли что откатывать: реальный (не пробный) пакет с выполненными и ещё не откаченными операциями.</summary>
    [JsonIgnore]
    public bool CanUndo => !IsDryRun && Operations.Any(o => o.Status == OperationStatus.Done && !o.Reverted);
}
