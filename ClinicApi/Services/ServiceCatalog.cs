using ClinicApi.Data;

namespace ClinicApi.Services;

/// <summary>A clinic service (provider/visit type) the admin can create, update and list.</summary>
public record ClinicService(int Id, string Name, string Description, int DurationMinutes, string Priority);

/// <summary>Body for POST /api/services and PUT /api/services/{id}.</summary>
public record ServiceRequest(string? Name, string? Description, int? DurationMinutes, string? Priority);

/// <summary>
/// Service Management module (A3 module 2). Each service has a name, description,
/// expected duration and priority level.
/// A4: stored through an IServiceRepository (Service table in SQL Server, in memory in the unit tests).
/// </summary>
public class ServiceCatalog
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;
    public const int MaxDurationMinutes = 480;
    public static readonly string[] Priorities = { "Low", "Medium", "High" };

    private readonly IServiceRepository _store;
    private readonly object _lock = new();

    /// <summary>Database version (the starting services come from the seed data in schema.sql).</summary>
    public ServiceCatalog(IServiceRepository store) => _store = store;

    /// <summary>In-memory version with the starting services (unit tests / no database).</summary>
    public ServiceCatalog() : this(new InMemoryServiceRepository())
    {
        // same starting services the admin pages show
        Create(new ServiceRequest("Dr. Nate", "General counseling visit", 60, "High"));
        Create(new ServiceRequest("Dr. Valdez", "General counseling visit", 45, "Medium"));
        Create(new ServiceRequest("Dr. Cole", "General counseling visit", 30, "Low"));
    }

    public IReadOnlyList<ClinicService> List()
    {
        lock (_lock) return _store.List();
    }

    public ClinicService Get(int id)
    {
        lock (_lock)
            return _store.Get(id)
                ?? throw new KeyNotFoundException($"Service {id} was not found.");
    }

    public ClinicService Create(ServiceRequest? request)
    {
        var clean = Validate(request);
        lock (_lock)
        {
            if (_store.FindByName(clean.Name) is not null)
                throw new InvalidOperationException($"A service named '{clean.Name}' already exists.");

            return _store.Add(clean);
        }
    }

    public ClinicService Update(int id, ServiceRequest? request)
    {
        var clean = Validate(request);
        lock (_lock)
        {
            if (_store.Get(id) is null)
                throw new KeyNotFoundException($"Service {id} was not found.");
            var sameName = _store.FindByName(clean.Name);
            if (sameName is not null && sameName.Id != id)
                throw new InvalidOperationException($"A service named '{clean.Name}' already exists.");

            var updated = clean with { Id = id };
            _store.Update(updated);
            return updated;
        }
    }

    // required fields, types/ranges and length limits; throws ApiValidationException (-> 400)
    private static ClinicService Validate(ServiceRequest? request)
    {
        if (request is null)
            throw new ApiValidationException("body", "Request body is required.");

        var errors = new Dictionary<string, string>();
        InputRules.RequiredText(errors, "name", "Service name", request.Name, MaxNameLength);
        InputRules.RequiredText(errors, "description", "Description", request.Description, MaxDescriptionLength);
        InputRules.IntInRange(errors, "durationMinutes", "Expected duration", request.DurationMinutes, 1, MaxDurationMinutes);

        var priority = Priorities.FirstOrDefault(p => p.Equals(request.Priority?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(request.Priority))
            errors["priority"] = "Priority is required.";
        else if (priority is null)
            errors["priority"] = "Priority must be Low, Medium, or High.";

        InputRules.ThrowIfInvalid(errors);

        return new ClinicService(0, request.Name!.Trim(), request.Description!.Trim(), request.DurationMinutes!.Value, priority!);
    }
}
