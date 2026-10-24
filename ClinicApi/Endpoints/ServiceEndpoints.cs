using ClinicApi.Services;

namespace ClinicApi.Endpoints;

/// <summary>
/// In Program.cs:
///   builder.Services.AddServiceManagement();   // with the other builder.Services lines
///   app.MapServiceEndpoints();                 // with the other app.Map... lines
/// </summary>
public static class ServiceEndpoints
{
    public static IServiceCollection AddServiceManagement(this IServiceCollection services)
    {
        services.AddSingleton<ServiceCatalog>();
        return services;
    }

    public static WebApplication MapServiceEndpoints(this WebApplication app)
    {
        var services = app.MapGroup("/api/services");

        // GET /api/services            -> list all services
        services.MapGet("", (ServiceCatalog catalog) => Results.Ok(catalog.List()));

        // GET /api/services/{id}       -> one service (404 if missing)
        services.MapGet("/{id:int}", (int id, ServiceCatalog catalog) =>
            NotificationHistoryEndpoints.Handle(() => Results.Ok(catalog.Get(id))));

        // POST /api/services           -> create (400 invalid, 409 duplicate name)
        services.MapPost("", (ServiceRequest? request, ServiceCatalog catalog) =>
            NotificationHistoryEndpoints.Handle(() => Results.Ok(catalog.Create(request))));

        // PUT /api/services/{id}       -> update (400 invalid, 404 missing, 409 duplicate name)
        services.MapPut("/{id:int}", (int id, ServiceRequest? request, ServiceCatalog catalog) =>
            NotificationHistoryEndpoints.Handle(() => Results.Ok(catalog.Update(id, request))));

        return app;
    }
}
