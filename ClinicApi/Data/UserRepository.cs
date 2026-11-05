using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;

namespace ClinicApi.Data;

/// <summary>One user: login info (UserCredentials) + personal details (UserProfile).</summary>
public class UserAccount
{
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "User";        // "User" or "Admin"
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
}

/// <summary>Where user accounts are stored.</summary>
public interface IUserRepository
{
    /// <summary>Email must already be trimmed and lower-case.</summary>
    UserAccount? FindByEmail(string email);
    UserAccount? FindById(int userId);

    /// <summary>Saves the login and the profile together and returns the new UserId.</summary>
    int Create(UserAccount account);
}

/// <summary>A3 behaviour: accounts in memory (used by the unit tests and when no database is set).</summary>
public class InMemoryUserRepository : IUserRepository
{
    private readonly List<UserAccount> _users = new();
    private int _nextId = 1;

    public UserAccount? FindByEmail(string email) => _users.FirstOrDefault(u => u.Email == email);

    public UserAccount? FindById(int userId) => _users.FirstOrDefault(u => u.UserId == userId);

    public int Create(UserAccount account)
    {
        account.UserId = _nextId++;
        _users.Add(account);
        return account.UserId;
    }
}

/// <summary>A4: UserCredentials + UserProfile tables.</summary>
public class SqlUserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlUserRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string SelectAccount =
        @"SELECT c.UserId, c.Email, c.PasswordHash, c.Role,
                 COALESCE(p.FirstName, '') AS FirstName, COALESCE(p.LastName, '') AS LastName,
                 p.Phone, p.DateOfBirth
          FROM UserCredentials c
          LEFT JOIN UserProfile p ON p.UserId = c.UserId ";

    public UserAccount? FindByEmail(string email)
    {
        using var db = _factory.Open();
        return db.QuerySingleOrDefault<UserAccount>(SelectAccount + "WHERE c.Email = @email", new { email });
    }

    public UserAccount? FindById(int userId)
    {
        using var db = _factory.Open();
        return db.QuerySingleOrDefault<UserAccount>(SelectAccount + "WHERE c.UserId = @userId", new { userId });
    }

    public int Create(UserAccount account)
    {
        using var db = _factory.Open();
        using var tx = db.BeginTransaction();   // login + profile are saved together or not at all

        var id = db.ExecuteScalar<int>(
            "INSERT INTO UserCredentials (Email, PasswordHash, Role) VALUES (@Email, @PasswordHash, @Role);" + _factory.SelectLastId,
            account, tx);

        db.Execute(
            @"INSERT INTO UserProfile (UserId, Email, FirstName, LastName, Phone, DateOfBirth)
              VALUES (@id, @Email, @FirstName, @LastName, @Phone, @DateOfBirth)",
            new { id, account.Email, account.FirstName, account.LastName, account.Phone, account.DateOfBirth }, tx);

        tx.Commit();
        account.UserId = id;
        return id;
    }
}
