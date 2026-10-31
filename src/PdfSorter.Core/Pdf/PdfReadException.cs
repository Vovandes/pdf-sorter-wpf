namespace PdfSorter.Core.Pdf;

/// <summary>PDF не удалось разобрать: файл повреждён, зашифрован или вовсе не PDF.</summary>
public sealed class PdfReadException : Exception
{
    public PdfReadException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
