using ClinicApi.Endpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using RadiantMindClinic.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddNotificationAndHistory();   // Notification + History modules (in memory)
builder.Services.AddWaitTime();                 // Wait-time module (registers WaitTimeService as a singleton)
builder.Services.AddControllers();              // Queue module uses a controller (QueueController)
builder.Services.AddSingleton<IQueueService, QueueService>();   // Queue module (in memory, one shared queue)
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

var hasher = new PasswordHasher<string>();   // built into ASP.NET Core, no extra package

// ---------- In-memory patient accounts (A3: no database; resets when the server restarts) ----------
var patients = new List<PatientAccount>();
var patientsLock = new object();
var nextPatientId = 1;

void AddPatient(string firstName, string lastName, string email, string password, string? phone = null, DateTime? dateOfBirth = null)
{
    var normalizedEmail = email.Trim().ToLowerInvariant();
    patients.Add(new PatientAccount
    {
        PatientId = nextPatientId++,
        FirstName = firstName.Trim(),
        LastName = lastName.Trim(),
        Email = normalizedEmail,
        Phone = phone,
        DateOfBirth = dateOfBirth,
        PasswordHash = hasher.HashPassword(normalizedEmail, password)
    });
}

// demo account that always exists
AddPatient("Test", "Patient", "patient@test.com", "password1");

// Serve the HTML/CSS/JS from the repo folder (one level up)
var site = new PhysicalFileProvider(Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..")));
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = site });
app.UseStaticFiles(new StaticFileOptions { FileProvider = site });

// ---------- REGISTER ----------
app.MapPost("/api/register", (RegisterRequest? req) =>
{
    if (req is null || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
        return Results.BadRequest(new { error = "Email and a password of at least 6 characters are required." });
    if (req.Email.Trim().Length > 100 || req.Password.Length > 100)
        return Results.BadRequest(new { error = "Email and password must be 100 characters or fewer." });
    if ((req.FirstName?.Length ?? 0) > 50 || (req.LastName?.Length ?? 0) > 50)
        return Results.BadRequest(new { error = "First and last name must be 50 characters or fewer." });

    lock (patientsLock)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (patients.Any(p => p.Email == email))
            return Results.Conflict(new { error = "An account with that email already exists." });

        AddPatient(req.FirstName ?? "", req.LastName ?? "", email, req.Password, req.Phone, req.DateOfBirth);
        return Results.Ok(new { patientId = patients[^1].PatientId });
    }
});

// ---------- LOGIN ----------
app.MapPost("/api/login", (LoginRequest? req) =>
{
    if (req is null || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "Email and password are required." });

    var email = req.Email.Trim().ToLowerInvariant();
    PatientAccount? user;
    lock (patientsLock)
    {
        user = patients.FirstOrDefault(p => p.Email == email);
    }

    if (user is null ||
        hasher.VerifyHashedPassword(email, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)
        return Results.Unauthorized();

    return Results.Ok(new { user.PatientId, user.FirstName, user.LastName, user.Email });
});

// ---------- NOTIFICATIONS + HISTORY (/api/notifications, /api/history, /api/queue-events) ----------
app.MapNotificationAndHistoryEndpoints();

// ---------- WAIT TIME (/api/wait-time) ----------
app.MapWaitTimeEndpoints();

// ---------- QUEUE (/api/queue, /api/queue/join, /api/queue/leave/{id}, /api/queue/serve-next, /api/queue/status/{id}) ----------
app.MapControllers();

app.Run();

record RegisterRequest(string? FirstName, string? LastName, DateTime? DateOfBirth, string? Phone, string? Email, string? Password);
record LoginRequest(string? Email, string? Password);

class PatientAccount
{
    public int PatientId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string PasswordHash { get; set; } = "";
}