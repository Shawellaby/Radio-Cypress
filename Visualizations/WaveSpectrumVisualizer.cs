using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Shawellaby.RadioCypress.Visualizations;

public sealed class WaveSpectrumVisualizer : IVisualizer
{
    private const int MaxWaveTraces = 18;

    private readonly List<WaveTrace> _waveTraces = new();
    private double _wavePhase;

    public void Draw(Canvas canvas, SpectrumVisualizationContext context)
    {
        canvas.Children.Clear();

        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        double[] bucketLevels = context.GetFrequencyBucketLevels();
        double energy = bucketLevels.Average();
        double bassEnergy = bucketLevels.Take(8).DefaultIfEmpty(0).Average();
        double trebleEnergy = bucketLevels.Skip(bucketLevels.Length / 2).DefaultIfEmpty(0).Average();

        _wavePhase += 0.16 + energy * 0.16;

        Rectangle background = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0, 8, 18), 0.0),
                    new GradientStop(Color.FromRgb(0, 28, 44), 0.45),
                    new GradientStop(Color.FromRgb(5, 0, 22), 1.0)
                }
            }
        };

        Canvas.SetLeft(background, 0);
        Canvas.SetTop(background, 0);
        canvas.Children.Add(background);

        List<Point> points = new();
        int pointCount = Math.Max(96, bucketLevels.Length * 5);
        double centerY = height * 0.5;
        double amplitude = height * (0.12 + energy * 0.38);

        for (int i = 0; i < pointCount; i++)
        {
            double ratio = i / (double)(pointCount - 1);
            double bucketPosition = ratio * (bucketLevels.Length - 1);
            int bucketIndex = Math.Clamp((int)Math.Round(bucketPosition), 0, bucketLevels.Length - 1);
            double level = bucketLevels[bucketIndex];

            double x = ratio * width;
            double primaryWave = Math.Sin(ratio * Math.PI * 4.0 + _wavePhase);
            double secondaryWave = Math.Sin(ratio * Math.PI * 11.0 - _wavePhase * 1.7) * 0.32;
            double ripple = Math.Sin(ratio * Math.PI * 24.0 + _wavePhase * 2.6 + bucketIndex) * level * 0.28;
            double y = centerY + (primaryWave + secondaryWave + ripple) * amplitude * (0.22 + level * 0.95);

            points.Add(new Point(x, y));
        }

        Color traceColor = SpectrumVisualizationContext.GetPsychedelicColor(178 + bassEnergy * 90 - trebleEnergy * 120 + _wavePhase * 18, 255);
        _waveTraces.Insert(0, new WaveTrace(points, energy, traceColor));

        if (_waveTraces.Count > MaxWaveTraces)
            _waveTraces.RemoveRange(MaxWaveTraces, _waveTraces.Count - MaxWaveTraces);

        for (int i = _waveTraces.Count - 1; i >= 0; i--)
        {
            WaveTrace trace = _waveTraces[i];
            double ageRatio = i / (double)Math.Max(1, MaxWaveTraces - 1);
            double fade = Math.Pow(1.0 - ageRatio, 1.7);
            double pulse = i == 0 ? 1.0 + energy * 1.6 + Math.Sin(_wavePhase * 4.0) * 0.25 : 1.0;

            Polyline glowLine = new Polyline
            {
                Points = new PointCollection(trace.Points),
                Stroke = new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(24 + fade * 95, 0, 140), trace.Color.R, trace.Color.G, trace.Color.B)),
                StrokeThickness = (10 + trace.Energy * 22) * fade * pulse,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = fade * 0.55
            };

            canvas.Children.Add(glowLine);

            Polyline waveLine = new Polyline
            {
                Points = new PointCollection(trace.Points),
                Stroke = new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(70 + fade * 185, 0, 255), trace.Color.R, trace.Color.G, trace.Color.B)),
                StrokeThickness = (1.8 + trace.Energy * 5.5) * fade * pulse,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = fade
            };

            canvas.Children.Add(waveLine);
        }

        double orbSize = 150 + energy * 80;

        Ellipse pulseOrb = new Ellipse
        {
            Width = orbSize,
            Height = orbSize,
            Fill = new RadialGradientBrush(
                Color.FromArgb((byte)Math.Clamp(95 + energy * 130, 95, 225), 120, 255, 245),
                Color.FromArgb(0, 0, 80, 120)),
            Stroke = new SolidColorBrush(Color.FromArgb(130, 170, 255, 255)),
            StrokeThickness = 1.5 + energy * 3.0
        };

        Canvas.SetLeft(pulseOrb, width * 0.5 - orbSize * 0.5);
        Canvas.SetTop(pulseOrb, centerY - orbSize * 0.5);
        canvas.Children.Add(pulseOrb);
    }

    private sealed record WaveTrace(List<Point> Points, double Energy, Color Color);
}