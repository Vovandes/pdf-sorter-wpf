using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfSorter.App.Services;
using PdfSorter.Core.Execution;
using PdfSorter.Core.IO;
using PdfSorter.Core.Journal;
using PdfSorter.Core.Naming;
using PdfSorter.Core.Planning;
using PdfSorter.Core.Rules;

namespace PdfSorter.App.ViewModels;

/// <summary>
/// Главное окно: настройки задания, редактор правил, предпросмотр, выполнение и журнал.
/// Длительные операции выполняются в фоне (Task.Run), UI получает прогресс через IProgress.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private const string RulesFileFilter = "Набор правил (*.json)|*.json|Все файлы (*.*)|*.*";

    private readonly SortPlanner _planner;
    private readonly SortExecutor _executor;
    private readonly UndoService _undoService;
    private readonly RuleSetStore _ruleSetStore;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _cancellation;

    [ObservableProperty]
    private string _sourceFolder = string.Empty;

    [ObservableProperty]
    private bool _includeSubfolders = true;

    [ObservableProperty]
    private string _targetFolder = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMoveMode), nameof(IsCopyMode))]
    private FileOperationMode _mode = FileOperationMode.Move;

    [ObservableProperty]
    private string _fileNameTemplate = "{name}";

    [ObservableProperty]
    private string _fallbackFolder = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveRuleCommand), nameof(MoveRuleUpCommand), nameof(MoveRuleDownCommand))]
    private RuleViewModel? _selectedRule;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand), nameof(DryRunCommand), nameof(ExecuteCommand), nameof(UndoCommand), nameof(CancelCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _statusText = "Выберите папки и нажмите «Предпросмотр».";

    [ObservableProperty]
    private string _planSummary = string.Empty;

    [ObservableProperty]
    private bool _isPlanOutdated;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private JournalBatch? _undoableBatch;

    public MainViewModel(
        SortPlanner planner,
        SortExecutor executor,
        UndoService undoService,
        RuleSetStore ruleSetStore,
        IDialogService dialogs,
        string journalDirectory)
    {
        _planner = planner;
        _executor = executor;
        _undoService = undoService;
        _ruleSetStore = ruleSetStore;
        _dialogs = dialogs;
        JournalDirectory = journalDirectory;
        Rules.CollectionChanged += (_, _) => OnRulesChanged();
    }

    public ObservableCollection<RuleViewModel> Rules { get; } = [];

    public ObservableCollection<PlanRowViewModel> PlanRows { get; } = [];

    public ObservableCollection<LogEntryViewModel> Log { get; } = [];

    public string JournalDirectory { get; }

    public string PlaceholdersHelp => PathTemplate.PlaceholdersHelp;

    public bool IsIdle => !IsBusy;

    public bool HasPlan => PlanRows.Count > 0;

    public bool IsMoveMode
    {
        get => Mode == FileOperationMode.Move;
        set
        {
            if (value)
            {
                Mode = FileOperationMode.Move;
            }
        }
    }

    public bool IsCopyMode
    {
        get => Mode == FileOperationMode.Copy;
        set
        {
            if (value)
            {
                Mode = FileOperationMode.Copy;
            }
        }
    }

    public string UndoDescription => UndoableBatch is { } batch
        ? $"Можно отменить пакет от {batch.StartedAt:dd.MM.yyyy HH:mm}: " +
          $"{(batch.Mode == FileOperationMode.Move ? "перемещено" : "скопировано")} {batch.Summary.Done - batch.Summary.Reverted} файл(ов)"
        : "Нет операций для отмены";

    // ---------- Настройки ----------

    public void ApplySettings(AppSettings settings)
    {
        SourceFolder = settings.SourceFolder;
        TargetFolder = settings.TargetFolder;
        IncludeSubfolders = settings.IncludeSubfolders;
        Mode = settings.Mode;
        ApplyRuleSet(settings.RuleSet);
        RefreshUndoState();
    }

    public AppSettings CaptureSettings() => new()
    {
        SourceFolder = SourceFolder,
        TargetFolder = TargetFolder,
        IncludeSubfolders = IncludeSubfolders,
        Mode = Mode,
        RuleSet = CreateRuleSet(),
    };

    public void ApplyRuleSet(RuleSet ruleSet)
    {
        FileNameTemplate = ruleSet.FileNameTemplate;
        FallbackFolder = ruleSet.FallbackFolder;
        Rules.Clear();
        foreach (var rule in ruleSet.Rules)
        {
            Rules.Add(RuleViewModel.Create(rule));
        }

        SelectedRule = Rules.FirstOrDefault();
    }

    /// <summary>Снимок текущего набора правил. Глубокая копия: фоновые операции не видят правок в редакторе.</summary>
    public RuleSet CreateRuleSet() => RuleSetStore.Deserialize(RuleSetStore.Serialize(new RuleSet
    {
        FileNameTemplate = FileNameTemplate,
        FallbackFolder = FallbackFolder,
        Rules = Rules.Select(r => r.Model).ToList(),
    }));

    public void LoadRulesFromFile(string path)
    {
        ApplyRuleSet(_ruleSetStore.Load(path));
        AddLog(LogLevel.Info, $"Загружен набор правил: {path}");
    }

    public void RefreshUndoState()
    {
        UndoableBatch = _undoService.FindLastUndoable();
        OnPropertyChanged(nameof(UndoDescription));
    }

    partial void OnSourceFolderChanged(string value) => MarkPlanOutdated();

    partial void OnIncludeSubfoldersChanged(bool value) => MarkPlanOutdated();

    partial void OnTargetFolderChanged(string value) => MarkPlanOutdated();

    partial void OnFileNameTemplateChanged(string value) => MarkPlanOutdated();

    partial void OnFallbackFolderChanged(string value) => MarkPlanOutdated();

    private void MarkPlanOutdated()
    {
        if (HasPlan && !IsBusy)
        {
            IsPlanOutdated = true;
        }
    }

    // ---------- Папки ----------

    [RelayCommand]
    private void BrowseSource()
    {
        if (_dialogs.PickFolder("Исходная папка с PDF", SourceFolder) is { } folder)
        {
            SourceFolder = folder;
        }
    }

    [RelayCommand]
    private void BrowseTarget()
    {
        if (_dialogs.PickFolder("Целевая папка", TargetFolder) is { } folder)
        {
            TargetFolder = folder;
        }
    }

    [RelayCommand]
    private void OpenJournalFolder() => _dialogs.OpenFolder(JournalDirectory);

    // ---------- Правила ----------

    [RelayCommand]
    private void AddFileNameRule() => AddRule(new FileNameRule { Name = "Новое правило по имени", Pattern = "Счет_*", TargetFolder = "Счета" });

    [RelayCommand]
    private void AddDateRule() => AddRule(new DateRule { Name = "Новое правило по дате", Source = DateSource.FileNameOrLastModified });

    [RelayCommand]
    private void AddPdfTextRule() => AddRule(new PdfTextRule { Name = "Новое правило по тексту", Pattern = "Договор", TargetFolder = "Договоры" });

    [RelayCommand(CanExecute = nameof(HasSelectedRule))]
    private void RemoveRule()
    {
        var index = Rules.IndexOf(SelectedRule!);
        Rules.RemoveAt(index);
        SelectedRule = Rules.Count == 0 ? null : Rules[Math.Min(index, Rules.Count - 1)];
    }

    [RelayCommand(CanExecute = nameof(CanMoveRuleUp))]
    private void MoveRuleUp() => MoveSelectedRule(-1);

    [RelayCommand(CanExecute = nameof(CanMoveRuleDown))]
    private void MoveRuleDown() => MoveSelectedRule(+1);

    [RelayCommand]
    private void LoadRules()
    {
        if (_dialogs.PickOpenFile("Открыть набор правил", RulesFileFilter) is not { } path)
        {
            return;
        }

        try
        {
            LoadRulesFromFile(path);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError("Не удалось открыть набор правил", ex.Message);
        }
    }

    [RelayCommand]
    private void SaveRules()
    {
        if (_dialogs.PickSaveFile("Сохранить набор правил", RulesFileFilter, "правила.json") is not { } path)
        {
            return;
        }

        try
        {
            _ruleSetStore.Save(path, CreateRuleSet());
            AddLog(LogLevel.Info, $"Набор правил сохранён: {path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError("Не удалось сохранить набор правил", ex.Message);
        }
    }

    [RelayCommand]
    private void ResetRules()
    {
        if (_dialogs.Confirm("Правила по умолчанию", "Заменить текущие правила набором по умолчанию?"))
        {
            ApplyRuleSet(RuleSet.CreateDefault());
        }
    }

    private bool HasSelectedRule() => SelectedRule is not null;

    private bool CanMoveRuleUp() => SelectedRule is not null && Rules.IndexOf(SelectedRule) > 0;

    private bool CanMoveRuleDown() => SelectedRule is not null && Rules.IndexOf(SelectedRule) < Rules.Count - 1;

    private void AddRule(SortRule rule)
    {
        var viewModel = RuleViewModel.Create(rule);
        Rules.Add(viewModel);
        SelectedRule = viewModel;
    }

    private void MoveSelectedRule(int offset)
    {
        var index = Rules.IndexOf(SelectedRule!);
        Rules.Move(index, index + offset);
        MoveRuleUpCommand.NotifyCanExecuteChanged();
        MoveRuleDownCommand.NotifyCanExecuteChanged();
    }

    private void OnRulesChanged()
    {
        for (var i = 0; i < Rules.Count; i++)
        {
            Rules[i].Number = i + 1;
            Rules[i].PropertyChanged -= OnRulePropertyChanged;
            Rules[i].PropertyChanged += OnRulePropertyChanged;
        }

        MarkPlanOutdated();
    }

    private void OnRulePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(RuleViewModel.Number) or nameof(RuleViewModel.DisplayName)
            or nameof(RuleViewModel.Description) or nameof(RuleViewModel.ValidationError)))
        {
            MarkPlanOutdated();
        }
    }

    // ---------- Предпросмотр, пробный прогон, выполнение ----------

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task PreviewAsync() => RunBusyAsync("Строю план…", async cancellationToken =>
    {
        var plan = await BuildPlanAsync(cancellationToken);
        if (plan is not null)
        {
            StatusText = "Предпросмотр готов. Файлы не изменялись.";
            AddLog(LogLevel.Info, $"Предпросмотр: {PlanSummary}");
        }
    });

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task DryRunAsync() => RunBusyAsync("Пробный прогон…", cancellationToken => ExecutePlanAsync(dryRun: true, cancellationToken));

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task ExecuteAsync() => RunBusyAsync("Выполняю…", cancellationToken => ExecutePlanAsync(dryRun: false, cancellationToken));

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel()
    {
        _cancellation?.Cancel();
        StatusText = "Останавливаю…";
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private Task UndoAsync() => RunBusyAsync("Отменяю последнюю операцию…", async _ =>
    {
        var batch = _undoService.FindLastUndoable();
        if (batch is null)
        {
            StatusText = "Нет операций для отмены.";
            RefreshUndoState();
            return;
        }

        var count = batch.Summary.Done - batch.Summary.Reverted;
        var action = batch.Mode == FileOperationMode.Move ? $"вернуть на место {count} файл(ов)" : $"удалить {count} копий";
        if (!_dialogs.Confirm("Отменить последнюю операцию", $"Пакет от {batch.StartedAt:dd.MM.yyyy HH:mm:ss}: {action}. Продолжить?"))
        {
            return;
        }

        var result = await Task.Run(() => _undoService.Undo(batch));
        ShowUndoResult(result);
        RefreshUndoState();
    });

    private bool CanUndo() => !IsBusy && UndoableBatch is not null;

    private async Task ExecutePlanAsync(bool dryRun, CancellationToken cancellationToken)
    {
        var plan = await BuildPlanAsync(cancellationToken);
        if (plan is null)
        {
            return;
        }

        if (plan.ReadyCount == 0 && !dryRun)
        {
            StatusText = "Нечего выполнять: нет файлов, готовых к обработке.";
            return;
        }

        if (!dryRun)
        {
            var verb = Mode == FileOperationMode.Move ? "Переместить" : "Скопировать";
            if (!_dialogs.Confirm("Выполнить сортировку", $"{verb} {plan.ReadyCount} файл(ов) в «{plan.Job.TargetFolder}»?"))
            {
                StatusText = "Выполнение отменено пользователем.";
                return;
            }
        }

        var title = dryRun ? "Пробный прогон" : (Mode == FileOperationMode.Move ? "Перемещение" : "Копирование");
        AddLog(LogLevel.Info, $"{title}: начато, файлов в плане — {plan.Items.Count}.");
        var mode = Mode;
        var progress = new Progress<ExecutionProgress>(p => OnExecutionProgress(p, plan.Job));

        // Токен не передаём в Task.Run: исполнитель сам корректно останавливается и сохраняет журнал.
        var result = await Task.Run(() => _executor.Execute(plan, mode, dryRun, progress, cancellationToken), CancellationToken.None);

        var summary = result.Summary;
        var doneText = dryRun ? $"проверено {summary.DryRun}" : $"выполнено {summary.Done}";
        var text = $"{title}: {doneText}, пропущено {summary.Skipped}, ошибок {summary.Failed}" +
                   (result.WasCancelled ? $", остановлено до выполнения {summary.Cancelled}" : string.Empty) + ".";
        AddLog(result.WasCancelled || summary.Failed > 0 ? LogLevel.Warning : LogLevel.Success, text);
        AddLog(LogLevel.Info, $"Журнал: {result.JournalPath}");
        StatusText = text + (dryRun ? " Файлы не изменялись." : string.Empty);
        RefreshUndoState();
    }

    private async Task<SortPlan?> BuildPlanAsync(CancellationToken cancellationToken)
    {
        var job = new SortJob
        {
            SourceFolder = SourceFolder.Trim(),
            TargetFolder = TargetFolder.Trim(),
            IncludeSubfolders = IncludeSubfolders,
            RuleSet = CreateRuleSet(),
        };

        var progress = new Progress<int>(percent =>
        {
            Progress = percent;
            ProgressText = $"Анализ файлов: {percent}%";
        });

        SortPlan plan;
        try
        {
            plan = await Task.Run(() => _planner.BuildPlan(job, progress, cancellationToken), cancellationToken);
        }
        catch (SortConfigurationException ex)
        {
            StatusText = "Проверьте настройки.";
            AddLog(LogLevel.Error, ex.Message.Replace(Environment.NewLine, " "));
            _dialogs.ShowWarning("Проверьте настройки", ex.Message);
            return null;
        }

        ShowPlan(plan);
        return plan;
    }

    private void ShowPlan(SortPlan plan)
    {
        PlanRows.Clear();
        var number = 1;
        foreach (var item in plan.Items)
        {
            PlanRows.Add(new PlanRowViewModel(number++, item, plan.Job.SourceFolder, plan.Job.TargetFolder, Mode));
        }

        PlanSummary = plan.Items.Count == 0
            ? "PDF-файлы не найдены"
            : $"Файлов: {plan.Items.Count} · к обработке: {plan.ReadyCount} · пропущено: {plan.SkippedCount} · ошибок: {plan.ErrorCount}";
        IsPlanOutdated = false;
        OnPropertyChanged(nameof(HasPlan));
    }

    private void OnExecutionProgress(ExecutionProgress progress, SortJob job)
    {
        Progress = progress.Processed * 100.0 / progress.Total;
        ProgressText = $"{progress.Processed} из {progress.Total}";
        if (progress.ItemIndex < PlanRows.Count)
        {
            PlanRows[progress.ItemIndex].Apply(progress.Operation);
        }

        var operation = progress.Operation;
        var source = PathUtil.ToDisplay(operation.Source, job.SourceFolder);
        var destination = operation.Destination is null ? null : PathUtil.ToDisplay(operation.Destination, job.TargetFolder);
        switch (operation.Status)
        {
            case OperationStatus.Done:
                AddLog(LogLevel.Success, $"{(operation.Action == FileOperationMode.Move ? "Перемещён" : "Скопирован")}: {source} → {destination}");
                break;
            case OperationStatus.DryRun:
                AddLog(LogLevel.Info, $"Пробный прогон: {source} → {destination}");
                break;
            case OperationStatus.Failed:
                AddLog(LogLevel.Error, $"Ошибка: {source} — {operation.Message}");
                break;
            case OperationStatus.Skipped:
                AddLog(LogLevel.Info, $"Пропущен: {source} — {operation.Message}");
                break;
        }
    }

    private void ShowUndoResult(UndoResult result)
    {
        var reverted = result.Batch.Operations.Where(o => o.Status == OperationStatus.Done).ToList();
        foreach (var row in PlanRows)
        {
            if (reverted.FirstOrDefault(o => PathUtil.AreSame(o.Source, row.SourcePath)) is { } operation)
            {
                row.MarkReverted(operation);
            }
        }

        var text = $"Отмена пакета от {result.Batch.StartedAt:dd.MM.yyyy HH:mm:ss}: откачено {result.RevertedCount}, " +
                   $"удалено пустых папок {result.RemovedDirectoryCount}" +
                   (result.IsComplete ? "." : $", не удалось — {result.Errors.Count}.");
        AddLog(result.IsComplete ? LogLevel.Success : LogLevel.Warning, text);
        foreach (var error in result.Errors)
        {
            AddLog(LogLevel.Error, error);
        }

        StatusText = text;
    }

    private async Task RunBusyAsync(string status, Func<CancellationToken, Task> action)
    {
        _cancellation = new CancellationTokenSource();
        IsBusy = true;
        StatusText = status;
        Progress = 0;
        ProgressText = string.Empty;
        try
        {
            await action(_cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Операция остановлена пользователем.";
            AddLog(LogLevel.Warning, StatusText);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException)
        {
            StatusText = "Операция завершилась с ошибкой.";
            AddLog(LogLevel.Error, ex.Message);
            _dialogs.ShowError("Ошибка", ex.Message);
        }
        finally
        {
            IsBusy = false;
            _cancellation.Dispose();
            _cancellation = null;
        }
    }

    private void AddLog(LogLevel level, string text) => Log.Add(new LogEntryViewModel(DateTime.Now, level, text));
}
