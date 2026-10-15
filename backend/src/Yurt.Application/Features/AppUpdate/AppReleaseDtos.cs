namespace Yurt.Application.Features.AppUpdate;

public record AppReleaseDto(
    Guid Id,
    string Version,
    string NotesEn,
    string NotesRu,
    string NotesKk,
    bool IsMandatory,
    DateTime CreatedAt);

public record SaveAppReleaseDto
{
    public string Version { get; init; } = string.Empty;
    public string NotesEn { get; init; } = string.Empty;
    public string NotesRu { get; init; } = string.Empty;
    public string NotesKk { get; init; } = string.Empty;
    public bool IsMandatory { get; init; }
}
