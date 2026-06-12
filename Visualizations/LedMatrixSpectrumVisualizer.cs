using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Shawellaby.RadioCypress.Visualizations;

public sealed class LedMatrixSpectrumVisualizer : IVisualizer
{
    private double _ledPhase;

    public void Draw(Canvas canvas, SpectrumVisualizationContext context)
    {
        canvas.Children.Clear();

        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        double[] bucketLevels = context.GetFrequencyBucketLevels();
        double energy = bucketLevels.DefaultIfEmpty(0).Average();
        double bassEnergy = bucketLevels.Take(10).DefaultIfEmpty(0).Average();
        double trebleEnergy = bucketLevels.Skip(bucketLevels.Length / 2).DefaultIfEmpty(0).Average();

        _ledPhase += 0.42 + energy * 0.55;

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
                    new GradientStop(Color.FromRgb(18, 18, 18), 0.0),
                    new GradientStop(Color.FromRgb(5, 5, 5), 0.55),
                    new GradientStop(Color.FromRgb(15, 4, 4), 1.0)
                }
            }
        };

        canvas.Children.Add(background);

        const double cellWidth = 9.0;
        const double cellHeight = 14.0;
        const double gapX = 3.0;
        const double gapY = 4.0;
        const double ledInsetX = 2.2;
        const double ledInsetY = 2.0;

        int columns = Math.Max(1, (int)Math.Floor((width + gapX) / (cellWidth + gapX)));
        int rows = Math.Max(1, (int)Math.Floor((height + gapY) / (cellHeight + gapY)));

        double matrixWidth = columns * cellWidth + (columns - 1) * gapX;
        double matrixHeight = rows * cellHeight + (rows - 1) * gapY;
        double startX = (width - matrixWidth) * 0.5;
        double startY = (height - matrixHeight) * 0.5;

        Rectangle grille = new Rectangle
        {
            Width = matrixWidth + 12,
            Height = matrixHeight + 12,
            RadiusX = 3,
            RadiusY = 3,
            Fill = new SolidColorBrush(Color.FromRgb(9, 9, 9)),
            Stroke = new SolidColorBrush(Color.FromRgb(28, 28, 28)),
            StrokeThickness = 2
        };

        Canvas.SetLeft(grille, startX - 6);
        Canvas.SetTop(grille, startY - 6);
        canvas.Children.Add(grille);

        for (int row = 0; row < rows; row++)
        {
            double rowRatio = rows <= 1 ? 0 : row / (double)(rows - 1);

            for (int column = 0; column < columns; column++)
            {
                double x = startX + column * (cellWidth + gapX);
                double y = startY + row * (cellHeight + gapY);

                Rectangle slot = new Rectangle
                {
                    Width = cellWidth,
                    Height = cellHeight,
                    RadiusX = 1.6,
                    RadiusY = 1.6,
                    Fill = new SolidColorBrush(Color.FromRgb(20, 20, 20)),
                    Stroke = new SolidColorBrush(Color.FromRgb(42, 42, 42)),
                    StrokeThickness = 0.7
                };

                Canvas.SetLeft(slot, x);
                Canvas.SetTop(slot, y);
                canvas.Children.Add(slot);

                int bucketIndex = Math.Clamp((int)Math.Round(column / (double)Math.Max(1, columns - 1) * (bucketLevels.Length - 1)), 0, bucketLevels.Length - 1);
                double level = bucketLevels[bucketIndex];

                double redWave = Math.Sin((column * 0.42) - _ledPhase + row * 0.82);
                double redSpark = Math.Sin((column * 1.17) - _ledPhase * 1.9 + row * 0.35);
                double yellowWave = Math.Sin((column * 0.48) + _ledPhase * 1.18 - row * 0.76);
                double yellowSpark = Math.Sin((column * 1.35) + _ledPhase * 2.15 + row * 0.28);

                bool redBand = rowRatio < 0.34 || rowRatio > 0.76;
                bool yellowBand = rowRatio >= 0.38 && rowRatio <= 0.68;

                double redScore = redWave * 0.65 + redSpark * 0.35 + level * 1.05 + energy * 0.35;
                double yellowScore = yellowWave * 0.72 + yellowSpark * 0.28 + bassEnergy * 0.85 + trebleEnergy * 0.25;

                bool redOn = redBand && redScore > 0.62;
                bool yellowOn = yellowBand && yellowScore > 0.50;

                if (!redOn && !yellowOn)
                    continue;

                Color coreColor;
                Color glowColor;
                double opacity;

                if (yellowOn && (!redOn || yellowScore > redScore))
                {
                    coreColor = Color.FromRgb(255, 232, 86);
                    glowColor = Color.FromRgb(255, 196, 38);
                    opacity = Math.Clamp(0.58 + yellowScore * 0.22, 0.58, 0.96);
                }
                else
                {
                    coreColor = Color.FromRgb(255, 36, 28);
                    glowColor = Color.FromRgb(255, 0, 0);
                    opacity = Math.Clamp(0.55 + redScore * 0.22, 0.55, 0.98);
                }

                Ellipse glow = new Ellipse
                {
                    Width = cellWidth * 2.15,
                    Height = cellHeight * 1.55,
                    Fill = new RadialGradientBrush(
                        Color.FromArgb(150, glowColor.R, glowColor.G, glowColor.B),
                        Color.FromArgb(0, glowColor.R, glowColor.G, glowColor.B)),
                    Opacity = opacity
                };

                Canvas.SetLeft(glow, x - cellWidth * 0.58);
                Canvas.SetTop(glow, y - cellHeight * 0.28);
                canvas.Children.Add(glow);

                Rectangle led = new Rectangle
                {
                    Width = cellWidth - ledInsetX * 2,
                    Height = cellHeight - ledInsetY * 2,
                    RadiusX = 2.5,
                    RadiusY = 2.5,
                    Fill = new SolidColorBrush(coreColor),
                    Opacity = opacity,
                    Effect = new DropShadowEffect
                    {
                        Color = glowColor,
                        BlurRadius = yellowOn ? 14 : 11,
                        ShadowDepth = 0,
                        Opacity = 0.9
                    }
                };

                Canvas.SetLeft(led, x + ledInsetX);
                Canvas.SetTop(led, y + ledInsetY);
                canvas.Children.Add(led);
            }
        }

        for (int row = 0; row < rows; row++)
        {
            double y = startY + row * (cellHeight + gapY) - gapY * 0.5;

            Line separator = new Line
            {
                X1 = startX - 4,
                Y1 = y,
                X2 = startX + matrixWidth + 4,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb(95, 0, 0, 0)),
                StrokeThickness = 2
            };

            canvas.Children.Add(separator);
        }

        Rectangle vignette = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new RadialGradientBrush(
                Color.FromArgb(0, 0, 0, 0),
                Color.FromArgb(155, 0, 0, 0))
            {
                RadiusX = 0.72,
                RadiusY = 0.84,
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5)
            }
        };

        canvas.Children.Add(vignette);
    }
}