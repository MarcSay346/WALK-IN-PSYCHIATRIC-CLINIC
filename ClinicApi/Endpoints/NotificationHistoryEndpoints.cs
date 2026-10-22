using System;
using System.Collections.Generic;
using ClinicApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicApi.Endpoints;

/// <summary>Body for the /api/queue-events/* endpoints.</summary>
public record QueueEventRequest(string? UserId, string? ServiceName, int? Position, int? EstimatedWaitMinutes);

/// <summary>
/// REST API for the Notification and History modules.
///
/// In Program.cs:
///   builder.Services.AddNotificationAndHistory();   // before builder.Build()
///   app.MapNotificationAndHistoryEndpoints();       // before app.Run()
/// </summary>
public static class NotificationHistoryEndpoints
{
    public static IServiceCollection AddNotificationAndHistory(this IServiceCollection services)
    {
        // one shared in-memory store for the whole app
        services.AddSingleton(_ => new NotificationService());
        services.AddSingleton(_ => new HistoryService());
        services.AddSingleton<QueueEventHandler>();

        // wrong JSON types (e.g. "abc" for a number) return 400 instead of throwing
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
        return services;
    }

    public static WebApplication MapNotificationAndHistoryEndpoints(this WebApplication app)
    {
        var notifications = app.MapGroup("/api/notifications");
        notifications.MapGet("/{userId}", GetNotifications);
        notifications.MapGet("/{userId}/unread-count", GetUnreadCount);
        notifications.MapPost("/{userId}/{id:int}/read", MarkRead);
        notifications.MapPost("/{userId}/read-all", MarkAllRead);
        notifications.MapDelete("/{userId}", ClearNotifications);
        notifications.MapGet("/{userId}/settings", GetSettings);
        notifications.MapPut("/{userId}/settings", UpdateSettings);

        var history = app.MapGroup("/api/history");
        history.MapGet("/{userId}", GetHistory);
        history.MapGet("/{userId}/summary", GetHistorySummary);

        var queueEvents = app.MapGroup("/api/queue-events");
        queueEvents.MapPost("/joined", QueueJoined);
        queueEvents.MapPost("/position", QueuePositionChanged);
        queueEvents.MapPost("/served", QueueServed);
        queueEvents.MapPost("/left", QueueLeft);

        return app;
    }

    // ---------- notifications ----------

    public static IResult GetNotifications(string userId, string? type, NotificationService service) =>
        Handle(() => Results.Ok(service.GetForUser(userId, type)));

    public static IResult GetUnreadCount(string userId, NotificationService service) =>
        Handle(() => Results.Ok(new { count = service.UnreadCount(userId) }));

    public static IResult MarkRead(string userId, int id, NotificationService service) =>
        Handle(() =>
        {
            if (!service.MarkRead(userId, id))
                return Results.NotFound(new { error = "Notification not found." });
            return Results.Ok(new { unread = service.UnreadCount(userId) });
        });

    public static IResult MarkAllRead(string userId, NotificationService service) =>
        Handle(() => Results.Ok(new { marked = service.MarkAllRead(userId) }));

    public static IResult ClearNotifications(string userId, NotificationService service) =>
        Handle(() => Results.Ok(new { removed = service.ClearForUser(userId) }));

    public static IResult GetSettings(string userId, NotificationService service) =>
        Handle(() => Results.Ok(service.GetSettings(userId)));

    public static IResult UpdateSettings(string userId, NotificationSettingsRequest? request, NotificationService service) =>
        Handle(() => Results.Ok(service.UpdateSettings(userId, request)));

    // ---------- history ----------

    public static IResult GetHistory(string userId, HistoryService history) =>
        Handle(() => Results.Ok(history.GetForUser(userId)));

    public static IResult GetHistorySummary(string userId, HistoryService history) =>
        Handle(() => Results.Ok(history.GetSummary(userId)));

    // ---------- queue events (called by the queue module / front end) ----------

    public static IResult QueueJoined(QueueEventRequest? request, QueueEventHandler events) =>
        Handle(() =>
        {
            var body = RequireBody(request);
            return Results.Ok(events.OnUserJoined(body.UserId, body.ServiceName, body.Position, body.EstimatedWaitMinutes));
        });

    public static IResult QueuePositionChanged(QueueEventRequest? request, QueueEventHandler events) =>
        Handle(() =>
        {
            var body = RequireBody(request);
            return Results.Ok(new { notification = events.OnPositionChanged(body.UserId, body.ServiceName, body.Position) });
        });

    public static IResult QueueServed(QueueEventRequest? request, QueueEventHandler events) =>
        Handle(() =>
        {
            var body = RequireBody(request);
            return Results.Ok(events.OnUserServed(body.UserId, body.ServiceName));
        });

    public static IResult QueueLeft(QueueEventRequest? request, QueueEventHandler events) =>
        Handle(() =>
        {
            var body = RequireBody(request);
            return Results.Ok(events.OnUserLeft(body.UserId, body.ServiceName));
        });

    // ---------- error handling ----------

    /// <summary>Turns module exceptions into HTTP responses: 400 invalid input, 404 not found, 409 conflict.</summary>
    public static IResult Handle(Func<IResult> action)
    {
        try
        {
            return action();
        }
        catch (ApiValidationException ex)
        {
            return Results.BadRequest(new { errors = ex.Errors });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static QueueEventRequest RequireBody(QueueEventRequest? request) =>
        request ?? throw new ApiValidationException("body", "Request body is required.");
}
