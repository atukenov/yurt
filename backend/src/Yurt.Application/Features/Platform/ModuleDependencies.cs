using Yurt.Domain;

namespace Yurt.Application.Features.Platform;

/// <summary>
/// Soft dependencies between modules. They only produce warnings in the /platform tools; nothing is blocked,
/// because switching a module off must always be possible.
/// </summary>
public static class ModuleDependencies
{
    /// <summary>Module key → alternatives of which at least one must be on for it to work.</summary>
    private static readonly Dictionary<string, string[]> NeedsOneOf = new()
    {
        // The loyalty wallet lives in the POS today (iiko); own_pos replaces it (docs/MODULARIZATION_TODO.md, Phase 6).
        [ModuleKeys.Loyalty] = [ModuleKeys.IikoSync, ModuleKeys.OwnPos],
    };

    /// <summary>Warnings per module key, given the set of enabled modules.</summary>
    public static Dictionary<string, List<string>> GetWarnings(IReadOnlySet<string> enabled)
    {
        var result = new Dictionary<string, List<string>>();
        foreach (var (module, alternatives) in NeedsOneOf)
        {
            if (!enabled.Contains(module)) continue;
            if (alternatives.Any(enabled.Contains)) continue;
            result[module] = [$"'{module}' needs one of: {string.Join(", ", alternatives)}."];
        }
        return result;
    }
}
