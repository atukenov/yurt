namespace Yurt.Application.Features.Locations.DTOs;

// Customer-facing (localized name)
public record LocationDto(
    Guid Id,
    string Name,
    string Address,
    string WorkingHours,
    string ContactPhone,
    bool IsActive,
    string? TwoGisUrl = null);

// Admin-facing
public record AdminLocationDto(
    Guid Id,
    string Name,
    string Address,
    string WorkingHours,
    string ContactPhone,
    bool IsActive,
    Guid? IikoTerminalGroupId = null,
    string? TwoGisUrl = null);

public record CreateLocationDto
{
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string WorkingHours { get; init; } = string.Empty;
    public string ContactPhone { get; init; } = string.Empty;
    public Guid? IikoTerminalGroupId { get; init; }
    public string? TwoGisUrl { get; init; }
}

public record UpdateLocationDto
{
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string WorkingHours { get; init; } = string.Empty;
    public string ContactPhone { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public Guid? IikoTerminalGroupId { get; init; }
    public string? TwoGisUrl { get; init; }
}
