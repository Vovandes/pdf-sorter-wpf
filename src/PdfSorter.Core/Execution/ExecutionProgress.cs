using PdfSorter.Core.Journal;

namespace PdfSorter.Core.Execution;

/// <summary>Сообщение о ходе выполнения: обработана строка плана с индексом <see cref="ItemIndex"/>.</summary>
public sealed record ExecutionProgress(int ItemIndex, int Processed, int Total, JournalOperation Operation);
