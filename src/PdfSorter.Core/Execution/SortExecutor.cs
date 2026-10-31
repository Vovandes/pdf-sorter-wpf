using System.IO.Abstractions;
using PdfSorter.Core.IO;
using PdfSorter.Core.Journal;
using PdfSorter.Core.Naming;
using PdfSorter.Core.Planning;

namespace PdfSorter.Core.Execution;

/// <summary>
/// Исполняет план: перемещает или копирует файлы и пишет каждую операцию в журнал.
/// Ошибка одного файла не останавливает пакет; отмена через <see cref="CancellationToken"/>
/// останавливает обработку, но уже сделанное попадает в журнал и может быть откачено.
/// </summary>
public sealed class SortExecutor
{
    /// <summary>Как часто сбрасывать журнал на диск во время длинного пакета.</summary>
    private const int JournalFlushInterval = 25;

    private readonly IFileSystem _fileSystem;
    private readonly IJournalStore _journal;
    private readonly TimeProvider _timeProvider;

    public SortExecutor(IFileSystem fileSystem, IJournalStore journal, TimeProvider? timeProvider = null)
    {
        _fileSystem = fileSystem;
        _journal = journal;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <param name="plan">План из <see cref="SortPlanner"/>.</param>
    /// <param name="mode">Перемещение или копирование.</param>
    /// <param name="dryRun">Пробный прогон: журнал пишется, файлы не трогаются.</param>
    /// <param name="progress">Получает сообщение после каждой строки плана.</param>
    /// <param name="cancellationToken">Остановка пакета; оставшиеся строки помечаются как отменённые.</param>
    public ExecutionResult Execute(
        SortPlan plan,
        FileOperationMode mode,
        bool dryRun = false,
        IProgress<ExecutionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = _timeProvider.GetLocalNow();
        var batch = new JournalBatch
        {
            Id = CreateBatchId(startedAt, dryRun),
            StartedAt = startedAt,
            Mode = mode,
            IsDryRun = dryRun,
            SourceFolder = plan.Job.SourceFolder,
            TargetFolder = plan.Job.TargetFolder,
        };

        var journalPath = _journal.Save(batch);
        try
        {
            for (var index = 0; index < plan.Items.Count; index++)
            {
                var item = plan.Items[index];
                JournalOperation operation;
                if (cancellationToken.IsCancellationRequested && item.Status == PlanItemStatus.Ready)
                {
                    batch.WasCancelled = true;
                    operation = CreateOperation(item, mode, OperationStatus.Cancelled, "пакет остановлен пользователем");
                }
                else
                {
                    operation = Process(item, mode, dryRun, batch);
                }

                batch.Operations.Add(operation);
                progress?.Report(new ExecutionProgress(index, index + 1, plan.Items.Count, operation));

                if ((index + 1) % JournalFlushInterval == 0)
                {
                    _journal.Save(batch);
                }
            }
        }
        finally
        {
            batch.FinishedAt = _timeProvider.GetLocalNow();
            journalPath = _journal.Save(batch);
        }

        return new ExecutionResult(batch, journalPath);
    }

    private JournalOperation Process(PlanItem item, FileOperationMode mode, bool dryRun, JournalBatch batch)
    {
        switch (item.Status)
        {
            case PlanItemStatus.Skipped:
                return CreateOperation(item, mode, OperationStatus.Skipped, item.Message);
            case PlanItemStatus.Error:
                return CreateOperation(item, mode, OperationStatus.Failed, item.Message);
        }

        if (dryRun)
        {
            return CreateOperation(item, mode, OperationStatus.DryRun, "пробный прогон: файл не изменён");
        }

        var operation = CreateOperation(item, mode, OperationStatus.Done, item.Message);
        try
        {
            var source = item.SourcePath;
            if (!_fileSystem.File.Exists(source))
            {
                throw new FileNotFoundException("Исходный файл не найден.", source);
            }

            // Пока план ждал выполнения, на целевом месте мог появиться файл — подберём свободное имя ещё раз.
            var destination = UniquePathResolver.Resolve(item.DestinationPath!, _fileSystem.File.Exists);
            EnsureDirectory(_fileSystem.Path.GetDirectoryName(destination)!, batch.CreatedDirectories);

            if (mode == FileOperationMode.Move)
            {
                _fileSystem.File.Move(source, destination);
            }
            else
            {
                _fileSystem.File.Copy(source, destination, overwrite: false);
            }

            operation.Destination = destination;
            if (!PathUtil.AreSame(destination, item.DestinationPath!))
            {
                operation.Message = UniquePathResolver.ConflictMessage;
            }
        }
        catch (Exception ex) when (ErrorDescriber.IsFileLevel(ex))
        {
            operation.Status = OperationStatus.Failed;
            operation.Message = ErrorDescriber.Describe(ex);
        }

        return operation;
    }

    /// <summary>Создаёт папку и запоминает все уровни, которых раньше не было, — чтобы отмена могла их удалить.</summary>
    private void EnsureDirectory(string directory, List<string> createdDirectories)
    {
        var missing = new Stack<string>();
        for (var current = directory; !string.IsNullOrEmpty(current) && !_fileSystem.Directory.Exists(current);
             current = _fileSystem.Path.GetDirectoryName(current))
        {
            missing.Push(current);
        }

        if (missing.Count == 0)
        {
            return;
        }

        _fileSystem.Directory.CreateDirectory(directory);
        createdDirectories.AddRange(missing);
    }

    private JournalOperation CreateOperation(PlanItem item, FileOperationMode mode, OperationStatus status, string? message) =>
        new()
        {
            Source = item.SourcePath,
            Destination = item.DestinationPath,
            Action = mode,
            Rule = item.RuleName,
            Status = status,
            Message = message,
            Timestamp = _timeProvider.GetLocalNow(),
        };

    private static string CreateBatchId(DateTimeOffset startedAt, bool dryRun) =>
        $"{startedAt:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid().ToString("N")[..6]}{(dryRun ? "-dryrun" : string.Empty)}";
}
