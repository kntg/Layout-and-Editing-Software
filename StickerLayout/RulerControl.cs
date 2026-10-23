using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace StickerLayout;

public sealed class RulerControl : FrameworkElement
{
    public bool IsHorizontal { get; set; } = true;
    public double Scale { get; set; } = 1;
    public double Origin { get; set; }
    public Brush BackgroundBrush { get; set; } = Brushes.Transparent;
    public Brush TickBrush { get; set; } = Brushes.Gray;
    public Brush LabelBrush { get; set; } = Brushes.White;

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(BackgroundBrush, null, new Rect(RenderSize));
        if (Scale <= 0 || double.IsNaN(Scale) || double.IsInfinity(Scale)) return;

        double length = IsHorizontal ? ActualWidth : ActualHeight;
        double thickness = IsHorizontal ? ActualHeight : ActualWidth;
        if (length <= 0 || thickness <= 0) return;

        double major = MajorStep(52 / Scale);
        double minor = major / 10;
        double first = Math.Ceiling(-Origin / (minor * Scale)) * minor;
        var pen = new Pen(TickBrush, 1);
        var typeface = new Typeface("Segoe UI");
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        for (double value = first; ; value += minor)
        {
            double position = Origin + value * Scale;
            if (position > length) break;
            if (position < 0) continue;

            double ratio = value / major;
            bool isMajor = Math.Abs(ratio - Math.Round(ratio)) < 0.00001;
            bool isMiddle = !isMajor && Math.Abs(value / (major / 5) - Math.Round(value / (major / 5))) < 0.00001;
            double tickLength = isMajor ? 9 : isMiddle ? 6 : 3;
            if (IsHorizontal)
                drawingContext.DrawLine(pen, new Point(position, thickness), new Point(position, thickness - tickLength));
            else
                drawingContext.DrawLine(pen, new Point(thickness, position), new Point(thickness - tickLength, position));

            if (!isMajor) continue;
            string text = value.ToString("0.#", CultureInfo.InvariantCulture);
            var label = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 9, LabelBrush, pixelsPerDip);
            if (IsHorizontal)
                drawingContext.DrawText(label, new Point(position - label.Width / 2, 2));
            else
            {
                drawingContext.PushTransform(new RotateTransform(-90, thickness / 2, position));
                drawingContext.DrawText(label, new Point(thickness / 2 - label.Width / 2, position - label.Height / 2));
                drawingContext.Pop();
            }
        }
    }

    static double MajorStep(double targetMm)
    {
        double power = Math.Pow(10, Math.Floor(Math.Log10(targetMm)));
        double factor = targetMm / power;
        return (factor <= 1 ? 1 : factor <= 2 ? 2 : factor <= 5 ? 5 : 10) * power;
    }
}