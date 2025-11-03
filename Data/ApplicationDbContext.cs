using CondoSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CondoSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // Add DbSets for your entities
        public DbSet<Condo> Condos { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<FrontDeskProfile> FrontDesks { get; set; }
        public DbSet<OwnerProfile> Owners { get; set; }

        // ✅ Override OnModelCreating to configure relationships
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Fix multiple cascade paths:
            builder.Entity<FrontDeskProfile>()
                .HasOne(fd => fd.Condo)
                .WithMany()
                .HasForeignKey(fd => fd.CondoId)
                .OnDelete(DeleteBehavior.Restrict); // 👈 prevent cascade delete loop
        }
    }
}
