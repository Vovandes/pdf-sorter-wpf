using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfSorter.App.Services;

/// <summary>Сохраняет содержимое окна в PNG через RenderTargetBitmap (без рамки и заголовка окна).</summary>
public static class ScreenshotRenderer
{
    public static void SaveToPng(Window window, string path, double scale = 1)
    {
        if (window.Content is not FrameworkElement content)
        {
            throw new InvalidOperationException("У окна нет содержимого для снимка.");
        }

        var size = new Size(content.ActualWidth, content.ActualHeight);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(size.Width * scale),
            (int)Math.Ceiling(size.Height * scale),
            96 * scale,
            96 * scale,
            PixelFormats.Pbgra32);

        // VisualBrush не зависит от смещения элемента внутри окна и сохраняет фон.
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new VisualBrush(content), null, new Rect(size));
        }

        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using var stream = File.Create(fullPath);
        encoder.Save(stream);
    }
}
