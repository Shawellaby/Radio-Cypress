using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Shawellaby.RadioCypress.Visualizations;

public sealed class OscilloscopeWaveformVisualizer : IVisualizer
{
    private const int SampleCount = 256;
    private const int WaveformWindowSampleCount = 512;
    private const double HorizontalPadding = 16.0;
    private const double VerticalPadding = 12.0;

    private readonly float[] _samples = new float[SampleCount];

    private readonly Brush _minorGridBrush;
    private readonly Brush _centerGridBrush;
    private readonly Brush _borderBrush;
    private readonly Brush _glowBrush;
    private readonly Brush _traceBrush;
    private readonly Brush _scanLineBrush;

    private readonly List<Line> _verticalGridLines = [];
    private readonly List<Line> _horizontalGridLines = [];

    private readonly Rectangle _border;
    private readonly Rectangle _scanLine;
    private readonly Polyline _glowLine;
    private readonly Polyline _traceLine;

    private Canvas? _attachedCanvas;
    private double _lastWidth = double.NaN;
    private double _lastHeight = double.NaN;

    public OscilloscopeWaveformVisualizer()
    {
        _minorGridBrush = Freeze(new SolidColorBrush(Color.FromArgb(32, 45, 120, 80)));
        _centerGridBrush = Freeze(new SolidColorBrush(Color.FromArgb(115, 60, 220, 130)));
        _borderBrush = Freeze(new SolidColorBrush(Color.FromArgb(70, 45, 120, 80)));
        _glowBrush = Freeze(new SolidColorBrush(Color.FromArgb(80, 75, 255, 150)));
        _traceBrush = Freeze(new SolidColorBrush(Color.FromRgb(95, 255, 165)));

        LinearGradientBrush scanLineBrush = new()
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.00),
                new GradientStop(Color.FromArgb(18, 120, 255, 170), 0.50),
                new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.00)
            }
        };

        _scanLineBrush = Freeze(scanLineBrush);

        _border = new Rectangle
        {
            Stroke = _borderBrush,
            StrokeThickness = 1.0,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };

        _scanLine = new Rectangle
        {
            Fill = _scanLineBrush,
            Opacity = 0.22,
            IsHitTestVisible = false
        };

        _glowLine = CreateWaveformLine(_glowBrush, 7.0);
        _traceLine = CreateWaveformLine(_traceBrush, 2.0);

        InitializePointCollection(_glowLine.Points);
        InitializePointCollection(_traceLine.Points);
    }

    public void Draw(Canvas canvas, SpectrumVisualizationContext context)
    {
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        EnsureVisualTree(canvas);

        if (!width.Equals(_lastWidth) || !height.Equals(_lastHeight))
            UpdateStaticLayout(width, height);

        ReadSamples(context);
        UpdateWaveformPoints(width, height);
    }

    private void EnsureVisualTree(Canvas canvas)
    {
        if (ReferenceEquals(_attachedCanvas, canvas))
            return;

        if (_attachedCanvas is not null)
            _attachedCanvas.Children.Clear();

        canvas.Children.Clear();

        _verticalGridLines.Clear();
        _horizontalGridLines.Clear();

        for (int i = 1; i < 10; i++)
        {
            Line line = new()
            {
                Stroke = i == 5 ? _centerGridBrush : _minorGridBrush,
                StrokeThickness = i == 5 ? 1.2 : 0.7,
                IsHitTestVisible = false
            };

            _verticalGridLines.Add(line);
            canvas.Children.Add(line);
        }

        for (int i = 1; i < 8; i++)
        {
            Line line = new()
            {
                Stroke = i == 4 ? _centerGridBrush : _minorGridBrush,
                StrokeThickness = i == 4 ? 1.2 : 0.7,
                IsHitTestVisible = false
            };

            _horizontalGridLines.Add(line);
            canvas.Children.Add(line);
        }

        canvas.Children.Add(_border);
        canvas.Children.Add(_glowLine);
        canvas.Children.Add(_traceLine);
        canvas.Children.Add(_scanLine);

        _attachedCanvas = canvas;
        _lastWidth = double.NaN;
        _lastHeight = double.NaN;
    }

    private void UpdateStaticLayout(double width, double height)
    {
        for (int i = 0; i < _verticalGridLines.Count; i++)
        {
            double x = width * (i + 1) / 10.0;
            Line line = _verticalGridLines[i];

            line.X1 = x;
            line.Y1 = 0;
            line.X2 = x;
            line.Y2 = height;
        }

        for (int i = 0; i < _horizontalGridLines.Count; i++)
        {
            double y = height * (i + 1) / 8.0;
            Line line = _horizontalGridLines[i];

            line.X1 = 0;
            line.Y1 = y;
            line.X2 = width;
            line.Y2 = y;
        }

        _border.Width = Math.Max(0, width - 1);
        _border.Height = Math.Max(0, height - 1);
        Canvas.SetLeft(_border, 0.5);
        Canvas.SetTop(_border, 0.5);

        _scanLine.Width = width;
        _scanLine.Height = height;

        _lastWidth = width;
        _lastHeight = height;
    }

    private void ReadSamples(SpectrumVisualizationContext context)
    {
        lock (context.FftLock)
        {
            int waveformWindow = Math.Min(WaveformWindowSampleCount, context.FftSize);
            int step = Math.Max(1, waveformWindow / SampleCount);
            int sourceIndex = context.GetFftBufferIndex() - waveformWindow;

            while (sourceIndex < 0)
                sourceIndex += context.FftSize;

            for (int i = 0; i < SampleCount; i++)
            {
                _samples[i] = context.FftBuffer[sourceIndex];

                sourceIndex += step;

                while (sourceIndex >= context.FftSize)
                    sourceIndex -= context.FftSize;
            }
        }
    }

    private void UpdateWaveformPoints(double width, double height)
    {
        double centerY = height / 2.0;
        double usableWidth = Math.Max(1.0, width - HorizontalPadding * 2.0);
        double usableHeight = Math.Max(1.0, height - VerticalPadding * 2.0);
        double amplitude = usableHeight * 0.42;

        UpdatePointCollection(_glowLine.Points, centerY, usableWidth, amplitude);
        UpdatePointCollection(_traceLine.Points, centerY, usableWidth, amplitude);
    }

    private void UpdatePointCollection(
        PointCollection points,
        double centerY,
        double usableWidth,
        double amplitude)
    {
        for (int i = 0; i < SampleCount; i++)
        {
            double normalizedX = SampleCount == 1 ? 0 : i / (double)(SampleCount - 1);
            double x = HorizontalPadding + normalizedX * usableWidth;

            double sample = Math.Clamp(_samples[i], -1.0f, 1.0f);
            double y = centerY - sample * amplitude;

            points[i] = new Point(x, y);
        }
    }

    private static Polyline CreateWaveformLine(Brush stroke, double thickness)
    {
        return new Polyline
        {
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            SnapsToDevicePixels = false,
            IsHitTestVisible = false
        };
    }

    private static void InitializePointCollection(PointCollection points)
    {
        for (int i = 0; i < SampleCount; i++)
            points.Add(new Point());
    }

    private static T Freeze<T>(T freezable)
        where T : Freezable
    {
        if (freezable.CanFreeze)
            freezable.Freeze();

        return freezable;
    }
}