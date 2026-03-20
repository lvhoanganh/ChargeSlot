using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Data
{
    /// <summary>
    /// Seed demo data for easy local testing.
    /// Safe to run multiple times (idempotent).
    /// </summary>
    public static class DataSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var provider = scope.ServiceProvider;

            var context = provider.GetRequiredService<ChargeSlotDbContext>();
            var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<int>>>();

            // ============================
            // 1. ROLES
            // ============================
            foreach (var role in RoleConstants.DbRoles)
            {
                if (!await roleManager.Roles.AnyAsync(r => r.Name == role))
                {
                    await roleManager.CreateAsync(new IdentityRole<int>
                    {
                        Name = role,
                        NormalizedName = role.ToUpperInvariant()
                    });
                }
            }

            const string defaultPassword = "Password1!";

            // ============================
            // 2. ADMIN
            // ============================
            var adminPhone = "0123456789";
            var adminPassword = "Admin123!";
            var adminUser = await userManager.FindByNameAsync(adminPhone);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminPhone,
                    PhoneNumber = adminPhone,
                    FullName = "System Administrator",
                    IsPhoneVerified = true,
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow
                };
                var r = await userManager.CreateAsync(adminUser, adminPassword);
                if (r.Succeeded) await userManager.AddToRoleAsync(adminUser, RoleConstants.Admin);
            }
            else if (!await userManager.IsInRoleAsync(adminUser, RoleConstants.Admin))
            {
                await userManager.AddToRoleAsync(adminUser, RoleConstants.Admin);
            }

            // ============================
            // 3. DRIVERS (5 accounts, each with 500K wallet)
            // ============================
            var driverUsers = new List<ApplicationUser>();
            for (int i = 1; i <= 5; i++)
            {
                var phone = $"09000000{i:00}";
                var user = await userManager.FindByNameAsync(phone);

                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = phone,
                        PhoneNumber = phone,
                        FullName = $"Nguyễn Văn Driver {i}",
                        IsPhoneVerified = true,
                        Status = "ACTIVE",
                        CreatedAt = DateTime.UtcNow
                    };
                    var r = await userManager.CreateAsync(user, defaultPassword);
                    if (r.Succeeded) await userManager.AddToRoleAsync(user, RoleConstants.Driver);
                }

                driverUsers.Add(user);

                if (!await context.Driver.AnyAsync(d => d.UserId == user.Id))
                {
                    context.Driver.Add(new Driver
                    {
                        UserId = user.Id,
                        VehicleType = i <= 3 ? "Xe máy điện" : "Ô tô điện",
                        LicensePlate = $"29-A1.{100 + i:000}",
                        LicenseNumber = $"DRV-{i:000}",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // Driver wallet
                if (!await context.Wallets.AnyAsync(w => w.UserId == user.Id && w.WalletType == WalletType.Driver))
                {
                    context.Wallets.Add(new Wallet
                    {
                        UserId = user.Id,
                        WalletType = WalletType.Driver,
                        AvailableBalance = 500000, // 500K để test
                        FrozenBalance = 0,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            // ============================
            // 4. OWNERS (3 accounts)
            // ============================
            var ownerUsers = new List<ApplicationUser>();
            for (int i = 1; i <= 3; i++)
            {
                var phone = $"09100000{i:00}";
                var user = await userManager.FindByNameAsync(phone);

                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = phone,
                        PhoneNumber = phone,
                        FullName = $"Trần Thị Owner {i}",
                        IsPhoneVerified = true,
                        Status = "ACTIVE",
                        CreatedAt = DateTime.UtcNow
                    };
                    var r = await userManager.CreateAsync(user, defaultPassword);
                    if (r.Succeeded) await userManager.AddToRoleAsync(user, RoleConstants.Owner);
                }

                ownerUsers.Add(user);

                if (!await context.Owner.AnyAsync(o => o.UserId == user.Id))
                {
                    context.Owner.Add(new Owner
                    {
                        UserId = user.Id,
                        BusinessName = $"Công ty Sạc {i}",
                        TaxCode = $"MST-00{i}",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // Owner wallet
                if (!await context.Wallets.AnyAsync(w => w.UserId == user.Id && w.WalletType == WalletType.Owner))
                {
                    context.Wallets.Add(new Wallet
                    {
                        UserId = user.Id,
                        WalletType = WalletType.Owner,
                        AvailableBalance = 0,
                        FrozenBalance = 0,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await context.SaveChangesAsync();

            // ============================
            // 5. SYSTEM WALLETS (ESCROW, PLATFORM_REVENUE, CLEARING)
            // ============================
            var systemWallets = new[] { "ESCROW", "PLATFORM_REVENUE", "CLEARING" };
            foreach (var code in systemWallets)
            {
                if (!await context.Wallets.AnyAsync(w => w.SystemCode == code))
                {
                    context.Wallets.Add(new Wallet
                    {
                        WalletType = WalletType.System,
                        SystemCode = code,
                        AvailableBalance = 0,
                        FrozenBalance = 0,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            await context.SaveChangesAsync();

            // ============================
            // 6. CHARGING STATIONS (2 stations, Approved + Active)
            // ============================
            if (!await context.ChargingStations.AnyAsync())
            {
                var owner1 = ownerUsers[0];
                var owner2 = ownerUsers.Count > 1 ? ownerUsers[1] : ownerUsers[0];

                var station1 = new ChargingStation
                {
                    OwnerUserId = owner1.Id,
                    Name = "Trạm Sạc Cầu Giấy",
                    Address = "120 Xuân Thủy, Cầu Giấy, Hà Nội",
                    Description = "Trạm sạc xe điện gần Đại học Quốc gia, mở 24/7. Có wifi miễn phí, nước uống.",
                    Latitude = 21.0383m,
                    Longitude = 105.7830m,
                    ApprovalStatus = ApprovalStatus.Approved,
                    OperationalStatus = OperationalStatus.Active,
                    SubmittedAt = DateTime.UtcNow.AddDays(-5),
                    ReviewedAt = DateTime.UtcNow.AddDays(-4),
                    ReviewedByUserId = adminUser.Id,
                    AdminNote = "Đạt yêu cầu, phê duyệt.",
                    CreatedAt = DateTime.UtcNow.AddDays(-7),
                    ChargingSlots = new List<ChargingSlot>
                    {
                        new ChargingSlot
                        {
                            SlotName = "Trụ A1",

                            PositionX = 10, PositionY = 20,
                            Status = SlotStatus.Active,
                            QrCodeToken = Guid.NewGuid().ToString("N").ToUpper(),
                            CreatedAt = DateTime.UtcNow.AddDays(-7)
                        },
                        new ChargingSlot
                        {
                            SlotName = "Trụ A2",

                            PositionX = 30, PositionY = 20,
                            Status = SlotStatus.Active,
                            QrCodeToken = Guid.NewGuid().ToString("N").ToUpper(),
                            CreatedAt = DateTime.UtcNow.AddDays(-7)
                        },
                        new ChargingSlot
                        {
                            SlotName = "Trụ B1",
                            PositionX = 10, PositionY = 50,
                            Status = SlotStatus.Active,
                            QrCodeToken = Guid.NewGuid().ToString("N").ToUpper(),
                            CreatedAt = DateTime.UtcNow.AddDays(-7)
                        }
                    },
                    Images = new List<StationImage>
                    {
                        new StationImage { ImageUrl = "https://placehold.co/800x400?text=Tram+Sac+Cau+Giay" }
                    },
                    OperatingHours = Enumerable.Range(0, 7).Select(day => new StationOperatingHours
                    {
                        DayOfWeek = (byte)day,
                        IsClosed = false,
                        OpenTime = new TimeOnly(0, 0),   // 24/7
                        CloseTime = new TimeOnly(23, 59)
                    }).ToList(),
                    ExtraServices = new List<ExtraService>
                    {
                        new ExtraService
                        {
                            ServiceName = "Cho thuê củ sạc",
                            Description = "Cho thuê củ sạc nhanh USB-C",
                            Price = 5000,
                            TotalStock = 10,
                            IsActive = true
                        },
                        new ExtraService
                        {
                            ServiceName = "Nước uống",
                            Description = "Nước lọc miễn phí",
                            Price = 0,
                            IsActive = true
                        }
                    }
                };

                var station2 = new ChargingStation
                {
                    OwnerUserId = owner2.Id,
                    Name = "Trạm Sạc Thanh Xuân",
                    Address = "196 Nguyễn Trãi, Thanh Xuân, Hà Nội",
                    Description = "Trạm sạc lớn gần Royal City, có bãi đỗ xe rộng rãi.",
                    Latitude = 20.9946m,
                    Longitude = 105.8053m,
                    ApprovalStatus = ApprovalStatus.Approved,
                    OperationalStatus = OperationalStatus.Active,
                    SubmittedAt = DateTime.UtcNow.AddDays(-3),
                    ReviewedAt = DateTime.UtcNow.AddDays(-2),
                    ReviewedByUserId = adminUser.Id,
                    AdminNote = "OK.",
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    ChargingSlots = new List<ChargingSlot>
                    {
                        new ChargingSlot
                        {
                            SlotName = "Slot 1",
                            PositionX = 5, PositionY = 10,
                            Status = SlotStatus.Active,
                            QrCodeToken = Guid.NewGuid().ToString("N").ToUpper(),
                            CreatedAt = DateTime.UtcNow.AddDays(-5)
                        },
                        new ChargingSlot
                        {
                            SlotName = "Slot 2",
                            PositionX = 25, PositionY = 10,
                            Status = SlotStatus.Active,
                            QrCodeToken = Guid.NewGuid().ToString("N").ToUpper(),
                            CreatedAt = DateTime.UtcNow.AddDays(-5)
                        },
                        new ChargingSlot
                        {
                            SlotName = "Slot VIP",
                            PositionX = 50, PositionY = 10,
                            Status = SlotStatus.Active,
                            QrCodeToken = Guid.NewGuid().ToString("N").ToUpper(),
                            CreatedAt = DateTime.UtcNow.AddDays(-5)
                        },
                        new ChargingSlot
                        {
                            SlotName = "Slot bảo trì",
                            PositionX = 45, PositionY = 30,
                            Status = SlotStatus.Maintenance,
                            QrCodeToken = Guid.NewGuid().ToString("N").ToUpper(),
                            CreatedAt = DateTime.UtcNow.AddDays(-5)
                        }
                    },
                    Images = new List<StationImage>
                    {
                        new StationImage { ImageUrl = "https://placehold.co/800x400?text=Tram+Sac+Thanh+Xuan" }
                    },
                    OperatingHours = Enumerable.Range(0, 7).Select(day => new StationOperatingHours
                    {
                        DayOfWeek = (byte)day,
                        IsClosed = day == 0, // Chủ nhật nghỉ
                        OpenTime = new TimeOnly(6, 0),
                        CloseTime = new TimeOnly(22, 0)
                    }).ToList(),
                    ExtraServices = new List<ExtraService>
                    {
                        new ExtraService
                        {
                            ServiceName = "Rửa xe",
                            Description = "Rửa xe máy điện tại chỗ",
                            Price = 20000,
                            IsActive = true
                        }
                    }
                };

                context.ChargingStations.AddRange(station1, station2);
                await context.SaveChangesAsync();

                // ============================
                // 7. SLOT PRICING (giá theo khung giờ)
                // ============================
                // Station 1 - Trụ A1: giá khác nhau theo khung giờ
                var slotA1 = station1.ChargingSlots.First(s => s.SlotName == "Trụ A1");
                var slotA2 = station1.ChargingSlots.First(s => s.SlotName == "Trụ A2");

                context.Set<SlotPricing>().AddRange(
                    // Trụ A1: 0h-8h = 8,000đ (giờ thấp điểm), 8h-17h = 15,000đ (giờ bình thường), 17h-24h = 20,000đ (giờ cao điểm)
                    new SlotPricing
                    {
                        SlotId = slotA1.Id,
                        StartTime = new TimeOnly(0, 0),
                        EndTime = new TimeOnly(8, 0),
                        PricePerHour = 8000,
                        Priority = 1,
                        EffectiveFrom = DateTime.UtcNow.AddDays(-7),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new SlotPricing
                    {
                        SlotId = slotA1.Id,
                        StartTime = new TimeOnly(8, 0),
                        EndTime = new TimeOnly(17, 0),
                        PricePerHour = 15000,
                        Priority = 1,
                        EffectiveFrom = DateTime.UtcNow.AddDays(-7),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new SlotPricing
                    {
                        SlotId = slotA1.Id,
                        StartTime = new TimeOnly(17, 0),
                        EndTime = new TimeOnly(23, 59),
                        PricePerHour = 20000,
                        Priority = 1,
                        EffectiveFrom = DateTime.UtcNow.AddDays(-7),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    // Trụ A2: cuối tuần giá tăng
                    new SlotPricing
                    {
                        SlotId = slotA2.Id,
                        DayOfWeek = 6, // Thứ 7
                        StartTime = new TimeOnly(0, 0),
                        EndTime = new TimeOnly(23, 59),
                        PricePerHour = 22000,
                        Priority = 2,
                        EffectiveFrom = DateTime.UtcNow.AddDays(-7),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new SlotPricing
                    {
                        SlotId = slotA2.Id,
                        DayOfWeek = 0, // Chủ nhật
                        StartTime = new TimeOnly(0, 0),
                        EndTime = new TimeOnly(23, 59),
                        PricePerHour = 22000,
                        Priority = 2,
                        EffectiveFrom = DateTime.UtcNow.AddDays(-7),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    }
                );

                await context.SaveChangesAsync();
            }
        }
    }
}