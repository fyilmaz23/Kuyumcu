using SQLite;

namespace Kuyumcu.Models;

// Barkodlu vitrin stoğu ve kasa (etiketsiz) ürünleri.
// Ağırlıklar tam sayı miligram (mg) olarak tutulur: 4,255 gr → 4255. Toplamlarda yuvarlama kayması olmaz.
// Milyemler binde olarak tutulur: 0,916 → 916.

/// <summary>Etiketli ürünün durumu.</summary>
public enum StockItemStatus
{
    /// <summary>Vitrinde / dükkânda.</summary>
    InStock = 0,
    Sold = 1,
    /// <summary>Sayımda bulunamadı ve kullanıcı tarafından açıkça kayıp işaretlendi.</summary>
    Lost = 2
}

/// <summary>Stok hareketi türü. Her değişiklik <see cref="StockMovement"/> tablosuna yazılır.</summary>
public enum StockMovementType
{
    Entry = 0,
    Sale = 1,
    Return = 2,
    /// <summary>Düzeltme (gram, ayar, kategori vb. değişikliği).</summary>
    Edit = 3,
    Delete = 4,
    /// <summary>Sayım farkı: kayıp olarak işaretlendi.</summary>
    Lost = 5,
    /// <summary>Kayıp ürün bulundu, vitrine geri alındı.</summary>
    Found = 6,

    // Kasa (etiketsiz) ürünleri
    BulkIn = 10,
    BulkOut = 11,
    /// <summary>Kasa sayımı / düzeltme: miktar yeni değere ayarlandı.</summary>
    BulkAdjust = 12
}

/// <summary>Kasa ürününün birimi.</summary>
public enum BulkUnit
{
    Piece = 0,
    Gram = 1
}

/// <summary>Tek tek etiketlenen vitrin ürünü (bilezik, kolye, yüzük…).</summary>
[Table("StockItem")]
public class StockItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>12 haneli, sadece rakam, "2" ile başlar. Benzersizdir ve asla yeniden kullanılmaz (silinen ürününki de).</summary>
    [Indexed(Name = "UX_StockItem_Code", Unique = true), MaxLength(12), NotNull]
    public string Code { get; set; } = string.Empty;

    [Indexed]
    public int CategoryId { get; set; }

    /// <summary>Ayar (14, 22…). Milyem <see cref="KaratDefinition"/> tablosundan okunur.</summary>
    [Indexed]
    public int Karat { get; set; }

    /// <summary>Model / kısa açıklama (örn. "Burgu", "Hasır").</summary>
    [MaxLength(100)]
    public string? ModelName { get; set; }

    /// <summary>Brüt ağırlık (mg). Etikete bu basılır.</summary>
    public int WeightMg { get; set; }

    /// <summary>Taş ağırlığı (mg). Net gram = brüt − taş; has hesabı netten yapılır.</summary>
    public int StoneWeightMg { get; set; }

    /// <summary>Konum (Vitrin 1, Kasa…). 0 = belirtilmemiş.</summary>
    [Indexed]
    public int LocationId { get; set; }

    [MaxLength(100)]
    public string? SupplierName { get; set; }

    /// <summary>Alış milyemi (binde, isteğe bağlı). Örn. 930 = 0,930.</summary>
    public int? PurchaseMilyem { get; set; }

    [Indexed]
    public StockItemStatus Status { get; set; } = StockItemStatus.InStock;

    public DateTime CreatedAt { get; set; }
    public DateTime? SoldAt { get; set; }

    /// <summary>Satış tutarı (TL, isteğe bağlı).</summary>
    public decimal? SalePrice { get; set; }
    public int? SaleCustomerId { get; set; }
    /// <summary>Satış müşteri hesabına (veresiye) yazıldıysa oluşturulan işlemin Id'si.</summary>
    public int? SaleTransactionId { get; set; }

    public int LabelPrintCount { get; set; }
    public DateTime? LastLabelPrintedAt { get; set; }

    [MaxLength(255)]
    public string? Note { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    [Ignore] public decimal Gram => WeightMg / 1000m;
    [Ignore] public decimal StoneGram => StoneWeightMg / 1000m;
    [Ignore] public int NetWeightMg => Math.Max(0, WeightMg - StoneWeightMg);
    [Ignore] public decimal NetGram => NetWeightMg / 1000m;
}

/// <summary>Stok hareket kaydı (giriş, satış, iade, düzeltme, silme, sayım farkı, kasa giriş/çıkış).</summary>
[Table("StockMovement")]
public class StockMovement
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Etiketli ürün hareketiyse ürün Id'si, değilse 0.</summary>
    [Indexed]
    public int ItemId { get; set; }

    /// <summary>Kasa ürünü hareketiyse ürün Id'si, değilse 0.</summary>
    [Indexed]
    public int BulkProductId { get; set; }

    /// <summary>Hareket anındaki ürün kodu (silinse bile okunabilsin).</summary>
    [MaxLength(12)]
    public string? Code { get; set; }

    public StockMovementType Type { get; set; }

    [Indexed]
    public DateTime Date { get; set; }

    [MaxLength(255)]
    public string? Description { get; set; }

    /// <summary>Düzeltmede değişen alanlar ("Gram: 4,25 → 4,30").</summary>
    [MaxLength(500)]
    public string? Details { get; set; }

    /// <summary>Hareket anındaki brüt ağırlık (etiketli ürün, mg).</summary>
    public int WeightMg { get; set; }

    /// <summary>Kasa ürünü miktar değişimi (adet ya da mg, işaretli).</summary>
    public long AmountDelta { get; set; }

    public decimal? Price { get; set; }
    public int? CustomerId { get; set; }
    public int? TransactionId { get; set; }
    public int? CountSessionId { get; set; }

    /// <summary>İşlemi yapan kişi. Şimdilik tek kullanıcı; ileride doldurulacak.</summary>
    [MaxLength(50)]
    public string? UserName { get; set; }
}

/// <summary>Ürün kategorisi (Bilezik, Kolye…). Kullanıcı ekleyip düzenleyebilir.</summary>
[Table("StockCategory")]
public class StockCategory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [MaxLength(50), NotNull]
    public string Name { get; set; } = string.Empty;

    /// <summary>Etikete basılan kısa ad (örn. "BLZ").</summary>
    [MaxLength(8)]
    public string? ShortName { get; set; }

    /// <summary>Seri girişte önerilen ayar.</summary>
    public int DefaultKarat { get; set; } = 22;

    /// <summary>Satış fiyatı önerisinde kullanılan işçilik milyemi (binde). Örn. 50 = 0,050.</summary>
    public int LaborMilyem { get; set; }

    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }

    public override string ToString() => Name;
}

/// <summary>Ayar tanımı ve has karşılığı için milyem.</summary>
[Table("KaratDefinition")]
public class KaratDefinition
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Name = "UX_KaratDefinition_Karat", Unique = true)]
    public int Karat { get; set; }

    /// <summary>Binde milyem: 916 = 0,916.</summary>
    public int Milyem { get; set; }

    public bool IsDeleted { get; set; }

    [Ignore] public string DisplayName => $"{Karat} Ayar";
    public override string ToString() => DisplayName;
}

/// <summary>Ürünün durduğu yer (Vitrin 1, Vitrin 2, Kasa…).</summary>
[Table("StockLocation")]
public class StockLocation
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [MaxLength(50), NotNull]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }

    public override string ToString() => Name;
}

/// <summary>Kasa ürünü: birbirinin aynısı, etiketlenmeyen mallar (çeyrek, yarım, tam, gram altın…).</summary>
[Table("BulkProduct")]
public class BulkProduct
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [MaxLength(50), NotNull]
    public string Name { get; set; } = string.Empty;

    public BulkUnit Unit { get; set; }
    public int Karat { get; set; } = 22;

    /// <summary>Adetle tutulan ürünün bir adedinin ağırlığı (mg). Has hesabı için.</summary>
    public int PieceWeightMg { get; set; }

    /// <summary>Mevcut miktar: birim adetse adet, gramsa mg.</summary>
    public long Amount { get; set; }

    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }

    public override string ToString() => Name;
}

public enum StockCountStatus
{
    Open = 0,
    Completed = 1,
    Cancelled = 2
}

/// <summary>Sayım oturumu.</summary>
[Table("StockCountSession")]
public class StockCountSession
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>Sayılan konum; 0 = tüm stok.</summary>
    public int LocationId { get; set; }

    public StockCountStatus Status { get; set; }

    public int ExpectedCount { get; set; }
    public int FoundCount { get; set; }
    public int MissingCount { get; set; }
    public int ExtraCount { get; set; }

    [MaxLength(255)]
    public string? Note { get; set; }
}

/// <summary>Sayım satırının sonucu.</summary>
public enum StockCountResult
{
    /// <summary>Beklenen ürün okutuldu.</summary>
    Found = 0,
    /// <summary>Beklenen ama okutulmayan ürün (sayım bitince yazılır).</summary>
    Missing = 1,
    /// <summary>Kayıtlı olmayan kod.</summary>
    Unknown = 2,
    /// <summary>Kayıtlı ama satılmış / kayıp / silinmiş ürün okutuldu.</summary>
    NotInStock = 3,
    /// <summary>Vitrinde ama başka konumda kayıtlı ürün okutuldu.</summary>
    OtherLocation = 4
}

/// <summary>Sayım satırı: okutulan her kod ve sayım bitince eksik çıkan her ürün.</summary>
[Table("StockCountLine")]
public class StockCountLine
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SessionId { get; set; }

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Kayıtlı ürünse Id'si, değilse 0.</summary>
    public int ItemId { get; set; }

    public StockCountResult Result { get; set; }
    public DateTime? ScannedAt { get; set; }

    /// <summary>Eksik ürün kayıp yapıldı / fazla ürün vitrine alındı gibi bir işlem yapıldı mı?</summary>
    public bool IsResolved { get; set; }
}

/// <summary>
/// Ürün kodu sayacı (tek satır). Kodlar asla yeniden kullanılmaz: sonraki kod = max(sayaç, tablodaki en büyük kod) + 1.
/// Eski bir yedek geri yüklense bile sayaç geri sarılmaz (geri yüklemede büyük olan korunur).
/// </summary>
[Table("StockCodeCounter")]
public class StockCodeCounter
{
    [PrimaryKey]
    public int Id { get; set; } = 1;

    public long LastSequence { get; set; }
}
