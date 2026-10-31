using System.Text;
using System.Text.RegularExpressions;

namespace PdfSorter.Core.Matching;

/// <summary>Преобразование масок вида <c>Счет_*</c> / <c>scan_??.pdf</c> в регулярные выражения.</summary>
public static class WildcardPattern
{
    /// <summary>
    /// <c>*</c> — любая последовательность символов (захватывается группой, первая группа идёт в <c>{match}</c>),
    /// <c>?</c> — ровно один символ. Выражение привязано к началу и концу строки.
    /// </summary>
    public static string ToRegex(string wildcard)
    {
        var builder = new StringBuilder("^", wildcard.Length + 8);
        foreach (var ch in wildcard.Trim())
        {
            builder.Append(ch switch
            {
                '*' => "(.*)",
                '?' => ".",
                _ => Regex.Escape(ch.ToString()),
            });
        }

        return builder.Append('$').ToString();
    }
}
