using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ANpay.Api.Data;
using ANpay.Api.Middleware;
using ANpay.Api.Models;
using ANpay.Api.Services;
using ANpay.Api.Hubs;
using ANpay.Api.Components.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
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
        IssuerSigningKey = new SymmetricSecurityKey(key)
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

// API Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<BeneficiaryService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<BranchService>();
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
builder.Services.AddScoped<DisputeService>();
builder.Services.AddScoped<FraudService>();
builder.Services.AddScoped<SystemSettingService>();
builder.Services.AddSingleton<IEmailService, ConsoleEmailService>();
builder.Services.AddSingleton<ISmsService, ConsoleSmsService>();

// SignalR
builder.Services.AddSignalR();

// Blazor Server Services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient<ApiService>();
builder.Services.AddScoped<AuthState>();

// Controllers
builder.Services.AddControllers();

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

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Global exception handling middleware (first in pipeline)
app.UseMiddleware<GlobalExceptionMiddleware>();

// Seed roles, admin, permissions, ledger, limits
using (var scope = app.Services.CreateScope())
{
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseStaticFiles();

app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

// Map API Controllers
app.MapControllers();

// Map Blazor Components
app.MapStaticAssets();
app.MapRazorComponents<ANpay.Api.Components.App>()
    .AddInteractiveServerRenderMode();

// Map SignalR Hub
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
