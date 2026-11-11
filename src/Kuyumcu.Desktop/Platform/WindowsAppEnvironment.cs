using Kuyumcu.Platform;
using Microsoft.Win32;

namespace Kuyumcu.Desktop.Platform;

public sealed class WindowsAppEnvironment : IAppEnvironment
{
    public WindowsAppEnvironment()
    {
        AppDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductInfo.DataFolderName);

        // Yedekler Belgeler klasöründe: uygulama kaldırılsa bile müşterinin yedekleri kalır
        BackupDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"{ProductInfo.Name} Yedekleri");

        CacheDirectory = Path.Combine(Path.GetTempPath(), ProductInfo.DataFolderName);

        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(CacheDirectory);
    }

    public string AppDataDirectory { get; }
    public string BackupDirectory { get; }
    public string CacheDirectory { get; }
    public string PicturesDirectory => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    public bool IsReadOnlyClient => false;
    public bool IsMobile => false;

    public string GetDeviceFingerprint()
    {
        // Windows kurulumuna özgü MachineGuid; okunamazsa bilgisayar adı kullanılır
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            if (key?.GetValue("MachineGuid") is string machineGuid && !string.IsNullOrWhiteSpace(machineGuid))
                return $"win-{machineGuid}";
        }
        catch
        {
            // yoksay
        }

        return $"win-{Environment.MachineName}-{Environment.UserName}";
    }
}
