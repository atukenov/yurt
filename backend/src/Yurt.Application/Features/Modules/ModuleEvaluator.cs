using Yurt.Domain;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Modules;

/// <summary>
/// Turns a license row into a status. Pure and side-effect free so the fail-open rules can be unit tested.
/// </summary>
public static class ModuleEvaluator
{
    /// <summary>
    /// A missing row means enabled (fail-open) and <see cref="ModuleKeys.Core"/> can never be switched off.
    /// </summary>
    public static ModuleStatus Evaluate(string key, ModuleLicense? license, DateTime utcNow, int graceDays)
    {
        if (license is null)
            return new ModuleStatus(key, ModuleState.Active, null, null, "default", null);

        var source = license.Source.ToString();

        if (key == ModuleKeys.Core)
            return new ModuleStatus(key, ModuleState.Active, null, null, source, license.Notes);

        if (!license.Enabled)
            return new ModuleStatus(key, ModuleState.Disabled, license.ValidUntil, null, source, license.Notes);

        if (license.ValidUntil is not { } validUntil || utcNow <= validUntil)
            return new ModuleStatus(key, ModuleState.Active, license.ValidUntil, null, source, license.Notes);

        var graceEnds = validUntil.AddDays(Math.Max(0, graceDays));
        var state = utcNow <= graceEnds ? ModuleState.Grace : ModuleState.Expired;
        return new ModuleStatus(key, state, validUntil, graceEnds, source, license.Notes);
    }
}
