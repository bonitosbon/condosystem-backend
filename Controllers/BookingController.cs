using CondoSystem.Data;
using CondoSystem.DTO.Booking;
using CondoSystem.Models;
using CondoSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CondoSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IQrCodeService _qrCodeService;
        private readonly IEmailService _emailService;

        public BookingController(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager,
            IQrCodeService qrCodeService,
            IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _qrCodeService = qrCodeService;
            _emailService = emailService;
        }

        // Public endpoint for guests to create booking requests
        [HttpPost("create")]
        [AllowAnonymous]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDto dto)
        {
            try
            {
                if (dto == null)
                    return BadRequest("Booking data is required.");

                // Validate ModelState
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value?.Errors.Count > 0)
                        .SelectMany(x => x.Value!.Errors.Select(e => $"{x.Key}: {e.ErrorMessage}"))
                        .ToList();
                    return BadRequest(new { message = "Validation failed", errors = errors });
                }

            // Validate condo exists
            var condo = await _context.Condos
                .FirstOrDefaultAsync(c => c.Id == dto.CondoId);
            
            if (condo == null)
                    return BadRequest(new { message = "Condo not found." });

            // Check if condo is available for the requested dates
            var conflictingBookings = await _context.Bookings
                .Where(b => b.CondoId == dto.CondoId && 
                           b.Status != "Rejected" && b.Status != "Cancelled" &&
                           ((b.StartDateTime <= dto.StartDateTime && b.EndDateTime > dto.StartDateTime) ||
                            (b.StartDateTime < dto.EndDateTime && b.EndDateTime >= dto.EndDateTime) ||
                            (b.StartDateTime >= dto.StartDateTime && b.EndDateTime <= dto.EndDateTime)))
                .ToListAsync();

            if (conflictingBookings.Any())
                    return BadRequest(new { message = "Condo is not available for the selected dates." });

            // Check guest count limit
            if (dto.GuestCount > condo.MaxGuests)
                    return BadRequest(new { message = $"Maximum {condo.MaxGuests} guests allowed for this condo." });

                // Limit PaymentImageUrl size to prevent database issues (PostgreSQL text can be large, but let's be safe)
                string paymentImageUrl = dto.PaymentImageUrl;
                if (!string.IsNullOrEmpty(paymentImageUrl) && paymentImageUrl.Length > 5000000) // 5MB limit
                {
                    return BadRequest(new { message = "Payment image is too large. Please use a smaller image (under 5MB)." });
                }

                // Convert DateTime to UTC (PostgreSQL requires UTC for timestamp with time zone)
                // When dates come from JSON, they're Unspecified, so we need to explicitly convert to UTC
                DateTime startDateTime = dto.StartDateTime;
                if (startDateTime.Kind == DateTimeKind.Unspecified)
                {
                    // Treat as UTC if unspecified (common when deserializing from JSON)
                    startDateTime = DateTime.SpecifyKind(startDateTime, DateTimeKind.Utc);
                }
                else if (startDateTime.Kind == DateTimeKind.Local)
                {
                    startDateTime = startDateTime.ToUniversalTime();
                }
                // Ensure it's UTC (final safety check)
                if (startDateTime.Kind != DateTimeKind.Utc)
                {
                    startDateTime = startDateTime.ToUniversalTime();
                }

                DateTime endDateTime = dto.EndDateTime;
                if (endDateTime.Kind == DateTimeKind.Unspecified)
                {
                    // Treat as UTC if unspecified (common when deserializing from JSON)
                    endDateTime = DateTime.SpecifyKind(endDateTime, DateTimeKind.Utc);
                }
                else if (endDateTime.Kind == DateTimeKind.Local)
                {
                    endDateTime = endDateTime.ToUniversalTime();
                }
                // Ensure it's UTC (final safety check)
                if (endDateTime.Kind != DateTimeKind.Utc)
                {
                    endDateTime = endDateTime.ToUniversalTime();
                }

            var booking = new Booking
            {
                    FullName = dto.FullName ?? string.Empty,
                    Email = dto.Email ?? string.Empty,
                    Contact = dto.Contact ?? string.Empty,
                GuestCount = dto.GuestCount,
                    StartDateTime = startDateTime,
                    EndDateTime = endDateTime,
                Notes = dto.Notes,
                    PaymentImageUrl = paymentImageUrl,
                CondoId = dto.CondoId,
                Status = "PendingApproval"
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Booking request created successfully. Awaiting owner approval.", bookingId = booking.Id });
            }
            catch (Exception ex)
            {
                // Log the full exception for debugging
                System.Console.WriteLine($"Error creating booking: {ex.Message}");
                System.Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                
                return StatusCode(500, new { 
                    message = "An error occurred while creating the booking.", 
                    error = ex.Message,
                    details = ex.InnerException?.Message ?? ""
                });
            }
        }

        // Owner: Get all pending bookings for their condos
        [HttpGet("pending")]
        [Authorize(Roles = "OWNER")]
        public async Task<IActionResult> GetPendingBookings()
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var pendingBookings = await _context.Bookings
                .Include(b => b.Condo)
                .Where(b => b.Condo.OwnerId == ownerId && b.Status == "PendingApproval")
                .Select(b => new BookingResponseDto
                {
                    Id = b.Id,
                    FullName = b.FullName,
                    Email = b.Email,
                    Contact = b.Contact,
                    GuestCount = b.GuestCount,
                    StartDateTime = b.StartDateTime,
                    EndDateTime = b.EndDateTime,
                    Status = b.Status,
                    CreatedAt = b.CreatedAt,
                    Notes = b.Notes,
                    PaymentImageUrl = b.PaymentImageUrl,
                    Condo = new CondoSummaryDto
                    {
                        Id = b.Condo.Id,
                        Name = b.Condo.Name,
                        Location = b.Condo.Location,
                        ImageUrl = b.Condo.ImageUrl
                    }
                })
                .ToListAsync();

            return Ok(pendingBookings);
        }

        // Owner: Approve or reject a booking
        [HttpPost("{bookingId}/approve")]
        [Authorize(Roles = "OWNER")]
        public async Task<IActionResult> ApproveBooking(int bookingId, [FromBody] ApprovalDto approvalDto)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var booking = await _context.Bookings
                .Include(b => b.Condo)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.Condo.OwnerId == ownerId);

            if (booking == null)
                return NotFound("Booking not found or you don't have permission to approve it.");

            if (booking.Status != "PendingApproval")
                return BadRequest("Booking is not pending approval.");

            if (approvalDto.IsApproved)
            {
                // Generate QR code for check-in
                var qrCodeData = GenerateQrCodeData(booking);
                booking.QrCodeData = qrCodeData;
                booking.Status = "Approved";
                booking.ApprovedAt = DateTime.UtcNow;
                booking.ApprovedBy = ownerId;
                
                // Update condo status to occupied
                booking.Condo.Status = "Occupied";
                booking.Condo.LastUpdated = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();

                // Generate QR code image and send approval email (fire-and-forget - don't block response)
                // Run in background task so API responds quickly even if email is slow
                _ = Task.Run(async () =>
                {
                    try
                    {
                        System.Console.WriteLine($"[BACKGROUND] Generating QR code and sending approval email for booking ID: {booking.Id}");
                        System.Console.WriteLine($"[BACKGROUND] Guest email: {booking.Email}");
                        System.Console.WriteLine($"[BACKGROUND] Guest name: {booking.FullName}");
                        
                        var qrCodeBase64 = _qrCodeService.GenerateQrCodeBase64(qrCodeData);
                        System.Console.WriteLine($"[BACKGROUND] QR code generated successfully. Length: {qrCodeBase64?.Length ?? 0}");
                        
                        await _emailService.SendBookingApprovalEmailAsync(
                            booking.Email,
                            booking.FullName,
                            booking.Condo.Name,
                            booking.Condo.Location,
                            qrCodeBase64,
                            booking.Id,
                            booking.GuestCount,
                            booking.StartDateTime,
                            booking.EndDateTime,
                            booking.Notes
                        );
                        
                        System.Console.WriteLine($"[BACKGROUND] Email service call completed for booking ID: {booking.Id}");
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[BACKGROUND] ERROR in approval email process: {ex.Message}");
                        System.Console.WriteLine($"[BACKGROUND] Error type: {ex.GetType().Name}");
                        System.Console.WriteLine($"[BACKGROUND] Stack trace: {ex.StackTrace}");
                        if (ex.InnerException != null)
                        {
                            System.Console.WriteLine($"[BACKGROUND] Inner exception: {ex.InnerException.Message}");
                        }
                        // Don't fail the approval if email fails
                    }
                });
                
                System.Console.WriteLine($"Booking approved successfully. Email sending in background for booking ID: {booking.Id}");
            }
            else
            {
                booking.Status = "Rejected";
                booking.RejectionReason = approvalDto.RejectionReason;
                
                await _context.SaveChangesAsync();

                // Send rejection email (fire-and-forget - don't block response)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        System.Console.WriteLine($"[BACKGROUND] Sending rejection email for booking ID: {booking.Id}");
                        await _emailService.SendBookingRejectionEmailAsync(
                            booking.Email,
                            booking.FullName,
                            booking.Condo.Name,
                            approvalDto.RejectionReason
                        );
                        System.Console.WriteLine($"[BACKGROUND] Rejection email sent for booking ID: {booking.Id}");
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[BACKGROUND] Error sending rejection email: {ex.Message}");
                        System.Console.WriteLine($"[BACKGROUND] Stack trace: {ex.StackTrace}");
                        // Don't fail the rejection if email fails
                    }
                });
            }

            return Ok(new { 
                message = approvalDto.IsApproved ? "Booking approved successfully." : "Booking rejected.",
                qrCodeData = approvalDto.IsApproved ? booking.QrCodeData : null
            });
        }

        // Front Desk: Get all bookings for their assigned condo
        [HttpGet("frontdesk")]
        [Authorize(Roles = "FRONTDESK")]
        public async Task<IActionResult> GetFrontDeskBookings()
        {
            var frontDeskId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var bookings = await _context.Bookings
                .Include(b => b.Condo)
                .Where(b => b.Condo.FrontDeskId == frontDeskId)
                .Select(b => new BookingResponseDto
                {
                    Id = b.Id,
                    FullName = b.FullName,
                    Email = b.Email,
                    Contact = b.Contact,
                    GuestCount = b.GuestCount,
                    StartDateTime = b.StartDateTime,
                    EndDateTime = b.EndDateTime,
                    Status = b.Status,
                    QrCodeData = b.QrCodeData,
                    CreatedAt = b.CreatedAt,
                    ApprovedAt = b.ApprovedAt,
                    CheckedInAt = b.CheckedInAt,
                    CheckedOutAt = b.CheckedOutAt,
                    Notes = b.Notes,
                    Condo = new CondoSummaryDto
                    {
                        Id = b.Condo.Id,
                        Name = b.Condo.Name,
                        Location = b.Condo.Location,
                        ImageUrl = b.Condo.ImageUrl
                    }
                })
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return Ok(bookings);
        }

        // Front Desk: Check-in guest using QR code
        [HttpPost("{bookingId}/checkin")]
        [Authorize(Roles = "FRONTDESK")]
        public async Task<IActionResult> CheckInGuest(int bookingId, [FromBody] CheckInDto checkInDto)
        {
            var frontDeskId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var booking = await _context.Bookings
                .Include(b => b.Condo)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.Condo.FrontDeskId == frontDeskId);

            if (booking == null)
                return NotFound("Booking not found or you don't have permission to check in this guest.");

            if (booking.Status != "Approved")
                return BadRequest("Booking must be approved before check-in.");

            if (booking.QrCodeData != checkInDto.QrCodeData)
                return BadRequest("Invalid QR code.");

            if (DateTime.UtcNow < booking.StartDateTime)
                return BadRequest("Check-in is not allowed before the scheduled start time.");

            booking.Status = "CheckedIn";
            booking.CheckedInAt = DateTime.UtcNow;
            booking.Condo.Status = "Occupied";
            booking.Condo.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Guest checked in successfully." });
        }

        // Front Desk: Check-out guest
        [HttpPost("{bookingId}/checkout")]
        [Authorize(Roles = "FRONTDESK")]
        public async Task<IActionResult> CheckOutGuest(int bookingId)
        {
            var frontDeskId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var booking = await _context.Bookings
                .Include(b => b.Condo)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.Condo.FrontDeskId == frontDeskId);

            if (booking == null)
                return NotFound("Booking not found or you don't have permission to check out this guest.");

            if (booking.Status != "CheckedIn")
                return BadRequest("Guest must be checked in before check-out.");

            booking.Status = "CheckedOut";
            booking.CheckedOutAt = DateTime.UtcNow;
            booking.Condo.Status = "Available";
            booking.Condo.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Guest checked out successfully." });
        }

        // Owner: Get all bookings for their condos
        [HttpGet("owner")]
        [Authorize(Roles = "OWNER")]
        public async Task<IActionResult> GetOwnerBookings()
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var bookings = await _context.Bookings
                .Include(b => b.Condo)
                .Where(b => b.Condo.OwnerId == ownerId)
                .Select(b => new BookingResponseDto
                {
                    Id = b.Id,
                    FullName = b.FullName,
                    Email = b.Email,
                    Contact = b.Contact,
                    GuestCount = b.GuestCount,
                    StartDateTime = b.StartDateTime,
                    EndDateTime = b.EndDateTime,
                    Status = b.Status,
                    QrCodeData = b.QrCodeData,
                    CreatedAt = b.CreatedAt,
                    ApprovedAt = b.ApprovedAt,
                    CheckedInAt = b.CheckedInAt,
                    CheckedOutAt = b.CheckedOutAt,
                    Notes = b.Notes,
                    PaymentImageUrl = b.PaymentImageUrl,
                    Condo = new CondoSummaryDto
                    {
                        Id = b.Condo.Id,
                        Name = b.Condo.Name,
                        Location = b.Condo.Location,
                        ImageUrl = b.Condo.ImageUrl
                    }
                })
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return Ok(bookings);
        }

        private string GenerateQrCodeData(Booking booking)
        {
            // Generate unique QR code data for this booking
            var qrData = $"CONDO_{booking.CondoId}_BOOKING_{booking.Id}_{Guid.NewGuid()}";
            return qrData;
        }
    }

    public class ApprovalDto
    {
        public bool IsApproved { get; set; }
        public string? RejectionReason { get; set; }
    }

    public class CheckInDto
    {
        public string QrCodeData { get; set; } = string.Empty;
    }
}
