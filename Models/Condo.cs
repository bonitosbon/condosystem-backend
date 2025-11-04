using CondoSystem.Models;
using System.ComponentModel.DataAnnotations;

public class Condo
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Location { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    public string? Amenities { get; set; } // JSON string or comma-separated
    
    public int MaxGuests { get; set; } = 4;
    
    public decimal PricePerNight { get; set; }
    
    public string? ImageUrl { get; set; }

    // Optional: if you really need a unique reference code for condos, keep this
    public string UniqueCode { get; set; } = Guid.NewGuid().ToString();
    
    // Booking link for guest booking page
    public string? BookingLink { get; set; }

    // Status: Available, Occupied, Maintenance, Unavailable
    public string Status { get; set; } = "Available";
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdated { get; set; }

    // ✅ Owner relationship (Landlord / Resident)
    public string OwnerId { get; set; }   // required now
    public ApplicationUser Owner { get; set; }   // required

    // ✅ One-to-one FrontDesk (required)
    [Required]
    public string FrontDeskId { get; set; }   // required, not nullable
    public ApplicationUser FrontDeskUser { get; set; }   // required
    
    // Navigation properties
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
