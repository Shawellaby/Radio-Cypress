using System.Windows.Controls;

namespace Shawellaby.RadioCypress.Visualizations;

public interface IVisualizer
{
    void Draw(Canvas canvas, SpectrumVisualizationContext context);
}