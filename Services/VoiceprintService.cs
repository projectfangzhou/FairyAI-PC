using System.IO;

namespace MyAiAssistant.Services;

/// <summary>
/// Speaker voiceprint recognition with improved MFCC-like feature extraction.
/// Uses multi-band spectral analysis + delta features for better speaker discrimination.
/// Supports enrollment, verification, and adaptive threshold.
/// </summary>
public class VoiceprintService
{
    private readonly string _embeddingPath;
    private readonly float _threshold;
    private const int FeatureDim = 39; // 13 MFCC + 13 delta + 13 delta-delta
    private const int NumBands = 26;   // Mel-scale bands
    private const int NumFrames = 10;  // Temporal frames for delta computation
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public bool IsEnrolled => File.Exists(_embeddingPath);

    public VoiceprintService()
    {
        var config = ConfigManager.Load();
        _embeddingPath = string.IsNullOrWhiteSpace(config.WakeWord.VoiceprintEmbeddingPath)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "voiceprint.dat")
            : config.WakeWord.VoiceprintEmbeddingPath;
        _threshold = config.WakeWord.VoiceprintThreshold > 0 ? config.WakeWord.VoiceprintThreshold : 0.65f;
    }

    /// <summary>Enroll user voiceprint from audio sample. Returns enrollment quality score.</summary>
    public async Task<(bool Success, float Quality)> EnrollAsync(byte[] audioData)
    {
        try
        {
            if (audioData.Length < 88) // at least some audio
            {
                Log("Enroll: audio too short");
                return (false, 0f);
            }

            var features = ExtractFeatures(audioData);
            if (features.Length == 0) return (false, 0f);

            // Compute quality metric: variance across features (higher = more distinctive)
            float mean = features.Average();
            float variance = features.Sum(f => (f - mean) * (f - mean)) / features.Length;
            float quality = Math.Min(1f, variance * 100);

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

    /// <summary>Verify if audio matches enrolled voiceprint. Returns (isMatch, confidence).</summary>
    public (bool IsMatch, float Confidence) Verify(byte[] audioData)
    {
        if (!IsEnrolled) return (true, 1f); // no enrollment = allow all

        try
        {
            var stored = System.Text.Json.JsonSerializer.Deserialize<float[]>(
                File.ReadAllText(_embeddingPath));
            var current = ExtractFeatures(audioData);
            if (stored == null || stored.Length == 0 || current.Length == 0)
                return (true, 1f);

            // Multi-metric matching: cosine + euclidean + correlation
            float cosine = CosineSimilarity(stored, current);
            float euclidean = EuclideanSimilarity(stored, current);
            float correlation = PearsonCorrelation(stored, current);

            // Weighted ensemble
            float confidence = cosine * 0.5f + euclidean * 0.25f + correlation * 0.25f;
            bool isMatch = confidence >= _threshold;

            Log($"Verify: cos={cosine:F3} euc={euclidean:F3} corr={correlation:F3} ensemble={confidence:F3} threshold={_threshold} match={isMatch}");
            return (isMatch, confidence);
        }
        catch (Exception ex)
        {
            Log($"Verify error: {ex.Message}");
            return (true, 0.5f); // fail open with low confidence
        }
    }

    /// <summary>Extract 39-dim voice features: 13 band energies + deltas + delta-deltas.</summary>
    private static float[] ExtractFeatures(byte[] audioData)
    {
        // Parse WAV: skip 44-byte header, 16-bit PCM mono
        if (audioData.Length < 44 + 200) return Array.Empty<float>();

        int sampleCount = (audioData.Length - 44) / 2;
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short s = BitConverter.ToInt16(audioData, 44 + i * 2);
            samples[i] = s / 32768f;
        }

        // Apply pre-emphasis filter (highlights formants)
        for (int i = sampleCount - 1; i > 0; i--)
            samples[i] = samples[i] - 0.97f * samples[i - 1];

        // Split into frames and compute band energies
        int frameCount = Math.Min(NumFrames, sampleCount / 256);
        if (frameCount < 2) return Array.Empty<float>();

        var frameEnergies = new float[frameCount][];
        for (int f = 0; f < frameCount; f++)
        {
            int frameStart = f * (sampleCount / frameCount);
            int frameEnd = Math.Min(frameStart + sampleCount / frameCount, sampleCount);
            frameEnergies[f] = ComputeMelBandEnergies(samples, frameStart, frameEnd);
        }

        // Average across frames for static features
        var staticFeatures = new float[NumBands];
        for (int b = 0; b < NumBands; b++)
        {
            float sum = 0;
            for (int f = 0; f < frameCount; f++)
                sum += frameEnergies[f][b];
            staticFeatures[b] = sum / frameCount;
        }

        // Compute delta (first derivative) and delta-delta (second derivative)
        var delta = new float[NumBands];
        var deltaDelta = new float[NumBands];
        for (int b = 0; b < NumBands; b++)
        {
            float d1 = 0, d2 = 0;
            for (int f = 1; f < frameCount; f++)
            {
                d1 += frameEnergies[f][b] - frameEnergies[f - 1][b];
                if (f > 1) d2 += (frameEnergies[f][b] - frameEnergies[f - 1][b]) - (frameEnergies[f - 1][b] - frameEnergies[f - 2][b]);
            }
            delta[b] = d1 / Math.Max(1, frameCount - 1);
            deltaDelta[b] = d2 / Math.Max(1, frameCount - 2);
        }

        // Downsample from 26 bands to 13 for compactness
        var result = new float[FeatureDim];
        for (int i = 0; i < 13; i++)
        {
            result[i] = staticFeatures[i * 2];     // 13 static
            result[13 + i] = delta[i * 2];         // 13 delta
            result[26 + i] = deltaDelta[i * 2];    // 13 delta-delta
        }

        // Normalize
        float max = result.Max(Math.Abs);
        if (max > 0)
            for (int i = 0; i < result.Length; i++)
                result[i] /= max;

        return result;
    }

    /// <summary>Compute Mel-scale band energies for a frame of audio.</summary>
    private static float[] ComputeMelBandEnergies(float[] samples, int start, int end)
    {
        var energies = new float[NumBands];
        int frameLen = end - start;
        if (frameLen < 4) return energies;

        // Simple DFT-based band energy
        for (int b = 0; b < NumBands; b++)
        {
            float energy = 0;
            int bandStart = b * (frameLen / NumBands);
            int bandEnd = Math.Min(bandStart + frameLen / NumBands, frameLen);

            for (int k = bandStart; k < bandEnd; k++)
            {
                float re = 0, im = 0;
                float freq = (float)k / frameLen;
                for (int n = 0; n < frameLen; n += 2) // subsample for speed
                {
                    float angle = 2f * MathF.PI * freq * n;
                    re += samples[start + n] * MathF.Cos(angle);
                    im -= samples[start + n] * MathF.Sin(angle);
                }
                energy += re * re + im * im;
            }
            energies[b] = MathF.Log(1f + energy / Math.Max(1, bandEnd - bandStart));
        }
        return energies;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return dot / (float)(Math.Sqrt(normA) * Math.Sqrt(normB) + 1e-8);
    }

    private static float EuclideanSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        float sum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            float diff = a[i] - b[i];
            sum += diff * diff;
        }
        float distance = (float)Math.Sqrt(sum);
        return 1f / (1f + distance); // convert distance to similarity
    }

    private static float PearsonCorrelation(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        float meanA = a.Average(), meanB = b.Average();
        float num = 0, denA = 0, denB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            float da = a[i] - meanA, db = b[i] - meanB;
            num += da * db;
            denA += da * da;
            denB += db * db;
        }
        return num / (float)(Math.Sqrt(denA * denB) + 1e-8);
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [VP] {msg}\n"); } catch { }
    }
}
