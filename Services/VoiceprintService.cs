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

    /// <summary>Verify voiceprint. Returns (isMatch, confidence).</summary>
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

            // Use only min length
            int len = Math.Min(stored.Length, current.Length);
            float cosine = CosineSimilarity(stored[..len], current[..len]);
            float euclidean = EuclideanSimilarity(stored[..len], current[..len]);
            float confidence = cosine * 0.7f + euclidean * 0.3f;
            bool isMatch = confidence >= _threshold;

            Log($"Verify: cos={cosine:F3} euc={euclidean:F3} conf={confidence:F3} thr={_threshold} match={isMatch}");
            return (isMatch, confidence);
        }
        catch (Exception ex)
        {
            Log($"Verify error: {ex.Message}");
            return (true, 0.5f);
        }
    }

    /// <summary>Extract simple but reliable features: energy stats + spectral features.</summary>
    private static float[] ExtractFeatures(byte[] audioData)
    {
        // Parse 16-bit PCM WAV (skip header)
        int headerSize = 44;
        if (audioData.Length < headerSize + 200) return Array.Empty<float>();

        int sampleCount = (audioData.Length - headerSize) / 2;
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
            samples[i] = BitConverter.ToInt16(audioData, headerSize + i * 2) / 32768f;

        // 1. Overall energy
        float energy = samples.Sum(s => s * s) / sampleCount;

        // 2. RMS
        float rms = (float)Math.Sqrt(energy);

        // 3. Zero-crossing rate
        int zcr = 0;
        for (int i = 1; i < sampleCount; i++)
            if ((samples[i] >= 0) != (samples[i - 1] >= 0)) zcr++;
        float zcrRate = (float)zcr / sampleCount;

        // 4. Spectral centroid (via simple DFT on subsample)
        float spectralCentroid = 0;
        int fftSize = 256;
        int frames = sampleCount / fftSize;
        if (frames > 0)
        {
            float weightedFreq = 0, totalMag = 0;
            for (int f = 0; f < Math.Min(frames, 10); f++)
            {
                int start = f * fftSize;
                for (int k = 1; k < fftSize / 2; k++)
                {
                    float re = 0, im = 0;
                    float freq = (float)k / fftSize;
                    for (int n = 0; n < fftSize; n += 4) // subsample for speed
                    {
                        float angle = 2f * MathF.PI * freq * n;
                        re += samples[start + n] * MathF.Cos(angle);
                        im -= samples[start + n] * MathF.Sin(angle);
                    }
                    float mag = (float)Math.Sqrt(re * re + im * im);
                    weightedFreq += mag * k;
                    totalMag += mag;
                }
            }
            spectralCentroid = totalMag > 0 ? weightedFreq / totalMag : 0;
        }

        // 5. Peak amplitude
        float peak = samples.Max(Math.Abs);

        // 6. Band energies (4 bands)
        var bandEnergies = new float[4];
        int bandSize = sampleCount / 4;
        for (int b = 0; b < 4; b++)
        {
            float sum = 0;
            for (int i = b * bandSize; i < (b + 1) * bandSize && i < sampleCount; i++)
                sum += samples[i] * samples[i];
            bandEnergies[b] = sum / bandSize;
        }

        // Combine features
        var features = new float[] { energy, rms, zcrRate, spectralCentroid, peak,
            bandEnergies[0], bandEnergies[1], bandEnergies[2], bandEnergies[3] };

        // Normalize
        float max = features.Max(Math.Abs);
        if (max > 0)
            for (int i = 0; i < features.Length; i++)
                features[i] /= max;

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
