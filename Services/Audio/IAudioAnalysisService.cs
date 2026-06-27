using Shawellaby.RadioCypress.Visualizations;

namespace Shawellaby.RadioCypress.Services.Audio;

public interface IAudioAnalysisService : IDisposable
{
    SpectrumVisualizationContext VisualizationContext { get; }

    void PushSamples(float[] buffer, int offset, int sampleCount, int channelCount);
}