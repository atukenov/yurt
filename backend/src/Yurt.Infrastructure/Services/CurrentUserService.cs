using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Yurt.Application.Common.Interfaces;

namespace Yurt.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor)
        => _accessor = accessor;

    public Guid? UserId
    {
        get
        {
            var value = _accessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Username
        => _accessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);

    public string? UserType
        => _accessor.HttpContext?.User?.FindFirstValue("user_type");

    public string? Role
        => _accessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);

    public bool IsAuthenticated
        => _accessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
