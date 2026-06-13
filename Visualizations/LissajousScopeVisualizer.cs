using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Shawellaby.RadioCypress.Visualizations;

public sealed class LissajousScopeVisualizer : IVisualizer
{
    private const int TraceHistoryLimit = 10;

    private readonly List<ScopeTrace> _traceHistory = new();
    private double _phase;
    private double _peakEnergy;

    public void Draw(Canvas canvas, SpectrumVisualizationContext context)
    {
        canvas.Children.Clear();

        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        (float[] leftSamples, float[] rightSamples) = context.GetStereoSamples();

        if (leftSamples.Length == 0 || rightSamples.Length == 0)
        {
            DrawBackground(canvas, width, height, 0, 0);
            DrawScopeFrame(canvas, width, height, 0, 0);
            DrawTitle(canvas, width, height, 0, 0);
            DrawVignette(canvas, width, height);
            return;
        }

        double[] bucketLevels = context.GetFrequencyBucketLevels();
        double energy = bucketLevels.DefaultIfEmpty(0).Average();
        double bassEnergy = bucketLevels.Take(8).DefaultIfEmpty(0).Average();
        double trebleEnergy = bucketLevels.Skip(bucketLevels.Length / 2).DefaultIfEmpty(0).Average();

        _phase += 0.035 + energy * 0.055;
        _peakEnergy = Math.Max(energy, _peakEnergy * 0.94);

        double stereoWidth = CalculateStereoWidth(leftSamples, rightSamples);
        double loudness = CalculateLoudness(leftSamples, rightSamples);

        DrawBackground(canvas, width, height, energy, bassEnergy);
        DrawScopeFrame(canvas, width, height, energy, stereoWidth);

        PointCollection tracePoints = BuildTracePoints(width, height, leftSamples, rightSamples, loudness, stereoWidth);

        Color traceColor = GetTraceColor(energy, stereoWidth, trebleEnergy);
        _traceHistory.Insert(0, new ScopeTrace(tracePoints, traceColor, energy, loudness, stereoWidth));

        if (_traceHistory.Count > TraceHistoryLimit)
            _traceHistory.RemoveRange(TraceHistoryLimit, _traceHistory.Count - TraceHistoryLimit);

        DrawTraceHistory(canvas, energy);
        DrawCenterGlow(canvas, width, height, energy, loudness, stereoWidth);
        DrawReadout(canvas, width, height, loudness, stereoWidth);
        DrawTitle(canvas, width, height, energy, stereoWidth);
        DrawVignette(canvas, width, height);
    }

    private static PointCollection BuildTracePoints(
        double width,
        double height,
        float[] leftSamples,
        float[] rightSamples,
        double loudness,
        double stereoWidth)
    {
        PointCollection points = new();

        int sampleCount = Math.Min(leftSamples.Length, rightSamples.Length);
        int step = Math.Max(1, sampleCount / 360);

        double centerX = width * 0.5;
        double centerY = height * 0.5;
        double radius = Math.Min(width, height) * 0.42;
        double gain = 1.6 + Math.Clamp(0.22 - loudness, 0.0, 0.22) * 5.0;

        for (int i = 0; i < sampleCount; i += step)
        {
            double left = Math.Clamp(leftSamples[i] * gain, -1.0, 1.0);
            double right = Math.Clamp(rightSamples[i] * gain, -1.0, 1.0);

            // Classic stereo vectorscope transform:
            // mono material forms a diagonal line, stereo difference opens into wider shapes.
            double xSignal = (left - right) * 0.707;
            double ySignal = (left + right) * 0.707;

            double scopeCompression = 0.78 + stereoWidth * 0.22;
            double x = centerX + xSignal * radius * scopeCompression;
            double y = centerY - ySignal * radius * 0.92;

            points.Add(new Point(x, y));
        }

        return points;
    }

    private static void DrawBackground(Canvas canvas, double width, double height, double energy, double bassEnergy)
    {
        Rectangle background = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.86,
                RadiusY = 0.92,
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(
                        (byte)Math.Clamp(3 + bassEnergy * 16, 3, 19),
                        (byte)Math.Clamp(16 + energy * 30, 16, 46),
                        (byte)Math.Clamp(12 + energy * 24, 12, 36)), 0.0),
                    new GradientStop(Color.FromRgb(1, 7, 6), 0.62),
                    new GradientStop(Color.FromRgb(0, 0, 0), 1.0)
                }
            }
        };

        canvas.Children.Add(background);
    }

    private static void DrawScopeFrame(Canvas canvas, double width, double height, double energy, double stereoWidth)
    {
        double centerX = width * 0.5;
        double centerY = height * 0.5;
        double radius = Math.Min(width, height) * 0.45;

        Ellipse outerGlow = new Ellipse
        {
            Width = radius * 2.05,
            Height = radius * 2.05,
            Stroke = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Clamp(45 + energy * 85, 45, 130),
                60,
                255,
                185)),
            StrokeThickness = 2.0 + energy * 2.0,
            Fill = Brushes.Transparent,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 160),
                BlurRadius = 18 + energy * 12,
                ShadowDepth = 0,
                Opacity = 0.55
            }
        };

        Canvas.SetLeft(outerGlow, centerX - radius * 1.025);
        Canvas.SetTop(outerGlow, centerY - radius * 1.025);
        canvas.Children.Add(outerGlow);

        for (int ring = 1; ring <= 4; ring++)
        {
            double ringRadius = radius * ring / 4.0;

            Ellipse gridRing = new Ellipse
            {
                Width = ringRadius * 2,
                Height = ringRadius * 2,
                Stroke = new SolidColorBrush(Color.FromArgb(46, 70, 255, 180)),
                StrokeThickness = 1,
                Fill = Brushes.Transparent
            };

            Canvas.SetLeft(gridRing, centerX - ringRadius);
            Canvas.SetTop(gridRing, centerY - ringRadius);
            canvas.Children.Add(gridRing);
        }

        for (int line = 0; line < 12; line++)
        {
            double angle = line * Math.PI / 6.0;
            double x = Math.Cos(angle) * radius;
            double y = Math.Sin(angle) * radius;

            Line radial = new Line
            {
                X1 = centerX - x,
                Y1 = centerY - y,
                X2 = centerX + x,
                Y2 = centerY + y,
                Stroke = new SolidColorBrush(Color.FromArgb(32, 70, 255, 180)),
                StrokeThickness = 1
            };

            canvas.Children.Add(radial);
        }

        Line horizontal = new Line
        {
            X1 = centerX - radius,
            Y1 = centerY,
            X2 = centerX + radius,
            Y2 = centerY,
            Stroke = new SolidColorBrush(Color.FromArgb(78, 120, 255, 200)),
            StrokeThickness = 1.2
        };

        Line vertical = new Line
        {
            X1 = centerX,
            Y1 = centerY - radius,
            X2 = centerX,
            Y2 = centerY + radius,
            Stroke = new SolidColorBrush(Color.FromArgb(78, 120, 255, 200)),
            StrokeThickness = 1.2
        };

        canvas.Children.Add(horizontal);
        canvas.Children.Add(vertical);

        double widthIndicator = radius * stereoWidth;

        Line stereoWidthLine = new Line
        {
            X1 = centerX - widthIndicator,
            Y1 = centerY + radius + 10,
            X2 = centerX + widthIndicator,
            Y2 = centerY + radius + 10,
            Stroke = new SolidColorBrush(Color.FromArgb(150, 140, 255, 210)),
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        canvas.Children.Add(stereoWidthLine);
    }

    private void DrawTraceHistory(Canvas canvas, double energy)
    {
        for (int i = _traceHistory.Count - 1; i >= 0; i--)
        {
            ScopeTrace trace = _traceHistory[i];

            if (trace.Points.Count < 2)
                continue;

            double ageRatio = i / (double)Math.Max(1, TraceHistoryLimit - 1);
            double fade = Math.Pow(1.0 - ageRatio, 1.9);
            byte glowAlpha = (byte)Math.Clamp((22 + trace.Energy * 85) * fade, 0, 120);
            byte coreAlpha = (byte)Math.Clamp((75 + trace.Energy * 180) * fade, 0, 255);

            Polyline glow = new Polyline
            {
                Points = trace.Points,
                Stroke = new SolidColorBrush(Color.FromArgb(glowAlpha, trace.Color.R, trace.Color.G, trace.Color.B)),
                StrokeThickness = (8.0 + trace.Loudness * 18.0 + energy * 5.0) * fade,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Effect = new BlurEffect { Radius = 7.0 + trace.Energy * 5.0 },
                Opacity = 0.82
            };

            canvas.Children.Add(glow);

            Polyline core = new Polyline
            {
                Points = trace.Points,
                Stroke = new SolidColorBrush(Color.FromArgb(coreAlpha, trace.Color.R, trace.Color.G, trace.Color.B)),
                StrokeThickness = (1.1 + trace.Loudness * 7.0 + trace.StereoWidth * 1.2) * fade,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            canvas.Children.Add(core);
        }
    }

    private static void DrawCenterGlow(Canvas canvas, double width, double height, double energy, double loudness, double stereoWidth)
    {
        double centerX = width * 0.5;
        double centerY = height * 0.5;
        double glowSize = Math.Min(width, height) * (0.14 + loudness * 0.38 + energy * 0.1);

        Ellipse glow = new Ellipse
        {
            Width = glowSize,
            Height = glowSize,
            Fill = new RadialGradientBrush(
                Color.FromArgb(
                    (byte)Math.Clamp(35 + loudness * 150, 35, 185),
                    120,
                    255,
                    (byte)Math.Clamp(160 + stereoWidth * 80, 160, 240)),
                Color.FromArgb(0, 0, 90, 60)),
            Effect = new BlurEffect { Radius = 10 + energy * 10 }
        };

        Canvas.SetLeft(glow, centerX - glowSize * 0.5);
        Canvas.SetTop(glow, centerY - glowSize * 0.5);
        canvas.Children.Add(glow);
    }

    private static void DrawReadout(Canvas canvas, double width, double height, double loudness, double stereoWidth)
    {
        TextBlock readout = new TextBlock
        {
            Text = $"LEVEL {loudness:0.00}   WIDTH {stereoWidth:0.00}",
            Width = width,
            TextAlignment = TextAlignment.Center,
            FontFamily = new FontFamily("Consolas"),
            FontSize = Math.Clamp(height * 0.045, 9, 13),
            Foreground = new SolidColorBrush(Color.FromArgb(150, 140, 255, 205)),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 160),
                BlurRadius = 7,
                ShadowDepth = 0,
                Opacity = 0.55
            }
        };

        Canvas.SetLeft(readout, 0);
        Canvas.SetTop(readout, height - 23);
        canvas.Children.Add(readout);
    }

    private static void DrawTitle(Canvas canvas, double width, double height, double energy, double stereoWidth)
    {
        TextBlock title = new TextBlock
        {
            Text = "LISSAJOUS STEREO SCOPE",
            Width = width,
            TextAlignment = TextAlignment.Center,
            FontFamily = new FontFamily("Consolas"),
            FontSize = Math.Clamp(height * 0.052, 10, 15),
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Clamp(110 + energy * 120, 110, 230),
                140,
                255,
                (byte)Math.Clamp(190 + stereoWidth * 55, 190, 245))),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 170),
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.72
            }
        };

        Canvas.SetLeft(title, 0);
        Canvas.SetTop(title, 7);
        canvas.Children.Add(title);
    }

    private static void DrawVignette(Canvas canvas, double width, double height)
    {
        Rectangle vignette = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new RadialGradientBrush(
                Color.FromArgb(0, 0, 0, 0),
                Color.FromArgb(218, 0, 0, 0))
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.78,
                RadiusY = 0.94
            }
        };

        canvas.Children.Add(vignette);

        Rectangle scanlineOverlay = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new DrawingBrush
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 1, 5),
                ViewportUnits = BrushMappingMode.Absolute,
                Drawing = new GeometryDrawing
                {
                    Brush = new SolidColorBrush(Color.FromArgb(20, 130, 255, 180)),
                    Geometry = new RectangleGeometry(new Rect(0, 0, 1, 1))
                }
            },
            Opacity = 0.18
        };

        canvas.Children.Add(scanlineOverlay);
    }

    private static double CalculateLoudness(float[] leftSamples, float[] rightSamples)
    {
        int count = Math.Min(leftSamples.Length, rightSamples.Length);

        if (count == 0)
            return 0;

        double sum = 0;

        for (int i = 0; i < count; i += 4)
        {
            double left = leftSamples[i];
            double right = rightSamples[i];
            sum += Math.Sqrt((left * left + right * right) * 0.5);
        }

        return Math.Clamp(sum / Math.Max(1, count / 4), 0.0, 1.0);
    }

    private static double CalculateStereoWidth(float[] leftSamples, float[] rightSamples)
    {
        int count = Math.Min(leftSamples.Length, rightSamples.Length);

        if (count == 0)
            return 0;

        double sumSide = 0;
        double sumMid = 0;

        for (int i = 0; i < count; i += 4)
        {
            double left = leftSamples[i];
            double right = rightSamples[i];

            double mid = (left + right) * 0.5;
            double side = (left - right) * 0.5;

            sumMid += Math.Abs(mid);
            sumSide += Math.Abs(side);
        }

        return Math.Clamp(sumSide / Math.Max(sumMid + sumSide, 0.0001) * 2.0, 0.0, 1.0);
    }

    private static Color GetTraceColor(double energy, double stereoWidth, double trebleEnergy)
    {
        Color monoGreen = Color.FromRgb(90, 255, 155);
        Color stereoCyan = Color.FromRgb(120, 245, 255);
        Color hotWhite = Color.FromRgb(235, 255, 225);

        Color baseColor = Blend(monoGreen, stereoCyan, stereoWidth);

        return Blend(baseColor, hotWhite, Math.Clamp(energy * 0.28 + trebleEnergy * 0.22, 0.0, 0.55));
    }

    private static Color Blend(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0.0, 1.0);

        return Color.FromRgb(
            (byte)(from.R + (to.R - from.R) * amount),
            (byte)(from.G + (to.G - from.G) * amount),
            (byte)(from.B + (to.B - from.B) * amount));
    }

    private sealed record ScopeTrace(
        PointCollection Points,
        Color Color,
        double Energy,
        double Loudness,
        double StereoWidth);
}