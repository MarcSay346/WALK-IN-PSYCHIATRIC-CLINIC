using ClinicApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicApi.Endpoints;

/// <summary>
/// In Program.cs:
///   builder.Services.AddWaitTime();   // right below AddNotificationAndHistory();
///   app.MapWaitTimeEndpoints();       // right below MapNotificationAndHistoryEndpoints();
/// </summary>
public static class WaitTimeEndpoints
{
    public static IServiceCollection AddWaitTime(this IServiceCollection services)
    {
        services.AddSingleton<WaitTimeService>();
        return services;
    }

    public static WebApplication MapWaitTimeEndpoints(this WebApplication app)
    {
        // GET /api/wait-time?position=3&durationMinutes=15
        app.MapGet("/api/wait-time", GetEstimate);
        return app;
    }

    public static IResult GetEstimate(int? position, int? durationMinutes, WaitTimeService service)
    {
        try
        {
            return Results.Ok(new { minutes = service.EstimateMinutes(position, durationMinutes) });
        }
        catch (ApiValidationException ex)
        {
            return Results.BadRequest(new { errors = ex.Errors });
        }
    }
}
