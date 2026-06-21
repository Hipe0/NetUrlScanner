using System.ComponentModel.DataAnnotations;

namespace NetURLScanner.Models
{
    public class BlacklistedBankAccount
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Ngân hàng (Mã ngân hàng hoặc tên rút gọn).")]
        [StringLength(100)]
        public string BankId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Số tài khoản.")]
        [StringLength(50)]
        public string BankAccountNumber { get; set; } = string.Empty;

        [StringLength(200)]
        public string? BankAccountOwnerName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập lý do đưa vào blacklist.")]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
