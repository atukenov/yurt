using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Common;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Infrastructure.Persistence;

/// <summary>
/// Everything one cafe owns, as a ZIP for a cafe that leaves Yurt: <c>tables/&lt;Table&gt;.csv</c> for every table with a
/// TenantId (found from the database model, so a new table is exported without touching this file), plus
/// <c>cafe.json</c> (the cafe row, row counts, what was left out) and a README in plain words.
/// Rows are read past the tenant filter and matched to the requested cafe explicitly, so the operator can export any
/// cafe from any address. Platform operators (SuperAdmin) are not part of a cafe and are never exported.
/// </summary>
public class CafeDataExporter : ICafeDataExporter
{
    public const int FormatVersion = 1;

    /// <summary>Tables left out on purpose; the reason is written into cafe.json.</summary>
    public static readonly IReadOnlyDictionary<Type, string> SkippedTables = new Dictionary<Type, string>
    {
        [typeof(TenantSecret)] = "Encrypted keys Yurt uses to talk to payment, push and WhatsApp services. The cafe keeps the originals in those services.",
        [typeof(RefreshToken)] = "Login sessions. They only work inside Yurt.",
        [typeof(PhoneVerification)] = "One-time sign-in codes.",
        [typeof(CustomerDeviceToken)] = "Push notification addresses of Yurt's app. Another app cannot use them.",
        [typeof(PaymentWebhookLog)] = "Raw messages from the payment provider. The payments themselves are in Payments.csv.",
        [typeof(PrintJob)] = "Temporary print queue.",
    };

    /// <summary>Columns never exported: password and key hashes and encrypted values.</summary>
    public static readonly IReadOnlySet<string> SkippedColumns = new HashSet<string>(StringComparer.Ordinal)
    {
        "PasswordHash", "PinHash", "CodeHash", "KeyHash", "Ciphertext",
    };

    private static readonly MethodInfo WriteTableMethod =
        typeof(CafeDataExporter).GetMethod(nameof(WriteTableAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly ApplicationDbContext _db;
    private readonly IAuditLogService _audit;

    public CafeDataExporter(ApplicationDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <summary>Tenant-owned tables in a stable order (by table name).</summary>
    public static IEnumerable<IEntityType> ExportedTables(IModel model)
        => model.GetEntityTypes()
            .Where(t => !t.IsOwned() && typeof(ITenantOwned).IsAssignableFrom(t.ClrType) && !SkippedTables.ContainsKey(t.ClrType))
            .OrderBy(t => t.GetTableName(), StringComparer.Ordinal);

    public async Task<CafeDataExport?> ExportAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return null;

        var path = Path.GetTempFileName();
        try
        {
            var counts = new List<object>();
            using (var zip = new ZipArchive(new FileStream(path, FileMode.Create, FileAccess.Write), ZipArchiveMode.Create))
            {
                foreach (var table in ExportedTables(_db.Model))
                {
                    var file = $"tables/{table.GetTableName()}.csv";
                    await using var writer = new StreamWriter(zip.CreateEntry(file).Open(), new UTF8Encoding(true));
                    var task = (Task<int>)WriteTableMethod.MakeGenericMethod(table.ClrType).Invoke(this, [table, writer, tenantId, ct])!;
                    counts.Add(new { file, rows = await task });
                }

                var manifest = new
                {
                    formatVersion = FormatVersion,
                    exportedAt = DateTime.UtcNow,
                    cafe = tenant,
                    tables = counts,
                    leftOut = SkippedTables.Select(s => new { table = _db.Model.FindEntityType(s.Key)?.GetTableName() ?? s.Key.Name, reason = s.Value }),
                    leftOutColumns = SkippedColumns,
                };
                await using (var w = new StreamWriter(zip.CreateEntry("cafe.json").Open(), new UTF8Encoding(false)))
                    await w.WriteAsync(JsonSerializer.Serialize(manifest, Json));
                await using (var w = new StreamWriter(zip.CreateEntry("README.txt").Open(), new UTF8Encoding(true)))
                    await w.WriteAsync(Readme(tenant.Name));
            }

            await _audit.LogAsync("CafeDataExported", "Tenant", tenantId.ToString(), $"Exported all data of cafe '{tenant.Slug}'", ct);
            var content = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.DeleteOnClose);
            return new CafeDataExport(content, $"{tenant.Slug}-data-{DateTime.UtcNow:yyyy-MM-dd}.zip");
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }

    private async Task<int> WriteTableAsync<T>(IEntityType table, TextWriter writer, Guid tenantId, CancellationToken ct) where T : class
    {
        var columns = table.GetProperties()
            .Where(p => p.PropertyInfo is not null && !SkippedColumns.Contains(p.Name))
            .ToList();
        await writer.WriteLineAsync(string.Join(',', columns.Select(c => Csv(c.Name))));

        var query = _db.Set<T>().IgnoreQueryFilters().AsNoTracking()
            .Where(e => EF.Property<Guid>(e, nameof(ITenantOwned.TenantId)) == tenantId);
        if (typeof(T) == typeof(AdminUser))
            query = query.Where(e => EF.Property<AdminRole>(e, nameof(AdminUser.Role)) != AdminRole.SuperAdmin);
        if (table.FindProperty(nameof(BaseEntity.CreatedAt)) is not null)
            query = query.OrderBy(e => EF.Property<DateTime>(e, nameof(BaseEntity.CreatedAt)));

        var rows = 0;
        await foreach (var row in query.AsAsyncEnumerable().WithCancellation(ct))
        {
            await writer.WriteLineAsync(string.Join(',', columns.Select(c => Csv(Format(c.PropertyInfo!.GetValue(row))))));
            rows++;
        }
        return rows;
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        byte[] bytes => Convert.ToBase64String(bytes),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        IEnumerable list => JsonSerializer.Serialize(list),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>RFC 4180 quoting. Text starting with = + - @ gets a leading apostrophe so a spreadsheet shows it
    /// instead of running it as a formula.</summary>
    internal static string Csv(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            value = "'" + value;
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    private static string Readme(string cafe) => $"""
        {cafe}: data export from Yurt
        ==============================

        This archive holds everything Yurt stored for this cafe: menu, prices, branches, customers, orders and
        payments, bonus wallets, stock, shifts, staff accounts, settings and the history of admin actions.

        - tables/*.csv: one file per table. Open them in Excel, Google Sheets or LibreOffice (UTF-8, comma separated).
          The first line of each file names the columns. Rows point to each other by the Id column
          (for example OrderItems.OrderId is an Id from Orders.csv).
        - cafe.json: the cafe itself, how many rows each file has, and what was left out and why.
        - Times are UTC (ISO 8601). Money is in the cafe's currency, with a dot as the decimal separator.
        - Text that starts with = + - or @ has an apostrophe in front, so spreadsheets do not run it as a formula.

        Not included: passwords and PIN codes (only scrambled copies are stored, and they are useless elsewhere),
        keys for payment and other services, login sessions, and push notification addresses.
        Menu pictures are links (ImageUrl); download them while the links still work.

        ---

        {cafe}: выгрузка данных из Yurt

        В архиве всё, что Yurt хранил для этого кафе: меню, цены, филиалы, клиенты, заказы и оплаты, бонусы,
        склад, смены, сотрудники, настройки и журнал действий админов.

        - tables/*.csv: по файлу на таблицу, открываются в Excel или Google Таблицах (UTF-8, через запятую).
        - cafe.json: данные кафе, число строк в каждом файле и что не вошло в выгрузку.
        - Время в UTC. Деньги в валюте кафе, дробная часть через точку.

        Не входят: пароли и PIN-коды, ключи платёжных и других сервисов, сессии входа и адреса push-уведомлений.
        Картинки меню даны ссылками (ImageUrl).
        """;
}
