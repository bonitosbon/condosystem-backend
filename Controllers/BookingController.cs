using CondoSystem.Data;
using CondoSystem.DTO.Booking;
using CondoSystem.Models;
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

        public BookingController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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

                var booking = new Booking
                {
                    FullName = dto.FullName,
                    Email = dto.Email,
                    Contact = dto.Contact,
                    GuestCount = dto.GuestCount,
                    StartDateTime = dto.StartDateTime,
                    EndDateTime = dto.EndDateTime,
                    Notes = dto.Notes,
                    PaymentImageUrl = dto.PaymentImageUrl,
                    CondoId = dto.CondoId,
                    Status = "PendingApproval"
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Booking request created successfully. Awaiting owner approval.", bookingId = booking.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating the booking.", error = ex.Message });
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
            }
            else
            {
                booking.Status = "Rejected";
                booking.RejectionReason = approvalDto.RejectionReason;
            }

            await _context.SaveChangesAsync();

            // TODO: Send email notification to guest with approval/rejection and QR code if approved

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
