using System.ComponentModel.DataAnnotations;

namespace CondoSystem.Models
{
    public class FrontDeskProfile
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        // ✅ Each front desk belongs to exactly one condo
        public int CondoId { get; set; }
        public Condo Condo { get; set; }
    }
}
