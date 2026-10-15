namespace Yurt.Application.Common.Models;

public class Result<T>
{
    public bool Succeeded { get; private set; }
    public T? Data { get; private set; }
    public string? Error { get; private set; }
    public int StatusCode { get; private set; }
    public int? MinutesRemaining { get; private set; }

    private Result() { }

    public static Result<T> Success(T data, int statusCode = 200)
        => new() { Succeeded = true, Data = data, StatusCode = statusCode };

    public static Result<T> Failure(string error, int statusCode = 400)
        => new() { Succeeded = false, Error = error, StatusCode = statusCode };

    public static Result<T> Locked(int minutesRemaining)
        => new() { Succeeded = false, Error = "Account locked.", StatusCode = 423, MinutesRemaining = minutesRemaining };

    public static Result<T> NotFound(string error = "Not found")
        => new() { Succeeded = false, Error = error, StatusCode = 404 };

    public static Result<T> Unauthorized(string error = "Unauthorized")
        => new() { Succeeded = false, Error = error, StatusCode = 401 };

    public static Result<T> Forbidden(string error = "Forbidden")
        => new() { Succeeded = false, Error = error, StatusCode = 403 };
}

public class Result
{
    public bool Succeeded { get; private set; }
    public string? Error { get; private set; }
    public int StatusCode { get; private set; }

    private Result() { }

    public static Result Success(int statusCode = 200)
        => new() { Succeeded = true, StatusCode = statusCode };
    public static Result Failure(string error, int statusCode = 400)
        => new() { Succeeded = false, Error = error, StatusCode = statusCode };
}
