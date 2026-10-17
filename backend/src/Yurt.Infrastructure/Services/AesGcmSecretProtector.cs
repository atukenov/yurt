using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using Yurt.Application.Common.Interfaces;

namespace Yurt.Infrastructure.Services;

/// <summary>
/// AES-256-GCM with a server-wide master key (Secrets:MasterKey, Base64 of 32 random bytes). The cafe id and key name
/// are authenticated data, so a stored value only decrypts for the cafe and key it was written for.
/// Format: "v1:" + Base64(nonce[12] + tag[16] + cipher).
/// </summary>
public class AesGcmSecretProtector : ISecretProtector
{
    private const string Prefix = "v1:";
    private const int NonceSize = 12, TagSize = 16;
    private readonly byte[]? _key;

    public AesGcmSecretProtector(IConfiguration configuration)
    {
        var raw = configuration["Secrets:MasterKey"];
        if (string.IsNullOrWhiteSpace(raw)) return;
        try
        {
            var bytes = Convert.FromBase64String(raw.Trim());
            if (bytes.Length == 32) _key = bytes;
        }
        catch (FormatException) { /* treated as not configured */ }
    }

    public bool IsAvailable => _key is not null;

    public string Protect(Guid tenantId, string key, string plainText)
    {
        if (_key is null) throw new InvalidOperationException("Secrets:MasterKey is not configured.");
        var plain = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var cipher = new byte[plain.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag, Aad(tenantId, key));
        return Prefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string? Unprotect(Guid tenantId, string key, string cipherText)
    {
        if (_key is null || !cipherText.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        try
        {
            var data = Convert.FromBase64String(cipherText[Prefix.Length..]);
            if (data.Length < NonceSize + TagSize) return null;
            var nonce = data[..NonceSize];
            var tag = data[NonceSize..(NonceSize + TagSize)];
            var cipher = data[(NonceSize + TagSize)..];
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain, Aad(tenantId, key));
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception e) when (e is CryptographicException or FormatException)
        {
            return null;
        }
    }

    private static byte[] Aad(Guid tenantId, string key) => Encoding.UTF8.GetBytes($"{tenantId:N}|{key}");
}
