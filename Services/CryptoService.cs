using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MyAiAssistant.Services;

/// <summary>
/// Encrypts/decrypts data using Windows DPAPI (Data Protection API).
/// Data is encrypted per-user per-machine — no password needed.
/// </summary>
public static class CryptoService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FairyAI_v1_Salt!");

    public static string Encrypt(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }

    public static string Decrypt(string encryptedBase64)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedBase64);
        var plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public static bool IsEncryptionSupported => true; // Windows only
}

/// <summary>
/// Filters sensitive information (API keys, tokens, passwords) from text.
/// Used to prevent AI responses from leaking credentials.
/// </summary>
public static class SensitiveInfoFilter
{
    private static readonly string[] Patterns = new[]
    {
        // API Keys: "sk-...", "key_...", "AKIA..."
        @"sk-[A-Za-z0-9]{20,}",
        @"key[_-]?[A-Za-z0-9]{20,}",
        @"AKIA[A-Z0-9]{16}",
        // Bearer tokens
        @"Bearer\s+[A-Za-z0-9\-._~+/]+=*",
        // Common password patterns
        @"password[=:]\s*\S{6,}",
        @"pwd[=:]\s*\S{6,}",
        // Connection strings
        @"Server=[^;]+;[^;]*Password=[^;]+",
    };

    /// <summary>Filter sensitive info from AI response text.</summary>
    public static string Filter(string text)
    {
        var result = text;
        foreach (var pattern in Patterns)
        {
            result = System.Text.RegularExpressions.Regex.Replace(
                result, pattern, "[已隐藏]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return result;
    }

    /// <summary>Check if text contains potential sensitive info.</summary>
    public static bool ContainsSensitiveInfo(string text)
    {
        foreach (var pattern in Patterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;
        }
        return false;
    }
}
