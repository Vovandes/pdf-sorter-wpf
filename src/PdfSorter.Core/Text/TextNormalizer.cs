using System.Text;

namespace PdfSorter.Core.Text;

/// <summary>Нормализация текста перед сравнением.</summary>
public static class TextNormalizer
{
    /// <summary>Заменяет любые последовательности пробельных символов одним пробелом и обрезает края.</summary>
    public static string CollapseWhitespace(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }
}
