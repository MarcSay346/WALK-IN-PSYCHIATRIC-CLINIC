using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
var connStr = builder.Configuration.GetConnectionString("ClinicDb");
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

// Serve the HTML/CSS/JS from the repo folder (one level up)
var site = new PhysicalFileProvider(Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..")));
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = site });
app.UseStaticFiles(new StaticFileOptions { FileProvider = site });

// ---------- REGISTER ----------
app.MapPost("/api/register", async (RegisterRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
        return Results.BadRequest(new { error = "Email and a password of at least 6 characters are required." });

    using var db = new SqlConnection(connStr);
    var exists = await db.ExecuteScalarAsync<int>(
        "SELECT COUNT(*) FROM Patient WHERE Email = @Email", new { req.Email });
    if (exists > 0)
        return Results.Conflict(new { error = "An account with that email already exists." });

    var hash = hasher.HashPassword(req.Email, req.Password);
    var id = await db.ExecuteScalarAsync<int>(@"
        INSERT INTO Patient (FirstName, LastName, DateOfBirth, Phone, Email, PasswordHash)
        OUTPUT INSERTED.PatientId
        VALUES (@FirstName, @LastName, @DateOfBirth, @Phone, @Email, @hash)",
        new { req.FirstName, req.LastName, req.DateOfBirth, req.Phone, req.Email, hash });

    return Results.Ok(new { patientId = id });
});

// ---------- LOGIN ----------
app.MapPost("/api/login", async (LoginRequest req) =>
{
    using var db = new SqlConnection(connStr);
    var user = await db.QuerySingleOrDefaultAsync<PatientLogin>(
        "SELECT PatientId, FirstName, LastName, PasswordHash FROM Patient WHERE Email = @Email",
        new { req.Email });

    if (user is null || user.PasswordHash is null ||
        hasher.VerifyHashedPassword(req.Email, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)
        return Results.Unauthorized();

    return Results.Ok(new { user.PatientId, user.FirstName, user.LastName });
});

// ---------- NOTIFICATIONS ----------
app.MapGet("/api/notifications", async (int patientId) =>
{
    using var db = new SqlConnection(connStr);
    return await db.QueryAsync(
        @"SELECT NotificationId, Message, IsRead, CreatedAt
          FROM Notification WHERE PatientId = @patientId
          ORDER BY CreatedAt DESC", new { patientId });
});

app.Run();

record RegisterRequest(string FirstName, string LastName, DateTime DateOfBirth, string? Phone, string Email, string Password);
record LoginRequest(string Email, string Password);

class PatientLogin
{
    public int PatientId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? PasswordHash { get; set; }
}