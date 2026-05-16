using TextCopy;

namespace CSnip.Abstractions;

public interface IClipboardService
{
    Task SetTextAsync(string text, CancellationToken cancellation = default);
}

public class ClipboardServiceAbstraction : IClipboardService
{
    public Task SetTextAsync(string text, CancellationToken cancellation = default)
    {
        return ClipboardService.SetTextAsync(text, cancellation);
    }
}