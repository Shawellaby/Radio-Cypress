using NAudio.Wave;

namespace Shawellaby.RadioCypress.Services.Audio;

public sealed class AudioBufferEventArgs : EventArgs
{
    public AudioBufferEventArgs(
        float[] buffer,
        int offset,
        int sampleCount,
        WaveFormat waveFormat)
    {
        Buffer = buffer;
        Offset = offset;
        SampleCount = sampleCount;
        WaveFormat = waveFormat;
    }

    public float[] Buffer { get; }

    public int Offset { get; }

    public int SampleCount { get; }

    public WaveFormat WaveFormat { get; }

    public int ChannelCount => WaveFormat.Channels;
}