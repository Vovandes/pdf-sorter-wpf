namespace PdfSorter.Core.Tests.Fakes;

/// <summary>В отличие от <see cref="Progress{T}"/> вызывает обработчик сразу, в том же потоке.</summary>
internal sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
