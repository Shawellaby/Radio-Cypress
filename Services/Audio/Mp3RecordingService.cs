using System.IO;
using NAudio.Lame;
using NAudio.Wave;

namespace Shawellaby.RadioCypress.Services.Audio;

public sealed class Mp3RecordingService : IRecordingService
{
    private readonly object _syncRoot = new();

    private LameMP3FileWriter? _writer;
    private WaveFormat? _waveFormat;
    private bool _isDisposed;

    public bool IsRecording
    {
        get
        {
            lock (_syncRoot)
                return _writer is not null;
        }
    }

    public string? CurrentPath { get; private set; }

    public void Start(string path, int sampleRate, int channelCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ThrowIfDisposed();

        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Sample rate must be greater than zero.");

        if (channelCount is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(channelCount), channelCount, "Only mono and stereo recording are supported.");

        lock (_syncRoot)
        {
            StopCore();

            string? directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount);
            _writer = new LameMP3FileWriter(path, _waveFormat, LAMEPreset.STANDARD);
            CurrentPath = path;
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

    public void WriteSamples(float[] buffer, int offset, int sampleCount, int channelCount)
    {
        if (_isDisposed)
            return;

        if (buffer.Length == 0 || sampleCount <= 0)
            return;

        lock (_syncRoot)
        {
            if (_writer is null || _waveFormat is null)
                return;

            int end = Math.Min(buffer.Length, offset + sampleCount);
            channelCount = Math.Max(1, channelCount);

            for (int index = offset; index < end; index += channelCount)
            {
                float left = Math.Clamp(buffer[index], -1.0f, 1.0f);
                float right = channelCount > 1 && index + 1 < end
                    ? Math.Clamp(buffer[index + 1], -1.0f, 1.0f)
                    : left;

                if (_waveFormat.Channels == 1)
                {
                    WriteFloat((left + right) / 2.0f);
                    continue;
                }

                WriteFloat(left);
                WriteFloat(right);
            }
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
        _writer?.Flush();
        _writer?.Dispose();
        _writer = null;
        _waveFormat = null;
    }

    private void WriteFloat(float sample)
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];
        BitConverter.TryWriteBytes(buffer, Math.Clamp(sample, -1.0f, 1.0f));
        _writer?.Write(buffer);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }
}