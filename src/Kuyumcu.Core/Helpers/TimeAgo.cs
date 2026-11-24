namespace Kuyumcu.Helpers
{
    /// <summary>"az önce", "12 dk önce", "dün 14:30" gibi göreli zaman metinleri.</summary>
    public static class TimeAgo
    {
        public static string Format(DateTime? utc)
        {
            if (utc is not { } value)
                return "henüz yok";

            var local = DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime();
            var diff = DateTime.Now - local;

            if (diff < TimeSpan.FromMinutes(1)) return "az önce";
            if (diff < TimeSpan.FromHours(1)) return $"{(int)diff.TotalMinutes} dk önce";
            if (local.Date == DateTime.Today) return $"{(int)diff.TotalHours} saat önce";
            if (local.Date == DateTime.Today.AddDays(-1)) return $"dün {local:HH:mm}";
            return local.ToString("dd.MM.yyyy HH:mm");
        }
    }
}
