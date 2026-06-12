using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Shawellaby.RadioCypress.Visualizations;

public sealed class VuMeterVisualizer : IVisualizer
{
    private double _leftPeak;
    private double _rightPeak;
    private double _leftNeedle;
    private double _rightNeedle;

    public void Draw(Canvas canvas, SpectrumVisualizationContext context)
    {
        canvas.Children.Clear();

        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        (double leftLevel, double rightLevel) = context.GetStereoLevels();

        leftLevel = Math.Clamp(leftLevel, 0.0, 1.0);
        rightLevel = Math.Clamp(rightLevel, 0.0, 1.0);

        _leftPeak = Math.Max(leftLevel, _leftPeak * 0.93);
        _rightPeak = Math.Max(rightLevel, _rightPeak * 0.93);

        _leftNeedle = Smooth(_leftNeedle, leftLevel, 0.32);
        _rightNeedle = Smooth(_rightNeedle, rightLevel, 0.32);

        DrawBackground(canvas, width, height);

        double meterMargin = Math.Max(18, width * 0.045);
        double meterWidth = width - meterMargin * 2;
        double meterHeight = Math.Max(42, height * 0.25);
        double gap = Math.Max(16, height * 0.08);

        double totalHeight = meterHeight * 2 + gap;
        double startY = height * 0.5 - totalHeight * 0.5;

        DrawMeter(canvas, "LEFT", meterMargin, startY, meterWidth, meterHeight, _leftNeedle, _leftPeak);
        DrawMeter(canvas, "RIGHT", meterMargin, startY + meterHeight + gap, meterWidth, meterHeight, _rightNeedle, _rightPeak);

        DrawTitle(canvas, width);
    }

    private static void DrawBackground(Canvas canvas, double width, double height)
    {
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
                    new GradientStop(Color.FromRgb(2, 4, 6), 0.0),
                    new GradientStop(Color.FromRgb(8, 14, 16), 0.45),
                    new GradientStop(Color.FromRgb(0, 0, 0), 1.0)
                }
            }
        };

        canvas.Children.Add(background);

        Rectangle scanlineOverlay = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new DrawingBrush
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 1, 4),
                ViewportUnits = BrushMappingMode.Absolute,
                Drawing = new GeometryDrawing
                {
                    Brush = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
                    Geometry = new RectangleGeometry(new Rect(0, 0, 1, 1))
                }
            },
            Opacity = 0.18
        };

        canvas.Children.Add(scanlineOverlay);
    }

    private static void DrawMeter(
        Canvas canvas,
        string label,
        double x,
        double y,
        double width,
        double height,
        double level,
        double peak)
    {
        BorderFrame(canvas, x, y, width, height);

        double labelWidth = Math.Clamp(width * 0.14, 52, 92);
        double barX = x + labelWidth;
        double barY = y + height * 0.27;
        double barWidth = width - labelWidth - 18;
        double barHeight = height * 0.46;

        TextBlock labelText = new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(118, 255, 220)),
            FontSize = Math.Clamp(height * 0.26, 13, 24),
            FontWeight = FontWeights.Bold,
            Width = labelWidth,
            TextAlignment = TextAlignment.Center,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 190),
                BlurRadius = 12,
                ShadowDepth = 0,
                Opacity = 0.85
            }
        };

        Canvas.SetLeft(labelText, x + 4);
        Canvas.SetTop(labelText, y + height * 0.34);
        canvas.Children.Add(labelText);

        Rectangle slot = new Rectangle
        {
            Width = barWidth,
            Height = barHeight,
            RadiusX = 5,
            RadiusY = 5,
            Fill = new SolidColorBrush(Color.FromRgb(8, 10, 10)),
            Stroke = new SolidColorBrush(Color.FromRgb(35, 55, 55)),
            StrokeThickness = 1
        };

        Canvas.SetLeft(slot, barX);
        Canvas.SetTop(slot, barY);
        canvas.Children.Add(slot);

        int segmentCount = 36;
        double gap = 3;
        double segmentWidth = Math.Max(2, (barWidth - gap * (segmentCount - 1)) / segmentCount);
        int activeSegments = (int)Math.Round(level * segmentCount);
        int peakSegment = Math.Clamp((int)Math.Round(peak * segmentCount), 0, segmentCount - 1);

        for (int i = 0; i < segmentCount; i++)
        {
            double ratio = i / (double)(segmentCount - 1);
            bool active = i < activeSegments;

            Color activeColor = ratio switch
            {
                < 0.68 => Color.FromRgb(50, 255, 70),
                < 0.86 => Color.FromRgb(255, 218, 38),
                _ => Color.FromRgb(255, 48, 28)
            };

            Color inactiveColor = ratio switch
            {
                < 0.68 => Color.FromRgb(7, 48, 12),
                < 0.86 => Color.FromRgb(55, 43, 3),
                _ => Color.FromRgb(55, 4, 2)
            };

            Rectangle segment = new Rectangle
            {
                Width = segmentWidth,
                Height = barHeight,
                RadiusX = 2,
                RadiusY = 2,
                Fill = new SolidColorBrush(active ? activeColor : inactiveColor),
                Opacity = active ? 0.95 : 0.45
            };

            if (active)
            {
                segment.Effect = new DropShadowEffect
                {
                    Color = activeColor,
                    BlurRadius = 9,
                    ShadowDepth = 0,
                    Opacity = 0.75
                };
            }

            Canvas.SetLeft(segment, barX + i * (segmentWidth + gap));
            Canvas.SetTop(segment, barY);
            canvas.Children.Add(segment);
        }

        double peakX = barX + peakSegment * (segmentWidth + gap);

        Rectangle peakMarker = new Rectangle
        {
            Width = Math.Max(2, segmentWidth * 0.55),
            Height = barHeight + 10,
            Fill = Brushes.White,
            Opacity = 0.88,
            Effect = new DropShadowEffect
            {
                Color = Colors.White,
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.9
            }
        };

        Canvas.SetLeft(peakMarker, peakX);
        Canvas.SetTop(peakMarker, barY - 5);
        canvas.Children.Add(peakMarker);

        DrawScale(canvas, barX, y + height * 0.08, barWidth, height * 0.16);
    }

    private static void BorderFrame(Canvas canvas, double x, double y, double width, double height)
    {
        Rectangle glow = new Rectangle
        {
            Width = width,
            Height = height,
            RadiusX = 10,
            RadiusY = 10,
            Fill = new SolidColorBrush(Color.FromArgb(26, 0, 255, 190)),
            Stroke = new SolidColorBrush(Color.FromArgb(150, 90, 255, 220)),
            StrokeThickness = 1.4,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 190),
                BlurRadius = 18,
                ShadowDepth = 0,
                Opacity = 0.42
            }
        };

        Canvas.SetLeft(glow, x);
        Canvas.SetTop(glow, y);
        canvas.Children.Add(glow);

        Rectangle panel = new Rectangle
        {
            Width = width,
            Height = height,
            RadiusX = 10,
            RadiusY = 10,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(18, 24, 24), 0.0),
                    new GradientStop(Color.FromRgb(5, 7, 7), 1.0)
                }
            },
            Stroke = new SolidColorBrush(Color.FromRgb(45, 75, 75)),
            StrokeThickness = 1
        };

        Canvas.SetLeft(panel, x);
        Canvas.SetTop(panel, y);
        canvas.Children.Add(panel);
    }

    private static void DrawScale(Canvas canvas, double x, double y, double width, double height)
    {
        string[] labels = ["-40", "-30", "-20", "-10", "-3", "0", "+3"];
        double[] positions = [0.00, 0.18, 0.36, 0.56, 0.74, 0.86, 1.00];

        for (int i = 0; i < labels.Length; i++)
        {
            double markerX = x + width * positions[i];

            Line tick = new Line
            {
                X1 = markerX,
                Y1 = y + height * 0.45,
                X2 = markerX,
                Y2 = y + height,
                Stroke = new SolidColorBrush(Color.FromArgb(130, 190, 255, 235)),
                StrokeThickness = 1
            };

            canvas.Children.Add(tick);

            TextBlock text = new TextBlock
            {
                Text = labels[i],
                Foreground = new SolidColorBrush(Color.FromArgb(150, 190, 255, 235)),
                FontSize = Math.Clamp(height * 0.62, 7, 11),
                Width = 34,
                TextAlignment = TextAlignment.Center
            };

            Canvas.SetLeft(text, markerX - 17);
            Canvas.SetTop(text, y - 1);
            canvas.Children.Add(text);
        }
    }

    private static void DrawTitle(Canvas canvas, double width)
    {
        TextBlock title = new TextBlock
        {
            Text = "STEREO VU METER",
            Foreground = new SolidColorBrush(Color.FromArgb(175, 125, 255, 225)),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Width = width,
            TextAlignment = TextAlignment.Center,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 190),
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.65
            }
        };

        Canvas.SetLeft(title, 0);
        Canvas.SetTop(title, 8);
        canvas.Children.Add(title);
    }

    private static double Smooth(double current, double target, double amount)
    {
        return current + (target - current) * Math.Clamp(amount, 0.0, 1.0);
    }
}