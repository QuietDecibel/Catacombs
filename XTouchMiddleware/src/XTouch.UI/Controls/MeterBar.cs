using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace XTouch.UI.Controls;

/// <summary>12-segment LED meter (MCU levels 0..12).</summary>
public sealed class MeterBar : Control
{
    public static readonly StyledProperty<int> LevelProperty = AvaloniaProperty.Register<MeterBar, int>(nameof(Level));

    private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#36D16B"));
    private static readonly IBrush Yellow = new SolidColorBrush(Color.Parse("#F2D33D"));
    private static readonly IBrush Red = new SolidColorBrush(Color.Parse("#FF4D4D"));
    private static readonly IBrush Off = new SolidColorBrush(Color.Parse("#1D2026"));

    static MeterBar() => AffectsRender<MeterBar>(LevelProperty);

    public int Level { get => GetValue(LevelProperty); set => SetValue(LevelProperty, value); }

    protected override Size MeasureOverride(Size availableSize) =>
        new(8, double.IsInfinity(availableSize.Height) ? 220 : availableSize.Height);

    public override void Render(DrawingContext ctx)
    {
        const int segments = 12;
        double gap = 2, h = (Bounds.Height - gap * (segments - 1)) / segments;
        if (h <= 0) return;
        for (int i = 0; i < segments; i++)
        {
            int segment = i + 1;
            double y = Bounds.Height - segment * h - i * gap;
            IBrush lit = segment >= 12 ? Red : segment >= 10 ? Yellow : Green;
            ctx.DrawRectangle(segment <= Level ? lit : Off, null, new Rect(0, y, Bounds.Width, h), 1, 1);
        }
    }
}
