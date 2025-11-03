using System.ComponentModel.DataAnnotations;

namespace CondoSystem.DTO.Condo
{
    public class CreateCondoDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string Location { get; set; } = string.Empty;
        
        public string? Description { get; set; }
        
        public string? Amenities { get; set; }
        
        [Range(1, 20)]
        public int MaxGuests { get; set; } = 4;
        
        [Range(0, double.MaxValue)]
        public decimal PricePerNight { get; set; }
        
        public string? ImageUrl { get; set; }
        
        [Required]
        [EmailAddress]
        public string FrontDeskEmail { get; set; } = string.Empty;
        
        [Required]
        [MinLength(6)]
        public string FrontDeskPassword { get; set; } = string.Empty;
    }
}
