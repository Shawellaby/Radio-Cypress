using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Shawellaby.RadioCypress.Visualizations;

public sealed class MatrixRainSpectrumVisualizer : IVisualizer
{
    private const string Glyphs = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワン";
    private const int MaxColumns = 96;

    private readonly Random _random = new(404);
    private readonly List<RainColumn> _columns = new();

    private double _phase;
    private double _beatFlash;

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

        _phase += 0.045 + energy * 0.085;
        _beatFlash = Math.Max(_beatFlash * 0.82, Math.Clamp((energy - 0.58) * 2.4 + bassEnergy * 0.45, 0.0, 1.0));

        double fontSize = Math.Clamp(width / 42.0, 11.0, 18.0);
        double columnWidth = fontSize * 0.82;
        int columnCount = Math.Clamp((int)(width / columnWidth), 24, MaxColumns);

        EnsureColumns(columnCount, height, fontSize);
        DrawBackground(canvas, width, height, energy, bassEnergy, _beatFlash);

        for (int i = 0; i < columnCount; i++)
        {
            RainColumn column = _columns[i];
            double bucketLevel = bucketLevels[Math.Clamp((int)Math.Round(i / (double)Math.Max(1, columnCount - 1) * (bucketLevels.Length - 1)), 0, bucketLevels.Length - 1)];

            UpdateColumn(column, height, fontSize, bassEnergy, bucketLevel);
            DrawColumn(canvas, column, i, columnWidth, height, fontSize, bucketLevel, energy, trebleEnergy, _beatFlash);
        }

        DrawScanlines(canvas, width, height, energy);
        DrawTitle(canvas, width, height, energy, bassEnergy, trebleEnergy);
        DrawVignette(canvas, width, height);
    }

    private void EnsureColumns(int columnCount, double height, double fontSize)
    {
        while (_columns.Count < columnCount)
        {
            RainColumn column = new();
            ResetColumn(column, height, fontSize, randomizePosition: true);
            _columns.Add(column);
        }

        if (_columns.Count > columnCount)
            _columns.RemoveRange(columnCount, _columns.Count - columnCount);
    }

    private void ResetColumn(RainColumn column, double height, double fontSize, bool randomizePosition)
    {
        column.HeadY = randomizePosition
            ? _random.NextDouble() * height
            : -fontSize * (4 + _random.Next(18));

        column.Speed = 0.65 + _random.NextDouble() * 1.45;
        column.Length = _random.Next(8, 26);
        column.GlyphOffset = _random.Next(Glyphs.Length);
        column.GlyphDrift = 0.03 + _random.NextDouble() * 0.12;
        column.Phase = _random.NextDouble() * Math.PI * 2.0;
        column.BrightnessBias = 0.65 + _random.NextDouble() * 0.55;
    }

    private void UpdateColumn(RainColumn column, double height, double fontSize, double bassEnergy, double bucketLevel)
    {
        double fallSpeed = fontSize * (0.34 + bassEnergy * 1.05 + bucketLevel * 0.52) * column.Speed;

        column.HeadY += fallSpeed;
        column.Phase += column.GlyphDrift + bucketLevel * 0.05;

        if (column.HeadY - column.Length * fontSize > height + fontSize * 4)
            ResetColumn(column, height, fontSize, randomizePosition: false);
    }

    private void DrawColumn(
        Canvas canvas,
        RainColumn column,
        int columnIndex,
        double columnWidth,
        double height,
        double fontSize,
        double bucketLevel,
        double energy,
        double trebleEnergy,
        double beatFlash)
    {
        double x = columnIndex * columnWidth + columnWidth * 0.5;
        double glyphStep = fontSize * 1.08;

        for (int row = 0; row < column.Length; row++)
        {
            double y = column.HeadY - row * glyphStep;

            if (y < -fontSize * 2 || y > height + fontSize * 2)
                continue;

            double tailRatio = 1.0 - row / (double)Math.Max(1, column.Length - 1);
            double pulse = 0.72 + Math.Sin(_phase * 8.0 + column.Phase + row * 0.73) * 0.28;
            double intensity = Math.Clamp((tailRatio * 0.85 + bucketLevel * 0.75 + trebleEnergy * 0.35) * column.BrightnessBias * pulse, 0.0, 1.0);

            bool isHead = row == 0;
            bool isHot = isHead || beatFlash > 0.68 && row < 3;

            char glyph = GetGlyph(column.GlyphOffset + row + (int)(_phase * 16.0 + column.Phase * 7.0));

            Color color = isHot
                ? Color.FromRgb(
                    225,
                    255,
                    (byte)Math.Clamp(225 + trebleEnergy * 30, 225, 255))
                : Color.FromRgb(
                    (byte)Math.Clamp(12 + intensity * 70 + beatFlash * 70, 12, 152),
                    (byte)Math.Clamp(85 + intensity * 170 + beatFlash * 70, 85, 255),
                    (byte)Math.Clamp(32 + intensity * 86 + trebleEnergy * 72, 32, 190));

            byte alpha = (byte)Math.Clamp(28 + intensity * 210 + (isHead ? 35 : 0) + beatFlash * 58, 24, 255);

            TextBlock text = new TextBlock
            {
                Text = glyph.ToString(),
                FontFamily = new FontFamily("Consolas"),
                FontSize = fontSize,
                FontWeight = isHead ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)),
                Opacity = Math.Clamp(0.22 + intensity * 0.78 + beatFlash * 0.18, 0.0, 1.0)
            };

            if (isHead || intensity > 0.72 || beatFlash > 0.35)
            {
                text.Effect = new DropShadowEffect
                {
                    Color = color,
                    BlurRadius = isHead ? 14 + beatFlash * 10 : 6 + intensity * 8,
                    ShadowDepth = 0,
                    Opacity = isHead ? 0.95 : 0.55
                };
            }

            Canvas.SetLeft(text, x);
            Canvas.SetTop(text, y);
            canvas.Children.Add(text);
        }
    }

    private static void DrawBackground(Canvas canvas, double width, double height, double energy, double bassEnergy, double beatFlash)
    {
        Rectangle background = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0, 0, 0), 0.0),
                    new GradientStop(Color.FromRgb(
                        0,
                        (byte)Math.Clamp(8 + bassEnergy * 26 + beatFlash * 28, 8, 62),
                        (byte)Math.Clamp(4 + energy * 18, 4, 32)), 0.5),
                    new GradientStop(Color.FromRgb(0, 0, 0), 1.0)
                }
            }
        };

        canvas.Children.Add(background);

        if (beatFlash > 0.03)
        {
            Rectangle flash = new Rectangle
            {
                Width = width,
                Height = height,
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)Math.Clamp(beatFlash * 82, 0, 82),
                    190,
                    255,
                    210))
            };

            canvas.Children.Add(flash);
        }

        double glowSize = Math.Max(width, height) * (0.72 + energy * 0.2);

        Ellipse glow = new Ellipse
        {
            Width = glowSize,
            Height = glowSize * 0.72,
            Fill = new RadialGradientBrush(
                Color.FromArgb((byte)Math.Clamp(28 + energy * 65 + beatFlash * 70, 28, 150), 0, 255, 105),
                Color.FromArgb(0, 0, 60, 20)),
            Opacity = 0.8,
            Effect = new BlurEffect { Radius = 18 }
        };

        Canvas.SetLeft(glow, width * 0.5 - glowSize * 0.5);
        Canvas.SetTop(glow, height * 0.55 - glowSize * 0.36);
        canvas.Children.Add(glow);
    }

    private static void DrawScanlines(Canvas canvas, double width, double height, double energy)
    {
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
                    Brush = new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(20 + energy * 36, 20, 56), 120, 255, 160)),
                    Geometry = new RectangleGeometry(new Rect(0, 0, 1, 1))
                }
            },
            Opacity = 0.2
        };

        canvas.Children.Add(scanlineOverlay);
    }

    private static void DrawTitle(Canvas canvas, double width, double height, double energy, double bassEnergy, double trebleEnergy)
    {
        TextBlock title = new TextBlock
        {
            Text = $"NEON RAIN  BASS {bassEnergy:0.00}  TREBLE {trebleEnergy:0.00}",
            Width = width,
            TextAlignment = TextAlignment.Center,
            FontFamily = new FontFamily("Consolas"),
            FontSize = Math.Clamp(height * 0.052, 9, 14),
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Clamp(95 + energy * 135, 95, 230),
                145,
                255,
                175)),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 255, 90),
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.68
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
                Color.FromArgb(220, 0, 0, 0))
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.48),
                RadiusX = 0.78,
                RadiusY = 0.95
            }
        };

        canvas.Children.Add(vignette);
    }

    private static char GetGlyph(int index)
    {
        index %= Glyphs.Length;

        if (index < 0)
            index += Glyphs.Length;

        return Glyphs[index];
    }

    private sealed class RainColumn
    {
        public double HeadY { get; set; }

        public double Speed { get; set; }

        public int Length { get; set; }

        public int GlyphOffset { get; set; }

        public double GlyphDrift { get; set; }

        public double Phase { get; set; }

        public double BrightnessBias { get; set; }
    }
}