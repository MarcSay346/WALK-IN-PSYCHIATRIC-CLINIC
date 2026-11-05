using System;
using System.Collections.Generic;
using ClinicApi.Data;
using ClinicApi.Endpoints;
using ClinicApi.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ClinicApi.Tests;

/// <summary>A4: login/registration stored in UserCredentials + UserProfile (SQLite test copy of the schema).</summary>
public class AccountServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly AccountService _accounts;

    public AccountServiceTests() => _accounts = new AccountService(new SqlUserRepository(_db));

    public void Dispose() => _db.Dispose();

    private static RegisterRequest Req(string? email = "new@test.com", string? password = "secret1",
        string? first = null, string? last = null, string? phone = null, DateTime? dob = null) =>
        new(first, last, dob, phone, email, password);

    private static int StatusOf(IResult result) => ((IStatusCodeHttpResult)result).StatusCode ?? 0;

    // ---------- registration ----------

    [Fact]
    public void Register_SavesCredentialsAndProfile()
    {
        var user = _accounts.Register(Req(first: "Ana", last: "Lopez", phone: "713-555-0000"));

        Assert.True(user.UserId > 0);
        Assert.Equal(user.UserId, user.PatientId);
        Assert.Equal("User", user.Role);
        Assert.Equal("Ana Lopez", user.Name);
        Assert.Equal(1, _db.Scalar<int>("SELECT COUNT(*) FROM UserCredentials WHERE Email = 'new@test.com'"));
        Assert.Equal("Ana", _db.Scalar<string>("SELECT FirstName FROM UserProfile WHERE UserId = @UserId", new { user.UserId }));
    }

    [Fact]
    public void Register_StoresHashedPassword_NotPlainText()
    {
        var user = _accounts.Register(Req());

        var stored = _db.Scalar<string>("SELECT PasswordHash FROM UserCredentials WHERE UserId = @UserId", new { user.UserId });
        Assert.NotEqual("secret1", stored);
        Assert.DoesNotContain("secret1", stored);
        Assert.True(stored.Length > 40);
    }

    [Fact]
    public void Register_NormalizesEmail_AndDefaultsNameFromEmail()
    {
        var user = _accounts.Register(Req(email: "  Sam.Rivera@Test.com "));

        Assert.Equal("sam.rivera@test.com", user.Email);
        Assert.Equal("sam.rivera", user.FirstName);
    }

    [Fact]
    public void Register_DuplicateEmail_IgnoringCase_IsRejected()
    {
        _accounts.Register(Req());
        Assert.Throws<InvalidOperationException>(() => _accounts.Register(Req(email: "NEW@test.com")));
        Assert.Equal(1, _db.Scalar<int>("SELECT COUNT(*) FROM UserCredentials"));
    }

    [Theory]
    [InlineData(null, "secret1", "email")]
    [InlineData("   ", "secret1", "email")]
    [InlineData("not-an-email", "secret1", "email")]
    [InlineData("a@b.com", null, "password")]
    [InlineData("a@b.com", "12345", "password")]
    public void Register_InvalidInput_ReportsField(string? email, string? password, string field)
    {
        var ex = Assert.Throws<ApiValidationException>(() => _accounts.Register(Req(email, password)));
        Assert.True(ex.Errors.ContainsKey(field));
    }

    [Fact]
    public void Register_FieldLengthLimits()
    {
        var longText = new string('x', 51);
        var ex = Assert.Throws<ApiValidationException>(() =>
            _accounts.Register(Req(email: new string('a', 95) + "@b.com", password: new string('p', 101),
                first: longText, last: longText, phone: new string('1', 21))));

        Assert.True(ex.Errors.ContainsKey("email"));
        Assert.True(ex.Errors.ContainsKey("password"));
        Assert.True(ex.Errors.ContainsKey("firstName"));
        Assert.True(ex.Errors.ContainsKey("lastName"));
        Assert.True(ex.Errors.ContainsKey("phone"));
    }

    [Fact]
    public void Register_FutureBirthDate_IsRejected()
    {
        var ex = Assert.Throws<ApiValidationException>(() => _accounts.Register(Req(dob: DateTime.UtcNow.AddDays(2))));
        Assert.True(ex.Errors.ContainsKey("dateOfBirth"));
    }

    [Fact]
    public void Register_NullBody_IsRejected()
    {
        Assert.Throws<ApiValidationException>(() => _accounts.Register(null));
    }

    // ---------- login ----------

    [Fact]
    public void Login_WithCorrectPassword_ReturnsUser()
    {
        var created = _accounts.Register(Req());

        var user = _accounts.Login(new LoginRequest(" NEW@test.com ", "secret1"));

        Assert.NotNull(user);
        Assert.Equal(created.UserId, user!.UserId);
    }

    [Fact]
    public void Login_PersistsAcrossServiceInstances()
    {
        _accounts.Register(Req());
        var fresh = new AccountService(new SqlUserRepository(_db));
        Assert.NotNull(fresh.Login(new LoginRequest("new@test.com", "secret1")));
    }

    [Fact]
    public void Login_WrongPasswordOrUnknownEmail_ReturnsNull()
    {
        _accounts.Register(Req());

        Assert.Null(_accounts.Login(new LoginRequest("new@test.com", "wrong-password")));
        Assert.Null(_accounts.Login(new LoginRequest("nobody@test.com", "secret1")));
        Assert.Null(_accounts.Login(new LoginRequest(new string('a', 120) + "@b.com", "secret1")));
    }

    [Fact]
    public void Login_MissingFields_Throws()
    {
        var ex = Assert.Throws<ApiValidationException>(() => _accounts.Login(new LoginRequest("", "")));
        Assert.True(ex.Errors.ContainsKey("email"));
        Assert.True(ex.Errors.ContainsKey("password"));
        Assert.Throws<ApiValidationException>(() => _accounts.Login(null));
    }

    [Fact]
    public void GetProfile_ReturnsUser_Or404()
    {
        var created = _accounts.Register(Req(first: "Ana"));

        Assert.Equal("Ana", _accounts.GetProfile(created.UserId).FirstName);
        Assert.Throws<KeyNotFoundException>(() => _accounts.GetProfile(9999));
    }

    // ---------- in-memory mode (no database) keeps the demo logins ----------

    [Fact]
    public void InMemory_DemoAccountsCanLogIn()
    {
        var memory = new AccountService();

        Assert.Equal("User", memory.Login(new LoginRequest("patient@test.com", "password1"))!.Role);
        Assert.Equal("Admin", memory.Login(new LoginRequest("admin@test.com", "admin123"))!.Role);
    }

    // ---------- endpoint status codes ----------

    [Fact]
    public void Endpoints_ReturnRightStatusCodes()
    {
        Assert.Equal(200, StatusOf(AccountEndpoints.Register(Req(), _accounts)));
        Assert.Equal(409, StatusOf(AccountEndpoints.Register(Req(), _accounts)));
        Assert.Equal(400, StatusOf(AccountEndpoints.Register(Req(email: "bad"), _accounts)));

        Assert.Equal(200, StatusOf(AccountEndpoints.Login(new LoginRequest("new@test.com", "secret1"), _accounts)));
        Assert.Equal(401, StatusOf(AccountEndpoints.Login(new LoginRequest("new@test.com", "nope123"), _accounts)));
        Assert.Equal(400, StatusOf(AccountEndpoints.Login(new LoginRequest("", ""), _accounts)));

        Assert.Equal(404, StatusOf(AccountEndpoints.GetProfile(9999, _accounts)));
    }
}
