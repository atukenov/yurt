using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.GroupOrders;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/group-orders")]
[Authorize(Policy = "CustomerOnly")]
[RequireModule(ModuleKeys.GroupOrders)]
public class GroupOrderController : ApiControllerBase
{
    private readonly GroupOrderService _service;
    private readonly ICurrentUserService _currentUser;

    public GroupOrderController(GroupOrderService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpPost]
    [BlockWhenCafeLimited]
    public async Task<IActionResult> Create([FromBody] CreateGroupCartRequest req, CancellationToken ct)
        => ToResult(await _service.CreateAsync(req.LocationId, _currentUser.UserId!.Value, ct));

    [HttpPost("join")]
    [BlockWhenCafeLimited]
    public async Task<IActionResult> Join([FromBody] JoinGroupCartRequest req, CancellationToken ct)
        => ToResult(await _service.JoinAsync(req.Code, _currentUser.UserId!.Value, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => ToResult(await _service.GetAsync(id, _currentUser.UserId!.Value, ct));

    [HttpPost("{id:guid}/items")]
    [BlockWhenCafeLimited]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddGroupCartItemRequest req, CancellationToken ct)
        => ToResult(await _service.AddItemAsync(id, _currentUser.UserId!.Value, req, ct));

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid id, Guid itemId, CancellationToken ct)
        => ToResult(await _service.RemoveItemAsync(id, itemId, _currentUser.UserId!.Value, ct));

    [HttpPost("{id:guid}/checkout")]
    [BlockWhenCafeLimited]
    public async Task<IActionResult> Checkout(Guid id, CancellationToken ct)
        => ToResult(await _service.CheckoutAsync(id, _currentUser.UserId!.Value, ct));
}
