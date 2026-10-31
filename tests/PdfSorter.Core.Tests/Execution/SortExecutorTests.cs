using PdfSorter.Core.Execution;
using PdfSorter.Core.Journal;
using PdfSorter.Core.Planning;
using PdfSorter.Core.Tests.Fakes;
using static PdfSorter.Core.Tests.Fakes.SortingFixture;

namespace PdfSorter.Core.Tests.Execution;

public class SortExecutorTests
{
    private readonly SortingFixture _fixture = new();

    public SortExecutorTests()
    {
        _fixture
            .AddPdf("Счет_1.pdf")
            .AddPdf(@"Сканы\Счет_2.pdf")
            .AddPdf("Памятка.pdf");
    }

    private SortPlan CreatePlan() => _fixture.Plan(NameRule("Счет*", "Счета/{yyyy}", fallback: "Прочее"));

    [Fact]
    public void DryRun_DoesNotChangeFileSystem_ButWritesJournal()
    {
        var before = _fixture.Snapshot();

        var result = _fixture.Executor.Execute(CreatePlan(), FileOperationMode.Move, dryRun: true);

        Assert.Equal(before, _fixture.Snapshot());
        Assert.True(result.Batch.IsDryRun);
        Assert.All(result.Batch.Operations, o => Assert.Equal(OperationStatus.DryRun, o.Status));
        Assert.True(_fixture.FileSystem.File.Exists(result.JournalPath));
        Assert.False(result.Batch.CanUndo);
    }

    [Fact]
    public void Move_MovesFilesAndRecordsCreatedFolders()
    {
        var result = _fixture.Executor.Execute(CreatePlan(), FileOperationMode.Move);

        Assert.Equal(3, result.Summary.Done);
        Assert.True(_fixture.FileSystem.File.Exists(Out(@"Счета\2024\Счет_1.pdf")));
        Assert.True(_fixture.FileSystem.File.Exists(Out(@"Счета\2024\Счет_2.pdf")));
        Assert.True(_fixture.FileSystem.File.Exists(Out(@"Прочее\Памятка.pdf")));
        Assert.False(_fixture.FileSystem.File.Exists(In("Счет_1.pdf")));
        // Файлы обрабатываются по алфавиту: «Памятка», «Сканы\Счет_2», «Счет_1».
        Assert.Equal(
            [Target, Out("Прочее"), Out("Счета"), Out(@"Счета\2024")],
            result.Batch.CreatedDirectories);
    }

    [Fact]
    public void Copy_KeepsSourceFiles()
    {
        var result = _fixture.Executor.Execute(CreatePlan(), FileOperationMode.Copy);

        Assert.Equal(3, result.Summary.Done);
        Assert.True(_fixture.FileSystem.File.Exists(In("Счет_1.pdf")));
        Assert.Equal(
            _fixture.FileSystem.File.ReadAllText(In("Счет_1.pdf")),
            _fixture.FileSystem.File.ReadAllText(Out(@"Счета\2024\Счет_1.pdf")));
    }

    [Fact]
    public void FailedFile_DoesNotStopTheBatch()
    {
        var plan = CreatePlan();
        _fixture.FileSystem.File.Delete(In(@"Сканы\Счет_2.pdf")); // файл исчез между предпросмотром и выполнением

        var result = _fixture.Executor.Execute(plan, FileOperationMode.Move);

        var failed = Assert.Single(result.Batch.Operations, o => o.Status == OperationStatus.Failed);
        Assert.Equal(In(@"Сканы\Счет_2.pdf"), failed.Source);
        Assert.Contains("не найден", failed.Message);
        Assert.Equal(2, result.Summary.Done);
    }

    [Fact]
    public void DestinationTakenAfterPlanning_GetsNumericSuffix()
    {
        var plan = CreatePlan();
        _fixture.FileSystem.AddFile(Out(@"Прочее\Памятка.pdf"), "появился позже");

        var result = _fixture.Executor.Execute(plan, FileOperationMode.Move);

        var memo = result.Batch.Operations.Single(o => o.Source == In("Памятка.pdf"));
        Assert.Equal(Out(@"Прочее\Памятка (2).pdf"), memo.Destination);
        Assert.Equal("появился позже", _fixture.FileSystem.File.ReadAllText(Out(@"Прочее\Памятка.pdf")));
    }

    [Fact]
    public void Cancellation_StopsBatchAndKeepsDoneOperationsInJournal()
    {
        using var cancellation = new CancellationTokenSource();
        var progress = new SyncProgress<ExecutionProgress>(p =>
        {
            if (p.Processed == 1)
            {
                cancellation.Cancel();
            }
        });

        var result = _fixture.Executor.Execute(CreatePlan(), FileOperationMode.Move, progress: progress, cancellationToken: cancellation.Token);

        Assert.True(result.WasCancelled);
        Assert.Equal(
            [OperationStatus.Done, OperationStatus.Cancelled, OperationStatus.Cancelled],
            result.Batch.Operations.Select(o => o.Status));
        Assert.True(_fixture.FileSystem.File.Exists(In(@"Сканы\Счет_2.pdf")));
        Assert.Equal(result.Batch.Id, _fixture.Journal.FindLastUndoable()?.Id); // сделанное можно откатить
    }

    [Fact]
    public void SkippedAndErrorRowsOfPlan_AreRecordedInJournal()
    {
        var plan = _fixture.Plan(NameRule("Счет*", "Счета")); // без папки «прочее» памятка пропускается

        var result = _fixture.Executor.Execute(plan, FileOperationMode.Move);

        var skipped = Assert.Single(result.Batch.Operations, o => o.Status == OperationStatus.Skipped);
        Assert.Equal(In("Памятка.pdf"), skipped.Source);
        Assert.Equal("ни одно правило не подошло", skipped.Message);
    }
}
