using System.Globalization;
using Kuyumcu.Models;
using Kuyumcu.Stock;
using SQLite;

namespace Kuyumcu.Services;

/// <summary>Stok listesi filtresi.</summary>
public sealed class StockItemQuery
{
    public string? Search { get; set; }
    public int? Karat { get; set; }
    public int? CategoryId { get; set; }
    public int? LocationId { get; set; }
    /// <summary>null = tüm durumlar.</summary>
    public StockItemStatus? Status { get; set; } = StockItemStatus.InStock;
}

/// <summary>Yeni ürün bilgileri (seri girişte kategori/ayar/model sabit kalır, gram değişir).</summary>
public sealed class StockItemDraft
{
    public int CategoryId { get; set; }
    public int Karat { get; set; }
    public string? ModelName { get; set; }
    public int WeightMg { get; set; }
    public int StoneWeightMg { get; set; }
    public int LocationId { get; set; }
    public string? SupplierName { get; set; }
    public int? PurchaseMilyem { get; set; }
    public string? Note { get; set; }
}

/// <summary>Kullanıcıya gösterilecek iş kuralı hatası (ör. "Ürün zaten satılmış").</summary>
public sealed class StockException(string message) : Exception(message);

/// <summary>
/// Barkodlu vitrin stoğu ve kasa ürünleri. Her stok değişikliği, ürün kaydıyla aynı veritabanı işleminde (transaction)
/// <see cref="StockMovement"/> tablosuna yazılır. Silme yerine IsDeleted kullanılır; ürün kodları asla yeniden kullanılmaz.
/// </summary>
public class StockService
{
    private static readonly CultureInfo Turkish = new("tr-TR");
    private readonly DatabaseService _database;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public StockService(DatabaseService database)
    {
        _database = database;
    }

    private Task<SQLiteAsyncConnection> Db() => _database.GetConnectionAsync();

    // ───────────────────────── Tanımlar: ayar, kategori, konum ─────────────────────────

    public async Task<List<KaratDefinition>> GetKaratsAsync(bool includeDeleted = false)
    {
        var db = await Db();
        var list = await db.Table<KaratDefinition>().ToListAsync();
        return list.Where(k => includeDeleted || !k.IsDeleted).OrderByDescending(k => k.Karat).ToList();
    }

    public async Task SaveKaratAsync(KaratDefinition karat)
    {
        if (karat.Karat is < 1 or > 24)
            throw new StockException("Ayar 1 ile 24 arasında olmalı.");
        if (karat.Milyem is < 1 or > 1000)
            throw new StockException("Milyem 0,001 ile 1,000 arasında olmalı.");

        var db = await Db();
        var existing = await db.Table<KaratDefinition>().Where(k => k.Karat == karat.Karat).FirstOrDefaultAsync();
        if (existing != null && existing.Id != karat.Id)
        {
            if (!existing.IsDeleted)
                throw new StockException($"{karat.Karat} ayar zaten tanımlı.");
            // Silinmiş aynı ayarı geri getir
            existing.IsDeleted = false;
            existing.Milyem = karat.Milyem;
            await db.UpdateAsync(existing);
            karat.Id = existing.Id;
            return;
        }

        if (karat.Id == 0) await db.InsertAsync(karat);
        else await db.UpdateAsync(karat);
    }

    public async Task DeleteKaratAsync(KaratDefinition karat)
    {
        var db = await Db();
        var inUse = await db.Table<StockItem>().Where(i => i.Karat == karat.Karat && !i.IsDeleted && i.Status == StockItemStatus.InStock).CountAsync();
        if (inUse > 0)
            throw new StockException($"Vitrinde {inUse} adet {karat.Karat} ayar ürün var; ayar silinemez.");
        karat.IsDeleted = true;
        await db.UpdateAsync(karat);
    }

    public async Task<List<StockCategory>> GetCategoriesAsync(bool includeDeleted = false)
    {
        var db = await Db();
        var list = await db.Table<StockCategory>().ToListAsync();
        return list.Where(c => includeDeleted || !c.IsDeleted)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name, StringComparer.Create(Turkish, true)).ToList();
    }

    /// <summary>Silinmiş olsa da kategoriyi döndürür (eski ürünlerin adı görünsün).</summary>
    public async Task<StockCategory?> GetCategoryAsync(int id)
    {
        var db = await Db();
        return await db.Table<StockCategory>().Where(c => c.Id == id).FirstOrDefaultAsync();
    }

    public async Task SaveCategoryAsync(StockCategory category)
    {
        category.Name = (category.Name ?? string.Empty).Trim();
        if (category.Name.Length == 0)
            throw new StockException("Kategori adı girin.");
        category.ShortName = string.IsNullOrWhiteSpace(category.ShortName) ? null : category.ShortName.Trim().ToUpper(Turkish);
        if (category.LaborMilyem is < 0 or > 500)
            throw new StockException("İşçilik milyemi 0 ile 0,500 arasında olmalı.");

        var db = await Db();
        var all = await db.Table<StockCategory>().ToListAsync();
        if (all.Any(c => c.Id != category.Id && !c.IsDeleted && string.Equals(c.Name, category.Name, StringComparison.CurrentCultureIgnoreCase)))
            throw new StockException($"\"{category.Name}\" kategorisi zaten var.");

        if (category.Id == 0)
        {
            category.SortOrder = all.Count == 0 ? 0 : all.Max(c => c.SortOrder) + 1;
            await db.InsertAsync(category);
        }
        else
        {
            await db.UpdateAsync(category);
        }
    }

    public async Task DeleteCategoryAsync(StockCategory category)
    {
        var db = await Db();
        category.IsDeleted = true;
        await db.UpdateAsync(category);
    }

    public async Task<List<StockLocation>> GetLocationsAsync(bool includeDeleted = false)
    {
        var db = await Db();
        var list = await db.Table<StockLocation>().ToListAsync();
        return list.Where(l => includeDeleted || !l.IsDeleted).OrderBy(l => l.SortOrder).ThenBy(l => l.Name).ToList();
    }

    public async Task SaveLocationAsync(StockLocation location)
    {
        location.Name = (location.Name ?? string.Empty).Trim();
        if (location.Name.Length == 0)
            throw new StockException("Konum adı girin.");

        var db = await Db();
        var all = await db.Table<StockLocation>().ToListAsync();
        if (all.Any(l => l.Id != location.Id && !l.IsDeleted && string.Equals(l.Name, location.Name, StringComparison.CurrentCultureIgnoreCase)))
            throw new StockException($"\"{location.Name}\" konumu zaten var.");

        if (location.Id == 0)
        {
            location.SortOrder = all.Count == 0 ? 0 : all.Max(l => l.SortOrder) + 1;
            await db.InsertAsync(location);
        }
        else
        {
            await db.UpdateAsync(location);
        }
    }

    public async Task DeleteLocationAsync(StockLocation location)
    {
        var db = await Db();
        location.IsDeleted = true;
        await db.UpdateAsync(location);
    }

    // ───────────────────────── Etiketli ürünler ─────────────────────────

    public async Task<List<StockItem>> GetItemsAsync(StockItemQuery? query = null, bool includeDeleted = false)
    {
        query ??= new StockItemQuery();
        var db = await Db();
        var items = await db.Table<StockItem>().ToListAsync();
        IEnumerable<StockItem> result = items.Where(i => includeDeleted || !i.IsDeleted);

        if (query.Status.HasValue) result = result.Where(i => i.Status == query.Status.Value);
        if (query.Karat.HasValue) result = result.Where(i => i.Karat == query.Karat.Value);
        if (query.CategoryId.HasValue) result = result.Where(i => i.CategoryId == query.CategoryId.Value);
        if (query.LocationId.HasValue) result = result.Where(i => i.LocationId == query.LocationId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower(Turkish);
            var categories = (await GetCategoriesAsync(true)).ToDictionary(c => c.Id, c => c.Name.ToLower(Turkish));
            result = result.Where(i =>
                i.Code.Contains(term, StringComparison.Ordinal) ||
                (i.ModelName?.ToLower(Turkish).Contains(term) ?? false) ||
                (i.SupplierName?.ToLower(Turkish).Contains(term) ?? false) ||
                (i.Note?.ToLower(Turkish).Contains(term) ?? false) ||
                (categories.TryGetValue(i.CategoryId, out var name) && name.Contains(term)));
        }

        return result.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id).ToList();
    }

    public async Task<StockItem?> GetItemAsync(int id)
    {
        var db = await Db();
        return await db.Table<StockItem>().Where(i => i.Id == id).FirstOrDefaultAsync();
    }

    /// <summary>Koda göre ürün (silinmiş olanlar dahil — okutmada "silinmiş ürün" uyarısı için).</summary>
    public async Task<StockItem?> GetItemByCodeAsync(string code)
    {
        var db = await Db();
        return await db.Table<StockItem>().Where(i => i.Code == code).FirstOrDefaultAsync();
    }

    /// <summary>Sıradaki ürün kodunu gösterir (kaydetmez).</summary>
    public async Task<string> PeekNextCodeAsync()
    {
        var db = await Db();
        string? next = null;
        await db.RunInTransactionAsync(conn => next = ProductCode.Format(NextSequence(conn)));
        return next!;
    }

    private static long NextSequence(SQLiteConnection conn)
    {
        var counter = conn.Find<StockCodeCounter>(1)?.LastSequence ?? 0;
        var maxCode = conn.ExecuteScalar<string>("SELECT MAX(Code) FROM StockItem");
        var maxInTable = ProductCode.TryGetSequence(maxCode, out var seq) ? seq : 0;
        return Math.Max(counter, maxInTable) + 1;
    }

    /// <summary>Yeni ürün kaydeder: yeni kod üretir, "Giriş" hareketi yazar.</summary>
    public async Task<StockItem> CreateItemAsync(StockItemDraft draft, string? description = null)
    {
        await ValidateAsync(draft.CategoryId, draft.Karat, draft.WeightMg, draft.StoneWeightMg);

        var db = await Db();
        var now = DateTime.Now;
        var item = new StockItem
        {
            CategoryId = draft.CategoryId,
            Karat = draft.Karat,
            ModelName = Clean(draft.ModelName),
            WeightMg = draft.WeightMg,
            StoneWeightMg = draft.StoneWeightMg,
            LocationId = draft.LocationId,
            SupplierName = Clean(draft.SupplierName),
            PurchaseMilyem = draft.PurchaseMilyem,
            Note = Clean(draft.Note),
            Status = StockItemStatus.InStock,
            CreatedAt = now
        };

        await _writeLock.WaitAsync();
        try
        {
            await db.RunInTransactionAsync(conn =>
            {
                var sequence = NextSequence(conn);
                item.Code = ProductCode.Format(sequence);
                conn.Insert(item);
                conn.InsertOrReplace(new StockCodeCounter { Id = 1, LastSequence = sequence });
                conn.Insert(Movement(item, StockMovementType.Entry, now, description ?? "Ürün girişi"));
            });
        }
        finally
        {
            _writeLock.Release();
        }

        return item;
    }

    /// <summary>Ürün bilgilerini düzeltir; değişen alanlar "Düzeltme" hareketine yazılır.</summary>
    public async Task<StockItem> UpdateItemAsync(StockItem changed, string? description = null)
    {
        await ValidateAsync(changed.CategoryId, changed.Karat, changed.WeightMg, changed.StoneWeightMg);

        return await WriteItemAsync(changed.Id, async (item, now) =>
        {
            if (item.IsDeleted)
                throw new StockException("Silinmiş ürün düzenlenemez.");

            var details = await DescribeChangesAsync(item, changed);
            if (details.Count == 0)
                return null;

            item.CategoryId = changed.CategoryId;
            item.Karat = changed.Karat;
            item.ModelName = Clean(changed.ModelName);
            item.WeightMg = changed.WeightMg;
            item.StoneWeightMg = changed.StoneWeightMg;
            item.LocationId = changed.LocationId;
            item.SupplierName = Clean(changed.SupplierName);
            item.PurchaseMilyem = changed.PurchaseMilyem;
            item.Note = Clean(changed.Note);

            var m = Movement(item, StockMovementType.Edit, now, description ?? "Düzeltme");
            m.Details = Truncate(string.Join("; ", details), 500);
            return m;
        });
    }

    /// <summary>Satıldı olarak işaretler. Fiyat ve müşteri isteğe bağlıdır.</summary>
    public Task<StockItem> MarkSoldAsync(int itemId, decimal? price, int? customerId, int? transactionId, string? description = null) =>
        WriteItemAsync(itemId, (item, now) =>
        {
            EnsureActive(item);
            if (item.Status == StockItemStatus.Sold)
                throw new StockException($"Bu ürün {item.SoldAt:dd.MM.yyyy HH:mm} tarihinde zaten satılmış.");
            if (item.Status == StockItemStatus.Lost)
                throw new StockException("Bu ürün kayıp olarak işaretli. Önce vitrine geri alın.");

            item.Status = StockItemStatus.Sold;
            item.SoldAt = now;
            item.SalePrice = price;
            item.SaleCustomerId = customerId;
            item.SaleTransactionId = transactionId;

            var m = Movement(item, StockMovementType.Sale, now, description ?? "Satış");
            m.Price = price;
            m.CustomerId = customerId;
            m.TransactionId = transactionId;
            return Task.FromResult<StockMovement?>(m);
        });

    /// <summary>Satılan ürünü iade alır, vitrine döner (aynı kodla).</summary>
    public Task<StockItem> ReturnAsync(int itemId, string? description = null) =>
        WriteItemAsync(itemId, (item, now) =>
        {
            EnsureActive(item);
            if (item.Status != StockItemStatus.Sold)
                throw new StockException("Sadece satılmış ürün iade alınabilir.");

            var m = Movement(item, StockMovementType.Return, now, description ?? "İade");
            m.Price = item.SalePrice;
            m.CustomerId = item.SaleCustomerId;
            m.TransactionId = item.SaleTransactionId;

            item.Status = StockItemStatus.InStock;
            item.SoldAt = null;
            item.SalePrice = null;
            item.SaleCustomerId = null;
            item.SaleTransactionId = null;
            return Task.FromResult<StockMovement?>(m);
        });

    /// <summary>Kayıp olarak işaretler (sayımdan sonra ayrı ve açık bir adım; otomatik yapılmaz).</summary>
    public Task<StockItem> MarkLostAsync(int itemId, int? countSessionId, string? description = null) =>
        WriteItemAsync(itemId, (item, now) =>
        {
            EnsureActive(item);
            if (item.Status != StockItemStatus.InStock)
                throw new StockException("Sadece vitrindeki ürün kayıp yapılabilir.");

            item.Status = StockItemStatus.Lost;
            var m = Movement(item, StockMovementType.Lost, now, description ?? "Kayıp (sayım farkı)");
            m.CountSessionId = countSessionId;
            return Task.FromResult<StockMovement?>(m);
        });

    /// <summary>Kayıp ürün bulundu: vitrine geri alınır.</summary>
    public Task<StockItem> MarkFoundAsync(int itemId, int? countSessionId, string? description = null) =>
        WriteItemAsync(itemId, (item, now) =>
        {
            EnsureActive(item);
            if (item.Status != StockItemStatus.Lost)
                throw new StockException("Ürün kayıp olarak işaretli değil.");

            item.Status = StockItemStatus.InStock;
            var m = Movement(item, StockMovementType.Found, now, description ?? "Bulundu, vitrine geri alındı");
            m.CountSessionId = countSessionId;
            return Task.FromResult<StockMovement?>(m);
        });

    /// <summary>Ürünü siler (IsDeleted). Kodu bir daha kullanılmaz.</summary>
    public Task<StockItem> DeleteItemAsync(int itemId, string reason) =>
        WriteItemAsync(itemId, (item, now) =>
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new StockException("Silme nedeni girin.");
            EnsureActive(item);

            item.IsDeleted = true;
            item.DeletedAt = now;
            return Task.FromResult<StockMovement?>(Movement(item, StockMovementType.Delete, now, reason.Trim()));
        });

    /// <summary>Etiket basıldı bilgisini kaydeder (stok hareketi değildir).</summary>
    public async Task RecordLabelPrintedAsync(int itemId)
    {
        var db = await Db();
        await db.ExecuteAsync("UPDATE StockItem SET LabelPrintCount = LabelPrintCount + 1, LastLabelPrintedAt = ? WHERE Id = ?",
            DateTime.Now, itemId);
    }

    public async Task<List<StockMovement>> GetItemMovementsAsync(int itemId)
    {
        var db = await Db();
        return await db.Table<StockMovement>().Where(m => m.ItemId == itemId)
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id).ToListAsync();
    }

    /// <summary>Tarih aralığındaki hareketler (raporlar).</summary>
    public async Task<List<StockMovement>> GetMovementsAsync(DateTime from, DateTime to, params StockMovementType[] types)
    {
        var db = await Db();
        var list = await db.Table<StockMovement>().Where(m => m.Date >= from && m.Date < to).ToListAsync();
        if (types.Length > 0)
            list = list.Where(m => types.Contains(m.Type)).ToList();
        return list.OrderByDescending(m => m.Date).ThenByDescending(m => m.Id).ToList();
    }

    /// <summary>Vitrindeki ürünlerin ayar bazında toplamı.</summary>
    public async Task<StockTotals> GetTotalsAsync(int? locationId = null)
    {
        var items = await GetItemsAsync(new StockItemQuery { Status = StockItemStatus.InStock, LocationId = locationId });
        return StockCalculator.Calculate(items, await GetKaratsAsync(true));
    }

    /// <summary>Tarih aralığında satılan ürünler (şu an hâlâ satılmış görünenler).</summary>
    public async Task<List<StockItem>> GetSoldItemsAsync(DateTime from, DateTime to)
    {
        var items = await GetItemsAsync(new StockItemQuery { Status = StockItemStatus.Sold });
        return items.Where(i => i.SoldAt >= from && i.SoldAt < to).OrderByDescending(i => i.SoldAt).ToList();
    }

    /// <summary>En az <paramref name="days"/> gündür vitrinde duran ürünler (en eski önce).</summary>
    public async Task<List<StockItem>> GetSlowItemsAsync(int days)
    {
        var cutoff = DateTime.Now.AddDays(-days);
        var items = await GetItemsAsync(new StockItemQuery { Status = StockItemStatus.InStock });
        return items.Where(i => i.CreatedAt <= cutoff).OrderBy(i => i.CreatedAt).ToList();
    }

    private async Task ValidateAsync(int categoryId, int karat, int weightMg, int stoneMg)
    {
        if (weightMg <= 0)
            throw new StockException("Gram 0'dan büyük olmalı.");
        if (stoneMg < 0 || stoneMg >= weightMg)
            throw new StockException("Taş gramı, ürün gramından küçük olmalı.");
        if (karat is < 1 or > 24)
            throw new StockException("Ayar seçin.");
        var category = await GetCategoryAsync(categoryId);
        if (category == null)
            throw new StockException("Kategori seçin.");
    }

    private async Task<StockItem> WriteItemAsync(int itemId, Func<StockItem, DateTime, Task<StockMovement?>> change)
    {
        var db = await Db();
        // Değişiklik açıklaması (kategori adları vb.) veritabanından okunur; yazma tek işlemde (transaction) yapılır
        var now = DateTime.Now;

        await _writeLock.WaitAsync();
        try
        {
            var fresh = await GetItemAsync(itemId) ?? throw new StockException("Ürün bulunamadı.");
            var movement = await change(fresh, now);
            if (movement == null)
                return fresh;

            await db.RunInTransactionAsync(conn =>
            {
                conn.Update(fresh);
                conn.Insert(movement);
            });
            return fresh;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<List<string>> DescribeChangesAsync(StockItem before, StockItem after)
    {
        var changes = new List<string>();
        if (before.CategoryId != after.CategoryId)
            changes.Add($"Kategori: {(await GetCategoryAsync(before.CategoryId))?.Name} → {(await GetCategoryAsync(after.CategoryId))?.Name}");
        if (before.Karat != after.Karat)
            changes.Add($"Ayar: {before.Karat} → {after.Karat}");
        if (before.WeightMg != after.WeightMg)
            changes.Add($"Gram: {WeightFormat.Gram(before.WeightMg)} → {WeightFormat.Gram(after.WeightMg)}");
        if (before.StoneWeightMg != after.StoneWeightMg)
            changes.Add($"Taş: {WeightFormat.Gram(before.StoneWeightMg)} → {WeightFormat.Gram(after.StoneWeightMg)}");
        if (before.LocationId != after.LocationId)
        {
            var locations = await GetLocationsAsync(true);
            string Name(int id) => locations.FirstOrDefault(l => l.Id == id)?.Name ?? "—";
            changes.Add($"Konum: {Name(before.LocationId)} → {Name(after.LocationId)}");
        }
        if ((before.ModelName ?? "") != (Clean(after.ModelName) ?? ""))
            changes.Add($"Model: {before.ModelName ?? "—"} → {Clean(after.ModelName) ?? "—"}");
        if ((before.SupplierName ?? "") != (Clean(after.SupplierName) ?? ""))
            changes.Add($"Tedarikçi: {before.SupplierName ?? "—"} → {Clean(after.SupplierName) ?? "—"}");
        if (before.PurchaseMilyem != after.PurchaseMilyem)
            changes.Add($"Alış milyemi: {Milyem(before.PurchaseMilyem)} → {Milyem(after.PurchaseMilyem)}");
        if ((before.Note ?? "") != (Clean(after.Note) ?? ""))
            changes.Add("Not değişti");
        return changes;

        static string Milyem(int? m) => m.HasValue ? WeightFormat.Milyem(m.Value) : "—";
    }

    private static void EnsureActive(StockItem item)
    {
        if (item.IsDeleted)
            throw new StockException("Bu ürün silinmiş.");
    }

    private static StockMovement Movement(StockItem item, StockMovementType type, DateTime now, string description) => new()
    {
        ItemId = item.Id,
        Code = item.Code,
        Type = type,
        Date = now,
        Description = Truncate(description, 255),
        WeightMg = item.WeightMg
    };

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    private static string? Truncate(string? text, int max) => text == null || text.Length <= max ? text : text[..max];

    // ───────────────────────── Kasa (etiketsiz) ürünleri ─────────────────────────

    public async Task<List<BulkProduct>> GetBulkProductsAsync(bool includeDeleted = false)
    {
        var db = await Db();
        var list = await db.Table<BulkProduct>().ToListAsync();
        return list.Where(b => includeDeleted || !b.IsDeleted).OrderBy(b => b.SortOrder).ThenBy(b => b.Name).ToList();
    }

    public async Task SaveBulkProductAsync(BulkProduct product)
    {
        product.Name = (product.Name ?? string.Empty).Trim();
        if (product.Name.Length == 0)
            throw new StockException("Ürün adı girin.");
        if (product.Unit == BulkUnit.Piece && product.PieceWeightMg <= 0)
            throw new StockException("Adetle tutulan ürün için bir adedin gramını girin (has hesabı için).");

        var db = await Db();
        var all = await db.Table<BulkProduct>().ToListAsync();
        if (all.Any(b => b.Id != product.Id && !b.IsDeleted && string.Equals(b.Name, product.Name, StringComparison.CurrentCultureIgnoreCase)))
            throw new StockException($"\"{product.Name}\" zaten var.");

        if (product.Id == 0)
        {
            product.SortOrder = all.Count == 0 ? 0 : all.Max(b => b.SortOrder) + 1;
            product.Amount = 0; // Miktar sadece hareketle değişir
            await db.InsertAsync(product);
        }
        else
        {
            // Miktar bu yoldan değiştirilemez; veritabanındaki değer korunur
            var existing = all.First(b => b.Id == product.Id);
            if (existing.Unit != product.Unit && existing.Amount != 0)
                throw new StockException("Miktarı sıfır olmayan ürünün birimi değiştirilemez.");
            product.Amount = existing.Amount;
            await db.UpdateAsync(product);
        }
    }

    public async Task DeleteBulkProductAsync(BulkProduct product)
    {
        if (product.Amount != 0)
            throw new StockException("Miktarı sıfır olmayan kasa ürünü silinemez. Önce çıkış ya da düzeltme yapın.");
        var db = await Db();
        product.IsDeleted = true;
        await db.UpdateAsync(product);
    }

    /// <summary>
    /// Kasa hareketi. Giriş/Çıkış: <paramref name="amount"/> eklenir/çıkarılır.
    /// Düzeltme: miktar <paramref name="amount"/> değerine ayarlanır (sayım). Birim adetse adet, gramsa mg.
    /// Veresiye satışta (sadece Çıkış) müşteri ve defterdeki işlem hareketle birlikte saklanır; işlemi çağıran açar.
    /// </summary>
    public async Task<BulkProduct> AddBulkMovementAsync(int productId, StockMovementType type, long amount, string? description = null,
        int? customerId = null, int? transactionId = null, decimal? price = null)
    {
        if (type is not (StockMovementType.BulkIn or StockMovementType.BulkOut or StockMovementType.BulkAdjust))
            throw new ArgumentOutOfRangeException(nameof(type));
        if (type != StockMovementType.BulkOut && (customerId.HasValue || transactionId.HasValue || price.HasValue))
            throw new ArgumentException("Müşteri ve fiyat sadece çıkışta (satış) verilir.");
        if (amount < 0 || (amount == 0 && type != StockMovementType.BulkAdjust))
            throw new StockException("Miktar 0'dan büyük olmalı.");

        var db = await Db();
        await _writeLock.WaitAsync();
        try
        {
            var product = await db.Table<BulkProduct>().Where(b => b.Id == productId).FirstOrDefaultAsync()
                          ?? throw new StockException("Ürün bulunamadı.");
            if (product.IsDeleted)
                throw new StockException("Bu kasa ürünü silinmiş.");

            var delta = type switch
            {
                StockMovementType.BulkIn => amount,
                StockMovementType.BulkOut => -amount,
                _ => amount - product.Amount
            };
            if (product.Amount + delta < 0)
                throw new StockException($"Kasada yeterli {product.Name} yok (mevcut: {FormatBulkAmount(product, product.Amount)}).");
            if (delta == 0)
                return product;

            product.Amount += delta;
            var now = DateTime.Now;
            var movement = new StockMovement
            {
                BulkProductId = product.Id,
                Type = type,
                Date = now,
                AmountDelta = delta,
                Description = Truncate(string.IsNullOrWhiteSpace(description)
                    ? type switch { StockMovementType.BulkIn => "Giriş", StockMovementType.BulkOut => "Çıkış", _ => "Düzeltme (sayım)" }
                    : description.Trim(), 255),
                CustomerId = customerId,
                TransactionId = transactionId,
                Price = price
            };

            await db.RunInTransactionAsync(conn =>
            {
                conn.Update(product);
                conn.Insert(movement);
            });
            return product;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<List<StockMovement>> GetBulkMovementsAsync(int productId, int take = 100)
    {
        var db = await Db();
        return await db.Table<StockMovement>().Where(m => m.BulkProductId == productId)
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id).Take(take).ToListAsync();
    }

    /// <summary>Kasa miktarını birimine göre yazar: "12 adet" / "35,40 gr".</summary>
    public static string FormatBulkAmount(BulkProduct product, long amount) =>
        product.Unit == BulkUnit.Gram ? $"{WeightFormat.Gram(amount)} gr" : $"{amount:N0} adet";

    // ───────────────────────── Etiket yazıcısı ayarları ─────────────────────────

    public async Task<LabelPrinterSettings> GetLabelSettingsAsync()
    {
        var db = await Db();
        return await db.Table<LabelPrinterSettings>().FirstOrDefaultAsync() ?? new LabelPrinterSettings();
    }

    public async Task SaveLabelSettingsAsync(LabelPrinterSettings settings)
    {
        if (settings.WidthMm is <= 0 or > 120 || settings.HeightMm is <= 0 or > 120)
            throw new StockException("Etiket ölçüsü 0–120 mm arasında olmalı.");
        if (settings.GapMm is < 0 or > 20)
            throw new StockException("Etiket arası boşluk 0–20 mm arasında olmalı.");
        settings.Id = 1;
        var db = await Db();
        await db.InsertOrReplaceAsync(settings);
    }
}
