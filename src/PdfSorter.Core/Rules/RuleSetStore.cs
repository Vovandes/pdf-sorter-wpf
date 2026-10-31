using System.IO.Abstractions;
using System.Text.Json;
using PdfSorter.Core.Serialization;

namespace PdfSorter.Core.Rules;

/// <summary>Сохранение и загрузка набора правил в JSON.</summary>
public sealed class RuleSetStore
{
    private readonly IFileSystem _fileSystem;

    public RuleSetStore(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public static string Serialize(RuleSet ruleSet) => JsonSerializer.Serialize(ruleSet, JsonDefaults.Options);

    /// <exception cref="InvalidDataException">Файл не является корректным набором правил.</exception>
    public static RuleSet Deserialize(string json)
    {
        try
        {
            var ruleSet = JsonSerializer.Deserialize<RuleSet>(json, JsonDefaults.Options)
                ?? throw new InvalidDataException("Файл правил пуст.");
            if (ruleSet.Version > RuleSet.CurrentVersion)
            {
                throw new InvalidDataException(
                    $"Файл правил создан более новой версией программы (версия формата {ruleSet.Version}).");
            }

            ruleSet.Rules ??= [];
            return ruleSet;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidDataException($"Файл правил повреждён: {ex.Message}", ex);
        }
    }

    public void Save(string path, RuleSet ruleSet)
    {
        var directory = _fileSystem.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            _fileSystem.Directory.CreateDirectory(directory);
        }

        _fileSystem.File.WriteAllText(path, Serialize(ruleSet));
    }

    public RuleSet Load(string path) => Deserialize(_fileSystem.File.ReadAllText(path));
}
