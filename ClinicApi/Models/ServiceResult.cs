namespace ClinicApi.Models;

// Shared result type any module can use to return success or validation errors.
public enum ResultStatus
{
    Ok,
    Invalid,        // 400 - validation failed
    Conflict,       // 409 - e.g. duplicate
    Unauthorized,   // 401
    NotFound        // 404
}

public class ServiceResult<T>
{
    public ResultStatus Status { get; private init; }
    public T? Value { get; private init; }
    public List<string> Errors { get; private init; } = new();

    public bool Success => Status == ResultStatus.Ok;

    public static ServiceResult<T> Ok(T value) => new() { Status = ResultStatus.Ok, Value = value };

    public static ServiceResult<T> Fail(ResultStatus status, params string[] errors) =>
        new() { Status = status, Errors = errors.ToList() };

    public static ServiceResult<T> Fail(ResultStatus status, List<string> errors) =>
        new() { Status = status, Errors = errors };
}
