using ClinicApi.Data;
using ClinicApi.Endpoints;
using Microsoft.Extensions.FileProviders;
using RadiantMindClinic.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDatabase(builder.Configuration);   // A4: SQL Server (ConnectionStrings:ClinicDb in user secrets)
builder.Services.AddAccounts();                  // Login/register module (UserCredentials, UserProfile tables)
builder.Services.AddNotificationAndHistory();   // Notification + History modules (Notification, VisitHistory tables)
builder.Services.AddWaitTime();                 // Wait-time module (registers WaitTimeService as a singleton)
builder.Services.AddControllers();              // Queue module uses a controller (QueueController)
builder.Services.AddSingleton<IQueueService, QueueService>();   // Queue module (in memory, one shared queue)
builder.Services.AddServiceManagement();       // Service Management module (Kayson)
var app = builder.Build();

// Debug: send error details back in the response
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsync(ex.GetType().Name + ": " + ex.Message);
    }
});

// Serve the HTML/CSS/JS from the repo folder (one level up)
var site = new PhysicalFileProvider(Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..")));
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = site });
app.UseStaticFiles(new StaticFileOptions { FileProvider = site });

// ---------- LOGIN + REGISTER (/api/login, /api/register, /api/users/{id}) ----------
app.MapAccountEndpoints();

// ---------- NOTIFICATIONS + HISTORY (/api/notifications, /api/history, /api/queue-events) ----------
app.MapNotificationAndHistoryEndpoints();

// ---------- WAIT TIME (/api/wait-time) ----------
app.MapWaitTimeEndpoints();

// ---------- SERVICES (/api/services) ----------
app.MapServiceEndpoints();

// ---------- QUEUE (/api/queue, /api/queue/join, /api/queue/leave/{id}, /api/queue/serve-next, /api/queue/status/{id}) ----------
app.MapControllers();

app.Run();
