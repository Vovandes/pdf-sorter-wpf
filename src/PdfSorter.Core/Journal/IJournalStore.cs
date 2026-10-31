namespace PdfSorter.Core.Journal;

/// <summary>Хранилище журнала операций.</summary>
public interface IJournalStore
{
    /// <summary>Папка, где лежат файлы журнала.</summary>
    string Location { get; }

    /// <summary>Сохраняет (перезаписывает) пакет и возвращает путь к его файлу.</summary>
    string Save(JournalBatch batch);

    /// <summary>Все пакеты, новые первыми. Повреждённые файлы пропускаются.</summary>
    IReadOnlyList<JournalBatch> LoadAll();

    /// <summary>Последний пакет, который можно отменить, или <c>null</c>.</summary>
    JournalBatch? FindLastUndoable();
}
