using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// The last order number given out at one branch on one business day (the branch's own calendar day). Order numbers
/// start again at 1 every day, so guests get a short number they can remember. Only <c>OrderNumbers.AssignAsync</c>
/// touches it: one atomic "insert or add 1" statement, so two orders placed at the same moment never share a number.
/// </summary>
public class OrderDayCounter : ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public DateOnly Day { get; set; }
    public int LastNumber { get; set; }
}
