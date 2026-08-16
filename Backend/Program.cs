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
using ANpay.Api.Services.BillPaymentProvider;
using ANpay.Api.Services.Crypto;
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

// P0: Virtual Cards
builder.Services.AddScoped<VirtualCardService>();

// P0: Bill Payments
builder.Services.AddScoped<BillPaymentService>();
if (builder.Configuration.GetSection("BillPayment:Baxi").Exists() &&
    !string.IsNullOrEmpty(builder.Configuration["BillPayment:Baxi:ApiKey"]))
{
    builder.Services.AddScoped<IBillPaymentProvider, BaxiBillPaymentProvider>();
}
else
{
    builder.Services.AddScoped<IBillPaymentProvider, MockBillPaymentProvider>();
}

// P0: AI Assistant
builder.Services.AddScoped<AiAssistantService>();

// P1: Credit Scoring
builder.Services.AddScoped<CreditScoreService>();

// P1: Loyalty & Rewards
builder.Services.AddScoped<LoyaltyService>();

// P1: Cross-Border Remittance
builder.Services.AddScoped<RemittanceService>();

// P2: BNPL
builder.Services.AddScoped<BnplService>();

// P2: Open Banking
builder.Services.AddScoped<OpenBankingService>();

// P2: POS
builder.Services.AddScoped<PosService>();

// P3: Microloans
builder.Services.AddScoped<MicroloanService>();

// P3: Insurance
builder.Services.AddScoped<InsuranceService>();

// P3: Investments
builder.Services.AddScoped<InvestmentService>();

// P3: White-Label
builder.Services.AddScoped<WhiteLabelService>();

// Market Data Service
builder.Services.AddHttpClient<MarketDataService>();

// Blockchain Services
builder.Services.AddScoped<BitcoinRpcService>();
builder.Services.AddScoped<EthereumRpcService>();

// SignalR
builder.Services.AddSignalR();

// HttpClient factory for webhook delivery and bill payment providers
builder.Services.AddHttpClient();

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
builder.Services.AddHostedService<WebhookDeliveryWorker>();
builder.Services.AddHostedService<InvestmentAccrualWorker>();
builder.Services.AddHostedService<MicroloanCollectionWorker>();
builder.Services.AddHostedService<InsuranceRenewalWorker>();
builder.Services.AddHostedService<CryptoDepositMonitorWorker>();

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

        // Seed Bill Providers
        if (!context.BillProviders.Any())
        {
            context.BillProviders.AddRange(
                new BillProvider { Name = "IKEDC", Category = BillCategory.Electricity, Code = "IKEDC", MinimumAmount = 1000, MaximumAmount = 500000, FixedFee = 100, Currency = "NGN", RequiresBillerCode = true, Description = "Ikeja Electric Distribution Company" },
                new BillProvider { Name = "PHED", Category = BillCategory.Electricity, Code = "PHED", MinimumAmount = 1000, MaximumAmount = 500000, FixedFee = 100, Currency = "NGN", RequiresBillerCode = true, Description = "Port Harcourt Electric Distribution Company" },
                new BillProvider { Name = "MTN", Category = BillCategory.Airtime, Code = "MTN", MinimumAmount = 50, MaximumAmount = 50000, FixedFee = 0, Currency = "NGN", RequiresBillerCode = false, Description = "MTN Airtime" },
                new BillProvider { Name = "Airtel", Category = BillCategory.Airtime, Code = "AIRTEL", MinimumAmount = 50, MaximumAmount = 50000, FixedFee = 0, Currency = "NGN", RequiresBillerCode = false, Description = "Airtel Airtime" },
                new BillProvider { Name = "Glo", Category = BillCategory.Airtime, Code = "GLO", MinimumAmount = 50, MaximumAmount = 50000, FixedFee = 0, Currency = "NGN", RequiresBillerCode = false, Description = "Glo Airtime" },
                new BillProvider { Name = "9Mobile", Category = BillCategory.Airtime, Code = "9MOBILE", MinimumAmount = 50, MaximumAmount = 50000, FixedFee = 0, Currency = "NGN", RequiresBillerCode = false, Description = "9Mobile Airtime" },
                new BillProvider { Name = "DStv", Category = BillCategory.CableTV, Code = "DSTV", MinimumAmount = 2000, MaximumAmount = 100000, FixedFee = 100, Currency = "NGN", RequiresBillerCode = true, Description = "DStv Subscription" },
                new BillProvider { Name = "GOtv", Category = BillCategory.CableTV, Code = "GOTV", MinimumAmount = 1000, MaximumAmount = 50000, FixedFee = 100, Currency = "NGN", RequiresBillerCode = true, Description = "GOtv Subscription" },
                new BillProvider { Name = "Startimes", Category = BillCategory.CableTV, Code = "STARTIMES", MinimumAmount = 1000, MaximumAmount = 50000, FixedFee = 100, Currency = "NGN", RequiresBillerCode = true, Description = "Startimes Subscription" },
                new BillProvider { Name = "MTN Data", Category = BillCategory.Data, Code = "MTNDATA", MinimumAmount = 100, MaximumAmount = 50000, FixedFee = 0, Currency = "NGN", RequiresBillerCode = false, Description = "MTN Data Bundle" },
                new BillProvider { Name = "Airtel Data", Category = BillCategory.Data, Code = "AIRTELDATA", MinimumAmount = 100, MaximumAmount = 50000, FixedFee = 0, Currency = "NGN", RequiresBillerCode = false, Description = "Airtel Data Bundle" }
            );
            await context.SaveChangesAsync();
        }

        // Seed AI Training Data
        var aiService = scope.ServiceProvider.GetRequiredService<AiAssistantService>();
        await aiService.SeedTrainingDataAsync();

        // Seed Remittance Partners
        if (!context.RemittancePartners.Any())
        {
            context.RemittancePartners.AddRange(
                new RemittancePartner { Name = "Wise", Code = "WISE", Country = "United Kingdom", Type = PartnerType.PaymentProvider, CommissionRate = 0.5m, MinimumAmount = 100, MaximumAmount = 100000 },
                new RemittancePartner { Name = "WorldRemit", Code = "WRLD", Country = "United States", Type = PartnerType.PaymentProvider, CommissionRate = 1.0m, MinimumAmount = 50, MaximumAmount = 50000 },
                new RemittancePartner { Name = "Remitly", Code = "RMLY", Country = "Canada", Type = PartnerType.PaymentProvider, CommissionRate = 0.8m, MinimumAmount = 50, MaximumAmount = 50000 },
                new RemittancePartner { Name = "Sendwave", Code = "SNWD", Country = "United Kingdom", Type = PartnerType.MobileMoney, CommissionRate = 0.5m, MinimumAmount = 10, MaximumAmount = 10000 },
                new RemittancePartner { Name = "Chipper Cash", Code = "CHPR", Country = "United States", Type = PartnerType.MobileMoney, CommissionRate = 0.0m, MinimumAmount = 1, MaximumAmount = 5000 }
            );
            await context.SaveChangesAsync();
        }
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
