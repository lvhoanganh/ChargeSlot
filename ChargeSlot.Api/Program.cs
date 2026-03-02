using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Implementation;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// =======================
// CONFIGURATION
// =======================
var configuration = builder.Configuration;
var jwtSection = configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new Exception("Jwt:Key is missing");

// =======================
// DATABASE
// =======================
builder.Services.AddDbContext<ChargeSlotDbContext>(options =>
{
    options.UseSqlServer(
        configuration.GetConnectionString("DefaultConnection")
    );
});

// =======================
// IDENTITY (PHONE-FIRST)
// =======================
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
    {
        // Password rules
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        // Lockout rules
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);

        // User rules
        options.User.RequireUniqueEmail = false; // ⭐ Phone-first
    })
    .AddEntityFrameworkStores<ChargeSlotDbContext>()
    .AddDefaultTokenProviders();

// =======================
// AUTHENTICATION (JWT)
// =======================
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            ),

            ClockSkew = TimeSpan.Zero
        };
    });

// =======================
// AUTHORIZATION
// =======================
builder.Services.AddAuthorization();

// =======================
// SERVICES (DI)
// =======================
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserOtpRepository, UserOtpRepository>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddScoped<IOwnerRepository, OwnerRepository>();
builder.Services.AddScoped<IDriverProfileService, DriverProfileService>();
builder.Services.AddScoped<IOwnerProfileService, OwnerProfileService>();


// (Sau này thêm)
// builder.Services.AddScoped<IBookingService, BookingService>();
// builder.Services.AddScoped<IChargingStationService, ChargingStationService>();

// =======================
// CONTROLLERS & SWAGGER
// =======================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// =======================
// BUILD APP
// =======================
var app = builder.Build();

// =======================
// MIDDLEWARE PIPELINE
// =======================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ⚠️ Thứ tự RẤT QUAN TRỌNG
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
