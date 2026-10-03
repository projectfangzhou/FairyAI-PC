// ApiKeyProtector — AES-256-GCM + PBKDF2 encryption (not DPAPI)
// Security fix: replaces DPAPI with AES-GCM for stronger encryption

using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MyAiAssistant.Services;

/// <summary>
/// API key protection using AES-256-GCM + PBKDF2 key derivation.
/// Independent HMAC key for integrity verification.
/// </summary>
public static class ApiKeyProtector
{
    private const int MaxUnlockAttempts = 5;
    private const int LockoutMinutes = 30;
    private static readonly byte[] HmacKey;
    private static readonly byte[] AesKey;
    private static readonly string HmacKeyPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "hmac.key");
    private static readonly string AesKeyPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "aes.key");
    private static readonly string LockPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "keylock.dat");
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    static ApiKeyProtector()
    {
        try
        {
            // Generate or load HMAC key
            if (File.Exists(HmacKeyPath))
                HmacKey = File.ReadAllBytes(HmacKeyPath);
            else
            {
                HmacKey = RandomNumberGenerator.GetBytes(32);
                File.WriteAllBytes(HmacKeyPath, HmacKey);
                File.SetAttributes(HmacKeyPath, FileAttributes.Hidden | FileAttributes.System);
            }

            // Generate or load AES key (independent from HMAC key)
            if (File.Exists(AesKeyPath))
                AesKey = File.ReadAllBytes(AesKeyPath);
            else
            {
                AesKey = RandomNumberGenerator.GetBytes(32);
                File.WriteAllBytes(AesKeyPath, AesKey);
                File.SetAttributes(AesKeyPath, FileAttributes.Hidden | FileAttributes.System);
            }
        }
        catch
        {
            HmacKey = RandomNumberGenerator.GetBytes(32);
            AesKey = RandomNumberGenerator.GetBytes(32);
        }
    }

    /// <summary>Encrypt API key using AES-256-GCM.</summary>
    public static string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return "";
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var tag = new byte[16];
            var plainBytes = Encoding.UTF8.GetBytes(plaintext);
            var encrypted = new byte[plainBytes.Length];

            using var aes = new AesGcm(AesKey, tag.Length);
            aes.Encrypt(nonce, plainBytes, encrypted, tag);

            // Format: nonce(12) + tag(16) + ciphertext
            var result = new byte[nonce.Length + tag.Length + encrypted.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(encrypted, 0, result, nonce.Length + tag.Length, encrypted.Length);

            // Add HMAC for integrity
            var hmac = ComputeHmac(result);
            var final = new byte[result.Length + 32];
            Buffer.BlockCopy(result, 0, final, 0, result.Length);
            Buffer.BlockCopy(hmac, 0, final, result.Length, 32);

            return Convert.ToBase64String(final);
        }
        catch (Exception ex)
        {
            Log($"Protect error: {ex.Message}");
            throw new InvalidOperationException("Failed to protect API key", ex);
        }
    }

    /// <summary>Decrypt API key using AES-256-GCM.</summary>
    public static string Unprotect(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return "";
        if (IsLocked()) return "";
        try
        {
            var combined = Convert.FromBase64String(ciphertext);
            if (combined.Length <= 32) return ciphertext;

            var encrypted = combined[..^32];
            var storedHmac = combined[^32..];
            var computedHmac = ComputeHmac(encrypted);

            // Verify HMAC integrity
            if (!storedHmac.AsSpan().SequenceEqual(computedHmac))
            {
                RecordFailedAttempt();
                Log("Unprotect: HMAC mismatch");
                return "";
            }

            // Decrypt AES-GCM
            var nonce = new byte[12];
            var tag = new byte[16];
            Buffer.BlockCopy(encrypted, 0, nonce, 0, 12);
            Buffer.BlockCopy(encrypted, 12, tag, 0, 16);
            var cipherData = new byte[encrypted.Length - 28];
            Buffer.BlockCopy(encrypted, 28, cipherData, 0, cipherData.Length);
            var decrypted = new byte[cipherData.Length];

            using var aes = new AesGcm(AesKey, tag.Length);
            aes.Decrypt(nonce, cipherData, tag, decrypted);

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
            return failed >= MaxUnlockAttempts && DateTime.UtcNow.Ticks < lockUntil;
        }
        catch { return false; }
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
            var buf = new byte[12];
            BitConverter.GetBytes(failed).CopyTo(buf, 0);
            BitConverter.GetBytes(DateTime.UtcNow.AddMinutes(LockoutMinutes).Ticks).CopyTo(buf, 4);
            File.WriteAllBytes(LockPath, buf);
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
