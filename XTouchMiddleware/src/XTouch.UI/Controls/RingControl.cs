using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace XTouch.UI.Controls;

/// <summary>
/// Encoder with an 11-LED ring, mirroring the X-Touch V-Pot. Drag vertically or use the mouse wheel to turn
/// (<see cref="TurnCommand"/> receives the delta in ticks); click to push (<see cref="PushCommand"/>).
/// </summary>
public sealed class RingControl : Control
{
    public static readonly StyledProperty<int> PositionProperty = AvaloniaProperty.Register<RingControl, int>(nameof(Position));
    public static readonly StyledProperty<string> ModeProperty = AvaloniaProperty.Register<RingControl, string>(nameof(Mode), "Dot");
    public static readonly StyledProperty<bool> CenterProperty = AvaloniaProperty.Register<RingControl, bool>(nameof(Center));
    public static readonly StyledProperty<ICommand?> TurnCommandProperty = AvaloniaProperty.Register<RingControl, ICommand?>(nameof(TurnCommand));
    public static readonly StyledProperty<ICommand?> PushCommandProperty = AvaloniaProperty.Register<RingControl, ICommand?>(nameof(PushCommand));

    private static readonly IBrush LedOn = new SolidColorBrush(Color.Parse("#FF8A1F"));
    private static readonly IBrush LedOff = new SolidColorBrush(Color.Parse("#3A2A1C"));
    private static readonly IBrush Knob = new SolidColorBrush(Color.Parse("#2B2E35"));
    private static readonly IPen KnobEdge = new Pen(new SolidColorBrush(Color.Parse("#5B606B")), 1.5);
    private const double PixelsPerTick = 6;
    private Point? _pressAt;
    private double _accum;
    private bool _dragged;

    static RingControl()
    {
        AffectsRender<RingControl>(PositionProperty, ModeProperty, CenterProperty);
    }

    public int Position { get => GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public string Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public bool Center { get => GetValue(CenterProperty); set => SetValue(CenterProperty, value); }
    public ICommand? TurnCommand { get => GetValue(TurnCommandProperty); set => SetValue(TurnCommandProperty, value); }
    public ICommand? PushCommand { get => GetValue(PushCommandProperty); set => SetValue(PushCommandProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(56, 56);

    /// <summary>Which of the 11 ring LEDs (1-based) are lit for a given mode and position.</summary>
    public static bool IsLit(int led, string mode, int position) => position > 0 && mode switch
    {
        "Wrap" => led <= position,
        "BoostCut" => position >= 6 ? led >= 6 && led <= position : led >= position && led <= 6,
        "Spread" => Math.Abs(led - 6) < position,
        _ => led == position,
    };

    public override void Render(DrawingContext ctx)
    {
        var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
        double r = Math.Min(Bounds.Width, Bounds.Height) / 2 - 4;
        for (int led = 1; led <= 11; led++)
        {
            double angle = (-225 + (led - 1) * 27) * Math.PI / 180; // 270° sweep, 11 LEDs
            var p = new Point(c.X + r * Math.Cos(angle), c.Y + r * Math.Sin(angle));
            ctx.DrawEllipse(IsLit(led, Mode, Position) ? LedOn : LedOff, null, p, 2.6, 2.6);
        }
        ctx.DrawEllipse(Knob, KnobEdge, c, r - 8, r - 8);
        ctx.DrawEllipse(Center ? LedOn : LedOff, null, new Point(c.X, c.Y + r + 1), 2, 2);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pressAt = e.GetPosition(this);
        _accum = 0;
        _dragged = false;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pressAt is not { } start) return;
        var pos = e.GetPosition(this);
        double dy = start.Y - pos.Y;
        _accum += dy;
        _pressAt = pos;
        int ticks = (int)(_accum / PixelsPerTick);
        if (ticks != 0)
        {
            _dragged = true;
            _accum -= ticks * PixelsPerTick;
            Turn(ticks);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressAt is null) return;
        _pressAt = null;
        e.Pointer.Capture(null);
        if (!_dragged && PushCommand?.CanExecute(null) == true) PushCommand.Execute(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Turn(Math.Sign(e.Delta.Y) * 2);
        e.Handled = true;
    }

    private void Turn(int ticks)
    {
        if (TurnCommand?.CanExecute(ticks) == true) TurnCommand.Execute(ticks);
    }
}
