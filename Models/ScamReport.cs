using System.ComponentModel.DataAnnotations;

namespace NetURLScanner.Models
{
    public class ScamReport
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập URL hoặc IP.")]
        [StringLength(500)]
        [Display(Name = "URL hoặc IP lừa đảo")]
        public string UrlOrIp { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng cung cấp lý do hoặc bằng chứng.")]
        [Display(Name = "Bằng chứng / Lý do")]
        public string Evidence { get; set; } = string.Empty;

        public int? UserId { get; set; }
        public User? User { get; set; }

        [Display(Name = "Ngày báo cáo")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Trạng thái: Pending, Approved, Rejected
        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = "Pending";
    }
}
