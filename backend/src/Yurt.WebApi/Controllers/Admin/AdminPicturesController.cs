using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Pictures;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>
/// Picture upload for the Brand screen (logo, icon, splash) and for menu and promotion photos. The answer holds the
/// picture's public link, which the admin app puts in the usual picture field and saves as before.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/pictures")]
[Authorize(Policy = "AdminOnly")]
public class AdminPicturesController(PictureService pictures) : ApiControllerBase
{
    [HttpPost]
    [RequestSizeLimit(PictureService.MaxBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = PictureService.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null) return BadRequest(new ProblemDetails { Title = "Choose a picture to upload." });
        if (file.Length > PictureService.MaxBytes)
            return StatusCode(413, new ProblemDetails { Status = 413, Title = "The picture is too big (max 2 MB)." });
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        return ToResult(await pictures.UploadAsync(buffer.ToArray(), ct));
    }
}
