using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Shawellaby.RadioCypress.Visualizations;

public sealed class PsychedelicSpectrumVisualizer : IVisualizer
{
    private double _psychedelicPhase;

    public void Draw(Canvas canvas, SpectrumVisualizationContext context)
    {
        canvas.Children.Clear();

        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        double[] bucketLevels = context.GetFrequencyBucketLevels();
        double centerX = width * 0.5;
        double centerY = height * 0.5;
        double maxRadius = Math.Min(width, height) * 0.48;

        _psychedelicPhase += 0.055;

        Rectangle background = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new RadialGradientBrush(Color.FromRgb(31, 0, 48), Color.FromRgb(4, 0, 12))
        };

        Canvas.SetLeft(background, 0);
        Canvas.SetTop(background, 0);
        canvas.Children.Add(background);

        for (int ring = 0; ring < 9; ring++)
        {
            double ringRatio = (ring + 1) / 9.0;
            double bucketLevel = bucketLevels[(ring * 3) % bucketLevels.Length];
            double radius = maxRadius * ringRatio * (0.76 + bucketLevel * 0.28);
            byte alpha = (byte)Math.Clamp(45 + bucketLevel * 115, 45, 160);

            Ellipse aura = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                StrokeThickness = 5 + bucketLevel * 10,
                Stroke = new SolidColorBrush(SpectrumVisualizationContext.GetPsychedelicColor(ring * 38 + _psychedelicPhase * 90, alpha)),
                Fill = Brushes.Transparent
            };

            Canvas.SetLeft(aura, centerX - radius);
            Canvas.SetTop(aura, centerY - radius);
            canvas.Children.Add(aura);
        }

        int bucketCount = bucketLevels.Length;
        double angleStep = 360.0 / bucketCount;

        for (int i = 0; i < bucketCount; i++)
        {
            double level = bucketLevels[i];
            double angle = (i * angleStep + _psychedelicPhase * 72) * Math.PI / 180.0;
            double mirroredAngle = angle + Math.PI;

            DrawPetal(canvas, centerX, centerY, maxRadius, angle, level, i);
            DrawPetal(canvas, centerX, centerY, maxRadius, mirroredAngle, level * 0.85, i + bucketCount);
        }

        for (int i = 0; i < bucketCount; i += 2)
        {
            double level = bucketLevels[i];
            double swirlAngle = (i * angleStep * 1.7 - _psychedelicPhase * 110) * Math.PI / 180.0;
            double distance = maxRadius * (0.18 + level * 0.68);
            double dotSize = 8 + level * 34;

            Ellipse dot = new Ellipse
            {
                Width = dotSize,
                Height = dotSize,
                Fill = new SolidColorBrush(SpectrumVisualizationContext.GetPsychedelicColor(i * 21 - _psychedelicPhase * 140, 210))
            };

            Canvas.SetLeft(dot, centerX + Math.Cos(swirlAngle) * distance - dotSize * 0.5);
            Canvas.SetTop(dot, centerY + Math.Sin(swirlAngle) * distance - dotSize * 0.5);
            canvas.Children.Add(dot);
        }
    }

    private void DrawPetal(Canvas canvas, double centerX, double centerY, double maxRadius, double angle, double level, int colorIndex)
    {
        double innerRadius = maxRadius * 0.12;
        double outerRadius = maxRadius * (0.26 + level * 0.78);
        double wobble = Math.Sin(_psychedelicPhase * 2.4 + colorIndex * 0.65) * 0.28;
        double thickness = 10 + level * 42;

        double x1 = centerX + Math.Cos(angle + wobble) * innerRadius;
        double y1 = centerY + Math.Sin(angle + wobble) * innerRadius;
        double x2 = centerX + Math.Cos(angle - wobble * 0.4) * outerRadius;
        double y2 = centerY + Math.Sin(angle - wobble * 0.4) * outerRadius;

        Line ray = new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Stroke = new SolidColorBrush(SpectrumVisualizationContext.GetPsychedelicColor(colorIndex * 17 + level * 180 + _psychedelicPhase * 120, 225))
        };

        canvas.Children.Add(ray);

        double flowerSize = 18 + level * 70;

        Ellipse flower = new Ellipse
        {
            Width = flowerSize,
            Height = flowerSize,
            Fill = new SolidColorBrush(SpectrumVisualizationContext.GetPsychedelicColor(colorIndex * 29 - _psychedelicPhase * 160, 185)),
            Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
            StrokeThickness = 1.5
        };

        Canvas.SetLeft(flower, x2 - flowerSize * 0.5);
        Canvas.SetTop(flower, y2 - flowerSize * 0.5);
        canvas.Children.Add(flower);
    }
}