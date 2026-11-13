namespace Yurt.Application.Features.Stock;

/// <summary>Cleaning of the numbers staff type or scan for a dish or an ingredient.</summary>
public static class Barcodes
{
    public const int MaxLength = 64;

    /// <summary>
    /// Trims the code. Empty means "no barcode" (null). Only letters, digits and - . _ are allowed, so a scanner that adds a
    /// stray control character cannot save an unreadable code. <paramref name="error"/> is set when the code is refused.
    /// </summary>
    public static string? Clean(string? raw, out string? error)
    {
        error = null;
        var code = raw?.Trim();
        if (string.IsNullOrEmpty(code)) return null;
        if (code.Length > MaxLength)
        {
            error = $"A barcode is at most {MaxLength} characters.";
            return null;
        }
        if (!code.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_'))
        {
            error = "A barcode has only letters, digits and - . _";
            return null;
        }
        return code;
    }
}
