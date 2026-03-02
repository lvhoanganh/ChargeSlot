using ChargeSlot.Api.Helpers;
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

            // Ensure roles exist (in case migrations not applied yet)
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

            // Demo driver user
            var driverPhone = "0900000001";
            var driverUser = await userManager.FindByNameAsync(driverPhone);
            if (driverUser == null)
            {
                driverUser = new ApplicationUser
                {
                    UserName = driverPhone,
                    PhoneNumber = driverPhone,
                    FullName = "Demo Driver",
                    IsPhoneVerified = true,
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(driverUser, defaultPassword);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(driverUser, RoleConstants.Driver);
                }
            }

            // Ensure driver profile exists
            if (!await context.Drivers.AnyAsync(d => d.UserId == driverUser.Id))
            {
                context.Drivers.Add(new Driver
                {
                    UserId = driverUser.Id,
                    VehicleType = "EV Car",
                    LicensePlate = "TEST-DRIVER-01",
                    LicenseNumber = "DRV-TEST-01",
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Demo owner user
            var ownerPhone = "0900000002";
            var ownerUser = await userManager.FindByNameAsync(ownerPhone);
            if (ownerUser == null)
            {
                ownerUser = new ApplicationUser
                {
                    UserName = ownerPhone,
                    PhoneNumber = ownerPhone,
                    FullName = "Demo Owner",
                    IsPhoneVerified = true,
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(ownerUser, defaultPassword);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(ownerUser, RoleConstants.Owner);
                }
            }

            // Ensure owner profile exists
            if (!await context.Owners.AnyAsync(o => o.UserId == ownerUser.Id))
            {
                context.Owners.Add(new Owner
                {
                    UserId = ownerUser.Id,
                    BusinessName = "Demo Charging Co.",
                    TaxCode = "TAX-DEMO-001",
                    CreatedAt = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync();
        }
    }
}

