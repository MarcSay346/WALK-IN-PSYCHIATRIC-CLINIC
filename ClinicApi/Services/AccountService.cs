using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ClinicApi.Data;
using Microsoft.AspNetCore.Identity;

namespace ClinicApi.Services;

/// <summary>Body of POST /api/register. Only email and password are required.</summary>
public record RegisterRequest(string? FirstName, string? LastName, DateTime? DateOfBirth, string? Phone, string? Email, string? Password);

/// <summary>Body of POST /api/login.</summary>
public record LoginRequest(string? Email, string? Password);

/// <summary>What the API sends back about a user (never the password hash).</summary>
public record AccountInfo(int UserId, int PatientId, string Email, string Name, string FirstName, string LastName, string? Phone, string Role);

/// <summary>
/// Login and registration module (A3 module 1, A4: UserCredentials + UserProfile tables).
/// Passwords are hashed with ASP.NET Core's PasswordHasher (PBKDF2) - plain text is never stored.
/// </summary>
public class AccountService
{
    public const int MaxEmailLength = 100;
    public const int MinPasswordLength = 6;
    public const int MaxPasswordLength = 100;
    public const int MaxNameLength = 50;
    public const int MaxPhoneLength = 20;

    private static readonly Regex EmailPattern = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$");

    private readonly IUserRepository _users;
    private readonly PasswordHasher<string> _hasher = new();
    private readonly object _lock = new();

    /// <summary>In-memory accounts with the two demo logins (unit tests / no database).</summary>
    public AccountService() : this(new InMemoryUserRepository())
    {
        Register(new RegisterRequest("Test", "Patient", null, null, "patient@test.com", "password1"));
        CreateAccount("admin@test.com", "admin123", "Clinic", "Admin", null, null, "Admin");
    }

    /// <summary>Database accounts (demo logins come from the seed data in schema.sql).</summary>
    public AccountService(IUserRepository users) => _users = users;

    public AccountInfo Register(RegisterRequest? request)
    {
        if (request is null)
            throw new ApiValidationException("body", "Request body is required.");

        var errors = new Dictionary<string, string>();
        ValidateEmail(errors, request.Email);
        if (string.IsNullOrEmpty(request.Password))
            errors["password"] = "Password is required.";
        else if (request.Password.Trim().Length < MinPasswordLength)
            errors["password"] = $"Password must be at least {MinPasswordLength} characters.";
        else if (request.Password.Length > MaxPasswordLength)
            errors["password"] = $"Password must be {MaxPasswordLength} characters or fewer.";
        if ((request.FirstName?.Trim().Length ?? 0) > MaxNameLength)
            errors["firstName"] = $"First name must be {MaxNameLength} characters or fewer.";
        if ((request.LastName?.Trim().Length ?? 0) > MaxNameLength)
            errors["lastName"] = $"Last name must be {MaxNameLength} characters or fewer.";
        if ((request.Phone?.Trim().Length ?? 0) > MaxPhoneLength)
            errors["phone"] = $"Phone must be {MaxPhoneLength} characters or fewer.";
        if (request.DateOfBirth is { } dob && dob.Date > DateTime.UtcNow.Date)
            errors["dateOfBirth"] = "Date of birth cannot be in the future.";
        InputRules.ThrowIfInvalid(errors);

        var email = Normalize(request.Email!);
        // the register page only asks for an email, so the name defaults to the part before "@"
        var firstName = string.IsNullOrWhiteSpace(request.FirstName) ? email.Split('@')[0] : request.FirstName.Trim();
        if (firstName.Length > MaxNameLength) firstName = firstName[..MaxNameLength];
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        return CreateAccount(email, request.Password!, firstName, request.LastName?.Trim() ?? "", phone, request.DateOfBirth, "User");
    }

    /// <summary>Returns the user when the email and password match, or null (-> 401).</summary>
    public AccountInfo? Login(LoginRequest? request)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(request?.Email)) errors["email"] = "Email is required.";
        if (string.IsNullOrEmpty(request?.Password)) errors["password"] = "Password is required.";
        InputRules.ThrowIfInvalid(errors);

        var email = Normalize(request!.Email!);
        if (email.Length > MaxEmailLength || request.Password!.Length > MaxPasswordLength) return null;

        var user = _users.FindByEmail(email);
        if (user is null) return null;

        var result = _hasher.VerifyHashedPassword(email, user.PasswordHash, request.Password);
        return result == PasswordVerificationResult.Failed ? null : ToInfo(user);
    }

    public AccountInfo GetProfile(int userId) =>
        ToInfo(_users.FindById(userId) ?? throw new KeyNotFoundException($"User {userId} was not found."));

    // ---------- helpers ----------

    private AccountInfo CreateAccount(string email, string password, string firstName, string lastName,
        string? phone, DateTime? dateOfBirth, string role)
    {
        lock (_lock)
        {
            if (_users.FindByEmail(email) is not null)
                throw new InvalidOperationException("An account with that email already exists.");

            var account = new UserAccount
            {
                Email = email,
                PasswordHash = _hasher.HashPassword(email, password),
                Role = role,
                FirstName = firstName,
                LastName = lastName,
                Phone = phone,
                DateOfBirth = dateOfBirth?.Date
            };
            _users.Create(account);
            return ToInfo(account);
        }
    }

    private static void ValidateEmail(IDictionary<string, string> errors, string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            errors["email"] = "Email is required.";
        else if (email.Trim().Length > MaxEmailLength)
            errors["email"] = $"Email must be {MaxEmailLength} characters or fewer.";
        else if (!EmailPattern.IsMatch(email.Trim()))
            errors["email"] = "Please enter a valid email address.";
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static AccountInfo ToInfo(UserAccount u)
    {
        var name = $"{u.FirstName} {u.LastName}".Trim();
        return new AccountInfo(u.UserId, u.UserId, u.Email, name.Length > 0 ? name : u.Email,
            u.FirstName, u.LastName, u.Phone, u.Role);
    }
}
