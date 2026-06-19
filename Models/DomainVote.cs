using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NetURLScanner.Models
{
    public class DomainVote
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Required]
        [MaxLength(255)]
        public string NormalizedDomain { get; set; } = string.Empty;

        /// <summary>+1 upvote, -1 downvote</summary>
        public int Vote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
