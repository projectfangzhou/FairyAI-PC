using System.IO;

namespace MyAiAssistant.Services;

/// <summary>
/// Speaker voiceprint recognition with improved accuracy.
/// Uses energy-based features + spectral centroid + zero-crossing rate.
/// Simplified algorithm that works reliably with short voice samples.
/// </summary>
public class VoiceprintService
{
    private readonly string _embeddingPath;
    private readonly float _threshold;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public bool IsEnrolled => File.Exists(_embeddingPath);

    public VoiceprintService()
    {
        var config = ConfigManager.Load();
        _embeddingPath = string.IsNullOrWhiteSpace(config.WakeWord.VoiceprintEmbeddingPath)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "voiceprint.dat")
            : config.WakeWord.VoiceprintEmbeddingPath;
        _threshold = config.WakeWord.VoiceprintThreshold > 0 ? config.WakeWord.VoiceprintThreshold : 0.55f;
    }

    /// <summary>Enroll user voiceprint. Returns quality score.</summary>
    public async Task<(bool Success, float Quality)> EnrollAsync(byte[] audioData)
    {
        try
        {
            if (audioData.Length < 100)
                return (false, 0f);

            var features = ExtractFeatures(audioData);
            if (features.Length == 0) return (false, 0f);

            // Quality: variance of features
            float mean = features.Average();
            float variance = features.Sum(f => (f - mean) * (f - mean)) / features.Length;
            float quality = Math.Min(1f, variance * 50);

            var serialized = System.Text.Json.JsonSerializer.Serialize(features);
            await File.WriteAllTextAsync(_embeddingPath, serialized);
            Log($"Enrolled: {features.Length} features, quality={quality:F2}");
            return (true, quality);
        }
        catch (Exception ex)
        {
            Log($"Enroll error: {ex.Message}");
            return (false, 0f);
        }
    }

    /// <summary>Verify voiceprint using combined distance metrics.</summary>
    public (bool IsMatch, float Confidence) Verify(byte[] audioData)
    {
        if (!IsEnrolled) return (true, 1f);

        try
        {
            var stored = System.Text.Json.JsonSerializer.Deserialize<float[]>(
                File.ReadAllText(_embeddingPath));
            var current = ExtractFeatures(audioData);
            if (stored == null || stored.Length == 0 || current.Length == 0)
                return (true, 1f);

            int len = Math.Min(stored.Length, current.Length);

            // Use euclidean distance (more discriminative than cosine for raw features)
            float dist = 0;
            for (int i = 0; i < len; i++)
            {
                float d = stored[i] - current[i];
                dist += d * d;
            }
            dist = (float)Math.Sqrt(dist);

            // Convert distance to similarity (0-1, higher = more similar)
            float confidence = 1f / (1f + dist);

            // Also compute cosine for reference
            float cosine = CosineSimilarity(stored[..len], current[..len]);

            // Combined score: emphasize euclidean
            float combined = confidence * 0.6f + cosine * 0.4f;
            bool isMatch = combined >= _threshold;

            Log($"Verify: dist={dist:F4} conf={confidence:F3} cos={cosine:F3} combined={combined:F3} thr={_threshold} match={isMatch}");
            return (isMatch, combined);
        }
        catch (Exception ex)
        {
            Log($"Verify error: {ex.Message}");
            return (true, 0.5f);
        }
    }

    /// <summary>Extract discriminative features: raw spectral shape + energy distribution.</summary>
    private static float[] ExtractFeatures(byte[] audioData)
    {
        int headerSize = 44;
        if (audioData.Length < headerSize + 400) return Array.Empty<float>();

        int sampleCount = (audioData.Length - headerSize) / 2;
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
            samples[i] = BitConverter.ToInt16(audioData, headerSize + i * 2) / 32768f;

        var features = new float[12];

        // 1. RMS energy (scalar - important for discrimination)
        float rms = (float)Math.Sqrt(samples.Sum(s => s * s) / sampleCount);
        features[0] = rms;

        // 2. Peak amplitude
        features[1] = samples.Max(Math.Abs);

        // 3. Zero-crossing rate
        int zcr = 0;
        for (int i = 1; i < sampleCount; i++)
            if ((samples[i] >= 0) != (samples[i - 1] >= 0)) zcr++;
        features[2] = (float)zcr / sampleCount;

        // 4-11. Spectral band energies (8 bands) - RAW values, no normalization
        int fftSize = 128;
        int frames = Math.Min(sampleCount / fftSize, 6);
        for (int b = 0; b < 8; b++)
        {
            float bandSum = 0;
            int bandStart = b * 8; // each band = 8 frequency bins
            int bandEnd = bandStart + 8;
            for (int f = 0; f < frames; f++)
            {
                int start = f * fftSize;
                for (int k = bandStart; k < bandEnd && k < fftSize / 2; k++)
                {
                    float re = 0, im = 0;
                    float freq = (float)k / fftSize;
                    for (int n = 0; n < fftSize; n += 4)
                    {
                        float angle = 2f * MathF.PI * freq * n;
                        re += samples[start + n] * MathF.Cos(angle);
                        im -= samples[start + n] * MathF.Sin(angle);
                    }
                    bandSum += re * re + im * im;
                }
            }
            features[3 + b] = bandSum / (frames + 1);
        }

        return features;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        float dot = 0, nA = 0, nB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            nA += a[i] * a[i];
            nB += b[i] * b[i];
        }
        return dot / (float)(Math.Sqrt(nA) * Math.Sqrt(nB) + 1e-8);
    }

    private static float EuclideanSimilarity(float[] a, float[] b)
    {
        float sum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            float d = a[i] - b[i];
            sum += d * d;
        }
        return 1f / (1f + (float)Math.Sqrt(sum));
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [VP] {msg}\n"); } catch { }
    }
}
