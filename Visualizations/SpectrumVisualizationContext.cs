using System.Windows;
using System.Windows.Media;
using CSCore;
using NAudio.Dsp;

namespace Shawellaby.RadioCypress.Visualizations;

public sealed class SpectrumVisualizationContext
{
    private double _visualMax = 0.0001;

    public SpectrumVisualizationContext(
        int fftSize,
        float[] fftBuffer,
        object fftLock,
        Func<int> getFftBufferIndex,
        Func<double> getSampleRate,
        Func<double> getSmoothedGain,
        double[] bucketFrequencies,
        Func<(double Left, double Right)>? getStereoLevels = null)
    {
        FftSize = fftSize;
        FftBuffer = fftBuffer;
        FftLock = fftLock;
        GetFftBufferIndex = getFftBufferIndex;
        GetSampleRate = getSampleRate;
        GetSmoothedGain = getSmoothedGain;
        BucketFrequencies = bucketFrequencies;
        GetStereoLevels = getStereoLevels ?? (() => (0.0, 0.0));
        FftComplex = new Complex[fftSize];
    }

    public int FftSize { get; }

    public float[] FftBuffer { get; }

    public object FftLock { get; }

    public Func<int> GetFftBufferIndex { get; }

    public Func<double> GetSampleRate { get; }

    public Func<double> GetSmoothedGain { get; }

    public double[] BucketFrequencies { get; }

    public Func<(double Left, double Right)> GetStereoLevels { get; }

    public Complex[] FftComplex { get; }

    public double VisualMax
    {
        get => _visualMax;
        set => _visualMax = value;
    }

    public double[] GetFrequencyBucketLevels()
    {
        double[] bucketFrequencies = BucketFrequencies;
        double[] levels = new double[bucketFrequencies.Length];
        float[] samples = new float[FftSize];

        lock (FftLock)
        {
            int srcIndex = GetFftBufferIndex();

            for (int i = 0; i < FftSize; i++)
            {
                samples[i] = FftBuffer[srcIndex];
                srcIndex = (srcIndex + 1) % FftSize;
            }
        }

        for (int i = 0; i < FftSize; i++)
        {
            FftComplex[i].X = (float)(samples[i] * FastFourierTransform.HammingWindow(i, FftSize));
            FftComplex[i].Y = 0;
        }

        FastFourierTransform.FFT(true, (int)Math.Log(FftSize, 2), FftComplex);

        double sampleRate = GetSampleRate();
        double nyquist = sampleRate / 2.0;

        for (int bucket = 0; bucket < bucketFrequencies.Length; bucket++)
        {
            double centerFrequency = bucketFrequencies[bucket];
            double lowFrequency = bucket == 0 ? 0 : Math.Sqrt(bucketFrequencies[bucket - 1] * centerFrequency);
            double highFrequency = bucket == bucketFrequencies.Length - 1 ? nyquist : Math.Sqrt(centerFrequency * bucketFrequencies[bucket + 1]);

            int startBin = (int)Math.Floor(lowFrequency / nyquist * (FftSize / 2));
            int endBin = (int)Math.Ceiling(highFrequency / nyquist * (FftSize / 2));

            startBin = Math.Clamp(startBin, 0, FftSize / 2);
            endBin = Math.Clamp(Math.Max(endBin, startBin + 1), 1, FftSize / 2);

            double magnitude = 0;

            for (int bin = startBin; bin < endBin; bin++)
            {
                double re = FftComplex[bin].X;
                double im = FftComplex[bin].Y;
                magnitude += Math.Sqrt(re * re + im * im);
            }

            magnitude /= Math.Max(1, endBin - startBin);

            double compressed = Math.Log10(1 + magnitude * GetSmoothedGain());
            VisualMax = Math.Max(VisualMax * 0.99, compressed);

            levels[bucket] = Math.Clamp(compressed / Math.Max(VisualMax, 0.0001), 0.0, 1.0);
        }

        return levels;
    }

    public static Color GetPsychedelicColor(double hue, byte alpha)
    {
        hue = ((hue % 360) + 360) % 360;

        double c = 1.0;
        double x = c * (1 - Math.Abs((hue / 60.0) % 2 - 1));
        double m = 0.0;

        double r;
        double g;
        double b;

        if (hue < 60)
        {
            r = c;
            g = x;
            b = 0;
        }
        else if (hue < 120)
        {
            r = x;
            g = c;
            b = 0;
        }
        else if (hue < 180)
        {
            r = 0;
            g = c;
            b = x;
        }
        else if (hue < 240)
        {
            r = 0;
            g = x;
            b = c;
        }
        else if (hue < 300)
        {
            r = x;
            g = 0;
            b = c;
        }
        else
        {
            r = c;
            g = 0;
            b = x;
        }

        return Color.FromArgb(alpha, (byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }

    public static string FormatFrequencyLabel(double frequency)
    {
        if (frequency >= 1000)
        {
            double value = frequency / 1000.0;
            return value % 1 == 0 ? $"{value:0}k" : $"{value:0.##}k";
        }

        return frequency % 1 == 0 ? $"{frequency:0}" : $"{frequency:0.#}";
    }
}