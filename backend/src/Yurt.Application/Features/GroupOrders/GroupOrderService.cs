using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Orders.DTOs;
using Yurt.Application.Features.Orders.Services;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.GroupOrders;

public class GroupOrderService
{
    private readonly IApplicationDbContext _db;
    private readonly IOrdersHubService _hub;
    private readonly OrderService _orders;

    public GroupOrderService(IApplicationDbContext db, IOrdersHubService hub, OrderService orders)
    {
        _db = db;
        _hub = hub;
        _orders = orders;
    }

    public async Task<Result<GroupCartDto>> CreateAsync(Guid locationId, Guid userId, CancellationToken ct = default)
    {
        var location = await _db.Locations.FindAsync([locationId], ct);
        if (location == null || !location.IsActive)
            return Result<GroupCartDto>.Failure("Location not found or inactive.", 400);

        var user = await _db.CustomerUsers.FindAsync([userId], ct);
        if (user == null) return Result<GroupCartDto>.Failure("User not found.", 400);

        var code = GenerateCode();
        var cart = new GroupCart
        {
            Code = code,
            LocationId = locationId,
            CreatedByUserId = userId,
            Status = GroupCartStatus.Open,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
        };
        cart.Members.Add(new GroupCartMember
        {
            GroupCartId = cart.Id,
            CustomerUserId = userId,
            DisplayName = (user.FirstName + " " + user.LastName).Trim() is { Length: > 0 } name ? name : user.MobileNumber,
        });

        _db.GroupCarts.Add(cart);
        await _db.SaveChangesAsync(ct);

        cart.Location = location;
        return Result<GroupCartDto>.Success(MapToDto(cart, userId), 201);
    }

    public async Task<Result<GroupCartDto>> JoinAsync(string code, Guid userId, CancellationToken ct = default)
    {
        var cart = await LoadCartAsync(code: code, ct: ct);
        if (cart == null) return Result<GroupCartDto>.Failure("Group cart not found.", 404);
        if (cart.Status != GroupCartStatus.Open || cart.ExpiresAt < DateTime.UtcNow)
            return Result<GroupCartDto>.Failure("Group cart is no longer active.", 400);

        if (!cart.Members.Any(m => m.CustomerUserId == userId))
        {
            var user = await _db.CustomerUsers.FindAsync([userId], ct);
            if (user == null) return Result<GroupCartDto>.Failure("User not found.", 400);

            var displayName = (user.FirstName + " " + user.LastName).Trim() is { Length: > 0 } n ? n : user.MobileNumber;
            var member = new GroupCartMember
            {
                GroupCartId = cart.Id,
                CustomerUserId = userId,
                DisplayName = displayName,
            };
            // Add directly to DbSet to avoid EF marking the parent GroupCart as Modified,
            // which would cause a spurious UPDATE and DbUpdateConcurrencyException.
            _db.GroupCartMembers.Add(member);
            await _db.SaveChangesAsync(ct);
            cart.Members.Add(member); // keep in-memory list in sync for DTO mapping
        }

        var dto = MapToDto(cart, userId);
        await _hub.NotifyGroupCartUpdatedAsync(cart.Id, dto, ct);
        return Result<GroupCartDto>.Success(dto);
    }

    public async Task<Result<GroupCartDto>> GetAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var cart = await LoadCartAsync(id: id, ct: ct);
        if (cart == null) return Result<GroupCartDto>.NotFound();
        if (!cart.Members.Any(m => m.CustomerUserId == userId))
            return Result<GroupCartDto>.Forbidden();
        return Result<GroupCartDto>.Success(MapToDto(cart, userId));
    }

    public async Task<Result<GroupCartDto>> AddItemAsync(
        Guid id, Guid userId, AddGroupCartItemRequest req, CancellationToken ct = default)
    {
        var cart = await LoadCartAsync(id: id, ct: ct);
        if (cart == null) return Result<GroupCartDto>.NotFound();
        if (!cart.Members.Any(m => m.CustomerUserId == userId)) return Result<GroupCartDto>.Forbidden();
        if (cart.Status != GroupCartStatus.Open || cart.ExpiresAt < DateTime.UtcNow)
            return Result<GroupCartDto>.Failure("Group cart is no longer active.", 400);

        var member = cart.Members.First(m => m.CustomerUserId == userId);
        var item = new GroupCartItem
        {
            GroupCartId = cart.Id,
            AddedByUserId = userId,
            AddedByName = member.DisplayName,
            MenuItemId = req.MenuItemId,
            MenuItemName = req.MenuItemName,
            UnitPrice = req.UnitPrice,
            Quantity = req.Quantity,
            ToppingsJson = JsonSerializer.Serialize(req.Toppings),
            Notes = req.Notes,
        };

        _db.GroupCartItems.Add(item);
        await _db.SaveChangesAsync(ct);
        cart.Items.Add(item); // keep in-memory list in sync for DTO mapping

        var dto = MapToDto(cart, userId);
        await _hub.NotifyGroupCartUpdatedAsync(cart.Id, dto, ct);
        return Result<GroupCartDto>.Success(dto);
    }

    public async Task<Result<GroupCartDto>> RemoveItemAsync(
        Guid id, Guid itemId, Guid userId, CancellationToken ct = default)
    {
        var cart = await LoadCartAsync(id: id, ct: ct);
        if (cart == null) return Result<GroupCartDto>.NotFound();
        if (!cart.Members.Any(m => m.CustomerUserId == userId)) return Result<GroupCartDto>.Forbidden();

        var item = cart.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return Result<GroupCartDto>.NotFound();
        if (item.AddedByUserId != userId) return Result<GroupCartDto>.Forbidden();

        _db.GroupCartItems.Remove(item);
        await _db.SaveChangesAsync(ct);

        // Reload to get fresh items list
        cart = (await LoadCartAsync(id: id, ct: ct))!;
        var dto = MapToDto(cart, userId);
        await _hub.NotifyGroupCartUpdatedAsync(cart.Id, dto, ct);
        return Result<GroupCartDto>.Success(dto);
    }

    public async Task<Result<OrderDto>> CheckoutAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var cart = await LoadCartAsync(id: id, ct: ct);
        if (cart == null) return Result<OrderDto>.NotFound();
        if (cart.CreatedByUserId != userId) return Result<OrderDto>.Forbidden();
        if (cart.Status != GroupCartStatus.Open || cart.ExpiresAt < DateTime.UtcNow)
            return Result<OrderDto>.Failure("Group cart is expired or already finalized.", 400);
        if (!cart.Items.Any())
            return Result<OrderDto>.Failure("Cart is empty.", 400);

        var orderItems = cart.Items.Select(i =>
        {
            var toppings = JsonSerializer.Deserialize<List<GroupCartToppingDto>>(i.ToppingsJson)
                ?? [];
            return new OrderItemInputDto(
                i.MenuItemId,
                i.Quantity,
                toppings.Select(t => new OrderItemToppingInputDto(t.ToppingId, t.ToppingName, t.Price)).ToList(),
                i.Notes);
        }).ToList();

        var createDto = new CreateOrderDto(cart.LocationId, orderItems, PaymentMethod.Other);
        var result = await _orders.CreateOrderAsync(userId, createDto, ct);
        if (!result.Succeeded) return result;

        cart.Status = GroupCartStatus.Finalized;
        await _db.SaveChangesAsync(ct);

        return result;
    }

    private async Task<GroupCart?> LoadCartAsync(Guid? id = null, string? code = null, CancellationToken ct = default)
    {
        var query = _db.GroupCarts
            .Include(c => c.Location)
            .Include(c => c.Members)
            .Include(c => c.Items)
            .AsQueryable();

        if (id.HasValue) query = query.Where(c => c.Id == id.Value);
        else if (code != null) query = query.Where(c => c.Code == code);
        else return null;

        return await query.FirstOrDefaultAsync(ct);
    }

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Range(0, 6).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }

    private static GroupCartDto MapToDto(GroupCart cart, Guid currentUserId)
        => new(
            cart.Id,
            cart.Code,
            cart.Location?.Name ?? "",
            cart.LocationId.ToString(),
            cart.Status,
            cart.ExpiresAt,
            cart.CreatedByUserId == currentUserId,
            cart.Members.Select(m => new GroupCartMemberDto(m.CustomerUserId, m.DisplayName)).ToList(),
            cart.Items.Select(i =>
            {
                var toppings = JsonSerializer.Deserialize<List<GroupCartToppingDto>>(i.ToppingsJson) ?? [];
                return new GroupCartItemDto(
                    i.Id, i.AddedByUserId, i.AddedByName,
                    i.MenuItemId, i.MenuItemName,
                    i.UnitPrice, i.Quantity, toppings, i.Notes);
            }).ToList());
}
