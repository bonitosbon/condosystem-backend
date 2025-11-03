using System.ComponentModel.DataAnnotations;

namespace CondoSystem.Models
{
    public class OwnerProfile
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; }
    }
}
