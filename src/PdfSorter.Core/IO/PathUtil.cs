namespace PdfSorter.Core.IO;

/// <summary>Сравнение путей в стиле Windows (без учёта регистра).</summary>
public static class PathUtil
{
    public static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    public static bool AreSame(string first, string second) =>
        Comparer.Equals(TrimSeparators(first), TrimSeparators(second));

    /// <summary><c>true</c>, если <paramref name="path"/> совпадает с <paramref name="root"/> или лежит внутри неё.</summary>
    public static bool IsSameOrInside(string path, string root)
    {
        var normalizedRoot = TrimSeparators(root);
        var normalizedPath = TrimSeparators(path);
        return Comparer.Equals(normalizedPath, normalizedRoot)
            || normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Путь относительно <paramref name="root"/> для отображения; если файл вне неё — полный путь.</summary>
    public static string ToDisplay(string path, string root) =>
        IsSameOrInside(path, root) && !AreSame(path, root)
            ? path[(TrimSeparators(root).Length + 1)..]
            : path;

    private static string TrimSeparators(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
