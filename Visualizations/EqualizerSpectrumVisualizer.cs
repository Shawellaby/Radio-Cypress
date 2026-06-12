using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NAudio.Dsp;

namespace Shawellaby.RadioCypress.Visualizations;

public sealed class EqualizerSpectrumVisualizer : IVisualizer
{
    public void Draw(Canvas canvas, SpectrumVisualizationContext context)
    {
        canvas.Children.Clear();

        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        double[] bucketFrequencies = context.BucketFrequencies;

        const int segmentCount = 60;
        const double columnGap = 5.0;
        const double labelHeight = 16.0;
        const double segmentGapRatio = 1.0 / 3.0;

        int barCount = bucketFrequencies.Length;
        double totalGapWidth = columnGap * (barCount - 1);
        double columnWidth = Math.Max(1.0, (width - totalGapWidth) / barCount);
        double stepX = columnWidth + columnGap;

        float[] samples = new float[context.FftSize];

        lock (context.FftLock)
        {
            int srcIndex = context.GetFftBufferIndex();

            for (int i = 0; i < context.FftSize; i++)
            {
                samples[i] = context.FftBuffer[srcIndex];
                srcIndex = (srcIndex + 1) % context.FftSize;
            }
        }

        for (int i = 0; i < context.FftSize; i++)
        {
            context.FftComplex[i].X = (float)(samples[i] * FastFourierTransform.HammingWindow(i, context.FftSize));
            context.FftComplex[i].Y = 0;
        }

        FastFourierTransform.FFT(true, (int)Math.Log(context.FftSize, 2), context.FftComplex);

        double sampleRate = context.GetSampleRate();
        double nyquist = sampleRate / 2.0;

        for (int bar = 0; bar < barCount; bar++)
        {
            double centerFrequency = bucketFrequencies[bar];
            double lowFrequency = bar == 0 ? 0 : Math.Sqrt(bucketFrequencies[bar - 1] * centerFrequency);
            double highFrequency = bar == barCount - 1 ? nyquist : Math.Sqrt(centerFrequency * bucketFrequencies[bar + 1]);

            int startBin = (int)Math.Floor(lowFrequency / nyquist * (context.FftSize / 2));
            int endBin = (int)Math.Ceiling(highFrequency / nyquist * (context.FftSize / 2));

            startBin = Math.Clamp(startBin, 0, context.FftSize / 2);
            endBin = Math.Clamp(Math.Max(endBin, startBin + 1), 1, context.FftSize / 2);

            double magnitude = 0;

            for (int bin = startBin; bin < endBin; bin++)
            {
                double re = context.FftComplex[bin].X;
                double im = context.FftComplex[bin].Y;
                magnitude += Math.Sqrt(re * re + im * im);
            }

            magnitude /= Math.Max(1, endBin - startBin);

            double compressed = Math.Log10(1 + magnitude * context.GetSmoothedGain());
            context.VisualMax = Math.Max(context.VisualMax * 0.99, compressed);

            double normalized = compressed / Math.Max(context.VisualMax, 0.0001);
            normalized = Math.Min(normalized, 1.0);

            int activeSegments = (int)Math.Round(normalized * segmentCount);
            activeSegments = Math.Clamp(activeSegments, 0, segmentCount);

            double x = bar * stepX;
            double segmentSpace = (height - labelHeight) / segmentCount;
            double segmentHeight = segmentSpace * (1.0 - segmentGapRatio);

            for (int segment = 0; segment < segmentCount; segment++)
            {
                Brush inactiveFill = segment switch
                {
                    < 8 => new SolidColorBrush(Color.FromRgb(13, 44, 7)),
                    < 40 => new SolidColorBrush(Color.FromRgb(52, 42, 0)),
                    < 52 => new SolidColorBrush(Color.FromRgb(50, 29, 0)),
                    _ => new SolidColorBrush(Color.FromRgb(48, 0, 0))
                };

                Brush activeFill = segment switch
                {
                    < 8 => new SolidColorBrush(Color.FromRgb(57, 198, 32 )),
                    < 40 => new SolidColorBrush(Color.FromRgb(238, 194, 1)),
                    < 52 => new SolidColorBrush(Color.FromRgb(229, 130, 0)),
                    _ => new SolidColorBrush(Color.FromRgb(212, 0, 0))
                };

                bool isActive = segment < activeSegments;
                Brush fill = isActive ? activeFill : inactiveFill;

                double y = (height - labelHeight) - ((segment + 1) * segmentSpace) + (segmentSpace - segmentHeight) * 0.5;

                Rectangle rect = new Rectangle
                {
                    Width = columnWidth,
                    Height = segmentHeight,
                    Fill = fill,
                    RadiusX = 1,
                    RadiusY = 1
                };

                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                canvas.Children.Add(rect);
            }

            TextBlock label = new TextBlock
            {
                Text = SpectrumVisualizationContext.FormatFrequencyLabel(centerFrequency),
                Foreground = new SolidColorBrush(Color.FromArgb(50, 30, 255, 215)),
                FontSize = 9,
                Width = columnWidth + 8,
                TextAlignment = System.Windows.TextAlignment.Center
            };

            Canvas.SetLeft(label, x - 4);
            Canvas.SetTop(label, height - 14);
            canvas.Children.Add(label);
        }
    }
}