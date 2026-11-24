using Kuyumcu.Models;
using Kuyumcu.Stock;
using SQLite;

namespace Kuyumcu.Services;

/// <summary>Sayımda bir okutmanın sonucu.</summary>
public enum CountScanKind
{
    /// <summary>Beklenen ürün, sayıldı.</summary>
    Found,
    /// <summary>Aynı kod bu sayımda zaten okutuldu (tekrar sayılmaz).</summary>
    Duplicate,
    /// <summary>Kayıtlı olmayan kod.</summary>
    Unknown,
    /// <summary>Satılmış, kayıp ya da silinmiş ürün.</summary>
    NotInStock,
    /// <summary>Vitrinde ama başka konumda kayıtlı.</summary>
    OtherLocation,
    /// <summary>Geçersiz giriş (rakam dışı karakter, yanlış uzunluk, kontrol hanesi).</summary>
    Invalid
}

public sealed record CountScanResult(CountScanKind Kind, string Message, StockItem? Item, StockCountLine? Line);

/// <summary>
/// Vitrin sayımı: başlat → okut → bitir. Bitince beklenip okutulmayanlar "eksik" yazılır.
/// Eksik ürünü kayıp yapmak ayrı ve açık bir adımdır (<see cref="MarkMissingAsLostAsync"/>); otomatik yapılmaz.
/// </summary>
public class StockCountService
{
    private readonly DatabaseService _database;
    private readonly StockService _stock;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public StockCountService(DatabaseService database, StockService stock)
    {
        _database = database;
        _stock = stock;
    }

    private Task<SQLiteAsyncConnection> Db() => _database.GetConnectionAsync();

    public async Task<StockCountSession?> GetOpenSessionAsync()
    {
        var db = await Db();
        return await db.Table<StockCountSession>().Where(s => s.Status == StockCountStatus.Open)
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync();
    }

    public async Task<StockCountSession?> GetSessionAsync(int id)
    {
        var db = await Db();
        return await db.Table<StockCountSession>().Where(s => s.Id == id).FirstOrDefaultAsync();
    }

    public async Task<List<StockCountSession>> GetSessionsAsync(int take = 20)
    {
        var db = await Db();
        return await db.Table<StockCountSession>().OrderByDescending(s => s.Id).Take(take).ToListAsync();
    }

    public async Task<List<StockCountLine>> GetLinesAsync(int sessionId)
    {
        var db = await Db();
        return await db.Table<StockCountLine>().Where(l => l.SessionId == sessionId).OrderByDescending(l => l.Id).ToListAsync();
    }

    /// <summary>Sayımda beklenen ürünler: vitrindeki (ve konum seçildiyse o konumdaki) ürünler.</summary>
    public Task<List<StockItem>> GetExpectedItemsAsync(StockCountSession session) =>
        _stock.GetItemsAsync(new StockItemQuery
        {
            Status = StockItemStatus.InStock,
            LocationId = session.LocationId == 0 ? null : session.LocationId
        });

    /// <summary>Yeni sayım başlatır. Açık sayım varsa önce o bitirilmeli ya da iptal edilmeli.</summary>
    public async Task<StockCountSession> StartAsync(int locationId, string? note = null)
    {
        if (await GetOpenSessionAsync() != null)
            throw new StockException("Açık bir sayım var. Önce onu bitirin ya da iptal edin.");

        var session = new StockCountSession
        {
            StartedAt = DateTime.Now,
            LocationId = locationId,
            Status = StockCountStatus.Open,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };
        session.ExpectedCount = (await GetExpectedItemsAsync(session)).Count;

        var db = await Db();
        await db.InsertAsync(session);
        return session;
    }

    /// <summary>Okutulan kodu sayıma ekler. Her okutma hemen kaydedilir (uygulama kapansa da sayım kaybolmaz).</summary>
    public async Task<CountScanResult> ScanAsync(int sessionId, string input)
    {
        var parsed = ProductCode.ParseInput(input);
        if (!parsed.IsValid)
            return new CountScanResult(CountScanKind.Invalid, parsed.Error!, null, null);
        var code = parsed.Code!;

        await _lock.WaitAsync();
        try
        {
            var db = await Db();
            var session = await GetSessionAsync(sessionId);
            if (session is not { Status: StockCountStatus.Open })
                throw new StockException("Sayım açık değil.");

            var already = await db.Table<StockCountLine>()
                .Where(l => l.SessionId == sessionId && l.Code == code && l.Result != StockCountResult.Missing)
                .FirstOrDefaultAsync();
            var item = await _stock.GetItemByCodeAsync(code);

            if (already != null)
                return new CountScanResult(CountScanKind.Duplicate,
                    $"{code} bu sayımda zaten okutuldu ({already.ScannedAt:HH:mm:ss}). Tekrar sayılmadı.", item, already);

            var line = new StockCountLine { SessionId = sessionId, Code = code, ItemId = item?.Id ?? 0, ScannedAt = DateTime.Now };
            CountScanKind kind;
            string message;

            if (item == null)
            {
                line.Result = StockCountResult.Unknown;
                kind = CountScanKind.Unknown;
                message = $"{code} kayıtlı değil.";
            }
            else if (item.IsDeleted || item.Status != StockItemStatus.InStock)
            {
                line.Result = StockCountResult.NotInStock;
                kind = CountScanKind.NotInStock;
                message = item.IsDeleted ? $"{code} silinmiş bir ürün."
                    : item.Status == StockItemStatus.Sold ? $"{code} satılmış görünüyor ({item.SoldAt:dd.MM.yyyy})."
                    : $"{code} kayıp olarak işaretli.";
            }
            else if (session.LocationId != 0 && item.LocationId != session.LocationId)
            {
                line.Result = StockCountResult.OtherLocation;
                kind = CountScanKind.OtherLocation;
                message = $"{code} başka bir konumda kayıtlı.";
            }
            else
            {
                line.Result = StockCountResult.Found;
                kind = CountScanKind.Found;
                message = "Sayıldı.";
            }

            await db.InsertAsync(line);
            return new CountScanResult(kind, message, item, line);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Okutmayı geri alır (yanlışlıkla okutulan ürün).</summary>
    public async Task RemoveLineAsync(int lineId)
    {
        var db = await Db();
        var line = await db.Table<StockCountLine>().Where(l => l.Id == lineId).FirstOrDefaultAsync();
        if (line == null) return;
        var session = await GetSessionAsync(line.SessionId);
        if (session?.Status != StockCountStatus.Open)
            throw new StockException("Bitmiş sayım değiştirilemez.");
        await db.DeleteAsync(line);
    }

    /// <summary>Sayımı bitirir: okutulmayan beklenen ürünler "eksik" satırı olarak kaydedilir.</summary>
    public async Task<StockCountSession> CompleteAsync(int sessionId)
    {
        await _lock.WaitAsync();
        try
        {
            var db = await Db();
            var session = await GetSessionAsync(sessionId) ?? throw new StockException("Sayım bulunamadı.");
            if (session.Status != StockCountStatus.Open)
                throw new StockException("Sayım açık değil.");

            var lines = await GetLinesAsync(sessionId);
            var expected = await GetExpectedItemsAsync(session);
            var counted = lines.Where(l => l.Result == StockCountResult.Found).Select(l => l.Code);
            var comparison = StockCalculator.CompareCount(expected, counted);

            var now = DateTime.Now;
            session.Status = StockCountStatus.Completed;
            session.CompletedAt = now;
            session.ExpectedCount = expected.Count;
            session.FoundCount = comparison.Found.Count;
            session.MissingCount = comparison.Missing.Count;
            session.ExtraCount = lines.Count(l => l.Result is StockCountResult.Unknown or StockCountResult.NotInStock or StockCountResult.OtherLocation);

            await db.RunInTransactionAsync(conn =>
            {
                foreach (var item in comparison.Missing)
                    conn.Insert(new StockCountLine { SessionId = sessionId, Code = item.Code, ItemId = item.Id, Result = StockCountResult.Missing });
                conn.Update(session);
            });
            return session;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task CancelAsync(int sessionId)
    {
        var db = await Db();
        var session = await GetSessionAsync(sessionId);
        if (session is not { Status: StockCountStatus.Open })
            return;
        session.Status = StockCountStatus.Cancelled;
        session.CompletedAt = DateTime.Now;
        await db.UpdateAsync(session);
    }

    /// <summary>
    /// Seçilen eksik satırlarının ürünlerini kayıp yapar (sayım farkı hareketi yazılır).
    /// Bu arada satılan ya da bulunan ürünler atlanır. Kayıp yapılan sayısını döndürür.
    /// </summary>
    public async Task<int> MarkMissingAsLostAsync(int sessionId, IEnumerable<int> lineIds)
    {
        var db = await Db();
        var ids = lineIds.ToHashSet();
        var lines = (await GetLinesAsync(sessionId))
            .Where(l => ids.Contains(l.Id) && l.Result == StockCountResult.Missing && !l.IsResolved).ToList();

        var count = 0;
        foreach (var line in lines)
        {
            var item = await _stock.GetItemAsync(line.ItemId);
            if (item is { IsDeleted: false, Status: StockItemStatus.InStock })
            {
                await _stock.MarkLostAsync(item.Id, sessionId, $"Sayım #{sessionId}: bulunamadı");
                count++;
            }
            line.IsResolved = true;
            await db.UpdateAsync(line);
        }
        return count;
    }

    /// <summary>
    /// Fazla satırını çözer: kayıp ürün bulunduysa vitrine geri alır, başka konumdaysa konumunu sayılan konuma taşır.
    /// </summary>
    public async Task ResolveExtraAsync(int lineId)
    {
        var db = await Db();
        var line = await db.Table<StockCountLine>().Where(l => l.Id == lineId).FirstOrDefaultAsync()
                   ?? throw new StockException("Satır bulunamadı.");
        var session = await GetSessionAsync(line.SessionId) ?? throw new StockException("Sayım bulunamadı.");
        var item = await _stock.GetItemAsync(line.ItemId) ?? throw new StockException("Ürün kayıtlı değil.");

        if (line.Result == StockCountResult.NotInStock && item is { IsDeleted: false, Status: StockItemStatus.Lost })
        {
            await _stock.MarkFoundAsync(item.Id, session.Id, $"Sayım #{session.Id}: bulundu");
            if (session.LocationId != 0 && item.LocationId != session.LocationId)
                await MoveAsync(item.Id, session);
        }
        else if (line.Result == StockCountResult.OtherLocation && session.LocationId != 0)
        {
            await MoveAsync(item.Id, session);
        }
        else
        {
            throw new StockException("Bu satır için otomatik işlem yok. Ürün detayından düzenleyin.");
        }

        line.IsResolved = true;
        await db.UpdateAsync(line);
    }

    private async Task MoveAsync(int itemId, StockCountSession session)
    {
        var item = await _stock.GetItemAsync(itemId) ?? throw new StockException("Ürün bulunamadı.");
        item.LocationId = session.LocationId;
        await _stock.UpdateItemAsync(item, $"Sayım #{session.Id}: konum düzeltildi");
    }
}
