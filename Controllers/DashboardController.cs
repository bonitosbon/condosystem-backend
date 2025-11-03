using CondoSystem.Data;
using CondoSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CondoSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Owner Dashboard: Get overview of their condos and bookings
        [HttpGet("owner")]
        [Authorize(Roles = "OWNER")]
        public async Task<IActionResult> GetOwnerDashboard()
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var dashboard = await _context.Condos
                .Where(c => c.OwnerId == ownerId)
                .Select(c => new
                {
                    Condo = new
                    {
                        c.Id,
                        c.Name,
                        c.Location,
                        c.Status,
                        c.MaxGuests,
                        c.PricePerNight,
                        c.ImageUrl,
                        c.CreatedAt
                    },
                    Bookings = c.Bookings.Select(b => new
                    {
                        b.Id,
                        b.Status,
                        b.StartDateTime,
                        b.EndDateTime,
                        b.GuestCount,
                        b.FullName
                    }).ToList(),
                    TotalBookings = c.Bookings.Count(),
                    PendingBookings = c.Bookings.Count(b => b.Status == "PendingApproval"),
                    ActiveBookings = c.Bookings.Count(b => b.Status == "Approved" || b.Status == "CheckedIn"),
                    Revenue = c.Bookings
                        .Where(b => b.Status == "CheckedOut")
                        .Sum(b => (b.EndDateTime - b.StartDateTime).Days * c.PricePerNight)
                })
                .ToListAsync();

            return Ok(dashboard);
        }

        // Front Desk Dashboard: Get overview of their assigned condo
        [HttpGet("frontdesk")]
        [Authorize(Roles = "FRONTDESK")]
        public async Task<IActionResult> GetFrontDeskDashboard()
        {
            var frontDeskId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var dashboard = await _context.Condos
                .Where(c => c.FrontDeskId == frontDeskId)
                .Select(c => new
                {
                    Condo = new
                    {
                        c.Id,
                        c.Name,
                        c.Location,
                        c.Status,
                        c.MaxGuests,
                        c.ImageUrl
                    },
                    TodayBookings = c.Bookings.Count(b => 
                        b.StartDateTime.Date <= DateTime.UtcNow.Date && 
                        b.EndDateTime.Date >= DateTime.UtcNow.Date),
                    PendingCheckIns = c.Bookings.Count(b => 
                        b.Status == "Approved" && 
                        b.StartDateTime.Date <= DateTime.UtcNow.Date),
                    ActiveGuests = c.Bookings.Count(b => b.Status == "CheckedIn"),
                    RecentBookings = c.Bookings
                        .Where(b => b.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                        .OrderByDescending(b => b.CreatedAt)
                        .Take(5)
                        .Select(b => new
                        {
                            b.Id,
                            b.FullName,
                            b.Status,
                            b.StartDateTime,
                            b.EndDateTime,
                            b.GuestCount
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (dashboard == null)
                return NotFound("No condo assigned to this front desk user.");

            return Ok(dashboard);
        }

        // Get condo availability for a specific date range
        [HttpGet("availability/{condoId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCondoAvailability(int condoId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            var condo = await _context.Condos
                .FirstOrDefaultAsync(c => c.Id == condoId);

            if (condo == null)
                return NotFound("Condo not found.");

            var conflictingBookings = await _context.Bookings
                .Where(b => b.CondoId == condoId && 
                           b.Status != "Rejected" && b.Status != "Cancelled" &&
                           ((b.StartDateTime <= startDate && b.EndDateTime > startDate) ||
                            (b.StartDateTime < endDate && b.EndDateTime >= endDate) ||
                            (b.StartDateTime >= startDate && b.EndDateTime <= endDate)))
                .Select(b => new
                {
                    b.StartDateTime,
                    b.EndDateTime,
                    b.Status
                })
                .ToListAsync();

            var isAvailable = !conflictingBookings.Any();
            var availableDates = new List<DateTime>();
            
            // Generate available dates if condo is available
            if (isAvailable)
            {
                for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
                {
                    availableDates.Add(date);
                }
            }

            return Ok(new
            {
                CondoId = condoId,
                CondoName = condo.Name,
                IsAvailable = isAvailable,
                AvailableDates = availableDates,
                ConflictingBookings = conflictingBookings,
                MaxGuests = condo.MaxGuests,
                PricePerNight = condo.PricePerNight
            });
        }

        // Get statistics for owners
        [HttpGet("owner/stats")]
        [Authorize(Roles = "OWNER")]
        public async Task<IActionResult> GetOwnerStats()
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var stats = await _context.Condos
                .Where(c => c.OwnerId == ownerId)
                .SelectMany(c => c.Bookings)
                .GroupBy(b => b.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalCondos = await _context.Condos.CountAsync(c => c.OwnerId == ownerId);
            var totalBookings = await _context.Condos
                .Where(c => c.OwnerId == ownerId)
                .SelectMany(c => c.Bookings)
                .CountAsync();

            var monthlyRevenue = await _context.Condos
                .Where(c => c.OwnerId == ownerId)
                .SelectMany(c => c.Bookings)
                .Where(b => b.Status == "CheckedOut" && 
                           b.CheckedOutAt >= DateTime.UtcNow.AddMonths(-1))
                .Join(_context.Condos, b => b.CondoId, c => c.Id, (b, c) => new { b, c })
                .SumAsync(x => (x.b.EndDateTime - x.b.StartDateTime).Days * x.c.PricePerNight);

            return Ok(new
            {
                TotalCondos = totalCondos,
                TotalBookings = totalBookings,
                MonthlyRevenue = monthlyRevenue,
                StatusBreakdown = stats,
                OccupancyRate = totalCondos > 0 ? 
                    (double)await _context.Condos.CountAsync(c => c.OwnerId == ownerId && c.Status == "Occupied") / totalCondos * 100 : 0
            });
        }
    }
}
