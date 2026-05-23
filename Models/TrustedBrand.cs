using System.ComponentModel.DataAnnotations;

namespace NetURLScanner.Models
{
    public class TrustedBrand
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên thương hiệu.")]
        [StringLength(100)]
        public string BrandName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập domain chính thức.")]
        [StringLength(200)]
        public string OfficialDomain { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn lĩnh vực.")]
        [StringLength(100)]
        public string Category { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}