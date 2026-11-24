using System.ComponentModel;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using Kuyumcu.Platform;

namespace Kuyumcu.Desktop.Platform;

/// <summary>
/// Etiket yazıcısına ham komut (RAW) gönderir: winspool.drv OpenPrinter → StartDocPrinter("RAW") → WritePrinter.
/// Sürücü çizim yapmaz; TSPL komutları olduğu gibi yazıcıya gider. (GDI / PrintDocument kullanılmaz.)
/// </summary>
public sealed class WindowsLabelPrinter : ILabelPrinter
{
    public bool IsSupported => true;

    public List<string> GetAvailablePrinters()
    {
        // Sadece kurulu yazıcı adlarının listesi (çizim yapılmaz)
        var printers = new List<string>();
        foreach (string name in PrinterSettings.InstalledPrinters)
            printers.Add(name);
        return printers;
    }

    public Task SendRawAsync(string printerName, byte[] data, string documentName) =>
        Task.Run(() => SendRaw(printerName, data, documentName));

    private static void SendRaw(string printerName, byte[] data, string documentName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new InvalidOperationException("Yazıcı seçilmemiş.");

        if (!OpenPrinter(printerName, out var handle, IntPtr.Zero))
            throw Error($"\"{printerName}\" yazıcısı açılamadı");

        try
        {
            var doc = new DOC_INFO_1 { pDocName = documentName, pDataType = "RAW" };
            if (StartDocPrinter(handle, 1, doc) == 0)
                throw Error("Yazdırma işi başlatılamadı");

            try
            {
                if (!StartPagePrinter(handle))
                    throw Error("Sayfa başlatılamadı");

                var unmanaged = Marshal.AllocCoTaskMem(data.Length);
                try
                {
                    Marshal.Copy(data, 0, unmanaged, data.Length);
                    if (!WritePrinter(handle, unmanaged, data.Length, out var written))
                        throw Error("Yazıcıya veri gönderilemedi");
                    if (written != data.Length)
                        throw new IOException($"Yazıcıya eksik veri gitti ({written}/{data.Length} bayt).");
                }
                finally
                {
                    Marshal.FreeCoTaskMem(unmanaged);
                    EndPagePrinter(handle);
                }
            }
            finally
            {
                EndDocPrinter(handle);
            }
        }
        finally
        {
            ClosePrinter(handle);
        }
    }

    private static Exception Error(string message)
    {
        var code = Marshal.GetLastWin32Error();
        return new IOException($"{message}: {new Win32Exception(code).Message} (kod {code})");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class DOC_INFO_1
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string? pDocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pDataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr hPrinter, int level, [In] DOC_INFO_1 pDocInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);
}
