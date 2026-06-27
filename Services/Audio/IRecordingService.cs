namespace Shawellaby.RadioCypress.Services.Audio;

public interface IRecordingService : IDisposable
{
    bool IsRecording { get; }

    string? CurrentPath { get; }

    void Start(string path, int sampleRate, int channelCount);

    void Stop();

    void WriteSamples(float[] buffer, int offset, int sampleCount, int channelCount);
}