using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ANpay.Api.Data;
using ANpay.Api.Middleware;
using ANpay.Api.Models;
using ANpay.Api.Services;
using ANpay.Api.Services.PaymentGateway;
using ANpay.Api.Hubs;
using ANpay.Api.Workers;
using ANpay.Api.Components.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis Cache
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "ANpay_";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// Data Protection - persist keys
builder.Services.AddDataProtection()
    .SetApplicationName(builder.Configuration["DataProtection:ApplicationName"] ?? "ANpay");

// Identity with strict password and lockout policies
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;

    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// API Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<BeneficiaryService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<BranchService>();
builder.Services.AddScoped<WebAuthnService>();
builder.Services.AddScoped<ScheduledTransferService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<KycService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<FeeService>();
builder.Services.AddScoped<LimitService>();
builder.Services.AddScoped<ApprovalService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<SupportService>();
builder.Services.AddScoped<ExchangeService>();
builder.Services.AddScoped<CashService>();
builder.Services.AddScoped<CryptoService>();
builder.Services.AddScoped<MerchantService>();
builder.Services.AddScoped<QrPaymentService>();
builder.Services.AddScoped<CacheService>();
builder.Services.AddScoped<DisputeService>();
builder.Services.AddScoped<FraudService>();
builder.Services.AddScoped<ReconciliationService>();
builder.Services.AddScoped<SystemSettingService>();

// Email & SMS - use real services when configured, console fallback otherwise
if (builder.Configuration.GetSection("Smtp").Exists() &&
    !string.IsNullOrEmpty(builder.Configuration["Smtp:Host"]))
{
    builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
}
else
{
    builder.Services.AddSingleton<IEmailService, ConsoleEmailService>();
}

if (builder.Configuration.GetSection("Twilio").Exists() &&
    !string.IsNullOrEmpty(builder.Configuration["Twilio:AccountSid"]))
{
    builder.Services.AddSingleton<ISmsService, TwilioSmsService>();
}
else
{
    builder.Services.AddSingleton<ISmsService, ConsoleSmsService>();
}

// Payment Gateway - use mock only in development
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IPaymentGateway, MockPaymentGateway>();
}
else
{
    // TODO: Register real payment gateway in production
    builder.Services.AddSingleton<IPaymentGateway, MockPaymentGateway>();
}

builder.Services.AddScoped<PaymentGatewayService>();

// Market Data Service
builder.Services.AddHttpClient<MarketDataService>();

// SignalR
builder.Services.AddSignalR();

// Health Checks
builder.Services.AddHealthChecks();

// Antiforgery
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Blazor Server Services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient<ApiService>(client =>
{
    var urls = builder.Configuration["urls"]
        ?? builder.Configuration["Urls"]
        ?? builder.WebHost.GetSetting("urls")
        ?? "http://localhost:5069";
    var firstUrl = urls.Split(';', StringSplitOptions.RemoveEmptyEntries).First().Trim();
    client.BaseAddress = new Uri(firstUrl);
});
builder.Services.AddScoped<AuthState>();

// Controllers
builder.Services.AddControllers();

// Background workers
builder.Services.AddHostedService<ScheduledTransferWorker>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ANpay API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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

// CORS - restrict to specific origins in production
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? new[] { builder.Configuration["AppUrl"] ?? "https://localhost" };
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

var app = builder.Build();

// Security headers middleware (first in pipeline)
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    if (app.Environment.IsProduction())
    {
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }
    await next();
});

// Global exception handling middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

// Rate limiting middleware
app.UseMiddleware<RateLimitingMiddleware>();

// Account lockout middleware
app.UseMiddleware<AccountLockoutMiddleware>();

// Apply migrations and seed data
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();

        var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
        await authService.SeedRolesAndAdminAsync();

        var permissionService = scope.ServiceProvider.GetRequiredService<PermissionService>();
        await permissionService.SeedPermissionsAsync();

        var ledgerService = scope.ServiceProvider.GetRequiredService<LedgerService>();
        await ledgerService.SeedAccountsAsync();

        var limitService = scope.ServiceProvider.GetRequiredService<LimitService>();
        await limitService.SeedDefaultLimitsAsync();

        var settingService = scope.ServiceProvider.GetRequiredService<SystemSettingService>();
        await settingService.SetAsync("PlatformName", "ANpay", "General", "Platform display name");
        await settingService.SetAsync("PlatformVersion", "1.0.0", "General", "Current platform version");
        await settingService.SetAsync("MaintenanceMode", "false", "General", "Enable maintenance mode");
        await settingService.SetAsync("RegistrationEnabled", "true", "General", "Allow new registrations");
        await settingService.SetAsync("DefaultCurrency", "USD", "General", "Default wallet currency");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsProduction())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseStaticFiles();

app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// Map API Controllers
app.MapControllers();

// Map Blazor Components
app.MapStaticAssets();
app.MapRazorComponents<ANpay.Api.Components.App>()
    .AddInteractiveServerRenderMode();

// Map SignalR Hub
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
