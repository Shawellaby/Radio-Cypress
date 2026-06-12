using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace RadioCypress.Visualizations;

public sealed class EtherealSpectrumVisualizer : IVisualizer
{
    private const int WaveLayerCount = 9;
    private const int HistoryLimit = 12;

    private readonly List<EtherealWaveFrame> _waveHistory = new();
    private double _etherealPhase;

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

        _etherealPhase += 0.075 + energy * 0.08;

        DrawBackground(canvas, width, height, energy, bassEnergy);
        DrawSpectrumColumns(canvas, width, height, bucketLevels, energy, trebleEnergy);
        DrawEtherealWaves(canvas, width, height, bucketLevels, energy, bassEnergy, midEnergy, trebleEnergy);
        DrawParticles(canvas, width, height, bucketLevels, energy, bassEnergy, trebleEnergy);
        DrawVignette(canvas, width, height);
    }

    private void DrawBackground(Canvas canvas, double width, double height, double energy, double bassEnergy)
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
                    new GradientStop(Color.FromRgb(1, 1, 4), 0.0),
                    new GradientStop(Color.FromRgb(4, 7, 16), 0.42),
                    new GradientStop(Color.FromRgb(12, 5, 10), 1.0)
                }
            }
        };

        canvas.Children.Add(background);

        double auraSize = Math.Max(width, height) * (0.78 + energy * 0.18);

        Ellipse coolAura = new Ellipse
        {
            Width = auraSize,
            Height = auraSize * 0.68,
            Fill = new RadialGradientBrush(
                Color.FromArgb((byte)Math.Clamp(36 + bassEnergy * 70, 36, 106), 72, 215, 255),
                Color.FromArgb(0, 72, 215, 255)),
            Opacity = 0.72
        };

        Canvas.SetLeft(coolAura, width * 0.12 - auraSize * 0.5);
        Canvas.SetTop(coolAura, height * 0.42 - auraSize * 0.34);
        canvas.Children.Add(coolAura);

        Ellipse warmAura = new Ellipse
        {
            Width = auraSize * 0.82,
            Height = auraSize * 0.56,
            Fill = new RadialGradientBrush(
                Color.FromArgb((byte)Math.Clamp(32 + energy * 80, 32, 112), 255, 160, 92),
                Color.FromArgb(0, 255, 160, 92)),
            Opacity = 0.68
        };

        Canvas.SetLeft(warmAura, width * 0.88 - auraSize * 0.41);
        Canvas.SetTop(warmAura, height * 0.56 - auraSize * 0.28);
        canvas.Children.Add(warmAura);
    }

    private void DrawSpectrumColumns(Canvas canvas, double width, double height, double[] bucketLevels, double energy, double trebleEnergy)
    {
        int columnCount = Math.Max(80, bucketLevels.Length * 4);
        double columnWidth = Math.Max(1.0, width / columnCount * 0.34);
        double centerX = width * 0.64;
        double baseY = height * 0.76;
        double maxColumnHeight = height * (0.48 + trebleEnergy * 0.22);

        for (int column = 0; column < columnCount; column++)
        {
            double ratio = column / (double)Math.Max(1, columnCount - 1);
            int bucketIndex = Math.Clamp((int)Math.Round(ratio * (bucketLevels.Length - 1)), 0, bucketLevels.Length - 1);
            double level = bucketLevels[bucketIndex];

            double skyline = 0.32
                             + level * 0.82
                             + Math.Sin(ratio * Math.PI * 8.0 + _etherealPhase * 1.8) * 0.12
                             + Math.Sin(ratio * Math.PI * 21.0 - _etherealPhase * 2.2) * 0.08;

            skyline = Math.Clamp(skyline, 0.04, 1.0);

            double clusterEnvelope = Math.Exp(-Math.Pow((ratio - 0.68) / 0.36, 2.0));
            double columnHeight = maxColumnHeight * skyline * (0.32 + clusterEnvelope * 0.88);
            double x = ratio * width;
            double y = baseY - columnHeight;

            byte alpha = (byte)Math.Clamp(34 + level * 130 + clusterEnvelope * 42, 24, 205);
            Color coreColor = Blend(
                Color.FromRgb(105, 232, 255),
                Color.FromRgb(255, 186, 126),
                Math.Clamp(ratio * 1.25 - 0.18 + energy * 0.22, 0.0, 1.0));

            Line glow = new Line
            {
                X1 = x,
                Y1 = baseY,
                X2 = x,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.45), coreColor.R, coreColor.G, coreColor.B)),
                StrokeThickness = columnWidth * 3.2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Effect = new BlurEffect { Radius = 4.5 }
            };

            canvas.Children.Add(glow);

            Line bar = new Line
            {
                X1 = x,
                Y1 = baseY,
                X2 = x,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb(alpha, coreColor.R, coreColor.G, coreColor.B)),
                StrokeThickness = columnWidth,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            canvas.Children.Add(bar);
        }
    }

    private void DrawEtherealWaves(
        Canvas canvas,
        double width,
        double height,
        double[] bucketLevels,
        double energy,
        double bassEnergy,
        double midEnergy,
        double trebleEnergy)
    {
        List<PointCollection> currentLayers = new();

        for (int layer = 0; layer < WaveLayerCount; layer++)
        {
            PointCollection points = new PointCollection();
            int pointCount = 150;
            double layerRatio = layer / (double)Math.Max(1, WaveLayerCount - 1);
            double centerY = height * (0.53 + Math.Sin(layer * 0.78 + _etherealPhase * 0.58) * 0.055);
            double amplitude = height * (0.12 + bassEnergy * 0.16 + layerRatio * 0.035);
            double frequency = 2.25 + layerRatio * 1.85;
            double drift = _etherealPhase * (0.9 + layerRatio * 0.75) + layer * 0.72;

            for (int i = 0; i < pointCount; i++)
            {
                double ratio = i / (double)(pointCount - 1);
                double bucketPosition = ratio * (bucketLevels.Length - 1);
                int bucketIndex = Math.Clamp((int)Math.Round(bucketPosition), 0, bucketLevels.Length - 1);
                double level = bucketLevels[bucketIndex];

                double x = ratio * width;
                double primary = Math.Sin(ratio * Math.PI * frequency + drift);
                double secondary = Math.Sin(ratio * Math.PI * (frequency * 2.9) - drift * 1.35) * 0.28;
                double shimmer = Math.Sin(ratio * Math.PI * 23.0 + _etherealPhase * 3.6 + layer) * level * 0.16;
                double y = centerY + (primary + secondary + shimmer) * amplitude * (0.44 + level * 0.9);

                points.Add(new Point(x, y));
            }

            currentLayers.Add(points);
        }

        _waveHistory.Insert(0, new EtherealWaveFrame(currentLayers, energy, bassEnergy, midEnergy, trebleEnergy));

        if (_waveHistory.Count > HistoryLimit)
            _waveHistory.RemoveRange(HistoryLimit, _waveHistory.Count - HistoryLimit);

        for (int frameIndex = _waveHistory.Count - 1; frameIndex >= 0; frameIndex--)
        {
            EtherealWaveFrame frame = _waveHistory[frameIndex];
            double ageRatio = frameIndex / (double)Math.Max(1, HistoryLimit - 1);
            double fade = Math.Pow(1.0 - ageRatio, 1.85);

            for (int layer = 0; layer < frame.Layers.Count; layer++)
            {
                double layerRatio = layer / (double)Math.Max(1, frame.Layers.Count - 1);

                Color layerColor = GetEtherealColor(layerRatio, frame.BassEnergy, frame.MidEnergy, frame.TrebleEnergy);
                byte glowAlpha = (byte)Math.Clamp((20 + frame.Energy * 92) * fade, 0, 132);
                byte lineAlpha = (byte)Math.Clamp((56 + frame.Energy * 142) * fade, 0, 230);

                Polyline glowLine = new Polyline
                {
                    Points = frame.Layers[layer],
                    Stroke = new SolidColorBrush(Color.FromArgb(glowAlpha, layerColor.R, layerColor.G, layerColor.B)),
                    StrokeThickness = (9.0 + frame.Energy * 14.0 + layerRatio * 5.0) * fade,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Effect = new BlurEffect { Radius = 7.0 },
                    Opacity = 0.92
                };

                canvas.Children.Add(glowLine);

                Polyline line = new Polyline
                {
                    Points = frame.Layers[layer],
                    Stroke = new SolidColorBrush(Color.FromArgb(lineAlpha, layerColor.R, layerColor.G, layerColor.B)),
                    StrokeThickness = (0.85 + frame.Energy * 2.4 + layerRatio * 1.2) * fade,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };

                canvas.Children.Add(line);
            }
        }
    }

    private void DrawParticles(Canvas canvas, double width, double height, double[] bucketLevels, double energy, double bassEnergy, double trebleEnergy)
    {
        int particleCount = Math.Clamp((int)(24 + energy * 84), 24, 108);

        for (int i = 0; i < particleCount; i++)
        {
            double seed = i * 12.9898;
            double bucketLevel = bucketLevels[i % bucketLevels.Length];
            double drift = _etherealPhase * (0.42 + (i % 7) * 0.045);

            double x = width * Fraction(Math.Sin(seed + 9.3) * 43758.5453 + Math.Sin(drift + i) * 0.045);
            double band = i % 3 == 0 ? 0.69 : i % 3 == 1 ? 0.78 : 0.88;
            double y = height * (band + Math.Sin(seed * 0.37 + drift * 2.2) * 0.17 - bucketLevel * 0.34);
            double size = 1.6 + bucketLevel * 8.0 + (i % 5 == 0 ? bassEnergy * 7.0 : trebleEnergy * 4.0);

            Color particleColor = GetEtherealColor(Fraction(i * 0.137 + _etherealPhase * 0.03), bassEnergy, energy, trebleEnergy);

            Ellipse glow = new Ellipse
            {
                Width = size * 3.4,
                Height = size * 3.4,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(120, particleColor.R, particleColor.G, particleColor.B),
                    Color.FromArgb(0, particleColor.R, particleColor.G, particleColor.B)),
                Opacity = 0.62
            };

            Canvas.SetLeft(glow, x - size * 1.7);
            Canvas.SetTop(glow, y - size * 1.7);
            canvas.Children.Add(glow);

            Ellipse particle = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb(210, particleColor.R, particleColor.G, particleColor.B)),
                Opacity = 0.82
            };

            Canvas.SetLeft(particle, x - size * 0.5);
            Canvas.SetTop(particle, y - size * 0.5);
            canvas.Children.Add(particle);
        }
    }

    private static void DrawVignette(Canvas canvas, double width, double height)
    {
        Rectangle vignette = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new RadialGradientBrush(
                Color.FromArgb(0, 0, 0, 0),
                Color.FromArgb(190, 0, 0, 0))
            {
                RadiusX = 0.78,
                RadiusY = 0.95,
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.48)
            }
        };

        canvas.Children.Add(vignette);
    }

    private static Color GetEtherealColor(double ratio, double bassEnergy, double midEnergy, double trebleEnergy)
    {
        Color cyan = Color.FromRgb(150, 245, 255);
        Color pearl = Color.FromRgb(255, 252, 220);
        Color gold = Color.FromRgb(255, 211, 104);
        Color coral = Color.FromRgb(255, 118, 92);

        double shifted = Math.Clamp(ratio + trebleEnergy * 0.16 - bassEnergy * 0.08, 0.0, 1.0);

        if (shifted < 0.42)
            return Blend(cyan, pearl, shifted / 0.42);

        if (shifted < 0.74)
            return Blend(pearl, gold, (shifted - 0.42) / 0.32);

        return Blend(gold, coral, Math.Clamp((shifted - 0.74) / 0.26 + midEnergy * 0.15, 0.0, 1.0));
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

    private sealed record EtherealWaveFrame(
        List<PointCollection> Layers,
        double Energy,
        double BassEnergy,
        double MidEnergy,
        double TrebleEnergy);
}