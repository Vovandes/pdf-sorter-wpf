using System.Security;
using System.Text.RegularExpressions;
using PdfSorter.Core.Pdf;

namespace PdfSorter.Core.IO;

/// <summary>Короткие понятные пользователю описания типичных ошибок обработки файла.</summary>
public static class ErrorDescriber
{
    /// <summary>Ошибки, которые относятся к одному файлу и не должны останавливать пакет.</summary>
    public static bool IsFileLevel(Exception exception) => exception is
        IOException or UnauthorizedAccessException or SecurityException or
        PdfReadException or RegexMatchTimeoutException or ArgumentException or NotSupportedException;

    public static string Describe(Exception exception) => exception switch
    {
        FileNotFoundException => "файл не найден (удалён или переименован)",
        DirectoryNotFoundException => "папка не найдена",
        PathTooLongException => "слишком длинный путь",
        IOException when IsSharingViolation(exception) => "файл занят другой программой",
        IOException => $"ошибка ввода-вывода ({exception.Message})",
        UnauthorizedAccessException or SecurityException => "нет прав доступа к файлу или папке",
        PdfReadException => exception.Message,
        RegexMatchTimeoutException => "регулярное выражение проверяется слишком долго",
        _ => exception.Message,
    };

    // ERROR_SHARING_VIOLATION (32) и ERROR_LOCK_VIOLATION (33) приходят как HRESULT 0x80070020 / 0x80070021.
    private static bool IsSharingViolation(Exception exception) =>
        (exception.HResult & 0xFFFF) is 32 or 33;
}
