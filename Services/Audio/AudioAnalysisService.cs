using Shawellaby.RadioCypress.Visualizations;

namespace Shawellaby.RadioCypress.Services.Audio;

public sealed class AudioAnalysisService : IAudioAnalysisService
{
    private const int FftSize = 1024;
    private const int StereoSampleBufferSize = 1024;
    private const double DefaultSampleRate = 44100.0;

    private readonly object _fftLock = new();
    private readonly object _stereoLock = new();

    private readonly float[] _fftBuffer = new float[FftSize];
    private readonly float[] _leftSamples = new float[StereoSampleBufferSize];
    private readonly float[] _rightSamples = new float[StereoSampleBufferSize];

    private int _fftBufferIndex;
    private int _stereoSampleIndex;
    private double _leftLevel;
    private double _rightLevel;
    private double _smoothedGain = 1.0;
    private bool _isDisposed;

    private static readonly double[] BucketFrequencies =
    [
        20, 25, 31.5, 40, 50, 63, 80, 100, 125, 150, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150, 4000, 5000, 6300, 8000, 10000, 12500, 16000, 20000
    ];

    public AudioAnalysisService()
    {
        VisualizationContext = new SpectrumVisualizationContext(
            FftSize,
            _fftBuffer,
            _fftLock,
            () => _fftBufferIndex,
            () => DefaultSampleRate,
            () => _smoothedGain,
            BucketFrequencies,
            GetStereoLevels,
            GetStereoSamples);
    }

    public SpectrumVisualizationContext VisualizationContext { get; }



     public void PushSamples(float[] buffer, int offset, int sampleCount, int channelCount)
    {
        if (_isDisposed)
            return;

        if (buffer.Length == 0 || sampleCount <= 0)
            return;

        channelCount = Math.Max(1, channelCount);


        int validSampleCount = sampleCount - sampleCount % channelCount;

        if (validSampleCount <= 0)
            return;

        int end = Math.Min(buffer.Length, offset + validSampleCount);

        lock (_fftLock)
        {
            for (int index = offset; index < end; index += channelCount)
            {
                float left = Math.Clamp(buffer[index], -1.0f, 1.0f);
                float right = channelCount > 1 && index + 1 < end
                    ? Math.Clamp(buffer[index + 1], -1.0f, 1.0f)
                    : left;

                float mono = (left + right) / 2.0f;

                _fftBuffer[_fftBufferIndex] = mono;
                _fftBufferIndex = (_fftBufferIndex + 1) % FftSize;
            }
        }

        lock (_stereoLock)
        {
            for (int index = offset; index < end; index += channelCount)
            {
                float left = Math.Clamp(buffer[index], -1.0f, 1.0f);
                float right = channelCount > 1 && index + 1 < end
                    ? Math.Clamp(buffer[index + 1], -1.0f, 1.0f)
                    : left;

                _leftSamples[_stereoSampleIndex] = left;
                _rightSamples[_stereoSampleIndex] = right;
                _stereoSampleIndex = (_stereoSampleIndex + 1) % StereoSampleBufferSize;

                _leftLevel = (_leftLevel * 0.85) + (Math.Abs(left) * 0.15);
                _rightLevel = (_rightLevel * 0.85) + (Math.Abs(right) * 0.15);

                double peak = Math.Max(_leftLevel, _rightLevel);
                double targetGain = peak > 0.0001
                    ? Math.Clamp(1.0 / peak, 1.0, 32.0)
                    : 1.0;

                _smoothedGain = (_smoothedGain * 0.95) + (targetGain * 0.05);
            }
        }
    }


    public void Dispose()
    {
        _isDisposed = true;
    }

    private (double Left, double Right) GetStereoLevels()
    {
        lock (_stereoLock)
        {
            return (_leftLevel, _rightLevel);
        }
    }

    private (float[] Left, float[] Right) GetStereoSamples()
    {
        float[] left = new float[StereoSampleBufferSize];
        float[] right = new float[StereoSampleBufferSize];

        lock (_stereoLock)
        {
            int sourceIndex = _stereoSampleIndex;

            for (int i = 0; i < StereoSampleBufferSize; i++)
            {
                left[i] = _leftSamples[sourceIndex];
                right[i] = _rightSamples[sourceIndex];

                sourceIndex = (sourceIndex + 1) % StereoSampleBufferSize;
            }
        }

        return (left, right);
    }
}