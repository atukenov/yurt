using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>Small key/value store for platform-wide switches such as maintenance mode.</summary>
public class PlatformSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public static class PlatformSettingKeys
{
    public const string MaintenanceEnabled = "maintenance.enabled";
    public const string MaintenanceMessage = "maintenance.message";
    /// <summary>The cafe's brand (name, colours, logo, support contact) as one JSON document.</summary>
    public const string Brand = "brand.settings";
}
