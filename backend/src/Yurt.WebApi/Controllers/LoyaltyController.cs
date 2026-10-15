using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty.Services;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/loyalty")]
[Authorize(Policy = "CustomerOnly")]
public class LoyaltyController : ApiControllerBase
{
    private readonly LoyaltyService _loyalty;
    private readonly ICurrentUserService _currentUser;

    public LoyaltyController(LoyaltyService loyalty, ICurrentUserService currentUser)
    {
        _loyalty = loyalty;
        _currentUser = currentUser;
    }

    /// <summary>Current customer's iiko loyalty balance. Degrades gracefully when iiko is unavailable.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyBalance(CancellationToken ct)
        => Ok(await _loyalty.GetBalanceAsync(_currentUser.UserId!.Value, linkIfMissing: true, ct));

    /// <summary>
    /// Offsite (in-shop counter) bonus history — app-order earn/spend already appears in
    /// the order history endpoints. Degrades gracefully when iiko is unavailable.
    /// </summary>
    [HttpGet("transactions")]
    public async Task<IActionResult> GetMyTransactions(CancellationToken ct)
        => Ok(await _loyalty.GetTransactionHistoryAsync(_currentUser.UserId!.Value, ct));
}
