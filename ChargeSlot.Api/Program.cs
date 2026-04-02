using ChargeSlot.Api.BackgroundJobs;
using ChargeSlot.Api.Hubs;
using ChargeSlot.Api.Data;
using Microsoft.EntityFrameworkCore;
using ChargeSlot.Api.Seeds;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Implementation;
using ChargeSlot.Api.Repositories.Implementations;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Services.Interfaces;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);

        options.User.RequireUniqueEmail = false;
    })
    .AddEntityFrameworkStores<ChargeSlotDbContext>()
    .AddDefaultTokenProviders();

// =======================
// AUTHENTICATION (JWT)
// =======================
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var jwtSection = builder.Configuration.GetSection("Jwt");
        var jwtKey = jwtSection["Key"] ?? throw new Exception("Jwt:Key is missing");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };

        // SignalR: đọc JWT từ query string cho WebSocket
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var userIdStr = context.Principal?.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out var userId))
                {
                    var user = await userManager.FindByIdAsync(userIdStr);
                    if (user == null || user.Status != ChargeSlot.Api.Constants.UserStatusConstants.Active)
                    {
                        context.Fail("User account is banned or inactive.");
                    }
                }
            }
        };
    });

// QUAN TRỌNG: chặn cookie redirect về /Account/Login
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});

// =======================
// CORS (cho phép Frontend gọi API)
// =======================
var corsOrigins = builder.Configuration.GetSection("CorsOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
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
builder.Services.AddScoped<ISmsService, EsmsSmsService>();

// Firebase Auth
var firebaseKeyPath = configuration["Firebase:ServiceAccountKeyPath"] ?? "firebase-service-account.json";
if (File.Exists(firebaseKeyPath))
{
    using var stream = new FileStream(firebaseKeyPath, FileMode.Open, FileAccess.Read);
    FirebaseApp.Create(new AppOptions()
    {
        Credential = GoogleCredential.FromStream(stream)
    });
}
else
{
    Console.WriteLine($"[WARNING] Firebase service account key not found at: {firebaseKeyPath}");
}
builder.Services.AddScoped<IFirebaseAuthService, FirebaseAuthService>();

// Firebase Storage
builder.Services.AddSingleton<IFileStorageService, FirebaseStorageService>();
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddScoped<IOwnerRepository, OwnerRepository>();
builder.Services.AddScoped<IDriverProfileService, DriverProfileService>();
builder.Services.AddScoped<IOwnerProfileService, OwnerProfileService>();
builder.Services.AddScoped<IAdminAccountService, AdminAccountService>();
builder.Services.AddScoped<IAdminAccountRepository, AdminAccountRepository>();
// ChargingStation & ChargingSlot
builder.Services.AddScoped<IChargingStationRepository, ChargingStationRepository>();
builder.Services.AddScoped<IChargingStationService, ChargingStationService>();
builder.Services.AddScoped<IChargingSlotRepository, ChargingSlotRepository>();
builder.Services.AddScoped<IChargingSlotService, ChargingSlotService>();

// Booking & Notification
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IBookingService, BookingService>();

// Payment & SePay
builder.Services.AddHttpClient(); // Needed for general API calls
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// Wallet
builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<IWalletService, WalletService>();

// Charging Session & Invoice
builder.Services.AddScoped<IChargingSessionRepository, ChargingSessionRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IChargingSessionService, ChargingSessionService>();

// Dispute
builder.Services.AddScoped<IDisputeRepository, DisputeRepository>();
builder.Services.AddScoped<IDisputeService, DisputeService>();

// Admin Revenue
builder.Services.AddScoped<IAdminRevenueService, AdminRevenueService>();

// Reviews
builder.Services.AddScoped<IReviewService, ReviewService>();

// Payout
builder.Services.AddScoped<IPayoutService, PayoutService>();

// Analytics & AI
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAiInsightsService, GeminiInsightsService>();

// Background Jobs
builder.Services.AddHostedService<PaymentExpiryJob>();
builder.Services.AddHostedService<InvoiceAutoConfirmJob>();
builder.Services.AddHostedService<DisputeAutoResolveJob>();
builder.Services.AddHostedService<NoShowJob>();
builder.Services.AddHostedService<WithdrawAutoConfirmJob>();
builder.Services.AddHostedService<UnbanAutoJob>();

// =======================
// CONTROLLERS & SWAGGER
// =======================
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ChargeSlot.Api",
        Version = "v1"
    });

    // 🔐 Add JWT Bearer to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// =======================
// BUILD APP
// =======================
var app = builder.Build();

// Auto-migrate database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChargeSlot.Api.Data.ChargeSlotDbContext>();
    await db.Database.MigrateAsync();
}

// Seed demo data
await DataSeeder.SeedAsync(app.Services);

// =======================
// MIDDLEWARE PIPELINE
// =======================
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

// Global Exception Handler - Returns 500 JSON without dropping CORS headers
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        if (context.Response.HasStarted)
        {
            throw;
        }

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var response = new 
        {
            message = "Lỗi hệ thống ngoài ý muốn.",
            detail = ex.Message
        };

        await context.Response.WriteAsJsonAsync(response);
    }
});


app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();