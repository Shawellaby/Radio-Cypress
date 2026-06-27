namespace Shawellaby.RadioCypress.Services.Audio;

public interface IRadioPlaybackService : IDisposable
{
    event EventHandler<AudioBufferEventArgs>? SamplesAvailable;

    bool IsMuted { get; }

    int SampleRate { get; }

    int ChannelCount { get; }

    void Play(string url);

    void Stop();

    void SetMuted(bool isMuted);
}