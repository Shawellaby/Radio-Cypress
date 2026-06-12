using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Shawellaby.RadioCypress.Visualizations;

public sealed class StarfieldSpectrumVisualizer : IVisualizer
{
    private const int StarCount = 170;

    private readonly List<Star> _stars = new();
    private readonly Random _random = new(8675309);
    private double _phase;

    public void Draw(Canvas canvas, SpectrumVisualizationContext context)
    {
        canvas.Children.Clear();

        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        EnsureStars();

        double[] bucketLevels = context.GetFrequencyBucketLevels();
        double energy = bucketLevels.DefaultIfEmpty(0).Average();
        double bassEnergy = bucketLevels.Take(8).DefaultIfEmpty(0).Average();
        double midEnergy = bucketLevels.Skip(8).Take(12).DefaultIfEmpty(0).Average();
        double trebleEnergy = bucketLevels.Skip(bucketLevels.Length / 2).DefaultIfEmpty(0).Average();

        _phase += 0.04 + energy * 0.08;

        double centerX = width * (0.5 + Math.Sin(_phase * 0.45) * bassEnergy * 0.04);
        double centerY = height * (0.5 + Math.Cos(_phase * 0.37) * midEnergy * 0.04);
        double maxDistance = Math.Sqrt(width * width + height * height) * 0.55;
        double warpSpeed = 0.009 + bassEnergy * 0.042 + energy * 0.018;

        DrawBackground(canvas, width, height, energy, bassEnergy, trebleEnergy);
        DrawWarpTunnel(canvas, width, height, centerX, centerY, energy, bassEnergy);

        foreach (Star star in _stars)
        {
            star.Depth -= warpSpeed * star.Speed;

            if (star.Depth <= 0.03)
                ResetStar(star, true);

            DrawStar(canvas, star, centerX, centerY, maxDistance, energy, bassEnergy, trebleEnergy);
        }

        DrawTrebleSparkles(canvas, width, height, centerX, centerY, bucketLevels, trebleEnergy);
        DrawHudText(canvas, width, height, energy, bassEnergy);
        DrawVignette(canvas, width, height);
    }

    private void EnsureStars()
    {
        while (_stars.Count < StarCount)
        {
            Star star = new();
            ResetStar(star, false);
            star.Depth = 0.08 + _random.NextDouble() * 0.92;
            _stars.Add(star);
        }
    }

    private void ResetStar(Star star, bool startFarAway)
    {
        double angle = _random.NextDouble() * Math.PI * 2.0;
        double radius = Math.Sqrt(_random.NextDouble());

        star.X = Math.Cos(angle) * radius;
        star.Y = Math.Sin(angle) * radius;
        star.Depth = startFarAway ? 1.0 : 0.08 + _random.NextDouble() * 0.92;
        star.Speed = 0.55 + _random.NextDouble() * 1.35;
        star.Size = 0.65 + _random.NextDouble() * 2.15;
        star.HueOffset = _random.NextDouble() * 360.0;
        star.Twinkle = _random.NextDouble() * Math.PI * 2.0;
    }

    private static void DrawBackground(Canvas canvas, double width, double height, double energy, double bassEnergy, double trebleEnergy)
    {
        Rectangle background = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.9,
                RadiusY = 0.9,
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(
                        (byte)Math.Clamp(4 + bassEnergy * 18, 4, 22),
                        (byte)Math.Clamp(7 + trebleEnergy * 18, 7, 25),
                        (byte)Math.Clamp(20 + energy * 44, 20, 64)), 0.0),
                    new GradientStop(Color.FromRgb(1, 2, 10), 0.58),
                    new GradientStop(Color.FromRgb(0, 0, 0), 1.0)
                }
            }
        };

        canvas.Children.Add(background);

        double nebulaSize = Math.Max(width, height) * (0.72 + energy * 0.25);

        Ellipse nebula = new Ellipse
        {
            Width = nebulaSize,
            Height = nebulaSize * 0.72,
            Fill = new RadialGradientBrush(
                Color.FromArgb((byte)Math.Clamp(28 + energy * 76, 28, 104), 60, 120, 255),
                Color.FromArgb(0, 20, 20, 80)),
            Opacity = 0.72,
            Effect = new BlurEffect { Radius = 18 }
        };

        Canvas.SetLeft(nebula, width * 0.5 - nebulaSize * 0.5);
        Canvas.SetTop(nebula, height * 0.52 - nebulaSize * 0.36);
        canvas.Children.Add(nebula);
    }

    private static void DrawWarpTunnel(
        Canvas canvas,
        double width,
        double height,
        double centerX,
        double centerY,
        double energy,
        double bassEnergy)
    {
        int ringCount = 8;
        double maxRadius = Math.Sqrt(width * width + height * height) * 0.5;

        for (int i = 0; i < ringCount; i++)
        {
            double ratio = (i + 1) / (double)ringCount;
            double pulse = 1.0 + Math.Sin(ratio * Math.PI * 8.0 + energy * 12.0) * 0.035;
            double radius = maxRadius * ratio * pulse;
            byte alpha = (byte)Math.Clamp((18 + bassEnergy * 54) * (1.0 - ratio * 0.62), 4, 76);

            Ellipse ring = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = new SolidColorBrush(Color.FromArgb(alpha, 80, 180, 255)),
                StrokeThickness = 1.0 + bassEnergy * 2.5,
                Fill = Brushes.Transparent,
                Effect = new BlurEffect { Radius = 2.5 + bassEnergy * 5.0 }
            };

            Canvas.SetLeft(ring, centerX - radius);
            Canvas.SetTop(ring, centerY - radius);
            canvas.Children.Add(ring);
        }
    }

    private void DrawStar(
        Canvas canvas,
        Star star,
        double centerX,
        double centerY,
        double maxDistance,
        double energy,
        double bassEnergy,
        double trebleEnergy)
    {
        double perspective = 1.0 / Math.Max(star.Depth, 0.035);
        double distance = maxDistance * (1.0 - star.Depth);
        double x = centerX + star.X * distance * perspective * 0.22;
        double y = centerY + star.Y * distance * perspective * 0.22;

        double previousDepth = Math.Clamp(star.Depth + 0.035 + bassEnergy * 0.09, 0.035, 1.0);
        double previousPerspective = 1.0 / previousDepth;
        double previousDistance = maxDistance * (1.0 - previousDepth);
        double previousX = centerX + star.X * previousDistance * previousPerspective * 0.22;
        double previousY = centerY + star.Y * previousDistance * previousPerspective * 0.22;

        if (x < -80 || x > canvas.ActualWidth + 80 || y < -80 || y > canvas.ActualHeight + 80)
        {
            ResetStar(star, true);
            return;
        }

        double nearFactor = 1.0 - star.Depth;
        double twinkle = 0.65 + Math.Sin(_phase * 8.0 + star.Twinkle) * 0.35;
        double intensity = Math.Clamp(nearFactor * 0.75 + energy * 0.85 + twinkle * 0.2, 0.0, 1.0);
        double size = star.Size * (0.65 + nearFactor * 3.7 + energy * 1.8);
        double streakThickness = Math.Clamp(size * 0.42, 0.7, 4.8);

        Color color = SpectrumVisualizationContext.GetPsychedelicColor(
            205 + star.HueOffset * 0.08 + trebleEnergy * 120 + _phase * 28,
            (byte)Math.Clamp(80 + intensity * 175, 80, 255));

        Line streakGlow = new Line
        {
            X1 = previousX,
            Y1 = previousY,
            X2 = x,
            Y2 = y,
            Stroke = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Clamp(22 + intensity * 90, 22, 112),
                color.R,
                color.G,
                color.B)),
            StrokeThickness = streakThickness * (2.2 + bassEnergy * 2.4),
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Effect = new BlurEffect { Radius = 4.5 + energy * 4.0 }
        };

        canvas.Children.Add(streakGlow);

        Line streak = new Line
        {
            X1 = previousX,
            Y1 = previousY,
            X2 = x,
            Y2 = y,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = streakThickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Opacity = 0.78 + intensity * 0.22
        };

        canvas.Children.Add(streak);

        Ellipse core = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Clamp(140 + intensity * 115, 140, 255),
                245,
                255,
                255)),
            Effect = new DropShadowEffect
            {
                Color = color,
                BlurRadius = 7 + energy * 10,
                ShadowDepth = 0,
                Opacity = 0.75
            }
        };

        Canvas.SetLeft(core, x - size * 0.5);
        Canvas.SetTop(core, y - size * 0.5);
        canvas.Children.Add(core);
    }

    private void DrawTrebleSparkles(
        Canvas canvas,
        double width,
        double height,
        double centerX,
        double centerY,
        double[] bucketLevels,
        double trebleEnergy)
    {
        int sparkleCount = Math.Clamp((int)(trebleEnergy * 36), 0, 36);

        for (int i = 0; i < sparkleCount; i++)
        {
            double level = bucketLevels[(bucketLevels.Length - 1 - i + bucketLevels.Length) % bucketLevels.Length];
            double angle = i * 2.399963 + _phase * (1.2 + level);
            double radius = Math.Min(width, height) * (0.1 + Fraction(Math.Sin(i * 91.7 + _phase) * 43758.5453) * 0.43);
            double x = centerX + Math.Cos(angle) * radius;
            double y = centerY + Math.Sin(angle) * radius;
            double sparkleSize = 3.0 + level * 13.0;

            Color color = SpectrumVisualizationContext.GetPsychedelicColor(265 + i * 19 + _phase * 80, 210);

            Line horizontal = new Line
            {
                X1 = x - sparkleSize,
                Y1 = y,
                X2 = x + sparkleSize,
                Y2 = y,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 1.2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Effect = new DropShadowEffect
                {
                    Color = color,
                    BlurRadius = 8,
                    ShadowDepth = 0,
                    Opacity = 0.85
                }
            };

            Line vertical = new Line
            {
                X1 = x,
                Y1 = y - sparkleSize,
                X2 = x,
                Y2 = y + sparkleSize,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 1.2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Effect = new DropShadowEffect
                {
                    Color = color,
                    BlurRadius = 8,
                    ShadowDepth = 0,
                    Opacity = 0.85
                }
            };

            canvas.Children.Add(horizontal);
            canvas.Children.Add(vertical);
        }
    }

    private static void DrawHudText(Canvas canvas, double width, double height, double energy, double bassEnergy)
    {
        TextBlock title = new TextBlock
        {
            Text = $"STARFIELD  WARP {(1 + bassEnergy * 8):0.0}",
            Foreground = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Clamp(100 + energy * 135, 100, 235),
                145,
                230,
                255)),
            FontSize = Math.Clamp(height * 0.055, 10, 15),
            FontWeight = FontWeights.SemiBold,
            Width = width,
            TextAlignment = TextAlignment.Center,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(70, 190, 255),
                BlurRadius = 9,
                ShadowDepth = 0,
                Opacity = 0.7
            }
        };

        Canvas.SetLeft(title, 0);
        Canvas.SetTop(title, 8);
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
                Color.FromArgb(210, 0, 0, 0))
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.78,
                RadiusY = 0.92
            }
        };

        canvas.Children.Add(vignette);
    }

    private static double Fraction(double value)
    {
        return value - Math.Floor(value);
    }

    private sealed class Star
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Depth { get; set; }

        public double Speed { get; set; }

        public double Size { get; set; }

        public double HueOffset { get; set; }

        public double Twinkle { get; set; }
    }
}