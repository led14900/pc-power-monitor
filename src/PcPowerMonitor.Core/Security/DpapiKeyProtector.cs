using System.Security.Cryptography;
using System.Text;

namespace PcPowerMonitor.Core.Security;

/// <summary>
/// Wraps Windows DPAPI (<see cref="ProtectedData"/>) to encrypt/decrypt a secret
/// (the Vertex AI service-account JSON key) at rest in <c>settings.json</c>. Scoped to <see
/// cref="DataProtectionScope.CurrentUser"/> — only the same Windows user profile on
/// the same machine can decrypt; copying the file elsewhere yields an undecryptable
/// blob (<see cref="TryDecrypt"/> returns false), never a crash.
/// </summary>
public static class DpapiKeyProtector
{
    /// <summary>Encrypts <paramref name="plainText"/>, returning a base64 blob safe to store in JSON.</summary>
    public static string Encrypt(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// Attempts to decrypt a base64 blob produced by <see cref="Encrypt"/>. Never throws —
    /// a corrupt blob, wrong user, or wrong machine all just return false with an empty
    /// <paramref name="plainText"/>, so callers never need to catch DPAPI exceptions.
    /// </summary>
    public static bool TryDecrypt(string base64, out string plainText)
    {
        plainText = string.Empty;
        if (string.IsNullOrWhiteSpace(base64))
            return false;

        try
        {
            var encrypted = Convert.FromBase64String(base64);
            var decrypted = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.CurrentUser);
            plainText = Encoding.UTF8.GetString(decrypted);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            // Corrupt base64, wrong user/machine, or tampered blob — treat as "not configured".
            return false;
        }
    }
}
