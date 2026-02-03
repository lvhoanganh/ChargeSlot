using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Data
{
    public class ChargeSlotDbContext
        : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        public ChargeSlotDbContext(DbContextOptions<ChargeSlotDbContext> options)
            : base(options)
        {
        }

        // ===== Domain DbSet (KHÔNG còn User / Role tự viết) =====
        public DbSet<ChargingStation> ChargingStations { get; set; }
        public DbSet<ChargingSlot> ChargingSlots { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<UserOtp> UserOtps { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>()
                .HasIndex(u => u.PhoneNumber)
                .IsUnique()
                .HasFilter("[PhoneNumber] IS NOT NULL");

            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(ChargeSlotDbContext).Assembly
            );
        }
    }
}
