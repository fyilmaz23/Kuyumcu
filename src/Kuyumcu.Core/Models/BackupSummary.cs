namespace Kuyumcu.Models;

/// <summary>Bir yedek dosyasının kurulum ekranında gösterilen özeti.</summary>
public sealed record BackupSummary(string? BusinessName, int CustomerCount);
