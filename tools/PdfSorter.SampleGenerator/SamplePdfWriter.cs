using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace PdfSorter.SampleGenerator;

/// <summary>
/// Рисует простые «деловые» документы через PdfDocumentBuilder.
/// Для кириллицы встраивается TrueType-шрифт Arial из Windows (стандартные 14 шрифтов PDF кириллицу не поддерживают).
/// </summary>
internal sealed class SamplePdfWriter : IDisposable
{
    private static readonly string FontsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

    private readonly byte[] _regularFont = LoadFont("arial.ttf");
    private readonly byte[] _boldFont = LoadFont("arialbd.ttf");

    public byte[] Write(SampleDocument document)
    {
        var builder = new PdfDocumentBuilder();
        builder.DocumentInformation.Title = document.Title;
        builder.DocumentInformation.Producer = "PdfSorter.SampleGenerator";

        var regular = builder.AddTrueTypeFont(_regularFont);
        var bold = builder.AddTrueTypeFont(_boldFont);
        var page = builder.AddPage(PageSize.A4);

        // Шапка
        page.SetTextAndFillColor(37, 99, 235);
        page.DrawRectangle(new PdfPoint(50, 790), 495, 3, 0, fill: true);
        page.SetTextAndFillColor(110, 117, 128);
        page.AddText("ООО «Ромашка» · документооборот", 9, new PdfPoint(50, 800), regular);

        // Заголовок и текст
        page.SetTextAndFillColor(20, 24, 31);
        page.AddText(document.Title, 15, new PdfPoint(50, 745), bold);

        var y = 710d;
        foreach (var line in document.Lines)
        {
            page.AddText(line, 11, new PdfPoint(50, y), regular);
            y -= 22;
        }

        // Подписи
        y -= 30;
        page.SetStrokeColor(160, 166, 175);
        page.DrawLine(new PdfPoint(50, y), new PdfPoint(250, y), 0.7);
        page.DrawLine(new PdfPoint(345, y), new PdfPoint(545, y), 0.7);
        page.SetTextAndFillColor(110, 117, 128);
        page.AddText("Руководитель", 9, new PdfPoint(50, y - 14), regular);
        page.AddText("Главный бухгалтер", 9, new PdfPoint(345, y - 14), regular);

        return builder.Build();
    }

    /// <summary>Файл с расширением .pdf, который на самом деле не PDF.</summary>
    public static byte[] CreateCorruptFile() =>
        Encoding.UTF8.GetBytes("%PDF-1.7\nЭтот файл повреждён при сканировании и не открывается.\n");

    public void Dispose()
    {
    }

    private static byte[] LoadFont(string fileName)
    {
        var path = Path.Combine(FontsDirectory, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Не найден шрифт {path}. Генератору нужен Arial из Windows для кириллицы.", path);
        }

        return File.ReadAllBytes(path);
    }
}
