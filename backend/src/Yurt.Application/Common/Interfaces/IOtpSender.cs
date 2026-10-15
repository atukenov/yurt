namespace Yurt.Application.Common.Interfaces;

/// <summary>Delivers a one-time verification code to a customer's mobile number.</summary>
public interface IOtpSender
{
    /// <summary>Send the code. Throws <see cref="OtpSendException"/> when delivery fails.</summary>
    Task SendAsync(string mobileNumber, string code, CancellationToken ct = default);
}

public class OtpSendException : Exception
{
    public OtpSendException(string message, Exception? inner = null) : base(message, inner) { }
}
