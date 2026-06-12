using System.Windows.Controls;

namespace RadioCypress.Visualizations;

public interface IVisualizer
{
    void Draw(Canvas canvas, SpectrumVisualizationContext context);
}