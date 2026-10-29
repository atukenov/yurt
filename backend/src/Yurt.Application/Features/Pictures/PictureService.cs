using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Pictures;

/// <summary>The public link of an uploaded picture, relative to the API address.</summary>
public record PictureDto(Guid Id, string Path, string ContentType, int Size);

/// <summary>A stored picture as it is sent to the browser.</summary>
public record PictureFile(byte[] Data, string ContentType);

/// <summary>
/// Pictures a cafe uploads for its logo, icon, splash screen, menu items and promotions. They live in the database next
/// to the rest of the cafe's data, so each cafe only ever sees and serves its own.
/// </summary>
public class PictureService(IApplicationDbContext db, IAuditLogService audit)
{
    /// <summary>Largest single file. The admin app shrinks big photos before sending, so this is rarely reached.</summary>
    public const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>All pictures of one cafe together, so one cafe cannot fill the shared database.</summary>
    public const long MaxBytesPerCafe = 200L * 1024 * 1024;

    public async Task<Result<PictureDto>> UploadAsync(byte[] data, CancellationToken ct = default)
    {
        if (data.Length == 0) return Result<PictureDto>.Failure("The file is empty.");
        if (data.Length > MaxBytes) return Result<PictureDto>.Failure("The picture is too big (max 2 MB).", 413);
        var type = Sniff(data);
        if (type is null) return Result<PictureDto>.Failure("Only PNG, JPEG and WebP pictures can be uploaded.", 415);

        var used = await db.Pictures.SumAsync(p => (long)p.Size, ct);
        if (used + data.Length > MaxBytesPerCafe)
            return Result<PictureDto>.Failure("This cafe has used all its picture space (200 MB).", 409);

        var picture = new Picture { ContentType = type, Data = data, Size = data.Length };
        db.Pictures.Add(picture);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("PictureUploaded", "Picture", picture.Id.ToString(), $"{type}, {data.Length} bytes", ct);
        return Result<PictureDto>.Success(new PictureDto(picture.Id, PathOf(picture.TenantId, picture.Id), type, data.Length), 201);
    }

    /// <summary>
    /// For the public link. A browser loading an &lt;img&gt; sends no cafe header, so the cafe comes from the link itself,
    /// and the picture must belong to that cafe.
    /// </summary>
    public async Task<PictureFile?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => await db.Pictures.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .Select(p => new PictureFile(p.Data, p.ContentType))
            .FirstOrDefaultAsync(ct);

    public static string PathOf(Guid tenantId, Guid id) => $"/api/v1/app/pictures/{tenantId}/{id}";

    /// <summary>The file type from the first bytes, or null for anything that is not PNG, JPEG or WebP.</summary>
    public static string? Sniff(ReadOnlySpan<byte> d)
    {
        if (d.Length >= 8 && d[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        if (d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return "image/jpeg";
        if (d.Length >= 12 && d[..4].SequenceEqual("RIFF"u8) && d[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
}
