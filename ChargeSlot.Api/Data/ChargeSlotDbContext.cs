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

        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<IdentityRolePermission> RolePermissions => Set<IdentityRolePermission>();
        public DbSet<Driver> Drivers => Set<Driver>();
        public DbSet<Owner> Owners => Set<Owner>();
        public DbSet<UserOtp> UserOtps => Set<UserOtp>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<ChargingStation> ChargingStations => Set<ChargingStation>();
        public DbSet<StationImage> StationImages => Set<StationImage>();
        public DbSet<StationOperatingHours> StationOperatingHours => Set<StationOperatingHours>();
        public DbSet<ExtraService> ExtraServices => Set<ExtraService>();
        public DbSet<ChargingSlot> ChargingSlots => Set<ChargingSlot>();
        public DbSet<SlotPricing> SlotPricings => Set<SlotPricing>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<BookingExtraService> BookingExtraServices => Set<BookingExtraService>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<ChargingSession> ChargingSessions => Set<ChargingSession>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<Rating> Ratings => Set<Rating>();
        public DbSet<Dispute> Disputes => Set<Dispute>();
        public DbSet<DisputeEvidence> DisputeEvidences => Set<DisputeEvidence>();
        public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
        public DbSet<PayoutRequest> PayoutRequests => Set<PayoutRequest>();
        public DbSet<Wallet> Wallets => Set<Wallet>();
        public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();
        public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChargeSlotDbContext).Assembly);
            SeedRoles(modelBuilder);
            SeedSystemWallets(modelBuilder);
        }

        private static void SeedRoles(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IdentityRole<int>>().HasData(
                new IdentityRole<int> { Id = 1, Name = "Admin", NormalizedName = "ADMIN", ConcurrencyStamp = "a1" },
                new IdentityRole<int> { Id = 2, Name = "Owner", NormalizedName = "OWNER", ConcurrencyStamp = "a2" },
                new IdentityRole<int> { Id = 3, Name = "Driver", NormalizedName = "DRIVER", ConcurrencyStamp = "a3" }
            );
        }

        private static void SeedSystemWallets(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Wallet>().HasData(
                new Wallet { Id = 1, WalletType = Enums.WalletType.System, SystemCode = "ESCROW", AvailableBalance = 0, FrozenBalance = 0, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Wallet { Id = 2, WalletType = Enums.WalletType.System, SystemCode = "PLATFORM_REVENUE", AvailableBalance = 0, FrozenBalance = 0, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Wallet { Id = 3, WalletType = Enums.WalletType.System, SystemCode = "CLEARING", AvailableBalance = 0, FrozenBalance = 0, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            );
        }
    }
}
