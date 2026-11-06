using System;
using System.Collections.Generic;
using System.Linq;
using ClinicApi.Services;
using Dapper;

namespace ClinicApi.Data;

/// <summary>Where the clinic's services are stored.</summary>
public interface IServiceRepository
{
    List<ClinicService> List();
    ClinicService? Get(int id);

    /// <summary>Name match ignores upper/lower case.</summary>
    ClinicService? FindByName(string name);

    /// <summary>Saves a new service and returns it with its new Id.</summary>
    ClinicService Add(ClinicService service);
    void Update(ClinicService service);
}

/// <summary>A3 behaviour: services in memory (used by the unit tests and when no database is set).</summary>
public class InMemoryServiceRepository : IServiceRepository
{
    private readonly List<ClinicService> _services = new();
    private int _nextId = 1;

    public List<ClinicService> List() => _services.ToList();

    public ClinicService? Get(int id) => _services.FirstOrDefault(s => s.Id == id);

    public ClinicService? FindByName(string name) =>
        _services.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public ClinicService Add(ClinicService service)
    {
        var saved = service with { Id = _nextId++ };
        _services.Add(saved);
        return saved;
    }

    public void Update(ClinicService service)
    {
        var index = _services.FindIndex(s => s.Id == service.Id);
        if (index >= 0) _services[index] = service;
    }
}

/// <summary>A4: Service table.</summary>
public class SqlServiceRepository : IServiceRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlServiceRepository(IDbConnectionFactory factory) => _factory = factory;

    private class ServiceRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int DurationMinutes { get; set; }
        public string Priority { get; set; } = "";
        public ClinicService ToModel() => new(Id, Name, Description, DurationMinutes, Priority);
    }

    private const string SelectColumns =
        "SELECT ServiceId AS Id, Name, Description, DurationMinutes, Priority FROM Service ";

    public List<ClinicService> List()
    {
        using var db = _factory.Open();
        return db.Query<ServiceRow>(SelectColumns + "ORDER BY ServiceId").Select(r => r.ToModel()).ToList();
    }

    public ClinicService? Get(int id)
    {
        using var db = _factory.Open();
        return db.QuerySingleOrDefault<ServiceRow>(SelectColumns + "WHERE ServiceId = @id", new { id })?.ToModel();
    }

    public ClinicService? FindByName(string name)
    {
        using var db = _factory.Open();
        return db.Query<ServiceRow>(SelectColumns + "WHERE LOWER(Name) = LOWER(@name)", new { name })
                 .FirstOrDefault()?.ToModel();
    }

    public ClinicService Add(ClinicService service)
    {
        using var db = _factory.Open();
        var id = db.ExecuteScalar<int>(
            @"INSERT INTO Service (Name, Description, DurationMinutes, Priority)
              VALUES (@Name, @Description, @DurationMinutes, @Priority);" + _factory.SelectLastId,
            new { service.Name, service.Description, service.DurationMinutes, service.Priority });
        return service with { Id = id };
    }

    public void Update(ClinicService service)
    {
        using var db = _factory.Open();
        db.Execute(
            @"UPDATE Service SET Name = @Name, Description = @Description,
                     DurationMinutes = @DurationMinutes, Priority = @Priority
              WHERE ServiceId = @Id",
            new { service.Id, service.Name, service.Description, service.DurationMinutes, service.Priority });
    }
}
