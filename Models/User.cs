using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NetURLScanner.Models
{
    [Table("AppUsers")]
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        public string? PasswordHash { get; set; }

        [MaxLength(64)]
        public string? GoogleSubjectId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = "User";

        [MaxLength(120)]
        [Display(Name = "Họ và tên")]
        public string? FullName { get; set; }

        [MaxLength(20)]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }

        [MaxLength(300)]
        public string? AvatarPath { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsPremium { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
