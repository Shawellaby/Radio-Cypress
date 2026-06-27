using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Shawellaby.RadioCypress.Services.Audio;

public sealed class RadioPlaybackService : IRadioPlaybackService
{
    private readonly object _syncRoot = new();

    private IWavePlayer? _waveOut;
    private MediaFoundationReader? _reader;
    private SampleForwardingProvider? _sampleForwardingProvider;
    private VolumeSampleProvider? _volumeProvider;

    private bool _isMuted;
    private bool _isDisposed;

    public event EventHandler<AudioBufferEventArgs>? SamplesAvailable;

    public bool IsMuted
    {
        get
        {
            lock (_syncRoot)
                return _isMuted;
        }
    }

    public int SampleRate
    {
        get
        {
            lock (_syncRoot)
                return _reader?.WaveFormat.SampleRate ?? 44100;
        }
    }

    public int ChannelCount
    {
        get
        {
            lock (_syncRoot)
                return _reader?.WaveFormat.Channels ?? 2;
        }
    }

    public void Play(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ThrowIfDisposed();

        lock (_syncRoot)
        {
            StopCore();

            _reader = new MediaFoundationReader(url);


            ISampleProvider sampleProvider = _reader.ToSampleProvider();

            if (sampleProvider.WaveFormat.Channels == 1)
                sampleProvider = new MonoToStereoSampleProvider(sampleProvider);


            _sampleForwardingProvider = new SampleForwardingProvider(sampleProvider);
            _sampleForwardingProvider.SamplesAvailable += OnSamplesAvailable;

            _volumeProvider = new VolumeSampleProvider(_sampleForwardingProvider)
            {
                Volume = _isMuted ? 0.0f : 1.0f
            };

            _waveOut = new WaveOutEvent();
            _waveOut.Init(_volumeProvider);
            _waveOut.Play();
        }
    }

    public void Stop()
    {
        ThrowIfDisposed();

        lock (_syncRoot)
        {
            StopCore();
        }
    }

    public void SetMuted(bool isMuted)
    {
        ThrowIfDisposed();

        lock (_syncRoot)
        {
            _isMuted = isMuted;

            if (_volumeProvider is not null)
                _volumeProvider.Volume = isMuted ? 0.0f : 1.0f;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        lock (_syncRoot)
        {
            if (_isDisposed)
                return;

            StopCore();
            _isDisposed = true;
        }
    }

    private void StopCore()
    {
        if (_sampleForwardingProvider is not null)
            _sampleForwardingProvider.SamplesAvailable -= OnSamplesAvailable;

        _waveOut?.Stop();
        _waveOut?.Dispose();
        _reader?.Dispose();

        _waveOut = null;
        _reader = null;
        _sampleForwardingProvider = null;
        _volumeProvider = null;
    }

    private void OnSamplesAvailable(object? sender, AudioBufferEventArgs e)
    {
        SamplesAvailable?.Invoke(this, e);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    private sealed class SampleForwardingProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;

        public SampleForwardingProvider(ISampleProvider source)
        {
            _source = source;
        }

        public event EventHandler<AudioBufferEventArgs>? SamplesAvailable;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = _source.Read(buffer, offset, count);

            if (samplesRead > 0)
            {
                int channelCount = Math.Max(1, WaveFormat.Channels);
                int validSampleCount = samplesRead - samplesRead % channelCount;
                const int TargetFramesPerChunk = 4096;
                int targetSamplesPerChunk = TargetFramesPerChunk * channelCount;

                int remainingSamples = validSampleCount;
                int chunkOffset = offset;

                while (remainingSamples > 0)
                {
                    int chunkSampleCount = Math.Min(remainingSamples, targetSamplesPerChunk);
                    chunkSampleCount -= chunkSampleCount % channelCount;

                    if (chunkSampleCount <= 0)
                        break;

                    SamplesAvailable?.Invoke(
                        this,
                        new AudioBufferEventArgs(
                            buffer,
                            chunkOffset,
                            chunkSampleCount,
                            WaveFormat));

                    chunkOffset += chunkSampleCount;
                    remainingSamples -= chunkSampleCount;
                }
            }

            return samplesRead;
        }
    }
}