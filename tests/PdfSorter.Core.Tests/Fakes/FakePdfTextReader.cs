using PdfSorter.Core.Pdf;

namespace PdfSorter.Core.Tests.Fakes;

/// <summary>Подставной «читатель PDF»: текст и ошибки задаются по пути файла, обращения считаются.</summary>
internal sealed class FakePdfTextReader : IPdfTextReader
{
    private readonly Dictionary<string, string> _texts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Exception> _errors = new(StringComparer.OrdinalIgnoreCase);

    public List<string> ReadPaths { get; } = [];

    public FakePdfTextReader WithText(string path, string text)
    {
        _texts[path] = text;
        return this;
    }

    public FakePdfTextReader WithError(string path, Exception error)
    {
        _errors[path] = error;
        return this;
    }

    public string ReadFirstPageText(string path)
    {
        ReadPaths.Add(path);
        if (_errors.TryGetValue(path, out var error))
        {
            throw error;
        }

        return _texts.GetValueOrDefault(path, string.Empty);
    }
}
