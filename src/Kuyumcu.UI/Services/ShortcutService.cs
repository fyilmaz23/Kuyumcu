using Microsoft.JSInterop;

namespace Kuyumcu.UI.Services;

/// <summary>
/// Klavye kısayollarını (F2, F3) o an açık olan sayfaya iletir.
/// Ctrl+K (müşteri arama) doğrudan tarayıcı tarafında işlenir.
/// </summary>
public class ShortcutService
{
    /// <summary>Kısayolu dinleyen sayfa (aynı anda tek sayfa).</summary>
    public Func<string, Task>? Handler { get; set; }

    [JSInvokable]
    public Task OnShortcut(string key) => Handler?.Invoke(key) ?? Task.CompletedTask;
}
