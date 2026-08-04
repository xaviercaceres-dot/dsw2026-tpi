using System.Text.Json;
using System.Threading.RateLimiting;
using Dsw2026Tpi.CrossCutting.Models;
using Microsoft.AspNetCore.RateLimiting;

namespace Dsw2026Tpi.Api.Configurations;

public record RateLimitOptions(int PermitLimit, int WindowSeconds);

public static class RateLimitingConfigurationExtensions
{
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimiting");
        var adminLogin = section.GetSection("AdminLogin").Get<RateLimitOptions>() ?? new RateLimitOptions(5, 60);
        var patientLogin = section.GetSection("PatientLogin").Get<RateLimitOptions>() ?? new RateLimitOptions(10, 60);
        var booking = section.GetSection("AppointmentBooking").Get<RateLimitOptions>() ?? new RateLimitOptions(5, 60);
        var global = section.GetSection("Global").Get<RateLimitOptions>() ?? new RateLimitOptions(100, 60);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("Rate limit excedido: {Path} desde {Ip}",
                    context.HttpContext.Request.Path, context.HttpContext.Connection.RemoteIpAddress);

                context.HttpContext.Response.ContentType = "application/json";
                var error = new ErrorResponse("RATE_LIMIT_EXCEEDED", "Se excedió el límite de solicitudes permitidas.");
                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(error, new JsonSerializerOptions(JsonSerializerDefaults.Web)), cancellationToken);
            };

            options.AddPolicy("AdminLogin", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = adminLogin.PermitLimit, Window = TimeSpan.FromSeconds(adminLogin.WindowSeconds), QueueLimit = 0 }));

            options.AddPolicy("PatientLogin", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = patientLogin.PermitLimit, Window = TimeSpan.FromSeconds(patientLogin.WindowSeconds), QueueLimit = 0 }));

            options.AddPolicy("AppointmentBooking", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = booking.PermitLimit, Window = TimeSpan.FromSeconds(booking.WindowSeconds), QueueLimit = 0 }));

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var key = httpContext.User.Identity?.IsAuthenticated == true
                    ? httpContext.User.Identity!.Name!
                    : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                { PermitLimit = global.PermitLimit, Window = TimeSpan.FromSeconds(global.WindowSeconds), QueueLimit = 0 });
            });
        });

        return services;
    }
}