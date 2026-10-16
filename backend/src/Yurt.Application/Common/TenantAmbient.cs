namespace Yurt.Application.Common;

/// <summary>
/// The tenant of the code currently running, for the few singletons that cannot take the scoped tenant context
/// (the module and maintenance gates cache per cafe). Set whenever <see cref="Interfaces.ITenantContext.Set"/> is called,
/// so it follows the request or background-job that set it.
/// </summary>
public static class TenantAmbient
{
    private static readonly AsyncLocal<Guid?> Value = new();

    public static Guid? Current
    {
        get => Value.Value;
        set => Value.Value = value;
    }
}
