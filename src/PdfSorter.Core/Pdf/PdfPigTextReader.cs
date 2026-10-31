using System.IO.Abstractions;
using PdfSorter.Core.Text;
using UglyToad.PdfPig;

namespace PdfSorter.Core.Pdf;

/// <summary>Реализация <see cref="IPdfTextReader"/> на PdfPig. Файл читается через <see cref="IFileSystem"/>.</summary>
public sealed class PdfPigTextReader : IPdfTextReader
{
    private readonly IFileSystem _fileSystem;

    public PdfPigTextReader(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string ReadFirstPageText(string path)
    {
        // Ошибки доступа (файл занят, нет прав) пробрасываем как есть — их сообщение понятнее.
        using var stream = _fileSystem.File.OpenRead(path);

        try
        {
            using var document = PdfDocument.Open(stream);
            if (document.NumberOfPages == 0)
            {
                return string.Empty;
            }

            var page = document.GetPage(1);
            var words = page.GetWords().Select(word => word.Text);
            return TextNormalizer.CollapseWhitespace(string.Join(' ', words));
        }
        catch (Exception ex) when (ex is not (IOException or UnauthorizedAccessException or OperationCanceledException))
        {
            throw new PdfReadException($"PDF не читается (повреждён или зашифрован: {ex.Message})", ex);
        }
    }
}
