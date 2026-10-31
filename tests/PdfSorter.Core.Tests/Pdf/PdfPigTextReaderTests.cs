using System.IO.Abstractions.TestingHelpers;
using PdfSorter.Core.Pdf;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace PdfSorter.Core.Tests.Pdf;

/// <summary>Интеграционные тесты PdfPig: PDF создаётся в памяти через <see cref="PdfDocumentBuilder"/>.</summary>
public class PdfPigTextReaderTests
{
    [Fact]
    public void ReadsTextOfFirstPageOnly()
    {
        var pdf = BuildPdf(
            ["CONTRACT No. 42-A", "Moscow, 20 January 2025"],
            ["Second page: INVOICE"]);
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            [@"C:\in\contract.pdf"] = new(pdf),
        });

        var text = new PdfPigTextReader(fileSystem).ReadFirstPageText(@"C:\in\contract.pdf");

        Assert.Contains("CONTRACT No. 42-A", text);
        Assert.Contains("Moscow, 20 January 2025", text);
        Assert.DoesNotContain("INVOICE", text);
    }

    [Fact]
    public void CorruptFile_ThrowsPdfReadException()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            [@"C:\in\broken.pdf"] = new("это не PDF, а просто текст"),
        });

        Assert.Throws<PdfReadException>(() => new PdfPigTextReader(fileSystem).ReadFirstPageText(@"C:\in\broken.pdf"));
    }

    private static byte[] BuildPdf(params string[][] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var lines in pages)
        {
            var page = builder.AddPage(PageSize.A4);
            var y = 780d;
            foreach (var line in lines)
            {
                page.AddText(line, 12, new PdfPoint(50, y), font);
                y -= 20;
            }
        }

        return builder.Build();
    }
}
