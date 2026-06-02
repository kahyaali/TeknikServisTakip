using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Routing;
using TeknikServisTakip.Middleware;

namespace TeknikServisTakip.Extensions;

public static class MiddlewareRegistration
{
    public static IApplicationBuilder UseGlobalExceptionMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<GlobalExceptionMiddleware>();
        return app;
    }

    public static IApplicationBuilder UseEnvironmentSpecificMiddleware(this IApplicationBuilder app, IHostEnvironment env)
    {
        if (!env.IsDevelopment())
        {
            app.UseHsts();
            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("Content-Security-Policy",
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.datatables.net https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://code.jquery.com; " +
                    "style-src 'self' 'unsafe-inline' https://cdn.datatables.net https://cdnjs.cloudflare.com https://fonts.googleapis.com; " +
                    "font-src 'self' https://cdnjs.cloudflare.com https://fonts.gstatic.com; " +
                    "img-src 'self' data: https:; " +
                    "connect-src 'self' https: wss: ws: http:;");
                await next();
            });
        }
        else
        {
           // app.UseDeveloperExceptionPage();
        }
        return app;
    }

    public static IApplicationBuilder UseStatusCodePagesWithReExecute(this IApplicationBuilder app)
    {
        app.UseStatusCodePagesWithReExecute("/Home/NotFound404", "?code={0}");
        return app;
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            if (!context.Response.Headers.ContainsKey("X-Content-Type-Options"))
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");

            if (!context.Response.Headers.ContainsKey("X-Frame-Options"))
                context.Response.Headers.Add("X-Frame-Options", "DENY");

            if (!context.Response.Headers.ContainsKey("X-XSS-Protection"))
                context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");

            if (!context.Response.Headers.ContainsKey("Referrer-Policy"))
                context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");

            await next();
        });
        return app;
    }

    public static IApplicationBuilder UseForwardedHeadersMiddleware(this IApplicationBuilder app)
    {
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
        });
        return app;
    }

    public static IApplicationBuilder UseCoreMiddleware(this IApplicationBuilder app)
    {
        app.UseForwardedHeadersMiddleware();
        app.UseHttpsRedirection();
        app.UseCors("AllowSpecificOrigins");
        app.UseStaticFiles();
        app.UseRouting();  // Bu önemli! UseRouting'ten sonra endpoint'ler tanımlanır
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    public static IApplicationBuilder UseRateLimiterAndSession(this IApplicationBuilder app)
    {
        app.UseRateLimiter();
        app.UseSession();
        return app;
    }

    // Bu metod IEndpointRouteBuilder almalı, IApplicationBuilder değil!
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        endpoints.MapHub<TeknikServisTakip.Hubs.NotificationHub>("/notificationHub");

        return endpoints;
    }
}