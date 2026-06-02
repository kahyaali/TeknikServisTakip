using Business.Abstract;
using Business.Concrete;
using DataAccess.Context;
using DataAccess.UnitOfWork;
using Entities.Concrete;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Threading.RateLimiting;
using TeknikServisTakip.Business.Abstract;
using TeknikServisTakip.Business.Concrete;
using TeknikServisTakip.Filters;
using TeknikServisTakip.Services;

namespace TeknikServisTakip.Extensions;

public static class ServiceRegistration
{
    public static IServiceCollection AddRateLimiterWithPolicy(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress.ToString(),
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 100,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        });
        return services;
    }

    public static IServiceCollection AddCorsWithPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             ?? configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>()
                             ?? new[] { "https://localhost:7042" };

        services.AddCors(options =>
        {
            options.AddPolicy("AllowSpecificOrigins", policy =>
            {
                if (allowedOrigins.Length == 1 && allowedOrigins[0] == "*")
                {
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                }
                else
                {
                    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
                }
            });
        });
        return services;
    }

    public static IServiceCollection AddFormOptions(this IServiceCollection services)
    {
        services.Configure<FormOptions>(options =>
        {
            options.ValueLengthLimit = int.MaxValue;
            options.MultipartBodyLengthLimit = 52428800;
            options.MemoryBufferThreshold = 52428800;
        });
        return services;
    }

    public static IServiceCollection AddControllersWithLogFilter(this IServiceCollection services)
    {
        services.AddControllersWithViews(options =>
        {
            options.Filters.Add<LogActionFilter>();
        });
        return services;
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly("TeknikServisTakip")
            ));
        return services;
    }

    public static IServiceCollection AddIdentityWithCookies(this IServiceCollection services)
    {
        services.AddIdentity<AppUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.User.RequireUniqueEmail = true;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "TeknikServisCookie";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Home/Forbidden403";
        });
        return services;
    }

    public static IServiceCollection AddCustomServices(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IMailService, MailService>();
        services.AddScoped<ILogService, LogService>();
        services.AddScoped<LogActionFilter>();

        // DEPO MODÜLÜ 
        services.AddScoped<IProductService, ProductManager>();
        services.AddScoped<ICategoryService, CategoryManager>();

        // LogSettings
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
        var logSettings = configuration.GetSection("LogSettings").Get<LogSettings>();
        services.AddSingleton(logSettings);

        return services;
    }

    public static IServiceCollection AddHttpContextAndSession(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSession(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.IdleTimeout = TimeSpan.FromMinutes(20);
        });
        return services;
    }
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services)
    {
        services.AddHostedService<StockAlertBackgroundService>();
        // Log temizleme servisi (Her gün 03:00)
        services.AddHostedService<LogCleanupService>();
        return services;
    }

    // Kültür Ayarı
    public static void ConfigureTurkishCulture(this WebApplicationBuilder builder)
    {
        var cultureInfo = new CultureInfo("tr-TR");
        cultureInfo.NumberFormat.CurrencySymbol = "₺";
        cultureInfo.NumberFormat.CurrencyDecimalSeparator = ",";
        cultureInfo.NumberFormat.CurrencyGroupSeparator = ".";
        cultureInfo.NumberFormat.NumberDecimalSeparator = ",";
        cultureInfo.NumberFormat.NumberGroupSeparator = ".";

        CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
        CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

        // .NET Core'a Türkçe karakterleri HTML koduna çevirme
        builder.Services.AddSingleton(System.Text.Encodings.Web.HtmlEncoder.Create(System.Text.Unicode.UnicodeRanges.All));
    }
}