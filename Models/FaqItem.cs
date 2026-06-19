using System.ComponentModel.DataAnnotations;

namespace NetURLScanner.Models
{
    public class FaqItem
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(300)]
        public string Question { get; set; } = string.Empty;

        [Required]
        public string Answer { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
