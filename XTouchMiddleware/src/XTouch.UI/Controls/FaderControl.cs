using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace XTouch.UI.Controls;

/// <summary>
/// Vertical fader styled after the X-Touch's 100 mm motor faders. <see cref="Value"/> is 0..1 (two-way);
/// <see cref="IsDragging"/> is true while the user holds the cap — like the hardware touch sensor,
/// incoming updates should not move it then.
/// </summary>
public sealed class FaderControl : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<FaderControl, double>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsDraggingProperty =
        AvaloniaProperty.Register<FaderControl, bool>(nameof(IsDragging), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsTouchedProperty =
        AvaloniaProperty.Register<FaderControl, bool>(nameof(IsTouched));

    private const double CapHeight = 26;
    private static readonly IBrush Track = new SolidColorBrush(Color.Parse("#0E0F12"));
    private static readonly IBrush Cap = new SolidColorBrush(Color.Parse("#C9CCD3"));
    private static readonly IBrush CapActive = new SolidColorBrush(Color.Parse("#F2B33D"));
    private static readonly IPen Tick = new Pen(new SolidColorBrush(Color.Parse("#4A4F5A")), 1);
    private static readonly IPen CapLine = new Pen(Brushes.Black, 2);
    private double _grabOffset;

    static FaderControl()
    {
        AffectsRender<FaderControl>(ValueProperty, IsDraggingProperty, IsTouchedProperty);
        FocusableProperty.OverrideDefaultValue<FaderControl>(true);
    }

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool IsDragging { get => GetValue(IsDraggingProperty); set => SetValue(IsDraggingProperty, value); }
    public bool IsTouched { get => GetValue(IsTouchedProperty); set => SetValue(IsTouchedProperty, value); }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 40 : Math.Min(availableSize.Width, 40),
            double.IsInfinity(availableSize.Height) ? 220 : availableSize.Height);

    private double TravelTop => CapHeight / 2;
    private double TravelBottom => Bounds.Height - CapHeight / 2;
    private double CapY(double v) => TravelBottom - Math.Clamp(v, 0, 1) * (TravelBottom - TravelTop);

    public override void Render(DrawingContext ctx)
    {
        double cx = Bounds.Width / 2;
        ctx.DrawRectangle(Track, null, new Rect(cx - 3, TravelTop, 6, TravelBottom - TravelTop), 3, 3);
        for (int i = 0; i <= 10; i++)
        {
            double y = TravelTop + i * (TravelBottom - TravelTop) / 10;
            ctx.DrawLine(Tick, new Point(cx - 14, y), new Point(cx - 7, y));
            ctx.DrawLine(Tick, new Point(cx + 7, y), new Point(cx + 14, y));
        }
        double capY = CapY(Value);
        var cap = new Rect(cx - 15, capY - CapHeight / 2, 30, CapHeight);
        ctx.DrawRectangle(IsDragging || IsTouched ? CapActive : Cap, null, cap, 3, 3);
        ctx.DrawLine(CapLine, new Point(cap.Left + 3, capY), new Point(cap.Right - 3, capY));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var y = e.GetPosition(this).Y;
        double capY = CapY(Value);
        _grabOffset = Math.Abs(y - capY) <= CapHeight / 2 ? y - capY : 0; // grab the cap where clicked, or jump to the click
        e.Pointer.Capture(this);
        IsDragging = true;
        SetFromPointer(y);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (IsDragging) SetFromPointer(e.GetPosition(this).Y);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!IsDragging) return;
        e.Pointer.Capture(null);
        IsDragging = false;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        IsDragging = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        IsDragging = true;
        Value = Math.Clamp(Value + Math.Sign(e.Delta.Y) * 0.01, 0, 1);
        IsDragging = false;
        e.Handled = true;
    }

    private void SetFromPointer(double y)
    {
        double travel = TravelBottom - TravelTop;
        if (travel <= 0) return;
        Value = Math.Clamp((TravelBottom - (y - _grabOffset)) / travel, 0, 1);
    }
}
