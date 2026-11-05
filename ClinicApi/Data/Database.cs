using System;
using System.Collections.Generic;
using System.Data;
using ClinicApi.Services;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicApi.Data;

/// <summary>
/// Opens database connections. The app uses SQL Server; the unit tests use an
/// in-memory SQLite database through the same interface.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Returns an open connection. Dispose it when done (use "using var db = ...").</summary>
    IDbConnection Open();

    /// <summary>SQL that returns the id of the row just inserted (differs between SQL Server and SQLite).</summary>
    string SelectLastId { get; }
}

public class SqlServerConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlServerConnectionFactory(string connectionString) => _connectionString = connectionString;

    public IDbConnection Open()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public string SelectLastId => "SELECT CAST(SCOPE_IDENTITY() AS INT);";
}

public static class DatabaseSetup
{
    /// <summary>
    /// Registers the SQL Server connection when "ConnectionStrings:ClinicDb" is set
    /// (in user secrets: dotnet user-secrets set "ConnectionStrings:ClinicDb" "...").
    /// Without it the app falls back to the in-memory A3 stores, so it still runs.
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ClinicDb");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IDbConnectionFactory>(new SqlServerConnectionFactory(connectionString));
            Console.WriteLine("[Database] Using SQL Server (ClinicDb).");
        }
        else
        {
            Console.WriteLine("[Database] No ClinicDb connection string found - using in-memory data.");
        }
        return services;
    }

    /// <summary>Picks the SQL version of a store when a database is configured, otherwise the in-memory one.</summary>
    public static T Pick<T>(IServiceProvider provider, Func<IDbConnectionFactory, T> sql, Func<T> inMemory)
    {
        var db = provider.GetService<IDbConnectionFactory>();
        return db is null ? inMemory() : sql(db);
    }
}

/// <summary>
/// The front end identifies a user by their numeric user id (from /api/login) or by email.
/// The database stores the numeric UserId, so this turns either one into the id.
/// </summary>
public static class DbUsers
{
    public static int? TryResolve(IDbConnection db, string user, IDbTransaction? tx = null)
    {
        var value = user.Trim();
        if (int.TryParse(value, out var id))
            return db.ExecuteScalar<int?>("SELECT UserId FROM UserCredentials WHERE UserId = @id", new { id }, tx);

        return db.ExecuteScalar<int?>("SELECT UserId FROM UserCredentials WHERE Email = @email",
            new { email = value.ToLowerInvariant() }, tx);
    }

    public static int Resolve(IDbConnection db, string user, IDbTransaction? tx = null) =>
        TryResolve(db, user, tx) ?? throw new KeyNotFoundException($"User '{user.Trim()}' was not found.");
}
