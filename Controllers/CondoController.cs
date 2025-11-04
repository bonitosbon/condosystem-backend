using CondoSystem.Data;
using CondoSystem.DTO.Condo;
using CondoSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Security.Claims;

namespace CondoSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CondoController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CondoController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost("create")]
        [Authorize(Roles = "OWNER")] // only Owner can create condos
        public async Task<IActionResult> CreateCondo([FromBody] CreateCondoDto dto)
        {
            // Check if a condo with the same name and location already exists
            if (await _context.Condos.AnyAsync(c => c.Name == dto.Name && c.Location == dto.Location))
                return BadRequest(new { message = "Condo already exists at this location." });

            // Check if front desk username is already in use
            var existingUser = await _userManager.FindByNameAsync(dto.FrontDeskUsername);
            if (existingUser != null)
                return BadRequest(new { message = "Front desk username is already taken." });

            // Get the logged-in owner's ID
            var ownerId = _userManager.GetUserId(User);

            // Create the Front Desk user
            // Generate email from username for Identity requirement (uses @condosystem.local domain)
            string frontDeskEmail = dto.FrontDeskUsername.Contains("@") 
                ? dto.FrontDeskUsername 
                : $"{dto.FrontDeskUsername}@condosystem.local";

            var frontDeskUser = new ApplicationUser
            {
                UserName = dto.FrontDeskUsername,
                Email = frontDeskEmail,
                FullName = $"Front Desk - {dto.Name}"
            };

            var result = await _userManager.CreateAsync(frontDeskUser, dto.FrontDeskPassword);
            if (!result.Succeeded)
                return BadRequest(new { message = "Failed to create front desk user", errors = result.Errors });

            // Assign role FRONTDESK
            await _userManager.AddToRoleAsync(frontDeskUser, "FRONTDESK");

            // Create Condo with linked FrontDesk
            var condo = new Condo
            {
                Name = dto.Name,
                Location = dto.Location,
                Description = dto.Description,
                Amenities = dto.Amenities,
                MaxGuests = dto.MaxGuests,
                PricePerNight = dto.PricePerNight,
                ImageUrl = dto.ImageUrl,
                OwnerId = ownerId,
                FrontDeskId = frontDeskUser.Id
            };

            _context.Condos.Add(condo);
            await _context.SaveChangesAsync();

            // Generate booking link after we have the condo ID
            var request = HttpContext.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";
            condo.BookingLink = $"{baseUrl}/booking.html?condoId={condo.Id}";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Condo and Front Desk created successfully", condo });
        }

        // Owner: Get all their condos
        [HttpGet("owner")]
        [Authorize(Roles = "OWNER")]
        public async Task<IActionResult> GetOwnerCondos()
        {
            var ownerId = _userManager.GetUserId(User);
            
            var condos = await _context.Condos
                .Where(c => c.OwnerId == ownerId)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Location,
                    c.Description,
                    c.Amenities,
                    c.MaxGuests,
                    c.PricePerNight,
                    c.Status,
                    c.ImageUrl,
                    c.BookingLink,
                    c.CreatedAt,
                    c.LastUpdated,
                    FrontDesk = new
                    {
                        c.FrontDeskUser.Email,
                        c.FrontDeskUser.FullName
                    },
                    TotalBookings = c.Bookings.Count(),
                    PendingBookings = c.Bookings.Count(b => b.Status == "PendingApproval"),
                    ActiveBookings = c.Bookings.Count(b => b.Status == "Approved" || b.Status == "CheckedIn")
                })
                .ToListAsync();

            return Ok(condos);
        }

        // Front Desk: Get their assigned condo details
        [HttpGet("frontdesk")]
        [Authorize(Roles = "FRONTDESK")]
        public async Task<IActionResult> GetFrontDeskCondo()
        {
            var frontDeskId = _userManager.GetUserId(User);
            
            var condo = await _context.Condos
                .Where(c => c.FrontDeskId == frontDeskId)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Location,
                    c.Description,
                    c.Amenities,
                    c.MaxGuests,
                    c.PricePerNight,
                    c.Status,
                    c.ImageUrl,
                    c.CreatedAt,
                    Owner = new
                    {
                        c.Owner.Email,
                        c.Owner.FullName
                    },
                    TodayBookings = c.Bookings.Count(b => 
                        b.StartDateTime.Date <= DateTime.UtcNow.Date && 
                        b.EndDateTime.Date >= DateTime.UtcNow.Date),
                    PendingCheckIns = c.Bookings.Count(b => 
                        b.Status == "Approved" && 
                        b.StartDateTime.Date <= DateTime.UtcNow.Date),
                    ActiveGuests = c.Bookings.Count(b => b.Status == "CheckedIn")
                })
                .FirstOrDefaultAsync();

            if (condo == null)
                return NotFound("No condo assigned to this front desk user.");

            return Ok(condo);
        }

        // Public: Get condo details for booking
        [HttpGet("public/{condoId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicCondo(int condoId)
        {
            var condo = await _context.Condos
                .Where(c => c.Id == condoId)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Location,
                    c.Description,
                    c.Amenities,
                    c.MaxGuests,
                    c.PricePerNight,
                    c.Status,
                    c.ImageUrl,
                    c.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (condo == null)
                return NotFound("Condo not found.");

            return Ok(condo);
        }

        // Owner: Update condo details
        [HttpPut("{condoId}")]
        [Authorize(Roles = "OWNER")]
        public async Task<IActionResult> UpdateCondo(int condoId, [FromBody] UpdateCondoDto dto)
        {
            var ownerId = _userManager.GetUserId(User);
            
            var condo = await _context.Condos
                .FirstOrDefaultAsync(c => c.Id == condoId && c.OwnerId == ownerId);

            if (condo == null)
                return NotFound("Condo not found or you don't have permission to update it.");

            // Update only allowed fields
            if (!string.IsNullOrEmpty(dto.Name)) condo.Name = dto.Name;
            if (!string.IsNullOrEmpty(dto.Location)) condo.Location = dto.Location;
            if (!string.IsNullOrEmpty(dto.Description)) condo.Description = dto.Description;
            if (!string.IsNullOrEmpty(dto.Amenities)) condo.Amenities = dto.Amenities;
            if (dto.MaxGuests.HasValue && dto.MaxGuests > 0) condo.MaxGuests = dto.MaxGuests.Value;
            if (dto.PricePerNight.HasValue && dto.PricePerNight >= 0) condo.PricePerNight = dto.PricePerNight.Value;
            if (!string.IsNullOrEmpty(dto.ImageUrl)) condo.ImageUrl = dto.ImageUrl;
            
            // Ensure booking link is set (generate if missing)
            if (string.IsNullOrEmpty(condo.BookingLink))
            {
                var request = HttpContext.Request;
                var baseUrl = $"{request.Scheme}://{request.Host}";
                condo.BookingLink = $"{baseUrl}/booking.html?condoId={condo.Id}";
            }
            
            condo.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Condo updated successfully", condo });
        }

        // Owner: Change condo status
        [HttpPatch("{condoId}/status")]
        [Authorize(Roles = "OWNER")]
        public async Task<IActionResult> UpdateCondoStatus(int condoId, [FromBody] UpdateStatusDto dto)
        {
            var ownerId = _userManager.GetUserId(User);
            
            var condo = await _context.Condos
                .FirstOrDefaultAsync(c => c.Id == condoId && c.OwnerId == ownerId);

            if (condo == null)
                return NotFound("Condo not found or you don't have permission to update it.");

            // Validate status
            var validStatuses = new[] { "Available", "Maintenance", "Unavailable" };
            if (!validStatuses.Contains(dto.Status))
                return BadRequest("Invalid status. Must be Available, Maintenance, or Unavailable.");

            condo.Status = dto.Status;
            condo.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Condo status updated successfully", status = condo.Status });
        }

        // Owner: Delete condo
        [HttpDelete("{condoId}")]
        [Authorize(Roles = "OWNER")]
        public async Task<IActionResult> DeleteCondo(int condoId)
        {
            var ownerId = _userManager.GetUserId(User);
            
            var condo = await _context.Condos
                .Include(c => c.FrontDeskUser)
                .FirstOrDefaultAsync(c => c.Id == condoId && c.OwnerId == ownerId);

            if (condo == null)
                return NotFound("Condo not found or you don't have permission to delete it.");

            // Delete the front desk user associated with this condo
            if (condo.FrontDeskUser != null)
            {
                await _userManager.DeleteAsync(condo.FrontDeskUser);
            }

            // Delete the condo
            _context.Condos.Remove(condo);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Condo deleted successfully" });
        }
    }

    public class UpdateCondoDto
    {
        public string? Name { get; set; }
        public string? Location { get; set; }
        public string? Description { get; set; }
        public string? Amenities { get; set; }
        public int? MaxGuests { get; set; }
        public decimal? PricePerNight { get; set; }
        public string? ImageUrl { get; set; }
        public string? Status { get; set; } // Status is handled separately via PATCH endpoint
    }

    public class UpdateStatusDto
    {
        public string Status { get; set; } = string.Empty;
    }
}

