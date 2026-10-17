using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Loyalty;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Platform;

/// <summary>
/// Operator tool for moving a cafe's bonus balances from iiko to the native wallet (Phase 6). Read-only on the iiko side:
/// it only reads balances. Safe to repeat: a customer who already has an imported balance is skipped. Run it for the cafe
/// the operator has picked, when no orders are in flight (iiko reports the balance without points currently on hold).
/// </summary>
public class LoyaltyImportService(
    IApplicationDbContext db,
    IIikoApiClient iiko,
    IikoOptions options,
    ITenantContext tenant,
    IAuditLogService audit,
    ILogger<LoyaltyImportService> logger)
{
    private const int MaxMismatchesListed = 50;

    public async Task<Result<LoyaltyImportDto>> ImportAsync(CancellationToken ct = default)
    {
        if (!options.IsEnabledFor(tenant.TenantId))
            return Result<LoyaltyImportDto>.Failure("iiko loyalty is not enabled for this cafe.", 400);

        int customers = 0, imported = 0, skipped = 0, failed = 0;
        decimal points = 0m;

        foreach (var user in await LinkedCustomersAsync(ct))
        {
            customers++;
            try
            {
                var balance = await IikoBalanceAsync(user, ct);
                var account = await db.LoyaltyAccounts.Include(a => a.Entries).FirstOrDefaultAsync(a => a.Phone == user.MobileNumber, ct);
                if (account != null && account.Entries.Any(e => e.Kind == LoyaltyEntryKind.Import)) { skipped++; continue; }

                account ??= db.LoyaltyAccounts.Add(new LoyaltyAccount
                {
                    Phone = user.MobileNumber, Name = user.FirstName, Surname = user.LastName,
                }).Entity;
                account.WalletId ??= Guid.NewGuid();
                // Always record the import, even at zero, so a repeat run skips this customer.
                db.LoyaltyEntries.Add(new LoyaltyEntry
                {
                    AccountId = account.Id, Kind = LoyaltyEntryKind.Import, Amount = balance, Comment = "Imported from iiko",
                });
                points += balance;
                await db.SaveChangesAsync(ct);
                imported++;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogWarning(ex, "Loyalty import failed for customer {CustomerId}", user.Id);
            }
        }

        await audit.LogAsync("LoyaltyImport", "Tenant", tenant.TenantId.ToString(),
            $"Imported {imported}, skipped {skipped}, failed {failed}, {points} points", ct);
        return Result<LoyaltyImportDto>.Success(new LoyaltyImportDto(customers, imported, skipped, failed, points));
    }

    /// <summary>Compares each linked customer's iiko balance with the native wallet. Changes nothing.</summary>
    public async Task<Result<LoyaltyCompareDto>> CompareAsync(CancellationToken ct = default)
    {
        if (!options.IsEnabledFor(tenant.TenantId))
            return Result<LoyaltyCompareDto>.Failure("iiko loyalty is not enabled for this cafe.", 400);

        int customers = 0, matching = 0, mismatched = 0, failed = 0;
        var mismatches = new List<LoyaltyMismatchDto>();

        foreach (var user in await LinkedCustomersAsync(ct))
        {
            customers++;
            try
            {
                var iikoBalance = await IikoBalanceAsync(user, ct);
                var nativeBalance = await db.LoyaltyEntries
                    .Where(e => e.Account.Phone == user.MobileNumber)
                    .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
                if (iikoBalance == nativeBalance) { matching++; continue; }

                mismatched++;
                if (mismatches.Count < MaxMismatchesListed)
                    mismatches.Add(new LoyaltyMismatchDto(user.MobileNumber, iikoBalance, nativeBalance));
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogWarning(ex, "Loyalty comparison failed for customer {CustomerId}", user.Id);
            }
        }

        return Result<LoyaltyCompareDto>.Success(new LoyaltyCompareDto(customers, matching, mismatched, failed, mismatches));
    }

    private Task<List<CustomerUser>> LinkedCustomersAsync(CancellationToken ct)
        => db.CustomerUsers
            .Where(u => u.IikoCustomerId != null && u.IikoWalletId != null)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync(ct);

    private async Task<decimal> IikoBalanceAsync(CustomerUser user, CancellationToken ct)
    {
        var info = await iiko.GetCustomerByPhoneAsync(user.MobileNumber, ct);
        if (info == null) return 0m;
        return info.WalletBalances.Where(w => w.Id == user.IikoWalletId).Sum(w => w.Balance);
    }
}
