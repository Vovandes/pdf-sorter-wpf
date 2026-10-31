using PdfSorter.Core.Journal;
using PdfSorter.Core.Tests.Fakes;
using static PdfSorter.Core.Tests.Fakes.SortingFixture;

namespace PdfSorter.Core.Tests.Execution;

public class UndoServiceTests
{
    private readonly SortingFixture _fixture = new();

    public UndoServiceTests()
    {
        _fixture
            .AddPdf("Счет_1.pdf")
            .AddPdf(@"Сканы\Счет_2.pdf")
            .AddPdf("Памятка.pdf");
    }

    [Fact]
    public void UndoMove_RestoresFilesAndRemovesCreatedFolders()
    {
        var before = _fixture.Snapshot();
        _fixture.Executor.Execute(_fixture.Plan(NameRule("Счет*", "Счета/{yyyy}/{MM}", fallback: "Прочее")), FileOperationMode.Move);

        var result = _fixture.Undo.UndoLast();

        Assert.NotNull(result);
        Assert.True(result.IsComplete);
        Assert.Equal(3, result.RevertedCount);
        Assert.Equal(5, result.RemovedDirectoryCount); // out, Счета, 2024, 11, Прочее
        Assert.Equal(before, _fixture.Snapshot());
        Assert.NotNull(result.Batch.UndoneAt);
    }

    [Fact]
    public void UndoCopy_DeletesOnlyCopies()
    {
        var before = _fixture.Snapshot();
        _fixture.Executor.Execute(_fixture.Plan(NameRule("Счет*", "Счета")), FileOperationMode.Copy);

        var result = _fixture.Undo.UndoLast();

        Assert.Equal(2, result?.RevertedCount);
        Assert.Equal(before, _fixture.Snapshot());
    }

    [Fact]
    public void Undo_KeepsFolderWithForeignFiles()
    {
        _fixture.Executor.Execute(_fixture.Plan(NameRule("Счет*", "Счета")), FileOperationMode.Move);
        _fixture.FileSystem.AddFile(Out(@"Счета\положил пользователь.pdf"), "чужой файл");

        _fixture.Undo.UndoLast();

        Assert.True(_fixture.FileSystem.File.Exists(Out(@"Счета\положил пользователь.pdf")));
        Assert.True(_fixture.FileSystem.File.Exists(In("Счет_1.pdf")));
    }

    [Fact]
    public void UndoLast_WorksLikeStack_AndIgnoresDryRuns()
    {
        _fixture.Executor.Execute(_fixture.Plan(NameRule("Счет*", "Счета")), FileOperationMode.Move);
        var second = _fixture.Executor.Execute(_fixture.Plan(NameRule("Памятка*", "Памятки")), FileOperationMode.Move);
        _fixture.Executor.Execute(_fixture.Plan(NameRule("*", "Всё")), FileOperationMode.Move, dryRun: true);

        var firstUndo = _fixture.Undo.UndoLast();
        Assert.Equal(second.Batch.Id, firstUndo?.Batch.Id);
        Assert.True(_fixture.FileSystem.File.Exists(In("Памятка.pdf")));
        Assert.False(_fixture.FileSystem.File.Exists(In("Счет_1.pdf"))); // первый пакет пока не тронут

        var secondUndo = _fixture.Undo.UndoLast();
        Assert.NotEqual(second.Batch.Id, secondUndo?.Batch.Id);
        Assert.True(_fixture.FileSystem.File.Exists(In("Счет_1.pdf")));

        Assert.Null(_fixture.Undo.UndoLast());
    }

    [Fact]
    public void Undo_DoesNotOverwriteFileThatReappearedAtSource()
    {
        _fixture.Executor.Execute(_fixture.Plan(NameRule("Счет*", "Счета")), FileOperationMode.Move);
        _fixture.FileSystem.AddFile(In("Счет_1.pdf"), "новый файл с тем же именем");

        var result = _fixture.Undo.UndoLast();

        Assert.NotNull(result);
        Assert.False(result.IsComplete);
        Assert.Equal(1, result.RevertedCount);
        Assert.Single(result.Errors);
        Assert.Equal("новый файл с тем же именем", _fixture.FileSystem.File.ReadAllText(In("Счет_1.pdf")));
        Assert.True(_fixture.FileSystem.File.Exists(Out(@"Счета\Счет_1.pdf")));
        Assert.Null(result.Batch.UndoneAt);
        Assert.Equal(result.Batch.Id, _fixture.Journal.FindLastUndoable()?.Id); // можно повторить после разбора
    }
}
