namespace CondoSystem.DTO.Booking
{
    public class BookingResponseDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
        public int GuestCount { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? QrCodeData { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? CheckedInAt { get; set; }
        public DateTime? CheckedOutAt { get; set; }
        public string? Notes { get; set; }
        public string? RejectionReason { get; set; }
        public string? PaymentImageUrl { get; set; }
        public CondoSummaryDto Condo { get; set; } = new();
    }
    
    public class CondoSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
    }
}
