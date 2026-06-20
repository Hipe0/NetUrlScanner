using System.ComponentModel.DataAnnotations;

namespace NetURLScanner.Models
{
    public class ScamReport
    {
        public int Id { get; set; }

        [Display(Name = "Loại báo cáo")]
        public string ReportType { get; set; } = "Url"; // "Url" or "BankAccount"

        [StringLength(500)]
        [Display(Name = "URL hoặc IP lừa đảo")]
        public string? UrlOrIp { get; set; }

        [Display(Name = "Ngân hàng")]
        public string? BankId { get; set; } // Bin or Code of the bank

        [Display(Name = "Số tài khoản")]
        public string? BankAccountNumber { get; set; }

        [Display(Name = "Tên chủ tài khoản")]
        public string? BankAccountOwnerName { get; set; }

        [Display(Name = "Số tiền bị lừa (VNĐ)")]
        public decimal? AmountLost { get; set; }

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
