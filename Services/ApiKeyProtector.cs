using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MyAiAssistant.Services;

/// <summary>
/// API key protection: hash-at-rest encryption + brute-force lockout.
/// Keys are encrypted with DPAPI + HMAC before storing in config.json.
/// </summary>
public static class ApiKeyProtector
{
    private const string Entropy = "FairyAI_KeyVault_2026";
    private const int MaxUnlockAttempts = 5;
    private const int LockoutMinutes = 30;
    // Independent HMAC key — NOT the same as DPAPI entropy
    private static readonly byte[] HmacKey;
    private static readonly string HmacKeyPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "hmac.key");
    private static readonly string LockPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "keylock.dat");
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    static ApiKeyProtector()
    {
        // Generate or load HMAC key (independent from DPAPI entropy)
        try
        {
            if (File.Exists(HmacKeyPath))
                HmacKey = File.ReadAllBytes(HmacKeyPath);
            else
            {
                HmacKey = RandomNumberGenerator.GetBytes(32);
                File.WriteAllBytes(HmacKeyPath, HmacKey);
                File.SetAttributes(HmacKeyPath, FileAttributes.Hidden | FileAttributes.System);
            }
        }
        catch
        {
            HmacKey = RandomNumberGenerator.GetBytes(32);
        }
    }

    /// <summary>Encrypt an API key for storage.</summary>
    public static string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return "";
        try
        {
            var bytes = Encoding.UTF8.GetBytes(plaintext);
            var entropy = Encoding.UTF8.GetBytes(Entropy);
            var encrypted = ProtectedData.Protect(bytes, entropy, DataProtectionScope.CurrentUser);
            // Add HMAC for integrity
            var hmac = ComputeHmac(encrypted);
            var combined = new byte[encrypted.Length + 32];
            Buffer.BlockCopy(encrypted, 0, combined, 0, encrypted.Length);
            Buffer.BlockCopy(hmac, 0, combined, encrypted.Length, 32);
            return Convert.ToBase64String(combined);
        }
        catch (Exception ex)
        {
            Log($"Protect error: {ex.Message}");
            throw new InvalidOperationException("Failed to protect API key — refusing to store plaintext", ex);
        }
    }

    /// <summary>Decrypt an API key from storage.</summary>
    public static string Unprotect(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return "";
        if (IsLocked()) return "";
        try
        {
            var combined = Convert.FromBase64String(ciphertext);
            if (combined.Length <= 32) return ciphertext; // not encrypted

            var encrypted = combined[..^32];
            var storedHmac = combined[^32..];
            var computedHmac = ComputeHmac(encrypted);

            // Verify integrity — reject if tampered
            if (!storedHmac.AsSpan().SequenceEqual(computedHmac))
            {
                RecordFailedAttempt();
                Log("Unprotect: HMAC mismatch — possible tampering");
                return "";
            }

            var entropy = Encoding.UTF8.GetBytes(Entropy);
            var decrypted = ProtectedData.Unprotect(encrypted, entropy, DataProtectionScope.CurrentUser);
            ResetAttempts();
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex)
        {
            RecordFailedAttempt();
            Log($"Unprotect error: {ex.Message}");
            return "";
        }
    }

    /// <summary>Check if key vault is locked due to brute-force attempts.</summary>
    public static bool IsLocked()
    {
        try
        {
            if (!File.Exists(LockPath)) return false;
            var data = File.ReadAllBytes(LockPath);
            if (data.Length < 12) return false;

            int failed = BitConverter.ToInt32(data, 0);
            long lockUntil = BitConverter.ToInt64(data, 4);

            if (failed >= MaxUnlockAttempts && DateTime.UtcNow.Ticks < lockUntil)
            {
                var remaining = TimeSpan.FromTicks(lockUntil - DateTime.UtcNow.Ticks);
                Log($"Key vault locked for {remaining.TotalMinutes:F0} more minutes");
                return true;
            }
            return false;
        }
        catch { return false; }
    }

    /// <summary>Get remaining lockout time (0 if not locked).</summary>
    public static TimeSpan GetLockoutRemaining()
    {
        try
        {
            if (!File.Exists(LockPath)) return TimeSpan.Zero;
            var data = File.ReadAllBytes(LockPath);
            if (data.Length < 12) return TimeSpan.Zero;
            long lockUntil = BitConverter.ToInt64(data, 4);
            if (DateTime.UtcNow.Ticks < lockUntil)
                return TimeSpan.FromTicks(lockUntil - DateTime.UtcNow.Ticks);
        }
        catch { }
        return TimeSpan.Zero;
    }

    private static void RecordFailedAttempt()
    {
        try
        {
            int failed = 0;
            if (File.Exists(LockPath))
            {
                var data = File.ReadAllBytes(LockPath);
                if (data.Length >= 4) failed = BitConverter.ToInt32(data, 0);
            }
            failed++;

            var lockUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes).Ticks;
            var buf = new byte[12];
            BitConverter.GetBytes(failed).CopyTo(buf, 0);
            BitConverter.GetBytes(lockUntil).CopyTo(buf, 4);
            File.WriteAllBytes(LockPath, buf);

            Log($"Key unlock attempt failed ({failed}/{MaxUnlockAttempts})");
        }
        catch { }
    }

    private static void ResetAttempts()
    {
        try { if (File.Exists(LockPath)) File.Delete(LockPath); } catch { }
    }

    private static byte[] ComputeHmac(byte[] data)
    {
        using var hmac = new HMACSHA256(HmacKey);
        return hmac.ComputeHash(data);
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [KEY] {msg}\n"); } catch { }
    }
}
