using System.ComponentModel.DataAnnotations;

namespace CondoSystem.Models
{
    public class Booking
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        public string Contact { get; set; } = string.Empty;
        
        [Required]
        [Range(1, 10)]
        public int GuestCount { get; set; }
        
        [Required]
        public DateTime StartDateTime { get; set; }
        
        [Required]
        public DateTime EndDateTime { get; set; }
        
        public string? PaymentImageUrl { get; set; }
        
        public string? Notes { get; set; }

        // Status: PendingApproval, Approved, CheckedIn, CheckedOut, Rejected, Cancelled
        public string Status { get; set; } = "PendingApproval";
        
        // QR Code for check-in
        public string? QrCodeData { get; set; }
        
        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ApprovedAt { get; set; }
        public DateTime? CheckedInAt { get; set; }
        public DateTime? CheckedOutAt { get; set; }
        
        // Approval details
        public string? ApprovedBy { get; set; }
        public string? RejectionReason { get; set; }

        // Relationships
        public int CondoId { get; set; }
        public Condo Condo { get; set; }
        
        // Guest user (optional - for registered guests)
        public string? GuestUserId { get; set; }
        public ApplicationUser? GuestUser { get; set; }
    }
}
