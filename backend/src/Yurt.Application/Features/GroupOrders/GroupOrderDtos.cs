using Yurt.Domain.Enums;

namespace Yurt.Application.Features.GroupOrders;

public record CreateGroupCartRequest(Guid LocationId);
public record JoinGroupCartRequest(string Code);
public record AddGroupCartItemRequest(
    Guid MenuItemId,
    string MenuItemName,
    decimal UnitPrice,
    int Quantity,
    List<GroupCartToppingDto> Toppings,
    string? Notes);

public record GroupCartToppingDto(Guid ToppingId, string ToppingName, decimal Price);

public record GroupCartDto(
    Guid Id,
    string Code,
    string LocationName,
    string LocationId,
    GroupCartStatus Status,
    DateTime ExpiresAt,
    bool IsCreator,
    List<GroupCartMemberDto> Members,
    List<GroupCartItemDto> Items);

public record GroupCartMemberDto(Guid CustomerId, string DisplayName);

public record GroupCartItemDto(
    Guid Id,
    Guid AddedByUserId,
    string AddedByName,
    Guid MenuItemId,
    string MenuItemName,
    decimal UnitPrice,
    int Quantity,
    List<GroupCartToppingDto> Toppings,
    string? Notes);
