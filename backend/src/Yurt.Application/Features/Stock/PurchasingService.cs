using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Stock;

/// <summary>
/// Purchasing (own_pos): suppliers and what they sell, orders to suppliers, delivery invoices that put everything on
/// the shelf in one go, whether each invoice is paid, and the "what to order" list from the warn-at numbers.
/// Posting an invoice works like "Delivery arrived" for every line: counts go up, the average cost is updated and each
/// line lands in the stock history linked to the invoice. Cancelling a posted invoice takes it back off the shelf.
/// </summary>
public class PurchasingService(
    IApplicationDbContext db, IAuditLogService audit, IMenuCacheService menuCache, ICurrentUserService user,
    StockMoveService moves)
{
    private const decimal MaxQuantity = 10_000_000m, MaxCost = 1_000_000_000m;
    private const int MaxLines = 200, MaxProducts = 500, MaxList = 200;

    // ── Suppliers ──

    /// <summary>All suppliers, active first. With <paramref name="withMoney"/>, each shows what the cafe owes it.</summary>
    public async Task<List<SupplierDto>> ListSuppliersAsync(bool withMoney, CancellationToken ct = default)
    {
        var suppliers = await db.Suppliers
            .Include(s => s.Products).ThenInclude(p => p.Ingredient)
            .OrderByDescending(s => s.IsActive).ThenBy(s => s.Name)
            .AsSplitQuery()
            .ToListAsync(ct);
        var counts = await db.Purchases.Where(p => p.Status == PurchaseStatuses.Posted)
            .GroupBy(p => p.SupplierId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        var today = DateTime.UtcNow.Date;
        var unpaid = withMoney
            ? await db.Purchases.Where(p => p.Status == PurchaseStatuses.Posted && !p.IsPaid)
                .Select(p => new { p.SupplierId, p.Total, p.DueDate }).ToListAsync(ct)
            : [];
        return suppliers.Select(s =>
        {
            var mine = unpaid.Where(u => u.SupplierId == s.Id).ToList();
            return ToDto(s, counts.GetValueOrDefault(s.Id),
                withMoney ? mine.Sum(u => u.Total) : null,
                withMoney ? mine.Where(u => u.DueDate < today).Sum(u => u.Total) : null);
        }).ToList();
    }

    public async Task<Result<SupplierDto>> CreateSupplierAsync(SaveSupplierDto dto, CancellationToken ct = default)
    {
        var supplier = new Supplier();
        if (await ApplyAsync(supplier, dto, ct) is { } error) return Result<SupplierDto>.Failure(error.Message, error.Status);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("SupplierCreated", "Supplier", supplier.Id.ToString(), supplier.Name, ct);
        return Result<SupplierDto>.Success(await SupplierDtoAsync(supplier.Id, ct), 201);
    }

    public async Task<Result<SupplierDto>> UpdateSupplierAsync(Guid id, SaveSupplierDto dto, CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.Include(s => s.Products).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null) return Result<SupplierDto>.Failure("Supplier not found.", 404);
        if (await ApplyAsync(supplier, dto, ct) is { } error) return Result<SupplierDto>.Failure(error.Message, error.Status);
        supplier.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("SupplierUpdated", "Supplier", supplier.Id.ToString(), supplier.Name, ct);
        return Result<SupplierDto>.Success(await SupplierDtoAsync(supplier.Id, ct));
    }

    /// <summary>
    /// Deletes a supplier with no orders or invoices. One that has them is only switched off, so its invoices keep
    /// their supplier. The result is true when the supplier was deleted, false when it was switched off.
    /// </summary>
    public async Task<Result<bool>> DeleteSupplierAsync(Guid id, CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null) return Result<bool>.Failure("Supplier not found.", 404);
        var used = await db.Purchases.AnyAsync(p => p.SupplierId == id, ct);
        if (used)
        {
            supplier.IsActive = false;
            supplier.UpdatedAt = DateTime.UtcNow;
        }
        else db.Suppliers.Remove(supplier);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(used ? "SupplierSwitchedOff" : "SupplierDeleted", "Supplier", id.ToString(), supplier.Name, ct);
        return Result<bool>.Success(!used);
    }

    private sealed record Error(string Message, int Status = 400);

    private async Task<Error?> ApplyAsync(Supplier supplier, SaveSupplierDto dto, CancellationToken ct)
    {
        var name = dto.Name?.Trim() ?? "";
        if (name.Length is 0 or > 200) return new("Type the supplier's name (up to 200 characters).");
        if (await db.Suppliers.AnyAsync(s => s.Name == name && s.Id != supplier.Id, ct))
            return new("A supplier with this name already exists.", 409);
        var terms = string.IsNullOrWhiteSpace(dto.PaymentTerms) ? SupplierPaymentTerms.Cash : dto.PaymentTerms.Trim().ToLowerInvariant();
        if (!SupplierPaymentTerms.All.Contains(terms)) return new("Unknown payment terms.");
        if (dto.PayInDays is < 0 or > 365) return new("Pay in 0 to 365 days.");
        if (Clean(dto.ContactName) is { Length: > 100 }) return new("Contact name is too long.");
        if (Clean(dto.Phone) is { Length: > 30 }) return new("Phone is too long.");
        if (Clean(dto.Bin) is { Length: > 20 }) return new("BIN is too long.");
        if (Clean(dto.Note) is { Length: > 500 }) return new("Note is too long (up to 500 characters).");

        var products = dto.Products ?? [];
        if (products.Count > MaxProducts) return new("Too many products.");
        if (products.GroupBy(p => p.IngredientId).Any(g => g.Count() > 1)) return new("An ingredient is listed twice.");
        foreach (var p in products)
        {
            if (p.PackSize <= 0 || p.PackSize > MaxQuantity) return new("Pack size must be more than zero.");
            if (p.PackPrice is < 0 or > MaxCost) return new("Pack price must be zero or more.");
            if (Clean(p.PackName) is { Length: > 100 }) return new("Pack name is too long.");
        }
        var ingredientIds = products.Select(p => p.IngredientId).ToList();
        var known = await db.Ingredients.Where(i => ingredientIds.Contains(i.Id)).Select(i => i.Id).ToListAsync(ct);
        if (known.Count != ingredientIds.Count) return new("Ingredient not found.", 404);

        supplier.Name = name;
        supplier.ContactName = Clean(dto.ContactName);
        supplier.Phone = Clean(dto.Phone);
        supplier.Bin = Clean(dto.Bin);
        supplier.PaymentTerms = terms;
        supplier.PayInDays = terms == SupplierPaymentTerms.Later ? dto.PayInDays : null;
        supplier.Note = Clean(dto.Note);
        if (dto.IsActive is { } active) supplier.IsActive = active;

        // Replace what the supplier sells, keeping rows that stay so their ids do not churn.
        foreach (var gone in supplier.Products.Where(p => !ingredientIds.Contains(p.IngredientId)).ToList())
        {
            supplier.Products.Remove(gone);
            db.SupplierProducts.Remove(gone);
        }
        foreach (var p in products)
        {
            var row = supplier.Products.FirstOrDefault(x => x.IngredientId == p.IngredientId);
            if (row is null)
            {
                row = new SupplierProduct { SupplierId = supplier.Id, IngredientId = p.IngredientId };
                supplier.Products.Add(row);
                db.SupplierProducts.Add(row); // explicit: a set id would otherwise be taken for an existing row
            }
            row.PackSize = p.PackSize;
            row.PackName = Clean(p.PackName);
            row.PackPrice = p.PackPrice;
        }
        return null;
    }

    private async Task<SupplierDto> SupplierDtoAsync(Guid id, CancellationToken ct)
    {
        var s = await db.Suppliers.Include(x => x.Products).ThenInclude(p => p.Ingredient).FirstAsync(x => x.Id == id, ct);
        var invoices = await db.Purchases.CountAsync(p => p.SupplierId == id && p.Status == PurchaseStatuses.Posted, ct);
        return ToDto(s, invoices, null, null);
    }

    private static SupplierDto ToDto(Supplier s, int invoices, decimal? owed, decimal? overdue) => new(
        s.Id, s.Name, s.ContactName, s.Phone, s.Bin, s.PaymentTerms, s.PayInDays, s.Note, s.IsActive,
        s.Products.OrderBy(p => p.Ingredient.Name)
            .Select(p => new SupplierProductDto(p.IngredientId, p.Ingredient.Name, p.Ingredient.Unit, p.PackSize, p.PackName, p.PackPrice))
            .ToList(),
        owed, overdue, invoices);

    // ── Orders and invoices ──

    /// <summary>Orders and invoices, newest first. <paramref name="statuses"/> is a comma list (e.g. "ordered").</summary>
    public async Task<List<PurchaseDto>> ListAsync(
        Guid? locationId, Guid? supplierId, string? statuses, bool unpaidOnly, int take, CancellationToken ct = default)
    {
        var statusList = (statuses ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var query = db.Purchases.AsQueryable();
        if (locationId is { } loc) query = query.Where(p => p.LocationId == loc);
        if (supplierId is { } sup) query = query.Where(p => p.SupplierId == sup);
        if (statusList.Count > 0) query = query.Where(p => statusList.Contains(p.Status));
        if (unpaidOnly) query = query.Where(p => p.Status == PurchaseStatuses.Posted && !p.IsPaid);
        var rows = await query
            .OrderByDescending(p => p.Date).ThenByDescending(p => p.CreatedAt)
            .Take(Math.Clamp(take, 1, MaxList))
            .Select(p => new { Purchase = p, SupplierName = p.Supplier.Name, p.Supplier.Phone, LocationName = p.Location.Name, Lines = p.Lines.Count })
            .ToListAsync(ct);
        return rows.Select(r => ToDto(r.Purchase, r.SupplierName, r.Phone, r.LocationName, r.Lines, [])).ToList();
    }

    public async Task<Result<PurchaseDto>> GetAsync(Guid id, CancellationToken ct = default)
        => await db.Purchases.AnyAsync(p => p.Id == id, ct)
            ? Result<PurchaseDto>.Success(await PurchaseDtoAsync(id, ct))
            : Result<PurchaseDto>.Failure("Invoice not found.", 404);

    public async Task<Result<PurchaseDto>> CreateAsync(SavePurchaseDto dto, CancellationToken ct = default)
    {
        var purchase = new Purchase { CreatedBy = user.Username ?? "unknown" };
        if (await ApplyAsync(purchase, dto, isNew: true, ct) is { } error) return Result<PurchaseDto>.Failure(error.Message, error.Status);
        db.Purchases.Add(purchase);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(purchase.Status == PurchaseStatuses.Ordered ? "PurchaseOrderCreated" : "PurchaseInvoiceCreated",
            "Purchase", purchase.Id.ToString(), $"{purchase.Lines.Count} lines, {purchase.Total}", ct);
        return Result<PurchaseDto>.Success(await PurchaseDtoAsync(purchase.Id, ct), 201);
    }

    /// <summary>Changes an order or an invoice that is not posted yet. An order becomes an invoice with status "draft".</summary>
    public async Task<Result<PurchaseDto>> UpdateAsync(Guid id, SavePurchaseDto dto, CancellationToken ct = default)
    {
        var purchase = await db.Purchases.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (purchase is null) return Result<PurchaseDto>.Failure("Invoice not found.", 404);
        if (!IsOpen(purchase)) return Result<PurchaseDto>.Failure("A posted or cancelled invoice cannot be changed.", 409);
        if (await ApplyAsync(purchase, dto, isNew: false, ct) is { } error) return Result<PurchaseDto>.Failure(error.Message, error.Status);
        purchase.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result<PurchaseDto>.Success(await PurchaseDtoAsync(id, ct));
    }

    /// <summary>Deletes an order or invoice that was never posted. Posted ones are cancelled instead.</summary>
    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var purchase = await db.Purchases.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (purchase is null) return Result<bool>.Failure("Invoice not found.", 404);
        if (!IsOpen(purchase)) return Result<bool>.Failure("A posted invoice cannot be deleted. Cancel it instead.", 409);
        db.Purchases.Remove(purchase);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("PurchaseDeleted", "Purchase", id.ToString(), purchase.Status, ct);
        return Result<bool>.Success(true);
    }

    /// <summary>
    /// The goods are here: every line goes on the shelf of the invoice's branch, with its price updating the average
    /// cost. A supplier paid in cash is marked paid at once; "pay later" gets a due date.
    /// </summary>
    public async Task<Result<PostPurchaseResultDto>> PostAsync(Guid id, CancellationToken ct = default)
    {
        var purchase = await db.Purchases.Include(p => p.Lines).Include(p => p.Supplier).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (purchase is null) return Result<PostPurchaseResultDto>.Failure("Invoice not found.", 404);
        if (!IsOpen(purchase)) return Result<PostPurchaseResultDto>.Failure("This invoice is already posted.", 409);
        if (purchase.Lines.Count == 0) return Result<PostPurchaseResultDto>.Failure("Add at least one line.", 400);
        var ids = purchase.Lines.Select(l => l.IngredientId).ToList();
        var ingredients = await db.Ingredients.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        if (purchase.Lines.FirstOrDefault(l => !ingredients.ContainsKey(l.IngredientId)) is { } gone)
            return Result<PostPurchaseResultDto>.Failure($"'{gone.IngredientName}' was deleted. Remove it from the invoice first.", 400);

        var label = Label(purchase);
        foreach (var line in purchase.Lines.OrderBy(l => l.SortOrder))
        {
            var ingredient = ingredients[line.IngredientId];
            line.CostBefore = ingredient.CostPerUnit;
            await moves.ReceiveAsync(ingredient, purchase.LocationId, line.Quantity, line.Total, label, purchase.Id, ct);
        }

        var now = DateTime.UtcNow;
        purchase.Status = PurchaseStatuses.Posted;
        purchase.PostedAt = now;
        purchase.PostedBy = user.Username ?? "unknown";
        purchase.UpdatedAt = now;
        purchase.DueDate = purchase.Supplier.PaymentTerms == SupplierPaymentTerms.Later
            ? purchase.Date.AddDays(purchase.Supplier.PayInDays ?? 0) : null;
        if (purchase.Supplier.PaymentTerms == SupplierPaymentTerms.Cash && !purchase.IsPaid)
        {
            purchase.IsPaid = true;
            purchase.PaidAt = now;
        }
        await db.SaveChangesAsync(ct);
        await menuCache.InvalidateAllMenuAsync();
        await audit.LogAsync("PurchasePosted", "Purchase", id.ToString(),
            $"{purchase.Supplier.Name} {purchase.Number}: {purchase.Lines.Count} lines, {purchase.Total}", ct);

        var stock = new List<StockMoveResultDto>();
        foreach (var ingredient in ingredients.Values.OrderBy(i => i.Name))
            stock.Add(new StockMoveResultDto("ingredient", ingredient.Id, ingredient.Name, ingredient.Unit,
                await moves.IngredientLeftAsync(purchase.LocationId, ingredient, ct) ?? 0m, ingredient.CostPerUnit));
        return Result<PostPurchaseResultDto>.Success(new PostPurchaseResultDto(await PurchaseDtoAsync(id, ct), stock));
    }

    /// <summary>
    /// Undoes a posted invoice (wrong supplier, typed twice...): its amounts are taken back off the shelf and the
    /// cost goes back. Refused when the branch has already used some of it; then write off or count instead.
    /// </summary>
    public async Task<Result<PurchaseDto>> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var purchase = await db.Purchases.Include(p => p.Lines).Include(p => p.Supplier).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (purchase is null) return Result<PurchaseDto>.Failure("Invoice not found.", 404);
        if (purchase.Status != PurchaseStatuses.Posted) return Result<PurchaseDto>.Failure("Only a posted invoice can be cancelled.", 409);
        var ids = purchase.Lines.Select(l => l.IngredientId).ToList();
        var ingredients = await db.Ingredients.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);

        // Check everything first, so a cancel never stops half way.
        foreach (var group in purchase.Lines.Where(l => ingredients.ContainsKey(l.IngredientId)).GroupBy(l => l.IngredientId))
        {
            var ingredient = ingredients[group.Key];
            var need = group.Sum(l => l.Quantity);
            var left = await moves.IngredientLeftAsync(purchase.LocationId, ingredient, ct) ?? 0m;
            if (left < need)
                return Result<PurchaseDto>.Failure(
                    $"Only {left:0.###} {ingredient.Unit} of '{ingredient.Name}' is left, the invoice added {need:0.###}. " +
                    "Part of it was already used: write off or count the shelf instead.", 409);
        }

        var note = "Cancelled: " + Label(purchase);
        foreach (var line in purchase.Lines.OrderByDescending(l => l.SortOrder))
        {
            if (!ingredients.TryGetValue(line.IngredientId, out var ingredient)) continue; // deleted since: nothing to take back
            if (await moves.TakeBackAsync(ingredient, purchase.LocationId, line.Quantity, line.Total, line.CostBefore, note, purchase.Id, ct) is { } error)
                return Result<PurchaseDto>.Failure(error, 409);
        }

        var now = DateTime.UtcNow;
        purchase.Status = PurchaseStatuses.Cancelled;
        purchase.CancelledAt = now;
        purchase.CancelledBy = user.Username ?? "unknown";
        purchase.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await menuCache.InvalidateAllMenuAsync();
        await audit.LogAsync("PurchaseCancelled", "Purchase", id.ToString(), $"{purchase.Supplier.Name} {purchase.Number}: {purchase.Total}", ct);
        return Result<PurchaseDto>.Success(await PurchaseDtoAsync(id, ct));
    }

    public async Task<Result<PurchaseDto>> SetPaidAsync(Guid id, SetPurchasePaidDto dto, CancellationToken ct = default)
    {
        var purchase = await db.Purchases.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (purchase is null) return Result<PurchaseDto>.Failure("Invoice not found.", 404);
        if (purchase.Status != PurchaseStatuses.Posted) return Result<PurchaseDto>.Failure("Only a posted invoice can be paid.", 409);
        purchase.IsPaid = dto.Paid;
        purchase.PaidAt = dto.Paid ? DateTime.UtcNow : null;
        purchase.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(dto.Paid ? "PurchasePaid" : "PurchaseUnpaid", "Purchase", id.ToString(), purchase.Total.ToString(), ct);
        return Result<PurchaseDto>.Success(await PurchaseDtoAsync(id, ct));
    }

    private static bool IsOpen(Purchase p) => p.Status is PurchaseStatuses.Ordered or PurchaseStatuses.Draft;

    private static string Label(Purchase p)
    {
        var text = $"Invoice {(string.IsNullOrWhiteSpace(p.Number) ? "" : p.Number + " ")}· {p.Supplier.Name}";
        return text.Length > 300 ? text[..300] : text;
    }

    private async Task<Error?> ApplyAsync(Purchase purchase, SavePurchaseDto dto, bool isNew, CancellationToken ct)
    {
        var status = string.IsNullOrWhiteSpace(dto.Status) ? PurchaseStatuses.Draft : dto.Status.Trim().ToLowerInvariant();
        if (status is not (PurchaseStatuses.Ordered or PurchaseStatuses.Draft))
            return new("New orders and invoices are 'ordered' or 'draft'. Post an invoice to put it on the shelf.");
        if (Clean(dto.Number) is { Length: > 50 }) return new("Invoice number is too long.");
        if (Clean(dto.Note) is { Length: > 300 }) return new("Note is too long (up to 300 characters).");
        var photo = Clean(dto.PhotoUrl);
        if (photo is { Length: > 500 } || (photo is not null && !photo.StartsWith("https://") && !photo.StartsWith("http://") && !photo.StartsWith('/')))
            return new("The photo link is not valid.");
        var lines = dto.Lines ?? [];
        if (lines.Count > MaxLines) return new("Too many lines.");
        if (lines.GroupBy(l => l.IngredientId).Any(g => g.Count() > 1)) return new("An ingredient is on the invoice twice. Add the amounts up in one line.");
        foreach (var l in lines)
        {
            if (l.Quantity <= 0 || l.Quantity > MaxQuantity) return new("Amount must be more than zero.");
            if (l.Total is < 0 or > MaxCost) return new("Price must be zero or more.");
        }

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == dto.SupplierId, ct);
        if (supplier is null) return new("Supplier not found.", 404);
        if (!supplier.IsActive && (isNew || supplier.Id != purchase.SupplierId)) return new("This supplier is switched off.");
        if (!await db.Locations.AnyAsync(l => l.Id == dto.LocationId, ct)) return new("Location not found.", 404);
        var ids = lines.Select(l => l.IngredientId).ToList();
        var ingredients = await db.Ingredients.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        if (ingredients.Count != ids.Count) return new("Ingredient not found.", 404);

        purchase.SupplierId = supplier.Id;
        purchase.LocationId = dto.LocationId;
        purchase.Status = status;
        purchase.Number = Clean(dto.Number);
        purchase.Date = (dto.Date ?? DateTime.UtcNow).Date;
        purchase.Date = DateTime.SpecifyKind(purchase.Date, DateTimeKind.Utc);
        purchase.Note = Clean(dto.Note);
        purchase.PhotoUrl = photo;

        foreach (var old in purchase.Lines.ToList())
        {
            purchase.Lines.Remove(old);
            db.PurchaseLines.Remove(old);
        }
        var order = 0;
        foreach (var l in lines)
        {
            var ingredient = ingredients[l.IngredientId];
            var line = new PurchaseLine
            {
                PurchaseId = purchase.Id, IngredientId = ingredient.Id, IngredientName = ingredient.Name, Unit = ingredient.Unit,
                Quantity = l.Quantity, Total = l.Total, SortOrder = order++,
            };
            purchase.Lines.Add(line);
            db.PurchaseLines.Add(line); // explicit: a set id would otherwise be taken for an existing row
        }
        purchase.Total = purchase.Lines.Sum(l => l.Total ?? 0m);
        return null;
    }

    private async Task<PurchaseDto> PurchaseDtoAsync(Guid id, CancellationToken ct)
    {
        var r = await db.Purchases.Where(p => p.Id == id)
            .Select(p => new { Purchase = p, SupplierName = p.Supplier.Name, p.Supplier.Phone, LocationName = p.Location.Name })
            .FirstAsync(ct);
        var lines = await db.PurchaseLines.Where(l => l.PurchaseId == id).OrderBy(l => l.SortOrder).ToListAsync(ct);
        return ToDto(r.Purchase, r.SupplierName, r.Phone, r.LocationName, lines.Count,
            lines.Select(l => new PurchaseLineDto(l.Id, l.IngredientId, l.IngredientName, l.Unit, l.Quantity, l.Total,
                l.Total is { } t && l.Quantity > 0 ? Math.Round(t / l.Quantity, 4) : null)).ToList());
    }

    private static PurchaseDto ToDto(Purchase p, string supplierName, string? phone, string locationName, int lineCount, List<PurchaseLineDto> lines)
        => new(p.Id, p.SupplierId, supplierName, phone, p.LocationId, locationName, p.Status, p.Number, p.Date, p.Note,
            p.PhotoUrl, p.Total, p.IsPaid, p.PaidAt, p.DueDate,
            IsOverdue: p.Status == PurchaseStatuses.Posted && !p.IsPaid && p.DueDate < DateTime.UtcNow.Date,
            p.CreatedBy, p.CreatedAt, p.PostedBy, p.PostedAt, p.CancelledBy, p.CancelledAt, lineCount, lines);

    // ── What to order ──

    /// <summary>
    /// What a branch should order now: every ingredient at or under its warn-at number, grouped by the supplier that
    /// sells it cheapest per unit, with an amount that fills the shelf up to its target in whole packs.
    /// </summary>
    public async Task<Result<List<ToOrderGroupDto>>> ToOrderAsync(Guid locationId, CancellationToken ct = default)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
            return Result<List<ToOrderGroupDto>>.Failure("Location not found.", 404);

        var low = await db.IngredientStocks
            .Where(s => s.LocationId == locationId && s.LowStockAt != null && s.Quantity <= s.LowStockAt)
            .Select(s => new { s.IngredientId, s.Ingredient.Name, s.Ingredient.Unit, s.Quantity, LowStockAt = s.LowStockAt!.Value, s.OrderUpTo })
            .ToListAsync(ct);
        var ids = low.Select(l => l.IngredientId).ToList();
        var ordered = await db.PurchaseLines
            .Where(l => ids.Contains(l.IngredientId) && l.Purchase.LocationId == locationId && l.Purchase.Status == PurchaseStatuses.Ordered)
            .GroupBy(l => l.IngredientId).Select(g => new { g.Key, Quantity = g.Sum(l => l.Quantity) })
            .ToDictionaryAsync(g => g.Key, g => g.Quantity, ct);
        var offers = await db.SupplierProducts
            .Where(p => ids.Contains(p.IngredientId) && p.Supplier.IsActive)
            .Select(p => new { p.IngredientId, p.SupplierId, SupplierName = p.Supplier.Name, p.Supplier.Phone, p.PackSize, p.PackName, p.PackPrice })
            .ToListAsync(ct);

        var lines = new List<(Guid? SupplierId, string? SupplierName, string? Phone, ToOrderLineDto Line)>();
        foreach (var item in low)
        {
            var offer = offers.Where(o => o.IngredientId == item.IngredientId)
                .OrderBy(o => o.PackPrice is { } price ? price / o.PackSize : decimal.MaxValue)
                .ThenBy(o => o.SupplierName)
                .FirstOrDefault();
            var already = ordered.GetValueOrDefault(item.IngredientId);
            var target = item.OrderUpTo ?? item.LowStockAt * 2m;
            var need = Math.Max(0m, target - item.Quantity - already);
            decimal suggested = Math.Ceiling(need * 1000m) / 1000m;
            decimal? packs = null, cost = null;
            if (offer is not null && need > 0)
            {
                packs = Math.Ceiling(need / offer.PackSize);
                suggested = packs.Value * offer.PackSize;
                cost = offer.PackPrice is { } price ? Math.Round(packs.Value * price, 2) : null;
            }
            lines.Add((offer?.SupplierId, offer?.SupplierName, offer?.Phone, new ToOrderLineDto(
                item.IngredientId, item.Name, item.Unit, item.Quantity, item.LowStockAt, item.OrderUpTo, already,
                suggested, packs, offer?.PackSize, offer?.PackName, cost)));
        }

        var groups = lines
            .GroupBy(l => l.SupplierId)
            .Select(g => new ToOrderGroupDto(g.Key, g.First().SupplierName, g.First().Phone,
                g.Select(x => x.Line).OrderBy(x => x.Name).ToList(),
                g.Any(x => x.Line.EstimatedCost is not null) ? g.Sum(x => x.Line.EstimatedCost ?? 0m) : null))
            .OrderBy(g => g.SupplierId is null) // "no supplier yet" last
            .ThenBy(g => g.SupplierName)
            .ToList();
        return Result<List<ToOrderGroupDto>>.Success(groups);
    }

    /// <summary>Sets how high "What to order" fills the shelf of a counted ingredient at a branch (null = twice warn-at).</summary>
    public async Task<Result<bool>> SetOrderUpToAsync(Guid ingredientId, Guid locationId, SetOrderUpToDto dto, CancellationToken ct = default)
    {
        if (dto.OrderUpTo is < 0 or > MaxQuantity) return Result<bool>.Failure("The number must be zero or more.", 400);
        var row = await db.IngredientStocks.FirstOrDefaultAsync(s => s.IngredientId == ingredientId && s.LocationId == locationId, ct);
        if (row is null) return Result<bool>.Failure("This ingredient is not counted at this branch.", 404);
        row.OrderUpTo = dto.OrderUpTo;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
