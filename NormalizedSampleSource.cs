using CSCore;

namespace Shawellaby.RadioCypress;

/// <summary>
/// Provides a wrapper around an ISampleSource that applies gain normalization to audio samples.
/// This class reads audio data from an underlying sample source and applies a gain factor
/// obtained from the owner window to normalize the audio levels.
/// </summary>
/// <remarks>
/// The NormalizedSampleSource acts as a pass-through for most ISampleSource operations,
/// delegating them to the wrapped source. During the Read operation, it applies gain
/// normalization to the audio samples on a per-channel basis, using the current gain value
/// retrieved from the associated MainWindow instance. This allows for dynamic volume control
/// and audio level normalization during playback.
/// </remarks>
internal class NormalizedSampleSource : ISampleSource
{
    private readonly ISampleSource _source;
    private readonly MainWindow _owner;

    public NormalizedSampleSource(ISampleSource source, MainWindow owner)
    {
        _source = source;
        _owner = owner;
    }

    public CSCore.WaveFormat WaveFormat => _source.WaveFormat;
    public bool CanSeek => _source.CanSeek;
    public long Length => _source.Length;

    public long Position
    {
        get => _source.Position;
        set => _source.Position = value;
    }

    /// <summary>
    /// Reads audio samples from the underlying source, applies level normalization and gain adjustments, and writes the processed samples to the output buffer.
    /// The method processes audio data in stereo or mono format, applying the current gain value and clamping the results to valid audio range.
    /// </summary>
    /// <param name="buffer">The destination array where the processed audio samples will be written.</param>
    /// <param name="offset">The zero-based index in the buffer at which to begin writing samples.</param>
    /// <param name="count">The maximum number of samples to read and process.</param>
    /// <return>
    /// The actual number of samples read and processed, or zero if no more samples are available.
    /// </return>
    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (read <= 0)
            return read;

        int channels = Math.Max(1, WaveFormat.Channels);
        float gain = _owner.GetCurrentGain();

        for (int i = offset; i < offset + read; i += channels)
        {
            float left = buffer[i];
            float right = channels > 1 && i + 1 < offset + read ? buffer[i + 1] : left;

            _owner.ApplyLevelNormalization(left, right);

            left = Math.Clamp(left * gain, -1f, 1f);
            right = Math.Clamp(right * gain, -1f, 1f);

            buffer[i] = left;

            if (channels > 1 && i + 1 < offset + read)
                buffer[i + 1] = right;

            _owner.WriteNormalizedRecording(left, right);
        }

        return read;
    }

    public void Dispose()
    {
        _source.Dispose();
    }


}