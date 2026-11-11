using System.Windows;
using Kuyumcu.Platform;
using Microsoft.Win32;

namespace Kuyumcu.Desktop.Platform;

public sealed class WindowsFilePickerService : IFilePickerService
{
    public Task<PickedFile?> PickFileAsync(string title, params string[] extensions)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var patterns = extensions.Length == 0 ? "*.*" : string.Join(";", extensions.Select(e => "*" + e));
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = $"Desteklenen dosyalar ({patterns})|{patterns}|Tüm dosyalar (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            return dialog.ShowDialog(Application.Current.MainWindow) == true
                ? new PickedFile(Path.GetFileName(dialog.FileName), dialog.FileName)
                : null;
        }).Task;
    }
}
