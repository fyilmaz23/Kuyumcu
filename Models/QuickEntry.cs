using SQLite;
using System.ComponentModel.DataAnnotations;
using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Kuyumcu.Models
{
    public class QuickEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Ad Soyad alanı zorunludur")]
        [MaxLength(100, ErrorMessage = "Ad Soyad en fazla 100 karakter olabilir")]
        public string FullName { get; set; }
        
        [StringLength(11, MinimumLength = 11, ErrorMessage = "TC Kimlik Numarası 11 haneli olmalıdır")]
        public string TcKimlikNo { get; set; }
        
        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        [Required(ErrorMessage = "Tutar alanı zorunludur")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Tutar sıfırdan büyük olmalıdır")]
        public decimal Tutar { get; set; }
        
        /// <summary>
        /// Kaydın işlenip işlenmediğini belirtir
        /// </summary>
        public bool IsProcessed { get; set; } = false;
        
        public override string ToString()
        {
            return $"{FullName} - {Tutar:C2}";
        }
    }
}
