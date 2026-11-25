namespace Kuyumcu.Droid.Services;

/// <summary>Android'in kendi onay/uyarı pencereleri (WebView içindeki confirm() yerine).</summary>
public static class NativeDialogs
{
    private static Page? CurrentPage => Application.Current?.Windows.FirstOrDefault()?.Page;

    public static Task<bool> ConfirmAsync(string title, string message, string accept, string cancel = "Vazgeç") =>
        MainThread.InvokeOnMainThreadAsync(() =>
            CurrentPage?.DisplayAlertAsync(title, message, accept, cancel) ?? Task.FromResult(false));

    public static Task AlertAsync(string title, string message) =>
        MainThread.InvokeOnMainThreadAsync(() =>
            CurrentPage?.DisplayAlertAsync(title, message, "Tamam") ?? Task.CompletedTask);

    /// <summary>Kısa bilgi mesajı (Android "toast").</summary>
    public static void Toast(string message) =>
        MainThread.BeginInvokeOnMainThread(() =>
            Android.Widget.Toast.MakeText(Android.App.Application.Context, message, Android.Widget.ToastLength.Short)?.Show());
}
