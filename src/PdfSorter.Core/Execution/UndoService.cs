using System.IO.Abstractions;
using PdfSorter.Core.IO;
using PdfSorter.Core.Journal;

namespace PdfSorter.Core.Execution;

/// <summary>Итог отмены пакета.</summary>
public sealed record UndoResult(JournalBatch Batch, int RevertedCount, int RemovedDirectoryCount, IReadOnlyList<string> Errors)
{
    public bool IsComplete => Errors.Count == 0;
}

/// <summary>
/// Откатывает пакет по журналу: перемещённые файлы возвращает на место, копии удаляет,
/// созданные пакетом папки удаляет, если они остались пустыми. Чужие файлы не перезаписываются.
/// </summary>
public sealed class UndoService
{
    private readonly IFileSystem _fileSystem;
    private readonly IJournalStore _journal;
    private readonly TimeProvider _timeProvider;

    public UndoService(IFileSystem fileSystem, IJournalStore journal, TimeProvider? timeProvider = null)
    {
        _fileSystem = fileSystem;
        _journal = journal;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Пакет, который будет отменён командой <see cref="UndoLast"/>, или <c>null</c>.</summary>
    public JournalBatch? FindLastUndoable() => _journal.FindLastUndoable();

    /// <summary>Отменяет последний неотменённый пакет. Возвращает <c>null</c>, если отменять нечего.</summary>
    public UndoResult? UndoLast() => FindLastUndoable() is { } batch ? Undo(batch) : null;

    public UndoResult Undo(JournalBatch batch)
    {
        if (batch.IsDryRun)
        {
            throw new InvalidOperationException("Пробный прогон не менял файлы — отменять нечего.");
        }

        var errors = new List<string>();
        var reverted = 0;

        // Обратный порядок: последний перемещённый файл возвращается первым.
        var operations = batch.Operations
            .Where(o => o.Status == OperationStatus.Done && !o.Reverted)
            .Reverse()
            .ToList();
        foreach (var operation in operations)
        {
            try
            {
                Revert(operation);
                operation.Reverted = true;
                operation.RevertError = null;
                reverted++;
            }
            catch (Exception ex) when (ErrorDescriber.IsFileLevel(ex))
            {
                operation.RevertError = ErrorDescriber.Describe(ex);
                errors.Add($"{operation.Destination}: {operation.RevertError}");
            }
        }

        var removedDirectories = RemoveCreatedDirectories(batch);
        if (errors.Count == 0)
        {
            batch.UndoneAt = _timeProvider.GetLocalNow();
        }

        _journal.Save(batch);
        return new UndoResult(batch, reverted, removedDirectories, errors);
    }

    private void Revert(JournalOperation operation)
    {
        var destination = operation.Destination
            ?? throw new InvalidDataException("В журнале не указан новый путь файла.");

        if (operation.Action == FileOperationMode.Copy)
        {
            if (!_fileSystem.File.Exists(destination))
            {
                return; // копию уже удалили вручную — цель отмены достигнута
            }

            if (_fileSystem.File.Exists(operation.Source)
                && _fileSystem.FileInfo.New(operation.Source).Length != _fileSystem.FileInfo.New(destination).Length)
            {
                throw new IOException("копия была изменена после сортировки — оставлена на месте");
            }

            _fileSystem.File.Delete(destination);
            return;
        }

        if (!_fileSystem.File.Exists(destination))
        {
            throw new FileNotFoundException("Файл не найден по новому пути.", destination);
        }

        if (_fileSystem.File.Exists(operation.Source))
        {
            throw new IOException("на исходном месте уже есть другой файл — перезаписывать не будем");
        }

        var sourceDirectory = _fileSystem.Path.GetDirectoryName(operation.Source);
        if (!string.IsNullOrEmpty(sourceDirectory))
        {
            _fileSystem.Directory.CreateDirectory(sourceDirectory);
        }

        _fileSystem.File.Move(destination, operation.Source);
    }

    /// <summary>Удаляет созданные пакетом папки, начиная с самых вложенных, но только пустые.</summary>
    private int RemoveCreatedDirectories(JournalBatch batch)
    {
        var removed = 0;
        foreach (var directory in Enumerable.Reverse(batch.CreatedDirectories))
        {
            try
            {
                if (_fileSystem.Directory.Exists(directory) && !_fileSystem.Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    _fileSystem.Directory.Delete(directory);
                    removed++;
                }
            }
            catch (Exception ex) when (ErrorDescriber.IsFileLevel(ex))
            {
                // Папку не удалось удалить (например, открыта в Проводнике) — это не повод считать отмену неудачной.
            }
        }

        return removed;
    }
}
