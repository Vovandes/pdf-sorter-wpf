namespace PdfSorter.Core.Pdf;

/// <summary>Чтение текста PDF. Абстракция позволяет тестировать правила без реальных файлов.</summary>
public interface IPdfTextReader
{
    /// <summary>Текст первой страницы с нормализованными пробелами (пустая строка, если текста нет).</summary>
    /// <exception cref="PdfReadException">Файл повреждён, зашифрован или не является PDF.</exception>
    /// <exception cref="IOException">Файл занят или недоступен.</exception>
    /// <exception cref="UnauthorizedAccessException">Нет прав на чтение.</exception>
    string ReadFirstPageText(string path);
}
