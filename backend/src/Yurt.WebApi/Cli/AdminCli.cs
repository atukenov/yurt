using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Yurt.Application.Common.Interfaces;
using Yurt.Infrastructure;
using Yurt.Infrastructure.Persistence;
using Yurt.Infrastructure.Services;

namespace Yurt.WebApi.Cli;

/// <summary>
/// Offline maintenance commands run on the server with database access, outside the HTTP API.
/// They exist so an operator can recover an account that cannot sign in (a forgotten or locked-out
/// admin or super admin) and support JWT secret rotation — without ever reading, exposing or trying
/// to recover the existing password, which is a one-way bcrypt hash.
///
/// Usage (from the backend host, e.g. a Railway one-off command or a local shell):
///   dotnet Yurt.WebApi.dll reset-admin-password &lt;username&gt;   # new password on stdin or $YURT_NEW_PASSWORD
///   dotnet Yurt.WebApi.dll set-admin-role &lt;username&gt; &lt;Ceo|Admin&gt;   # hand a cafe's money screens to its owner
///   dotnet Yurt.WebApi.dll list-admins                        # usernames and roles only, never hashes
///   dotnet Yurt.WebApi.dll revoke-all-sessions                # force every user to sign in again
///
/// See docs/admin-password-reset.md and docs/jwt-rotation.md.
/// </summary>
public static class AdminCli
{
    public static readonly string[] Commands =
        ["reset-admin-password", "list-admins", "revoke-all-sessions", "set-admin-role"];

    public static bool IsCommand(string[] args)
        => args.Length > 0 && Commands.Contains(args[0]);

    public static async Task<int> RunAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddInfrastructureDataAccess(builder.Configuration);
        // The DbContext constructor needs a tenant context; the default one is fine because every
        // command below uses IgnoreQueryFilters() and so sees rows across all cafes.
        builder.Services.AddScoped<ITenantContext, TenantContext>();
        using var host = builder.Build();

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        try
        {
            return args[0] switch
            {
                "reset-admin-password" => await ResetAdminPasswordAsync(db, hasher, args),
                "set-admin-role" => await SetAdminRoleAsync(db, args),
                "list-admins" => await ListAdminsAsync(db),
                "revoke-all-sessions" => await RevokeAllSessionsAsync(db),
                _ => Usage()
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Command failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ResetAdminPasswordAsync(
        ApplicationDbContext db, IPasswordHasher hasher, string[] args)
    {
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            Console.Error.WriteLine("Usage: reset-admin-password <username>");
            return 2;
        }

        var username = args[1].Trim();

        // IgnoreQueryFilters so a locked-out admin or super admin in any cafe can be found.
        var admin = await db.AdminUsers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Username == username);
        if (admin is null)
        {
            Console.Error.WriteLine($"No admin found with username '{username}'. Run 'list-admins' to see them.");
            return 3;
        }

        var newPassword = ReadNewPassword();
        if (newPassword is null)
            return 2;
        if (newPassword.Length < 8)
        {
            Console.Error.WriteLine("New password must be at least 8 characters.");
            return 2;
        }

        admin.PasswordHash = hasher.Hash(newPassword);
        // Force a change at next sign-in and clear any deactivation so the account can log in.
        admin.MustChangePassword = true;
        admin.IsActive = true;
        admin.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        Console.WriteLine(
            $"Password reset for '{admin.Username}' ({admin.Role}). " +
            "They must set a new password on their next sign-in. " +
            "Existing access tokens keep working until they expire (~15 min); " +
            "run 'revoke-all-sessions' if you need to sign this account out everywhere right now.");
        return 0;
    }

    private static async Task<int> SetAdminRoleAsync(ApplicationDbContext db, string[] args)
    {
        if (args.Length < 3 ||
            !Enum.TryParse<Yurt.Domain.Enums.AdminRole>(args[2], true, out var role) ||
            role is not (Yurt.Domain.Enums.AdminRole.Ceo or Yurt.Domain.Enums.AdminRole.Admin))
        {
            Console.Error.WriteLine("Usage: set-admin-role <username> <Ceo|Admin>");
            return 2;
        }

        var username = args[1].Trim();
        var admin = await db.AdminUsers.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Username == username);
        if (admin is null || admin.Role is not (Yurt.Domain.Enums.AdminRole.Ceo or Yurt.Domain.Enums.AdminRole.Admin))
        {
            Console.Error.WriteLine($"No Admin or CEO account '{username}'. Run 'list-admins' to see them.");
            return 3;
        }

        admin.Role = role;
        admin.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Console.WriteLine($"'{admin.Username}' is now {role}. They get the new access at their next sign-in.");
        return 0;
    }

    private static async Task<int> ListAdminsAsync(ApplicationDbContext db)
    {
        var admins = await db.AdminUsers.IgnoreQueryFilters()
            .OrderBy(u => u.Role).ThenBy(u => u.Username)
            .Select(u => new { u.Username, u.Role, u.IsActive, u.MustChangePassword, u.TenantId })
            .ToListAsync();

        if (admins.Count == 0)
        {
            Console.WriteLine("No admin accounts exist.");
            return 0;
        }

        Console.WriteLine($"{"USERNAME",-24} {"ROLE",-12} {"ACTIVE",-7} {"MUSTCHANGE",-11} TENANT");
        foreach (var a in admins)
            Console.WriteLine(
                $"{a.Username,-24} {a.Role,-12} {a.IsActive,-7} {a.MustChangePassword,-11} {a.TenantId}");
        return 0;
    }

    private static async Task<int> RevokeAllSessionsAsync(ApplicationDbContext db)
    {
        var now = DateTime.UtcNow;
        // Set-based update so it stays fast even with many stored tokens.
        var count = await db.RefreshTokens.IgnoreQueryFilters()
            .Where(t => !t.IsRevoked && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.IsRevoked, true)
                .SetProperty(t => t.RevokedAt, now));

        Console.WriteLine(
            $"Revoked {count} refresh token(s). Every user must sign in again once their current " +
            "access token expires (~15 min). Combine with a JWT secret rotation to cut sessions off sooner.");
        return 0;
    }

    /// <summary>
    /// Reads the new password from $YURT_NEW_PASSWORD, or from stdin (piped or interactive).
    /// Preferring stdin keeps the password out of the process list and shell history.
    /// </summary>
    private static string? ReadNewPassword()
    {
        var fromEnv = Environment.GetEnvironmentVariable("YURT_NEW_PASSWORD");
        if (!string.IsNullOrEmpty(fromEnv))
            return fromEnv;

        if (Console.IsInputRedirected)
        {
            var line = Console.In.ReadLine();
            if (string.IsNullOrEmpty(line))
            {
                Console.Error.WriteLine(
                    "No password provided. Pipe it in (echo -n 'newpass' | ... reset-admin-password <user>) " +
                    "or set the YURT_NEW_PASSWORD environment variable.");
                return null;
            }
            return line;
        }

        Console.Write("New password: ");
        var password = ReadMasked();
        Console.Write("Confirm password: ");
        var confirm = ReadMasked();
        if (password != confirm)
        {
            Console.Error.WriteLine("Passwords do not match.");
            return null;
        }
        return password;
    }

    private static string ReadMasked()
    {
        var chars = new List<char>();
        ConsoleKeyInfo key;
        while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0) chars.RemoveAt(chars.Count - 1);
            }
            else if (!char.IsControl(key.KeyChar))
            {
                chars.Add(key.KeyChar);
            }
        }
        Console.WriteLine();
        return new string(chars.ToArray());
    }

    private static int Usage()
    {
        Console.Error.WriteLine(
            "Unknown command. Available: " + string.Join(", ", Commands));
        return 2;
    }
}
