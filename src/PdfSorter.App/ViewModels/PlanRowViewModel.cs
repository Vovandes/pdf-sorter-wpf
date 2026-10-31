using CommunityToolkit.Mvvm.ComponentModel;
using PdfSorter.Core.IO;
using PdfSorter.Core.Journal;
using PdfSorter.Core.Planning;

namespace PdfSorter.App.ViewModels;

/// <summary>Состояние строки предпросмотра — определяет цвет статуса.</summary>
public enum RowState
{
    Ready,
    Skipped,
    Error,
    Done,
    DryRun,
    Cancelled,
    Reverted,
}

/// <summary>Строка таблицы «Исходный файл → Новый путь → Правило → Статус».</summary>
public sealed partial class PlanRowViewModel : ObservableObject
{
    private readonly string _targetRoot;

    [ObservableProperty]
    private string _destination = string.Empty;

    [ObservableProperty]
    private RowState _state;

    /// <summary>Полный текст статуса (в подсказке), включая технические подробности ошибки.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortStatus))]
    private string _status = string.Empty;

    public PlanRowViewModel(int number, PlanItem item, string sourceRoot, string targetRoot, FileOperationMode mode)
    {
        Number = number;
        SourcePath = item.SourcePath;
        _targetRoot = targetRoot;
        Source = PathUtil.ToDisplay(item.SourcePath, sourceRoot);
        Rule = item.RuleName ?? "—";
        Destination = FormatDestination(item.DestinationPath);
        (State, Status) = item.Status switch
        {
            PlanItemStatus.Ready => (RowState.Ready, Join(mode == FileOperationMode.Move ? "Будет перемещён" : "Будет скопирован", item.Message)),
            PlanItemStatus.Skipped => (RowState.Skipped, Join("Пропущен", item.Message)),
            _ => (RowState.Error, Join("Ошибка", item.Message)),
        };
    }

    public int Number { get; }

    public string SourcePath { get; }

    public string Source { get; }

    public string Rule { get; }

    /// <summary>Статус для ячейки таблицы: без технических подробностей в скобках.</summary>
    public string ShortStatus => Status.IndexOf(" (", StringComparison.Ordinal) is var index and > 0 ? Status[..index] : Status;

    /// <summary>Обновляет строку по результату операции из журнала.</summary>
    public void Apply(JournalOperation operation)
    {
        Destination = FormatDestination(operation.Destination);
        (State, Status) = operation.Status switch
        {
            OperationStatus.Done => (RowState.Done,
                Join(operation.Action == FileOperationMode.Move ? "Перемещён" : "Скопирован", operation.Message)),
            OperationStatus.DryRun => (RowState.DryRun, "Проверен (dry run)"),
            OperationStatus.Skipped => (RowState.Skipped, Join("Пропущен", operation.Message)),
            OperationStatus.Cancelled => (RowState.Cancelled, "Не выполнен — пакет остановлен"),
            _ => (RowState.Error, Join("Ошибка", operation.Message)),
        };
    }

    public void MarkReverted(JournalOperation operation)
    {
        (State, Status) = operation.Reverted
            ? (RowState.Reverted, operation.Action == FileOperationMode.Move ? "Отменено: файл возвращён" : "Отменено: копия удалена")
            : (RowState.Error, Join("Не удалось отменить", operation.RevertError));
    }

    private string FormatDestination(string? path) =>
        path is null ? "—" : PathUtil.ToDisplay(path, _targetRoot);

    private static string Join(string status, string? details) =>
        string.IsNullOrWhiteSpace(details) ? status : $"{status}: {details}";
}
