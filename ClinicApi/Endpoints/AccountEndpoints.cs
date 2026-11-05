using ClinicApi.Data;
using ClinicApi.Services;

namespace ClinicApi.Endpoints;

/// <summary>
/// Login / registration API (UserCredentials + UserProfile tables).
/// In Program.cs:
///   builder.Services.AddAccounts();   // with the other builder.Services lines
///   app.MapAccountEndpoints();        // with the other app.Map... lines
/// </summary>
public static class AccountEndpoints
{
    public static IServiceCollection AddAccounts(this IServiceCollection services)
    {
        services.AddSingleton(sp => DatabaseSetup.Pick(sp,
            db => new AccountService(new SqlUserRepository(db)),
            () => new AccountService()));
        return services;
    }

    public static WebApplication MapAccountEndpoints(this WebApplication app)
    {
        // POST /api/register   -> 200 { user }, 400 invalid, 409 email already used
        app.MapPost("/api/register", Register);

        // POST /api/login      -> 200 { user }, 400 missing fields, 401 wrong email/password
        app.MapPost("/api/login", Login);

        // GET /api/users/{id}  -> 200 profile, 404 missing
        app.MapGet("/api/users/{userId:int}", GetProfile);

        return app;
    }

    public static IResult Register(RegisterRequest? request, AccountService accounts) =>
        NotificationHistoryEndpoints.Handle(() => Results.Ok(accounts.Register(request)));

    public static IResult Login(LoginRequest? request, AccountService accounts) =>
        NotificationHistoryEndpoints.Handle(() =>
        {
            var user = accounts.Login(request);
            return user is null
                ? Results.Json(new { error = "Incorrect email or password." }, statusCode: StatusCodes.Status401Unauthorized)
                : Results.Ok(user);
        });

    public static IResult GetProfile(int userId, AccountService accounts) =>
        NotificationHistoryEndpoints.Handle(() => Results.Ok(accounts.GetProfile(userId)));
}
