namespace Yurt.Application.Features.Workers;

public record WorkerDto(Guid Id, string Username, string Role, bool IsActive, DateTime CreatedAt);

public record CreateWorkerDto
{
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    /// <summary>"Worker" (default) or "Admin". Only the CEO can add an Admin.</summary>
    public string? Role { get; init; }
}

public record UpdateWorkerDto
{
    public string Username { get; init; } = "";
    public bool IsActive { get; init; }
}

public record ResetWorkerPasswordDto
{
    public string NewPassword { get; init; } = "";
}

public record SetWorkerActiveDto(bool IsActive);
