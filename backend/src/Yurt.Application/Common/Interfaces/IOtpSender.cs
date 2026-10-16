namespace Yurt.Application.Common.Interfaces;

/// <summary>Delivers a one-time verification code to a customer's mobile number.</summary>
public interface IOtpSender
{
    /// <summary>Send the code. Throws <see cref="OtpSendException"/> when delivery fails.</summary>
    /// <param name="brandName">Fills the <c>{brand}</c> placeholder of the message template; null means "Yurt".</param>
    Task SendAsync(string mobileNumber, string code, string? brandName = null, CancellationToken ct = default);
}

public class OtpSendException : Exception
{
    public OtpSendException(string message, Exception? inner = null) : base(message, inner) { }
}
