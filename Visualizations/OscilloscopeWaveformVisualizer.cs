using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Shawellaby.RadioCypress.Visualizations;

public sealed class OscilloscopeWaveformVisualizer : IVisualizer
{
    private const int SampleCount = 256;
    private const double HorizontalPadding = 16.0;
    private const double VerticalPadding = 12.0;

    public void Draw(Canvas canvas, SpectrumVisualizationContext context)
    {
        canvas.Children.Clear();

        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        DrawGraticule(canvas, width, height);

        float[] samples = ReadSamples(context);
        double centerY = height / 2.0;
        double usableWidth = Math.Max(1.0, width - HorizontalPadding * 2.0);
        double usableHeight = Math.Max(1.0, height - VerticalPadding * 2.0);
        double amplitude = usableHeight * 0.42;

        Polyline glowLine = CreateWaveformLine(
            samples,
            width,
            centerY,
            usableWidth,
            amplitude,
            new SolidColorBrush(Color.FromArgb(80, 75, 255, 150)),
            7.0);

        Polyline traceLine = CreateWaveformLine(
            samples,
            width,
            centerY,
            usableWidth,
            amplitude,
            new SolidColorBrush(Color.FromRgb(95, 255, 165)),
            2.0);

        canvas.Children.Add(glowLine);
        canvas.Children.Add(traceLine);

        DrawScanLine(canvas, width, height);
    }

    private static float[] ReadSamples(SpectrumVisualizationContext context)
    {
        float[] samples = new float[SampleCount];

        lock (context.FftLock)
        {
            int sourceIndex = context.GetFftBufferIndex();
            int step = Math.Max(1, context.FftSize / SampleCount);

            for (int i = 0; i < SampleCount; i++)
            {
                samples[i] = context.FftBuffer[sourceIndex] * (float)context.GetSmoothedGain();

                sourceIndex += step;

                while (sourceIndex >= context.FftSize)
                    sourceIndex -= context.FftSize;
            }
        }

        return samples;
    }

    private static Polyline CreateWaveformLine(
        float[] samples,
        double canvasWidth,
        double centerY,
        double usableWidth,
        double amplitude,
        Brush stroke,
        double thickness)
    {
        Polyline line = new Polyline
        {
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            SnapsToDevicePixels = false
        };

        for (int i = 0; i < samples.Length; i++)
        {
            double normalizedX = samples.Length == 1 ? 0 : i / (double)(samples.Length - 1);
            double x = HorizontalPadding + normalizedX * usableWidth;

            double sample = Math.Clamp(samples[i], -1.0f, 1.0f);
            double y = centerY - sample * amplitude;

            line.Points.Add(new System.Windows.Point(x, y));
        }

        return line;
    }

    private static void DrawGraticule(Canvas canvas, double width, double height)
    {
        Brush majorBrush = new SolidColorBrush(Color.FromArgb(70, 45, 120, 80));
        Brush minorBrush = new SolidColorBrush(Color.FromArgb(32, 45, 120, 80));
        Brush centerBrush = new SolidColorBrush(Color.FromArgb(115, 60, 220, 130));

        for (int i = 1; i < 10; i++)
        {
            double x = width * i / 10.0;
            canvas.Children.Add(new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = i == 5 ? centerBrush : minorBrush,
                StrokeThickness = i == 5 ? 1.2 : 0.7
            });
        }

        for (int i = 1; i < 8; i++)
        {
            double y = height * i / 8.0;
            canvas.Children.Add(new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = i == 4 ? centerBrush : minorBrush,
                StrokeThickness = i == 4 ? 1.2 : 0.7
            });
        }

        Rectangle border = new Rectangle
        {
            Width = Math.Max(0, width - 1),
            Height = Math.Max(0, height - 1),
            Stroke = majorBrush,
            StrokeThickness = 1.0,
            Fill = Brushes.Transparent
        };

        Canvas.SetLeft(border, 0.5);
        Canvas.SetTop(border, 0.5);
        canvas.Children.Add(border);
    }

    private static void DrawScanLine(Canvas canvas, double width, double height)
    {
        Rectangle scanLine = new Rectangle
        {
            Width = width,
            Height = height,
            IsHitTestVisible = false,
            Fill = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(0, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.00),
                    new GradientStop(Color.FromArgb(18, 120, 255, 170), 0.50),
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.00)
                }
            },
            Opacity = 0.22
        };

        canvas.Children.Add(scanLine);
    }
}