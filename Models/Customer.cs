using SQLite;
using System.ComponentModel.DataAnnotations;
using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Kuyumcu.Models
{
    public class Customer
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        [Required(ErrorMessage = "İsim alanı zorunludur")]
        [MaxLength(100, ErrorMessage = "İsim en fazla 100 karakter olabilir")]
        [Collation("NOCASE")]
        public string Name { get; set; }
        
        [MaxLength(10, ErrorMessage = "Telefon numarası en fazla 10 karakter olabilir")]
        public string PhoneNumber { get; set; }
        
        /// <summary>
        /// Kaydın silinip silinmediğini belirtir (Soft delete için)
        /// </summary>
        public bool IsDeleted { get; set; } = false;
        
        public override string ToString()
        {
            return Name;
        }
    }
}
