using System.IO.Abstractions.TestingHelpers;
using PdfSorter.Core.Execution;
using PdfSorter.Core.Journal;
using PdfSorter.Core.Planning;
using PdfSorter.Core.Rules;

namespace PdfSorter.Core.Tests.Fakes;

/// <summary>Окружение для тестов планировщика, исполнителя и отмены — всё в памяти, диск не трогается.</summary>
internal sealed class SortingFixture
{
    public const string Source = @"C:\in";
    public const string Target = @"C:\out";
    public const string Logs = @"C:\logs";

    public SortingFixture()
    {
        FileSystem.AddDirectory(Source);
        Journal = new JsonJournalStore(FileSystem, Logs);
        Planner = new SortPlanner(FileSystem, Reader);
        Executor = new SortExecutor(FileSystem, Journal, Time);
        Undo = new UndoService(FileSystem, Journal, Time);
    }

    public MockFileSystem FileSystem { get; } = new();

    public FakePdfTextReader Reader { get; } = new();

    public ManualTimeProvider Time { get; } = new();

    public JsonJournalStore Journal { get; }

    public SortPlanner Planner { get; }

    public SortExecutor Executor { get; }

    public UndoService Undo { get; }

    public static string In(string relativePath) => Path.Combine(Source, relativePath);

    public static string Out(string relativePath) => Path.Combine(Target, relativePath);

    /// <summary>Добавляет «PDF» (содержимое неважно — текст отдаёт <see cref="FakePdfTextReader"/>).</summary>
    public SortingFixture AddPdf(string relativePath, string text = "", DateTime? modified = null)
    {
        var path = In(relativePath);
        FileSystem.AddFile(path, new MockFileData($"%PDF-1.7 {relativePath}")
        {
            LastWriteTime = modified ?? new DateTime(2024, 11, 5),
        });
        Reader.WithText(path, text);
        return this;
    }

    public SortJob Job(RuleSet ruleSet, bool includeSubfolders = true, string target = Target) => new()
    {
        SourceFolder = Source,
        TargetFolder = target,
        IncludeSubfolders = includeSubfolders,
        RuleSet = ruleSet,
    };

    public SortPlan Plan(RuleSet ruleSet, bool includeSubfolders = true) => Planner.BuildPlan(Job(ruleSet, includeSubfolders));

    /// <summary>Снимок файловой системы (без журналов): пути файлов с содержимым и пути папок.</summary>
    public SortedDictionary<string, string> Snapshot()
    {
        var snapshot = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in FileSystem.AllFiles.Where(f => !f.StartsWith(Logs, StringComparison.OrdinalIgnoreCase)))
        {
            snapshot[file] = FileSystem.GetFile(file).TextContents;
        }

        foreach (var directory in FileSystem.AllDirectories.Where(d => !d.StartsWith(Logs, StringComparison.OrdinalIgnoreCase)))
        {
            snapshot[directory + @"\"] = "<dir>";
        }

        return snapshot;
    }

    /// <summary>Простой набор правил: одно правило по маске имени.</summary>
    public static RuleSet NameRule(string mask, string folder, string? nameTemplate = null, string fallback = "") => new()
    {
        FallbackFolder = fallback,
        Rules = [new FileNameRule { Name = folder, Pattern = mask, TargetFolder = folder, FileNameTemplate = nameTemplate }],
    };
}
