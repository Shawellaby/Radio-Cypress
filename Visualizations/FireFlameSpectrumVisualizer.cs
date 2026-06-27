using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Shawellaby.RadioCypress.Visualizations;

public sealed class FireFlameSpectrumVisualizer : IVisualizer
{
    private const int FlameHistoryLimit = 8;
    private const int SparkLimit = 90;

    private readonly List<FlameFrame> _flameHistory = new();
    private readonly List<Spark> _sparks = new();
    private readonly Random _random = new(7331);

    private double _phase;

    public void Draw(Canvas canvas, SpectrumVisualizationContext context)
    {
        canvas.Children.Clear();

        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        double[] bucketLevels = context.GetFrequencyBucketLevels();
        double energy = bucketLevels.DefaultIfEmpty(0).Average();
        double bassEnergy = bucketLevels.Take(8).DefaultIfEmpty(0).Average();
        double midEnergy = bucketLevels.Skip(8).Take(12).DefaultIfEmpty(0).Average();
        double trebleEnergy = bucketLevels.Skip(bucketLevels.Length / 2).DefaultIfEmpty(0).Average();

        _phase += 0.075 + energy * 0.16;

        DrawBackground(canvas, width, height, energy, bassEnergy);
        DrawEmbers(canvas, width, height, energy, trebleEnergy);
        DrawFlames(canvas, width, height, bucketLevels, energy, bassEnergy, midEnergy, trebleEnergy);
        DrawSparks(canvas, width, height, energy, bassEnergy, trebleEnergy);
        DrawHeatHaze(canvas, width, height, energy, midEnergy);
        DrawVignette(canvas, width, height);
    }

    private static void DrawBackground(Canvas canvas, double width, double height, double energy, double bassEnergy)
    {
        Rectangle background = new()
        {
            Width = width,
            Height = height,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(2, 0, 0), 0.0),
                    new GradientStop(Color.FromRgb(12, 2, 0), 0.42),
                    new GradientStop(Color.FromRgb(38, 7, 0), 0.78),
                    new GradientStop(Color.FromRgb(80, 15, 0), 1.0)
                }
            }
        };

        canvas.Children.Add(background);

        double glowSize = Math.Max(width, height) * (0.85 + bassEnergy * 0.25);

        Ellipse baseGlow = new()
        {
            Width = glowSize,
            Height = glowSize * 0.55,
            Fill = new RadialGradientBrush(
                Color.FromArgb((byte)Math.Clamp(65 + energy * 130, 65, 195), 255, 65, 0),
                Color.FromArgb(0, 120, 8, 0)),
            Opacity = 0.85,
            Effect = new BlurEffect { Radius = 22 }
        };

        Canvas.SetLeft(baseGlow, width * 0.5 - glowSize * 0.5);
        Canvas.SetTop(baseGlow, height * 0.78 - glowSize * 0.28);
        canvas.Children.Add(baseGlow);

        Rectangle coalBed = new()
        {
            Width = width,
            Height = Math.Max(20, height * 0.12),
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.0),
                    new GradientStop(Color.FromArgb(185, 80, 8, 0), 0.52),
                    new GradientStop(Color.FromArgb(245, 5, 0, 0), 1.0)
                }
            }
        };

        Canvas.SetTop(coalBed, height - coalBed.Height);
        canvas.Children.Add(coalBed);
    }

    private void DrawFlames(
        Canvas canvas,
        double width,
        double height,
        double[] bucketLevels,
        double energy,
        double bassEnergy,
        double midEnergy,
        double trebleEnergy)
    {
        List<PointCollection> currentFlames = new();

        int flameCount = Math.Max(28, bucketLevels.Length);
        double baseY = height * 0.98;
        double segmentWidth = width / flameCount;

        for (int flame = 0; flame < flameCount; flame++)
        {
            double ratio = flame / (double)Math.Max(1, flameCount - 1);
            int bucketIndex = Math.Clamp((int)Math.Round(ratio * (bucketLevels.Length - 1)), 0, bucketLevels.Length - 1);
            double level = bucketLevels[bucketIndex];

            double x = ratio * width;
            double flameHeight = height * (0.18 + bassEnergy * 0.36 + level * 0.44);
            double wave = Math.Sin(_phase * 2.0 + ratio * Math.PI * 7.0) * midEnergy * segmentWidth * 1.6;
            double flicker = Math.Sin(_phase * 5.4 + flame * 0.83) * segmentWidth * (0.35 + level * 0.9);

            PointCollection points = new()
            {
                new Point(x - segmentWidth * 0.85, baseY),
                new Point(x - segmentWidth * 0.62 + wave * 0.25, baseY - flameHeight * 0.28),
                new Point(x - segmentWidth * 0.28 - wave * 0.35, baseY - flameHeight * 0.62),
                new Point(x + flicker, baseY - flameHeight * (0.92 + level * 0.22)),
                new Point(x + segmentWidth * 0.34 + wave * 0.25, baseY - flameHeight * 0.56),
                new Point(x + segmentWidth * 0.70 - wave * 0.18, baseY - flameHeight * 0.24),
                new Point(x + segmentWidth * 0.86, baseY)
            };

            currentFlames.Add(points);
        }

        _flameHistory.Insert(0, new FlameFrame(currentFlames, energy, bassEnergy, midEnergy, trebleEnergy));

        if (_flameHistory.Count > FlameHistoryLimit)
            _flameHistory.RemoveRange(FlameHistoryLimit, _flameHistory.Count - FlameHistoryLimit);

        for (int frameIndex = _flameHistory.Count - 1; frameIndex >= 0; frameIndex--)
        {
            FlameFrame frame = _flameHistory[frameIndex];
            double ageRatio = frameIndex / (double)Math.Max(1, FlameHistoryLimit - 1);
            double fade = Math.Pow(1.0 - ageRatio, 1.45);

            for (int i = 0; i < frame.Flames.Count; i++)
            {
                double ratio = i / (double)Math.Max(1, frame.Flames.Count - 1);
                Color outerColor = GetFlameColor(ratio, frame.Energy, frame.BassEnergy, frame.MidEnergy, frame.TrebleEnergy, inner: false);
                Color innerColor = GetFlameColor(ratio, frame.Energy, frame.BassEnergy, frame.MidEnergy, frame.TrebleEnergy, inner: true);

                Polygon outerFlame = new()
                {
                    Points = frame.Flames[i],
                    Fill = new SolidColorBrush(Color.FromArgb(
                        (byte)Math.Clamp((28 + frame.Energy * 92) * fade, 0, 135),
                        outerColor.R,
                        outerColor.G,
                        outerColor.B)),
                    Stroke = Brushes.Transparent,
                    Effect = new BlurEffect { Radius = 7.5 },
                    Opacity = 0.72
                };

                canvas.Children.Add(outerFlame);

                Polygon flame = new()
                {
                    Points = frame.Flames[i],
                    Fill = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 1),
                        EndPoint = new Point(0, 0),
                        GradientStops =
                        {
                            new GradientStop(Color.FromArgb(
                                (byte)Math.Clamp((150 + frame.Energy * 85) * fade, 0, 245),
                                outerColor.R,
                                outerColor.G,
                                outerColor.B), 0.0),
                            new GradientStop(Color.FromArgb(
                                (byte)Math.Clamp((95 + frame.Energy * 120) * fade, 0, 235),
                                innerColor.R,
                                innerColor.G,
                                innerColor.B), 0.58),
                            new GradientStop(Color.FromArgb(
                                (byte)Math.Clamp((18 + frame.Energy * 108) * fade, 0, 145),
                                255,
                                248,
                                170), 1.0)
                        }
                    },
                    Stroke = new SolidColorBrush(Color.FromArgb(
                        (byte)Math.Clamp((28 + frame.TrebleEnergy * 80) * fade, 0, 120),
                        255,
                        230,
                        115)),
                    StrokeThickness = Math.Clamp(0.5 + frame.TrebleEnergy * 1.6, 0.5, 2.1) * fade,
                    Opacity = Math.Clamp(0.42 + fade * 0.64, 0.0, 1.0)
                };

                canvas.Children.Add(flame);
            }
        }

        DrawInnerCore(canvas, width, height, energy, bassEnergy);
    }

    private static void DrawInnerCore(Canvas canvas, double width, double height, double energy, double bassEnergy)
    {
        double coreHeight = height * (0.16 + bassEnergy * 0.24);
        double coreWidth = width * (0.72 + energy * 0.18);

        Rectangle core = new()
        {
            Width = coreWidth,
            Height = coreHeight,
            RadiusX = coreWidth * 0.12,
            RadiusY = coreHeight * 0.7,
            Fill = new RadialGradientBrush(
                Color.FromArgb((byte)Math.Clamp(95 + energy * 135, 95, 230), 255, 245, 160),
                Color.FromArgb(0, 255, 80, 0)),
            Opacity = 0.82,
            Effect = new BlurEffect { Radius = 18 }
        };

        Canvas.SetLeft(core, width * 0.5 - coreWidth * 0.5);
        Canvas.SetTop(core, height - coreHeight * 0.92);
        canvas.Children.Add(core);
    }

    private void DrawSparks(Canvas canvas, double width, double height, double energy, double bassEnergy, double trebleEnergy)
    {
        int newSparkCount = Math.Clamp((int)(trebleEnergy * 9 + energy * 3), 0, 14);

        for (int i = 0; i < newSparkCount && _sparks.Count < SparkLimit; i++)
        {
            double x = width * (0.08 + _random.NextDouble() * 0.84);
            double y = height * (0.86 + _random.NextDouble() * 0.12);

            _sparks.Add(new Spark(
                x,
                y,
                (_random.NextDouble() - 0.5) * (1.8 + trebleEnergy * 5.5),
                -(2.0 + _random.NextDouble() * 5.8 + trebleEnergy * 7.8 + bassEnergy * 3.8),
                1.0,
                1.4 + _random.NextDouble() * 4.5 + trebleEnergy * 5.0,
                _random.NextDouble() * Math.PI * 2.0));
        }

        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            Spark spark = _sparks[i];

            spark.X += spark.VelocityX + Math.Sin(_phase * 2.0 + spark.Phase) * 0.45;
            spark.Y += spark.VelocityY;
            spark.VelocityY += 0.07;
            spark.Life -= 0.018 + Math.Max(0.0, spark.Y / Math.Max(1.0, height)) * 0.006;

            if (spark.Life <= 0 || spark.Y < -20 || spark.X < -20 || spark.X > width + 20)
            {
                _sparks.RemoveAt(i);
                continue;
            }

            byte alpha = (byte)Math.Clamp(spark.Life * (150 + trebleEnergy * 105), 0, 255);
            Color sparkColor = energy > 0.58
                ? Color.FromRgb(255, 248, 190)
                : Color.FromRgb(255, 170, 45);

            Ellipse glow = new()
            {
                Width = spark.Size * 3.6,
                Height = spark.Size * 3.6,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(alpha, sparkColor.R, sparkColor.G, sparkColor.B),
                    Color.FromArgb(0, sparkColor.R, sparkColor.G, sparkColor.B)),
                Opacity = 0.72
            };

            Canvas.SetLeft(glow, spark.X - spark.Size * 1.8);
            Canvas.SetTop(glow, spark.Y - spark.Size * 1.8);
            canvas.Children.Add(glow);

            Ellipse ember = new()
            {
                Width = spark.Size,
                Height = spark.Size,
                Fill = new SolidColorBrush(Color.FromArgb(alpha, sparkColor.R, sparkColor.G, sparkColor.B)),
                Effect = new DropShadowEffect
                {
                    Color = sparkColor,
                    BlurRadius = 8,
                    ShadowDepth = 0,
                    Opacity = 0.85
                }
            };

            Canvas.SetLeft(ember, spark.X - spark.Size * 0.5);
            Canvas.SetTop(ember, spark.Y - spark.Size * 0.5);
            canvas.Children.Add(ember);
        }
    }

    private void DrawEmbers(Canvas canvas, double width, double height, double energy, double trebleEnergy)
    {
        int emberCount = Math.Clamp((int)(42 + energy * 70), 42, 112);

        for (int i = 0; i < emberCount; i++)
        {
            double seed = i * 17.371;
            double drift = _phase * (0.18 + (i % 7) * 0.035);
            double x = width * Fraction(Math.Sin(seed + 3.2) * 9137.19 + Math.Sin(drift) * 0.035);
            double y = height * (0.52 + Fraction(Math.Sin(seed + 8.1) * 7132.73) * 0.44);
            double pulse = 0.55 + Math.Sin(_phase * 3.2 + i * 0.91) * 0.45;
            double size = 1.0 + pulse * 2.2 + trebleEnergy * 2.4;

            Ellipse ember = new()
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)Math.Clamp(35 + pulse * 80 + energy * 70, 35, 185),
                    255,
                    (byte)Math.Clamp(70 + energy * 110, 70, 180),
                    25)),
                Opacity = Math.Clamp(0.28 + pulse * 0.38 + energy * 0.25, 0.22, 0.95)
            };

            Canvas.SetLeft(ember, x);
            Canvas.SetTop(ember, y);
            canvas.Children.Add(ember);
        }
    }

    private void DrawHeatHaze(Canvas canvas, double width, double height, double energy, double midEnergy)
    {
        int hazeLines = 16;

        for (int i = 0; i < hazeLines; i++)
        {
            double ratio = i / (double)Math.Max(1, hazeLines - 1);
            double y = height * (0.34 + ratio * 0.42);
            double offset = Math.Sin(_phase * 1.7 + i * 0.83) * width * 0.025 * midEnergy;

            Polyline haze = new()
            {
                Stroke = new SolidColorBrush(Color.FromArgb(
                    (byte)Math.Clamp(8 + energy * 36, 8, 48),
                    255,
                    205,
                    120)),
                StrokeThickness = 1.0 + energy * 1.4,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Effect = new BlurEffect { Radius = 5.0 },
                Opacity = 0.45
            };

            int points = 60;

            for (int p = 0; p < points; p++)
            {
                double pointRatio = p / (double)(points - 1);
                double x = pointRatio * width;
                double wave = Math.Sin(pointRatio * Math.PI * 5.0 + _phase * 2.4 + i) * width * 0.012 * (0.4 + midEnergy);
                haze.Points.Add(new Point(x + offset + wave, y + Math.Sin(pointRatio * Math.PI * 3.0 + i) * 3.0));
            }

            canvas.Children.Add(haze);
        }
    }

    private static void DrawVignette(Canvas canvas, double width, double height)
    {
        Rectangle vignette = new()
        {
            Width = width,
            Height = height,
            Fill = new RadialGradientBrush(
                Color.FromArgb(0, 0, 0, 0),
                Color.FromArgb(220, 0, 0, 0))
            {
                Center = new Point(0.5, 0.62),
                GradientOrigin = new Point(0.5, 0.7),
                RadiusX = 0.82,
                RadiusY = 0.92
            }
        };

        canvas.Children.Add(vignette);
    }

    private static Color GetFlameColor(
        double ratio,
        double energy,
        double bassEnergy,
        double midEnergy,
        double trebleEnergy,
        bool inner)
    {
        double heat = Math.Clamp(energy * 0.7 + bassEnergy * 0.18 + trebleEnergy * 0.22 + midEnergy * 0.12, 0.0, 1.0);
        double shifted = Math.Clamp(ratio * 0.25 + heat, 0.0, 1.0);

        Color deepRed = Color.FromRgb(150, 10, 0);
        Color orange = Color.FromRgb(255, 95, 0);
        Color gold = Color.FromRgb(255, 190, 45);
        Color whiteHot = Color.FromRgb(255, 250, 215);

        if (inner)
            shifted = Math.Clamp(shifted + 0.22, 0.0, 1.0);

        if (shifted < 0.38)
            return Blend(deepRed, orange, shifted / 0.38);

        if (shifted < 0.76)
            return Blend(orange, gold, (shifted - 0.38) / 0.38);

        return Blend(gold, whiteHot, (shifted - 0.76) / 0.24);
    }

    private static Color Blend(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0.0, 1.0);

        return Color.FromRgb(
            (byte)(from.R + (to.R - from.R) * amount),
            (byte)(from.G + (to.G - from.G) * amount),
            (byte)(from.B + (to.B - from.B) * amount));
    }

    private static double Fraction(double value)
    {
        return value - Math.Floor(value);
    }

    private sealed record FlameFrame(
        List<PointCollection> Flames,
        double Energy,
        double BassEnergy,
        double MidEnergy,
        double TrebleEnergy);

    private sealed class Spark
    {
        public Spark(double x, double y, double velocityX, double velocityY, double life, double size, double phase)
        {
            X = x;
            Y = y;
            VelocityX = velocityX;
            VelocityY = velocityY;
            Life = life;
            Size = size;
            Phase = phase;
        }

        public double X { get; set; }

        public double Y { get; set; }

        public double VelocityX { get; set; }

        public double VelocityY { get; set; }

        public double Life { get; set; }

        public double Size { get; }

        public double Phase { get; }
    }
}