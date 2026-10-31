using PdfSorter.Core.Pdf;
using PdfSorter.Core.Planning;
using PdfSorter.Core.Rules;
using PdfSorter.Core.Tests.Fakes;
using static PdfSorter.Core.Tests.Fakes.SortingFixture;

namespace PdfSorter.Core.Tests.Planning;

public class SortPlannerTests
{
    private readonly SortingFixture _fixture = new();

    [Fact]
    public void FirstMatchingRuleWins()
    {
        _fixture.AddPdf("Счет_1.pdf", text: "Акт выполненных работ");
        var ruleSet = new RuleSet
        {
            Rules =
            [
                new FileNameRule { Name = "Счета", Pattern = "Счет_*", TargetFolder = "Счета" },
                new PdfTextRule { Name = "Акты", Pattern = "Акт", TargetFolder = "Акты" },
            ],
        };

        var item = Assert.Single(_fixture.Plan(ruleSet).Items);

        Assert.Equal(PlanItemStatus.Ready, item.Status);
        Assert.Equal("Счета", item.RuleName);
        Assert.Equal(Out(@"Счета\Счет_1.pdf"), item.DestinationPath);
        Assert.Empty(_fixture.Reader.ReadPaths); // до правила по тексту дело не дошло — PDF не открывался
    }

    [Fact]
    public void DisabledRuleIsIgnored()
    {
        _fixture.AddPdf("Счет_1.pdf");
        var ruleSet = new RuleSet
        {
            Rules =
            [
                new FileNameRule { Name = "Выключено", Pattern = "*", TargetFolder = "A", IsEnabled = false },
                new FileNameRule { Name = "Счета", Pattern = "Счет*", TargetFolder = "B" },
            ],
        };

        Assert.Equal("Счета", Assert.Single(_fixture.Plan(ruleSet).Items).RuleName);
    }

    [Fact]
    public void UnmatchedFiles_GoToFallbackFolderOrAreSkipped()
    {
        _fixture.AddPdf("Памятка.pdf");

        var withFallback = Assert.Single(_fixture.Plan(NameRule("Счет*", "Счета", fallback: "Прочее")).Items);
        var withoutFallback = Assert.Single(_fixture.Plan(NameRule("Счет*", "Счета")).Items);

        Assert.Equal(Out(@"Прочее\Памятка.pdf"), withFallback.DestinationPath);
        Assert.Equal(SortPlanner.FallbackRuleName, withFallback.RuleName);
        Assert.Equal(PlanItemStatus.Skipped, withoutFallback.Status);
        Assert.Null(withoutFallback.DestinationPath);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 3)]
    public void IncludeSubfolders_ControlsRecursion(bool includeSubfolders, int expectedCount)
    {
        _fixture.AddPdf("a.pdf").AddPdf(@"Сканы\b.pdf").AddPdf(@"Сканы\2025\c.PDF");
        _fixture.FileSystem.AddFile(In("readme.txt"), "не PDF");

        var plan = _fixture.Plan(NameRule("*", "Все"), includeSubfolders);

        Assert.Equal(expectedCount, plan.Items.Count);
    }

    [Fact]
    public void TargetInsideSource_IsNotScannedAgain()
    {
        _fixture.AddPdf("new.pdf").AddPdf(@"sorted\Все\old.pdf");
        var job = _fixture.Job(NameRule("*", "Все"), target: In("sorted"));

        var item = Assert.Single(_fixture.Planner.BuildPlan(job).Items);

        Assert.Equal(In("new.pdf"), item.SourcePath);
    }

    [Fact]
    public void TextRule_ExtractsValueIntoFolderAndName_DateComesFromFileName()
    {
        _fixture.AddPdf(@"Сканы\scan 2025-01-20.pdf", text: "ДОГОВОР ПОСТАВКИ № Д-2025/017 г. Москва");
        var ruleSet = new RuleSet
        {
            Rules =
            [
                new PdfTextRule
                {
                    Name = "Договоры",
                    Pattern = @"Договор[^№]*№\s*([\w\-/]+)",
                    IsRegex = true,
                    TargetFolder = "Договоры/{yyyy}",
                    FileNameTemplate = "{date:yyyy-MM-dd}_Договор_{match}",
                },
            ],
        };

        var item = Assert.Single(_fixture.Plan(ruleSet).Items);

        Assert.Equal(Out(@"Договоры\2025\2025-01-20_Договор_Д-2025_017.pdf"), item.DestinationPath);
    }

    [Fact]
    public void NameConflicts_WithinPlanAndOnDisk_GetNumericSuffix()
    {
        _fixture.AddPdf("a.pdf", text: "Договор № 17").AddPdf("b.pdf", text: "Договор № 17").AddPdf("c.pdf", text: "Договор № 17");
        _fixture.FileSystem.AddFile(Out(@"Договоры\Договор_17 (2).pdf"), "уже лежит в целевой папке");
        var ruleSet = new RuleSet
        {
            Rules = [new PdfTextRule { Pattern = @"Договор № (\d+)", IsRegex = true, TargetFolder = "Договоры", FileNameTemplate = "Договор_{match}" }],
        };

        var destinations = _fixture.Plan(ruleSet).Items.Select(i => i.DestinationPath).ToList();

        Assert.Equal(
            [Out(@"Договоры\Договор_17.pdf"), Out(@"Договоры\Договор_17 (3).pdf"), Out(@"Договоры\Договор_17 (4).pdf")],
            destinations);
    }

    [Fact]
    public void Counter_IsNumberedSeparatelyForEachRule()
    {
        _fixture.AddPdf("Акт 1.pdf").AddPdf("Акт 2.pdf").AddPdf("Счет 1.pdf").AddPdf("Акт 3.pdf");
        var ruleSet = new RuleSet
        {
            Rules =
            [
                new FileNameRule { Pattern = "Акт*", TargetFolder = "Акты", FileNameTemplate = "Акт_{counter:000}" },
                new FileNameRule { Pattern = "Счет*", TargetFolder = "Счета", FileNameTemplate = "Счет_{counter:000}" },
            ],
        };

        var names = _fixture.Plan(ruleSet).Items.Select(i => Path.GetFileName(i.DestinationPath)).ToList();

        Assert.Equal(["Акт_001.pdf", "Акт_002.pdf", "Акт_003.pdf", "Счет_001.pdf"], names);
    }

    [Fact]
    public void BrokenPdf_MarksOnlyThatRowAsError()
    {
        _fixture.AddPdf("broken.pdf").AddPdf("good.pdf", text: "Акт выполненных работ");
        _fixture.Reader.WithError(In("broken.pdf"), new PdfReadException("PDF не читается (повреждён или зашифрован: Invalid header)"));
        var ruleSet = new RuleSet
        {
            Rules = [new PdfTextRule { Name = "Акты", Pattern = "Акт выполненных работ", TargetFolder = "Акты" }],
        };

        var plan = _fixture.Plan(ruleSet);

        var broken = plan.Items.Single(i => i.SourcePath == In("broken.pdf"));
        Assert.Equal(PlanItemStatus.Error, broken.Status);
        Assert.Equal("Акты", broken.RuleName); // правило, на котором споткнулись
        Assert.StartsWith("PDF не читается", broken.Message);
        Assert.Equal(PlanItemStatus.Ready, plan.Items.Single(i => i.SourcePath == In("good.pdf")).Status);
        Assert.Equal((1, 0, 1), (plan.ReadyCount, plan.SkippedCount, plan.ErrorCount));
    }

    [Fact]
    public void FileAlreadyInPlace_IsSkipped()
    {
        _fixture.AddPdf("Счет_1.pdf");
        var job = _fixture.Job(NameRule("Счет*", folder: ""), target: Source);

        var item = Assert.Single(_fixture.Planner.BuildPlan(job).Items);

        Assert.Equal(PlanItemStatus.Skipped, item.Status);
        Assert.Contains("на своём месте", item.Message);
    }

    [Fact]
    public void InvalidConfiguration_IsReportedBeforeScanning()
    {
        var ruleSet = new RuleSet
        {
            FileNameTemplate = "{oops}",
            Rules = [new PdfTextRule { Name = "Сломанное", Pattern = "(", IsRegex = true }],
        };
        var job = _fixture.Job(ruleSet) with { SourceFolder = @"C:\нет-такой-папки" };

        var error = Assert.Throws<SortConfigurationException>(() => _fixture.Planner.BuildPlan(job));

        Assert.Equal(3, error.Errors.Count);
        Assert.Contains(error.Errors, e => e.StartsWith("Исходная папка не найдена"));
        Assert.Contains(error.Errors, e => e.Contains("{oops}"));
        Assert.Contains(error.Errors, e => e.StartsWith("Правило «Сломанное»"));
    }

    [Fact]
    public void CancelledToken_StopsPlanning()
    {
        _fixture.AddPdf("a.pdf");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            _fixture.Planner.BuildPlan(_fixture.Job(NameRule("*", "Все")), cancellationToken: cancellation.Token));
    }
}
