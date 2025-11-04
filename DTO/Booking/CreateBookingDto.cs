using System.ComponentModel.DataAnnotations;

namespace CondoSystem.DTO.Booking
{
    public class CreateBookingDto
    {
        [Required]
        public string FullName { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        [Phone]
        public string Contact { get; set; } = string.Empty;
        
        [Required]
        [Range(1, 10)]
        public int GuestCount { get; set; }
        
        [Required]
        public DateTime StartDateTime { get; set; }
        
        [Required]
        public DateTime EndDateTime { get; set; }
        
        public string? Notes { get; set; }
        
        public string? PaymentImageUrl { get; set; }
        
        [Required]
        public int CondoId { get; set; }
    }
}
