using System.IO;

namespace MyAiAssistant.Services;

/// <summary>
/// Custom noise reduction algorithm for voice input.
/// Implements spectral subtraction + adaptive gain + voice activity detection
/// to enhance speech clarity and increase recognition sensitivity.
/// </summary>
public class NoiseReductionService
{
    private const int FrameSize = 256;
    private const int Overlap = 128;
    private const float NoiseFloorFactor = 0.3f;
    private const float SpectralFloor = 0.01f;
    private const float SensitivityGain = 1.8f;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    // Running noise profile
    private float[]? _noiseProfile;
    private bool _noiseProfileReady;
    private int _calibrationFrames;
    private const int CalibrationFrameCount = 20;

    /// <summary>Process audio buffer through noise reduction pipeline.</summary>
    public float[] Process(float[] audio)
    {
        if (audio.Length < FrameSize) return audio;

        var output = new float[audio.Length];
        var hopSize = FrameSize - Overlap;
        var window = CreateHannWindow(FrameSize);

        // Process overlapping frames
        for (int pos = 0; pos + FrameSize <= audio.Length; pos += hopSize)
        {
            var frame = new float[FrameSize];
            for (int i = 0; i < FrameSize; i++)
                frame[i] = audio[pos + i] * window[i];

            // Forward FFT
            var spectrum = FFT(frame);

            // Magnitude spectrum
            var magnitude = new float[FrameSize / 2 + 1];
            var phase = new float[FrameSize / 2 + 1];
            for (int i = 0; i <= FrameSize / 2; i++)
            {
                magnitude[i] = (float)Math.Sqrt(spectrum[2 * i] * spectrum[2 * i] + spectrum[2 * i + 1] * spectrum[2 * i + 1]);
                phase[i] = (float)Math.Atan2(spectrum[2 * i + 1], spectrum[2 * i]);
            }

            // Update noise profile during first N frames
            if (!_noiseProfileReady)
            {
                if (_noiseProfile == null)
                    _noiseProfile = new float[FrameSize / 2 + 1];
                for (int i = 0; i <= FrameSize / 2; i++)
                    _noiseProfile[i] = (_noiseProfile[i] * _calibrationFrames + magnitude[i]) / (_calibrationFrames + 1);
                _calibrationFrames++;
                if (_calibrationFrames >= CalibrationFrameCount)
                    _noiseProfileReady = true;
            }

            // Spectral subtraction
            if (_noiseProfileReady && _noiseProfile != null)
            {
                for (int i = 0; i <= FrameSize / 2; i++)
                {
                    float noiseEst = _noiseProfile[i] * NoiseFloorFactor;
                    float enhanced = magnitude[i] - noiseEst;

                    // Spectral floor to prevent musical noise
                    if (enhanced < SpectralFloor * magnitude[i])
                        enhanced = SpectralFloor * magnitude[i];

                    magnitude[i] = Math.Max(0, enhanced);
                }
            }

            // Voice Activity Detection: boost speech frames
            float frameEnergy = 0;
            for (int i = 0; i < magnitude.Length; i++)
                frameEnergy += magnitude[i] * magnitude[i];
            frameEnergy /= magnitude.Length;

            bool isSpeech = frameEnergy > 0.001f;
            if (isSpeech)
            {
                // Apply sensitivity gain to speech
                for (int i = 0; i <= FrameSize / 2; i++)
                    magnitude[i] *= SensitivityGain;
            }
            else
            {
                // Attenuate non-speech frames
                for (int i = 0; i <= FrameSize / 2; i++)
                    magnitude[i] *= 0.5f;
            }

            // Reconstruct spectrum from magnitude + phase
            for (int i = 0; i <= FrameSize / 2; i++)
            {
                spectrum[2 * i] = magnitude[i] * (float)Math.Cos(phase[i]);
                spectrum[2 * i + 1] = magnitude[i] * (float)Math.Sin(phase[i]);
            }
            // Mirror for conjugate symmetry
            for (int i = 1; i < FrameSize / 2; i++)
            {
                spectrum[2 * (FrameSize - i)] = spectrum[2 * i];
                spectrum[2 * (FrameSize - i) + 1] = -spectrum[2 * i + 1];
            }

            // Inverse FFT
            var processed = IFFT(spectrum);

            // Overlap-add
            for (int i = 0; i < FrameSize; i++)
                output[pos + i] += processed[i] * window[i];
        }

        // Normalize overlap-add
        for (int i = 0; i < output.Length; i++)
            output[i] /= 2.0f; // Hann window overlap compensation

        Log($"NoiseReduction: processed {audio.Length} samples, noise_profile_ready={_noiseProfileReady}");
        return output;
    }

    /// <summary>Process 16-bit PCM byte buffer.</summary>
    public byte[] ProcessPcm16(byte[] pcm)
    {
        var samples = new float[pcm.Length / 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = BitConverter.ToInt16(pcm, i * 2) / 32768f;

        var processed = Process(samples);

        var result = new byte[processed.Length * 2];
        for (int i = 0; i < processed.Length; i++)
        {
            short s = (short)(Math.Clamp(processed[i], -1f, 1f) * 32767f);
            BitConverter.GetBytes(s).CopyTo(result, i * 2);
        }
        return result;
    }

    /// <summary>Reset noise profile (e.g., when moving to a new environment).</summary>
    public void ResetNoiseProfile()
    {
        _noiseProfile = null;
        _noiseProfileReady = false;
        _calibrationFrames = 0;
    }

    private static float[] CreateHannWindow(int size)
    {
        var window = new float[size];
        for (int i = 0; i < size; i++)
            window[i] = 0.5f * (1 - (float)Math.Cos(2 * Math.PI * i / (size - 1)));
        return window;
    }

    /// <summary>Simple radix-2 FFT (in-place, real input → complex output packed).</summary>
    private static float[] FFT(float[] input)
    {
        int n = input.Length;
        var output = new float[2 * n];
        for (int i = 0; i < n; i++)
        {
            output[2 * i] = input[i];
            output[2 * i + 1] = 0;
        }
        FFTInPlace(output, n);
        return output;
    }

    private static float[] IFFT(float[] spectrum)
    {
        int n = spectrum.Length / 2;
        var output = new float[n];
        // Conjugate
        for (int i = 0; i < n; i++)
            spectrum[2 * i + 1] = -spectrum[2 * i + 1];
        FFTInPlace(spectrum, n);
        for (int i = 0; i < n; i++)
            output[i] = spectrum[2 * i] / n;
        return output;
    }

    private static void FFTInPlace(float[] data, int n)
    {
        // Bit reversal
        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (data[2 * i], data[2 * j]) = (data[2 * j], data[2 * i]);
                (data[2 * i + 1], data[2 * j + 1]) = (data[2 * j + 1], data[2 * i + 1]);
            }
            int k = n / 2;
            while (k <= j) { j -= k; k /= 2; }
            j += k;
        }

        // Cooley-Tukey
        for (int step = 1; step < n; step *= 2)
        {
            float angle = (float)(-Math.PI / step);
            float wReal = (float)Math.Cos(angle);
            float wImag = (float)Math.Sin(angle);
            for (int group = 0; group < n; group += 2 * step)
            {
                float curReal = 1, curImag = 0;
                for (int pair = 0; pair < step; pair++)
                {
                    int a = group + pair;
                    int b = a + step;
                    float tReal = curReal * data[2 * b] - curImag * data[2 * b + 1];
                    float tImag = curReal * data[2 * b + 1] + curImag * data[2 * b];
                    data[2 * b] = data[2 * a] - tReal;
                    data[2 * b + 1] = data[2 * a + 1] - tImag;
                    data[2 * a] += tReal;
                    data[2 * a + 1] += tImag;
                    float newReal = curReal * wReal - curImag * wImag;
                    curImag = curReal * wImag + curImag * wReal;
                    curReal = newReal;
                }
            }
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [NR] {msg}\n"); } catch { }
    }
}
