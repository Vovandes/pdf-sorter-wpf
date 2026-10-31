using PdfSorter.Core.Naming;

namespace PdfSorter.Core.Tests.Naming;

public class PathTemplateTests
{
    private static readonly TemplateContext Context = new(
        Name: "scan_0012",
        Extension: "pdf",
        Date: new DateTime(2025, 3, 14),
        Counter: 7,
        Match: "Д-2025/017",
        RuleName: "Договоры");

    [Theory]
    [InlineData("{name}", "scan_0012.pdf")]
    [InlineData("{date:yyyy-MM-dd}_{name}", "2025-03-14_scan_0012.pdf")]
    [InlineData("{date:dd.MM.yyyy} {counter:000}", "14.03.2025 007.pdf")]
    [InlineData("{counter}", "7.pdf")]
    [InlineData("Договор_{match}", "Договор_Д-2025_017.pdf")] // «/» из значения не создаёт папку
    [InlineData("{rule} {yyyy}-{MM}-{dd}", "Договоры 2025-03-14.pdf")]
    [InlineData("{name}.{ext}", "scan_0012.pdf")]               // расширение не дублируется
    [InlineData("{name}.PDF", "scan_0012.PDF")]
    [InlineData("{NAME}", "scan_0012.pdf")]                     // ключи без учёта регистра
    [InlineData("{date:dd MMMM yyyy}", "14 марта 2025.pdf")]    // названия месяцев по-русски
    public void RenderFileName_SubstitutesPlaceholders(string template, string expected)
    {
        Assert.Equal(expected, PathTemplate.RenderFileName(template, Context));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{match}")]
    public void RenderFileName_EmptyResult_FallsBackToOriginalName(string template)
    {
        var context = Context with { Match = null };

        Assert.Equal("scan_0012.pdf", PathTemplate.RenderFileName(template, context));
    }

    [Theory]
    [InlineData("{yyyy}/{MM}", new[] { "2025", "03" })]
    [InlineData(@"Договоры\{yyyy}", new[] { "Договоры", "2025" })]
    [InlineData("Архив/{date:yyyy}/{date:MM}", new[] { "Архив", "2025", "03" })]
    [InlineData("{match}", new[] { "Д-2025_017" })]
    [InlineData("../../Windows/{yyyy}", new[] { "_", "_", "Windows", "2025" })] // выйти из целевой папки нельзя
    [InlineData("C:/Temp", new[] { "C_", "Temp" })]
    [InlineData("", new string[0])]
    public void RenderFolder_BuildsRelativeSegments(string template, string[] expectedSegments)
    {
        Assert.Equal(Path.Combine(expectedSegments), PathTemplate.RenderFolder(template, Context));
    }

    [Theory]
    [InlineData("{name}_{date:yyyy-MM-dd}_{counter:000}_{match}_{ext}")]
    [InlineData("Счета/{yyyy}/{MM}")]
    [InlineData("без подстановок")]
    public void Validate_AcceptsKnownPlaceholders(string template)
    {
        Assert.Empty(PathTemplate.Validate(template));
    }

    [Theory]
    [InlineData("{year}", "неизвестная подстановка {year}")]
    [InlineData("{mm}", "неизвестная подстановка {mm}")] // минуты не поддерживаются, только {MM}
    [InlineData("{name:000}", "некорректный формат")]
    [InlineData("{name", "непарные фигурные скобки")]
    public void Validate_ReportsProblems(string template, string expectedFragment)
    {
        Assert.Contains(PathTemplate.Validate(template), error => error.Contains(expectedFragment));
    }
}
