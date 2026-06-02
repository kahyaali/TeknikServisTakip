using TeknikServisTakip.Services;

namespace TeknikServisTakip.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bir hata oluştu: {Message}", ex.Message);

                using (var scope = scopeFactory.CreateScope())
                {
                    var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
                    var controller = context.Request.RouteValues["controller"]?.ToString();
                    var action = context.Request.RouteValues["action"]?.ToString();

                    await logService.LogErrorAsync(ex, controller, action, context);
                }

                // Hata sayfasına yönlendir
                context.Response.Clear();
                context.Response.StatusCode = 500;
                context.Response.Redirect("/Home/Error");
            }
        }
    }
}