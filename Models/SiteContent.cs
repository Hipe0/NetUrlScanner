using System.ComponentModel.DataAnnotations;

namespace NetURLScanner.Models
{
    public class SiteContent
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string ContentKey { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Body { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
