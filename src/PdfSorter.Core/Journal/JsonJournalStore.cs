using System.IO.Abstractions;
using System.Text.Json;
using PdfSorter.Core.Serialization;

namespace PdfSorter.Core.Journal;

/// <summary>Журнал в виде JSON-файлов: один пакет — один файл <c>batch_….json</c>.</summary>
public sealed class JsonJournalStore : IJournalStore
{
    private const string FilePrefix = "batch_";

    private readonly IFileSystem _fileSystem;

    public JsonJournalStore(IFileSystem fileSystem, string directory)
    {
        _fileSystem = fileSystem;
        Location = directory;
    }

    /// <summary><c>%LOCALAPPDATA%\PdfSorter\logs</c>.</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfSorter", "logs");

    public string Location { get; }

    public string GetFilePath(JournalBatch batch) => _fileSystem.Path.Combine(Location, $"{FilePrefix}{batch.Id}.json");

    public string Save(JournalBatch batch)
    {
        _fileSystem.Directory.CreateDirectory(Location);
        var path = GetFilePath(batch);
        _fileSystem.File.WriteAllText(path, JsonSerializer.Serialize(batch, JsonDefaults.Options));
        return path;
    }

    public IReadOnlyList<JournalBatch> LoadAll()
    {
        if (!_fileSystem.Directory.Exists(Location))
        {
            return [];
        }

        var batches = new List<JournalBatch>();
        foreach (var path in _fileSystem.Directory.EnumerateFiles(Location, FilePrefix + "*.json"))
        {
            try
            {
                if (JsonSerializer.Deserialize<JournalBatch>(_fileSystem.File.ReadAllText(path), JsonDefaults.Options) is { } batch)
                {
                    batches.Add(batch);
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                // Повреждённый или недоступный файл журнала не должен мешать работе с остальными.
            }
        }

        return batches
            .OrderByDescending(b => b.StartedAt)
            .ThenByDescending(b => b.Id, StringComparer.Ordinal)
            .ToList();
    }

    public JournalBatch? FindLastUndoable() => LoadAll().FirstOrDefault(b => b.CanUndo);
}
