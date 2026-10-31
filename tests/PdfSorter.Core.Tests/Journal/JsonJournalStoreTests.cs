using System.Text.Json;
using PdfSorter.Core.Journal;
using PdfSorter.Core.Tests.Fakes;
using static PdfSorter.Core.Tests.Fakes.SortingFixture;

namespace PdfSorter.Core.Tests.Journal;

public class JsonJournalStoreTests
{
    private readonly SortingFixture _fixture = new();

    [Fact]
    public void Journal_IsReadableJsonWithSummaryAndOperations()
    {
        _fixture.AddPdf("Счет_1.pdf").AddPdf("Памятка.pdf");
        var result = _fixture.Executor.Execute(_fixture.Plan(NameRule("Счет*", "Счета")), FileOperationMode.Copy);

        var json = _fixture.FileSystem.File.ReadAllText(result.JournalPath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.StartsWith(Logs, result.JournalPath);
        Assert.Equal("Copy", root.GetProperty("mode").GetString());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("done").GetInt32());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("skipped").GetInt32());
        Assert.Equal(2, root.GetProperty("operations").GetArrayLength());
        Assert.Contains("Счет_1.pdf", json); // кириллица без \u-экранирования
    }

    [Fact]
    public void LoadAll_ReturnsNewestFirst_AndSkipsCorruptFiles()
    {
        _fixture.AddPdf("a.pdf");
        var first = _fixture.Executor.Execute(_fixture.Plan(NameRule("*", "A")), FileOperationMode.Copy, dryRun: true);
        var second = _fixture.Executor.Execute(_fixture.Plan(NameRule("*", "B")), FileOperationMode.Copy, dryRun: true);
        _fixture.FileSystem.AddFile(Path.Combine(Logs, "batch_corrupt.json"), "{ не json");

        var batches = _fixture.Journal.LoadAll();

        Assert.Equal([second.Batch.Id, first.Batch.Id], batches.Select(b => b.Id));
    }
}
