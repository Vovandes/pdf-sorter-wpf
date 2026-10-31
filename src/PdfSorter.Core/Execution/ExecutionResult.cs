using PdfSorter.Core.Journal;

namespace PdfSorter.Core.Execution;

/// <summary>Итог выполнения плана.</summary>
/// <param name="Batch">Записанный пакет журнала.</param>
/// <param name="JournalPath">Путь к JSON-файлу журнала.</param>
public sealed record ExecutionResult(JournalBatch Batch, string JournalPath)
{
    public JournalSummary Summary => Batch.Summary;

    public bool WasCancelled => Batch.WasCancelled;
}
