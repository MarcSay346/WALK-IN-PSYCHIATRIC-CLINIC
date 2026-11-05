using System;
using System.Data;
using ClinicApi.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace ClinicApi.Tests;

/// <summary>
/// A fresh in-memory SQLite database for one test class instance.
/// It has the same tables, keys and constraints as database/schema.sql (SQLite syntax),
/// so the SQL repositories can be tested without a SQL Server.
///
/// Usage in a test class:
///   private readonly TestDatabase _db = new();
///   ... new SqlNotificationRepository(_db) ...
///   public void Dispose() => _db.Dispose();
/// </summary>
public sealed class TestDatabase : IDbConnectionFactory, IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _keepAlive;   // the in-memory database lives while this stays open

    public TestDatabase()
    {
        _connectionString = $"Data Source=test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
        _keepAlive.Execute(Schema);
    }

    public IDbConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        connection.Execute("PRAGMA foreign_keys = ON;");
        return connection;
    }

    public string SelectLastId => "SELECT last_insert_rowid();";

    public void Dispose() => _keepAlive.Dispose();

    // ---------- helpers for setting up test data ----------

    /// <summary>Adds a login + profile and returns the new UserId.</summary>
    public int AddUser(string email, string role = "User", string firstName = "Test", string lastName = "User")
    {
        using var db = Open();
        var id = db.ExecuteScalar<int>(
            "INSERT INTO UserCredentials (Email, PasswordHash, Role) VALUES (@email, 'hash', @role); SELECT last_insert_rowid();",
            new { email, role });
        db.Execute("INSERT INTO UserProfile (UserId, Email, FirstName, LastName) VALUES (@id, @email, @firstName, @lastName)",
            new { id, email, firstName, lastName });
        return id;
    }

    /// <summary>Adds a service and returns the new ServiceId.</summary>
    public int AddService(string name, int durationMinutes = 30, string priority = "Medium")
    {
        using var db = Open();
        return db.ExecuteScalar<int>(
            @"INSERT INTO Service (Name, Description, DurationMinutes, Priority)
              VALUES (@name, 'Test service', @durationMinutes, @priority); SELECT last_insert_rowid();",
            new { name, durationMinutes, priority });
    }

    public T Scalar<T>(string sql, object? args = null)
    {
        using var db = Open();
        return db.ExecuteScalar<T>(sql, args);
    }

    // Same design as database/schema.sql, written for SQLite (dates are stored as TEXT).
    private const string Schema = @"
CREATE TABLE UserCredentials (
    UserId       INTEGER PRIMARY KEY AUTOINCREMENT,
    Email        TEXT NOT NULL UNIQUE CHECK (length(Email) <= 100 AND Email LIKE '%_@_%._%'),
    PasswordHash TEXT NOT NULL,
    Role         TEXT NOT NULL DEFAULT 'User' CHECK (Role IN ('User','Admin')),
    CreatedAt    TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE UserProfile (
    UserId           INTEGER PRIMARY KEY REFERENCES UserCredentials(UserId),
    Email            TEXT NOT NULL UNIQUE REFERENCES UserCredentials(Email),
    FirstName        TEXT NOT NULL CHECK (length(FirstName) <= 50),
    LastName         TEXT NOT NULL CHECK (length(LastName) <= 50),
    DateOfBirth      TEXT NULL,
    Phone            TEXT NULL CHECK (Phone IS NULL OR length(Phone) <= 20),
    QueueUpdates     INTEGER NOT NULL DEFAULT 1,
    StatusChanges    INTEGER NOT NULL DEFAULT 1,
    AlertWithinSpots INTEGER NOT NULL DEFAULT 3 CHECK (AlertWithinSpots BETWEEN 1 AND 10),
    Popups           INTEGER NOT NULL DEFAULT 1
);
CREATE TABLE Service (
    ServiceId       INTEGER PRIMARY KEY AUTOINCREMENT,
    Name            TEXT NOT NULL UNIQUE COLLATE NOCASE CHECK (length(Name) <= 100),
    Description     TEXT NOT NULL CHECK (length(Description) <= 500),
    DurationMinutes INTEGER NOT NULL CHECK (DurationMinutes BETWEEN 1 AND 480),
    Priority        TEXT NOT NULL CHECK (Priority IN ('Low','Medium','High'))
);
CREATE TABLE Queue (
    QueueId   INTEGER PRIMARY KEY AUTOINCREMENT,
    ServiceId INTEGER NOT NULL REFERENCES Service(ServiceId),
    Status    TEXT NOT NULL DEFAULT 'Open' CHECK (Status IN ('Open','Closed')),
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE UNIQUE INDEX UX_Queue_OpenPerService ON Queue(ServiceId) WHERE Status = 'Open';
CREATE TABLE QueueEntry (
    EntryId     INTEGER PRIMARY KEY AUTOINCREMENT,
    QueueId     INTEGER NOT NULL REFERENCES Queue(QueueId),
    UserId      INTEGER NOT NULL REFERENCES UserCredentials(UserId),
    DisplayName TEXT NOT NULL CHECK (length(DisplayName) <= 100),
    Contact     TEXT NULL CHECK (Contact IS NULL OR length(Contact) <= 100),
    Priority    INTEGER NOT NULL DEFAULT 3 CHECK (Priority BETWEEN 1 AND 3),
    Position    INTEGER NOT NULL CHECK (Position >= 1),
    JoinTime    TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Status      TEXT NOT NULL DEFAULT 'Waiting' CHECK (Status IN ('Waiting','Served','Canceled')),
    EndedAt     TEXT NULL
);
CREATE UNIQUE INDEX UX_QueueEntry_WaitingOnce ON QueueEntry(QueueId, UserId) WHERE Status = 'Waiting';
CREATE TABLE Notification (
    NotificationId INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId         INTEGER NOT NULL REFERENCES UserCredentials(UserId),
    Type           TEXT NOT NULL DEFAULT 'queue' CHECK (Type IN ('queue','status')),
    Title          TEXT NOT NULL CHECK (length(Title) <= 100),
    Message        TEXT NOT NULL CHECK (length(Message) <= 500),
    Status         TEXT NOT NULL DEFAULT 'Sent' CHECK (Status IN ('Sent','Viewed')),
    CreatedAt      TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE VisitHistory (
    HistoryId   INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId      INTEGER NOT NULL REFERENCES UserCredentials(UserId),
    EntryId     INTEGER NULL REFERENCES QueueEntry(EntryId),
    ServiceName TEXT NOT NULL CHECK (length(ServiceName) <= 100),
    JoinedAt    TEXT NOT NULL,
    EndedAt     TEXT NULL,
    Outcome     TEXT NOT NULL DEFAULT 'Waiting' CHECK (Outcome IN ('Waiting','Served','Left'))
);";
}
