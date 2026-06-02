


// program.cs sadeleştirilmiş hali

using TeknikServisTakip.Data;
using TeknikServisTakip.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Service Registrations
builder.Services.AddRateLimiterWithPolicy();
builder.Services.AddCorsWithPolicy(builder.Configuration);
builder.Services.AddFormOptions();
builder.Services.AddControllersWithLogFilter();
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddIdentityWithCookies();
builder.Services.AddCustomServices();
builder.Services.AddHttpContextAndSession();
builder.Services.AddSignalR();
// Background servisler
builder.Services.AddBackgroundServices();

// Kültür Ayarı
builder.ConfigureTurkishCulture();

var app = builder.Build();


// ========== OTOMATIK MIGRATION ==========
await app.EnsureMigrationAsync();

// Middleware Pipeline
app.UseGlobalExceptionMiddleware();
app.UseEnvironmentSpecificMiddleware(app.Environment);
app.UseStatusCodePagesWithReExecute();
app.UseSecurityHeaders();
app.UseCoreMiddleware();
app.UseRateLimiterAndSession();

// Endpoint Mapping (Direkt burada yap, en temizi)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<TeknikServisTakip.Hubs.NotificationHub>("/notificationHub");

// Database Initialization
using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider);
}

app.Run();