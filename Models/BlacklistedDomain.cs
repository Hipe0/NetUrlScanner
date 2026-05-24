using System.ComponentModel.DataAnnotations;

namespace NetURLScanner.Models
{
    public class BlacklistedDomain
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập domain.")]
        [StringLength(200)]
        public string Domain { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn loại rủi ro.")]
        [StringLength(100)]
        public string Category { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập lý do đưa vào blacklist.")]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn mức độ.")]
        [StringLength(50)]
        public string Severity { get; set; } = "High";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}