using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Data
{
    /// <summary>
    /// Seed some demo data for easy local testing.
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

            // Ensure roles exist
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
            // SEED DRIVERS
            // ============================

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
                        FullName = $"Demo Driver {i}",
                        IsPhoneVerified = true,
                        Status = "ACTIVE",
                        CreatedAt = DateTime.UtcNow
                    };

                    var createResult = await userManager.CreateAsync(user, defaultPassword);
                    if (createResult.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, RoleConstants.Driver);
                    }
                }

                if (!await context.Driver.AnyAsync(d => d.UserId == user.Id))
                {
                    context.Driver.Add(new Driver
                    {
                        UserId = user.Id,
                        VehicleType = "EV Car",
                        LicensePlate = $"DRIVER-{i:00}",
                        LicenseNumber = $"DRV-LIC-{i:00}",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            // ============================
            // SEED OWNERS
            // ============================

            for (int i = 1; i <= 5; i++)
            {
                var phone = $"09100000{i:00}";
                var user = await userManager.FindByNameAsync(phone);

                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = phone,
                        PhoneNumber = phone,
                        FullName = $"Demo Owner {i}",
                        IsPhoneVerified = true,
                        Status = "ACTIVE",
                        CreatedAt = DateTime.UtcNow
                    };

                    var createResult = await userManager.CreateAsync(user, defaultPassword);
                    if (createResult.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, RoleConstants.Owner);
                    }
                }

                if (!await context.Owner.AnyAsync(o => o.UserId == user.Id))
                {
                    context.Owner.Add(new Owner
                    {
                        UserId = user.Id,
                        BusinessName = $"Charging Company {i}",
                        TaxCode = $"TAX-00{i}",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await context.SaveChangesAsync();
        }
    }
}