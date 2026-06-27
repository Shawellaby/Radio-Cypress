using Shawellaby.RadioCypress.Models;
using Shawellaby.RadioCypress.Visualizations;

namespace Shawellaby.RadioCypress.Services.Visualization;

public sealed class VisualizationRegistry
{
    private readonly Dictionary<VisualizationMode, IVisualizer> _visualizers = new()
    {
        [VisualizationMode.Equalizer] = new EqualizerSpectrumVisualizer(),
        [VisualizationMode.Psychedelic] = new PsychedelicSpectrumVisualizer(),
        [VisualizationMode.Wave] = new WaveSpectrumVisualizer(),
        [VisualizationMode.LedMatrix] = new LedMatrixSpectrumVisualizer(),
        [VisualizationMode.Ethereal] = new EtherealSpectrumVisualizer(),
        [VisualizationMode.Starfield] = new StarfieldSpectrumVisualizer(),
        [VisualizationMode.Oscilloscope] = new OscilloscopeWaveformVisualizer(),
        [VisualizationMode.VuMeter] = new VuMeterVisualizer(),
        [VisualizationMode.MatrixRain] = new MatrixRainSpectrumVisualizer(),
        [VisualizationMode.LissajousScope] = new LissajousScopeVisualizer()
    };

    public IVisualizer? GetVisualizer(VisualizationMode mode)
    {
        return _visualizers.TryGetValue(mode, out IVisualizer? visualizer)
            ? visualizer
            : null;
    }
}