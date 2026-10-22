namespace Yurt.Application.Common;

/// <summary>
/// A save tried to link one cafe's row to another cafe's row, or to move a row to another cafe.
/// The API answers it as "not found", the same as reading another cafe's row by id.
/// </summary>
public class CrossTenantReferenceException(string message) : InvalidOperationException(message);
