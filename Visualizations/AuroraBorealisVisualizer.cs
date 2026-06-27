using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Shawellaby.RadioCypress.Visualizations;

public sealed class AuroraBorealisVisualizer : IVisualizer
{
    private const int RibbonCount = 7;
    private const int HistoryLimit = 10;

    private readonly List<AuroraFrame> _history = new();
    private readonly Random _random = new(91827);

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

        _phase += 0.045 + energy * 0.075;

        DrawSky(canvas, width, height, energy, bassEnergy);
        DrawStars(canvas, width, height, energy, trebleEnergy);
        DrawAuroraRibbons(canvas, width, height, bucketLevels, energy, bassEnergy, midEnergy, trebleEnergy);
        DrawShimmerParticles(canvas, width, height, bucketLevels, energy, bassEnergy, trebleEnergy);
        DrawHorizon(canvas, width, height, energy, bassEnergy);
        DrawVignette(canvas, width, height);
    }

    private static void DrawSky(Canvas canvas, double width, double height, double energy, double bassEnergy)
    {
        Rectangle sky = new()
        {
            Width = width,
            Height = height,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(1, 3, 18), 0.0),
                    new GradientStop(Color.FromRgb(4, 12, 34), 0.38),
                    new GradientStop(Color.FromRgb(8, 8, 25), 0.72),
                    new GradientStop(Color.FromRgb(2, 3, 12), 1.0)
                }
            }
        };

        canvas.Children.Add(sky);

        double moonGlowSize = Math.Max(width, height) * (0.62 + energy * 0.12);

        Ellipse moonGlow = new()
        {
            Width = moonGlowSize,
            Height = moonGlowSize * 0.62,
            Fill = new RadialGradientBrush(
                Color.FromArgb((byte)Math.Clamp(28 + energy * 65 + bassEnergy * 42, 28, 135), 80, 210, 255),
                Color.FromArgb(0, 20, 60, 120)),
            Opacity = 0.72,
            Effect = new BlurEffect { Radius = 16 }
        };

        Canvas.SetLeft(moonGlow, width * 0.62 - moonGlowSize * 0.5);
        Canvas.SetTop(moonGlow, height * 0.22 - moonGlowSize * 0.31);
        canvas.Children.Add(moonGlow);

        double polarGlowSize = Math.Max(width, height) * (0.82 + bassEnergy * 0.16);

        Ellipse polarGlow = new()
        {
            Width = polarGlowSize,
            Height = polarGlowSize * 0.42,
            Fill = new RadialGradientBrush(
                Color.FromArgb((byte)Math.Clamp(34 + energy * 76, 34, 125), 40, 255, 170),
                Color.FromArgb(0, 0, 40, 60)),
            Opacity = 0.58,
            Effect = new BlurEffect { Radius = 24 }
        };

        Canvas.SetLeft(polarGlow, width * 0.5 - polarGlowSize * 0.5);
        Canvas.SetTop(polarGlow, height * 0.42 - polarGlowSize * 0.21);
        canvas.Children.Add(polarGlow);
    }

    private void DrawAuroraRibbons(
        Canvas canvas,
        double width,
        double height,
        double[] bucketLevels,
        double energy,
        double bassEnergy,
        double midEnergy,
        double trebleEnergy)
    {
        List<PointCollection> ribbons = new();

        for (int ribbon = 0; ribbon < RibbonCount; ribbon++)
        {
            PointCollection points = new();
            int pointCount = 170;
            double ribbonRatio = ribbon / (double)Math.Max(1, RibbonCount - 1);

            double baseY = height * (0.22 + ribbonRatio * 0.075);
            double bassLift = height * bassEnergy * (0.09 + ribbonRatio * 0.055);
            double amplitude = height * (0.045 + bassEnergy * 0.24 + ribbonRatio * 0.026);
            double frequency = 1.55 + ribbonRatio * 2.25;
            double drift = _phase * (0.78 + ribbonRatio * 0.42) + ribbon * 0.91;

            for (int i = 0; i < pointCount; i++)
            {
                double ratio = i / (double)(pointCount - 1);
                int bucketIndex = Math.Clamp((int)Math.Round(ratio * (bucketLevels.Length - 1)), 0, bucketLevels.Length - 1);
                double level = bucketLevels[bucketIndex];

                double primary = Math.Sin(ratio * Math.PI * frequency + drift);
                double deformation = Math.Sin(ratio * Math.PI * (frequency * 3.4) - drift * 1.7 + ribbon) * (0.18 + midEnergy * 0.58);
                double fineWave = Math.Sin(ratio * Math.PI * 27.0 + _phase * 4.2 + ribbon * 1.33) * level * (0.05 + trebleEnergy * 0.14);

                double x = ratio * width;
                double y = baseY
                           + bassLift
                           + (primary + deformation + fineWave) * amplitude * (0.65 + level * 0.85)
                           + Math.Sin(ratio * Math.PI + ribbon) * height * 0.10;

                points.Add(new Point(x, y));
            }

            ribbons.Add(points);
        }

        _history.Insert(0, new AuroraFrame(ribbons, energy, bassEnergy, midEnergy, trebleEnergy));

        if (_history.Count > HistoryLimit)
            _history.RemoveRange(HistoryLimit, _history.Count - HistoryLimit);

        for (int frameIndex = _history.Count - 1; frameIndex >= 0; frameIndex--)
        {
            AuroraFrame frame = _history[frameIndex];
            double ageRatio = frameIndex / (double)Math.Max(1, HistoryLimit - 1);
            double fade = Math.Pow(1.0 - ageRatio, 1.65);
            double brightness = Math.Clamp(0.48 + frame.Energy * 1.35, 0.48, 1.0);

            for (int ribbon = 0; ribbon < frame.Ribbons.Count; ribbon++)
            {
                double ribbonRatio = ribbon / (double)Math.Max(1, frame.Ribbons.Count - 1);
                Color color = GetAuroraColor(ribbonRatio, frame.BassEnergy, frame.MidEnergy, frame.TrebleEnergy);

                Polyline broadGlow = new()
                {
                    Points = frame.Ribbons[ribbon],
                    Stroke = new SolidColorBrush(Color.FromArgb(
                        (byte)Math.Clamp((20 + frame.Energy * 92) * fade * brightness, 0, 145),
                        color.R,
                        color.G,
                        color.B)),
                    StrokeThickness = (24 + frame.BassEnergy * 52 + ribbonRatio * 10) * fade,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Effect = new BlurEffect { Radius = 15 },
                    Opacity = 0.72
                };

                canvas.Children.Add(broadGlow);

                Polyline veil = new()
                {
                    Points = frame.Ribbons[ribbon],
                    Stroke = new SolidColorBrush(Color.FromArgb(
                        (byte)Math.Clamp((45 + frame.Energy * 135) * fade * brightness, 0, 230),
                        color.R,
                        color.G,
                        color.B)),
                    StrokeThickness = (5.2 + frame.BassEnergy * 13 + ribbonRatio * 3.5) * fade,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Effect = new BlurEffect { Radius = 2.6 }
                };

                canvas.Children.Add(veil);

                Polyline brightEdge = new()
                {
                    Points = frame.Ribbons[ribbon],
                    Stroke = new SolidColorBrush(Color.FromArgb(
                        (byte)Math.Clamp((82 + frame.Energy * 148) * fade, 0, 245),
                        (byte)Math.Clamp(color.R + 32, 0, 255),
                        (byte)Math.Clamp(color.G + 32, 0, 255),
                        (byte)Math.Clamp(color.B + 32, 0, 255))),
                    StrokeThickness = (0.8 + frame.TrebleEnergy * 2.2) * fade,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };

                canvas.Children.Add(brightEdge);

                DrawRibbonCurtain(canvas, frame.Ribbons[ribbon], color, frame.Energy, fade);
            }
        }
    }

    private static void DrawRibbonCurtain(Canvas canvas, PointCollection points, Color color, double energy, double fade)
    {
        int step = 9;

        for (int i = 0; i < points.Count; i += step)
        {
            Point point = points[i];
            double curtainHeight = 34 + energy * 115 + Math.Sin(i * 0.27) * 18;

            Line curtain = new()
            {
                X1 = point.X,
                Y1 = point.Y,
                X2 = point.X + Math.Sin(i * 0.13) * 10,
                Y2 = point.Y + curtainHeight,
                Stroke = new SolidColorBrush(Color.FromArgb(
                    (byte)Math.Clamp((16 + energy * 62) * fade, 0, 92),
                    color.R,
                    color.G,
                    color.B)),
                StrokeThickness = Math.Clamp(1.0 + energy * 2.8, 1.0, 3.8) * fade,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Effect = new BlurEffect { Radius = 5.5 }
            };

            canvas.Children.Add(curtain);
        }
    }

    private void DrawStars(Canvas canvas, double width, double height, double energy, double trebleEnergy)
    {
        int starCount = Math.Clamp((int)(58 + trebleEnergy * 76), 58, 134);

        for (int i = 0; i < starCount; i++)
        {
            double x = width * Fraction(Math.Sin(i * 19.719) * 21942.431);
            double y = height * Fraction(Math.Sin(i * 7.313 + 4.7) * 17621.83) * 0.78;
            double twinkle = 0.58 + Math.Sin(_phase * (2.6 + i % 5) + i * 1.17) * 0.42;
            double size = 0.8 + Fraction(Math.Sin(i * 13.13) * 5182.2) * 1.7 + trebleEnergy * twinkle * 1.8;

            Ellipse star = new()
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)Math.Clamp(70 + twinkle * 105 + energy * 75, 45, 235),
                    210,
                    245,
                    255)),
                Opacity = Math.Clamp(0.34 + twinkle * 0.48 + trebleEnergy * 0.22, 0.25, 1.0)
            };

            Canvas.SetLeft(star, x);
            Canvas.SetTop(star, y);
            canvas.Children.Add(star);
        }
    }

    private void DrawShimmerParticles(
        Canvas canvas,
        double width,
        double height,
        double[] bucketLevels,
        double energy,
        double bassEnergy,
        double trebleEnergy)
    {
        int particleCount = Math.Clamp((int)(18 + trebleEnergy * 110), 18, 128);

        for (int i = 0; i < particleCount; i++)
        {
            double bucketLevel = bucketLevels[i % bucketLevels.Length];
            double seed = i * 31.77;
            double drift = _phase * (0.45 + (i % 9) * 0.035);

            double x = width * Fraction(Math.Sin(seed + 2.1) * 9127.512 + drift * 0.06);
            double y = height * (0.18 + Fraction(Math.Sin(seed + 8.8) * 3712.14) * 0.52)
                       + Math.Sin(drift * 3.2 + i) * height * 0.045
                       + bassEnergy * height * 0.035;

            double size = 1.5 + bucketLevel * 5.2 + trebleEnergy * 5.5;
            Color color = GetAuroraColor(Fraction(i * 0.163 + _phase * 0.025), bassEnergy, energy, trebleEnergy);

            Ellipse glow = new()
            {
                Width = size * 3.4,
                Height = size * 3.4,
                Fill = new RadialGradientBrush(
                    Color.FromArgb((byte)Math.Clamp(70 + trebleEnergy * 120, 70, 190), color.R, color.G, color.B),
                    Color.FromArgb(0, color.R, color.G, color.B)),
                Opacity = Math.Clamp(0.25 + energy * 0.55, 0.25, 0.82)
            };

            Canvas.SetLeft(glow, x - size * 1.7);
            Canvas.SetTop(glow, y - size * 1.7);
            canvas.Children.Add(glow);

            Ellipse particle = new()
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb(215, color.R, color.G, color.B)),
                Opacity = Math.Clamp(0.38 + bucketLevel * 0.42 + trebleEnergy * 0.35, 0.38, 1.0)
            };

            Canvas.SetLeft(particle, x - size * 0.5);
            Canvas.SetTop(particle, y - size * 0.5);
            canvas.Children.Add(particle);
        }
    }

    private static void DrawHorizon(Canvas canvas, double width, double height, double energy, double bassEnergy)
    {
        double horizonY = height * 0.82;

        Rectangle glow = new()
        {
            Width = width,
            Height = height * 0.24,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.0),
                    new GradientStop(Color.FromArgb((byte)Math.Clamp(38 + energy * 58, 38, 110), 35, 160, 170), 0.46),
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 1.0)
                }
            },
            Opacity = 0.52
        };

        Canvas.SetTop(glow, horizonY - height * 0.12);
        canvas.Children.Add(glow);

        Polyline mountain = new()
        {
            Stroke = new SolidColorBrush(Color.FromArgb(210, 5, 12, 24)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(235, 1, 3, 12))
        };

        mountain.Points.Add(new Point(0, height));
        mountain.Points.Add(new Point(0, horizonY + height * 0.025));

        int peaks = 12;

        for (int i = 0; i <= peaks; i++)
        {
            double ratio = i / (double)peaks;
            double x = ratio * width;
            double peak = Math.Sin(i * 1.73) * height * 0.035 + Math.Sin(i * 0.81) * height * 0.052;
            double y = horizonY - Math.Abs(peak) - bassEnergy * height * 0.035;
            mountain.Points.Add(new Point(x, y));
        }

        mountain.Points.Add(new Point(width, height));
        canvas.Children.Add(mountain);
    }

    private static void DrawVignette(Canvas canvas, double width, double height)
    {
        Rectangle vignette = new()
        {
            Width = width,
            Height = height,
            Fill = new RadialGradientBrush(
                Color.FromArgb(0, 0, 0, 0),
                Color.FromArgb(215, 0, 0, 0))
            {
                Center = new Point(0.5, 0.48),
                GradientOrigin = new Point(0.5, 0.4),
                RadiusX = 0.82,
                RadiusY = 0.92
            }
        };

        canvas.Children.Add(vignette);
    }

    private static Color GetAuroraColor(double ratio, double bassEnergy, double midEnergy, double trebleEnergy)
    {
        Color green = Color.FromRgb(74, 255, 158);
        Color cyan = Color.FromRgb(88, 238, 255);
        Color blue = Color.FromRgb(98, 137, 255);
        Color violet = Color.FromRgb(198, 96, 255);
        Color rose = Color.FromRgb(255, 116, 196);

        double shifted = Math.Clamp(ratio + trebleEnergy * 0.18 - bassEnergy * 0.08 + midEnergy * 0.06, 0.0, 1.0);

        if (shifted < 0.32)
            return Blend(green, cyan, shifted / 0.32);

        if (shifted < 0.58)
            return Blend(cyan, blue, (shifted - 0.32) / 0.26);

        if (shifted < 0.82)
            return Blend(blue, violet, (shifted - 0.58) / 0.24);

        return Blend(violet, rose, (shifted - 0.82) / 0.18);
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

    private sealed record AuroraFrame(
        List<PointCollection> Ribbons,
        double Energy,
        double BassEnergy,
        double MidEnergy,
        double TrebleEnergy);
}