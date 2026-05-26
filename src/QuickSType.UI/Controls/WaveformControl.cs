using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace QuickSType.UI.Controls;

public sealed class WaveformControl : Control
{
    private const int BarCount = 20;
    private const float Decay = 0.7f;
    private const float Attack = 0.3f;
    private const float MinBarHeightFraction = 0.04f; // 2px minimum at 50px height

    private readonly float[] _bars = new float[BarCount];
    private readonly DispatcherTimer _renderTimer;
    private float _incomingLevel;
    private readonly object _levelLock = new();

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<WaveformControl, bool>(nameof(IsActive));

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    // Pre-allocated brushes — never new on render tick
    private SolidColorBrush _fillBrush = new(Colors.Gray);

    public static readonly StyledProperty<Color> BarColorProperty =
        AvaloniaProperty.Register<WaveformControl, Color>(nameof(BarColor), Colors.Gray);

    public Color BarColor
    {
        get => GetValue(BarColorProperty);
        set => SetValue(BarColorProperty, value);
    }

    static WaveformControl()
    {
        BarColorProperty.Changed.AddClassHandler<WaveformControl>((c, _) => c.UpdateBrush());
        AffectsRender<WaveformControl>(BarColorProperty);
    }

    public WaveformControl()
    {
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _renderTimer.Tick += OnRenderTick;
        _renderTimer.Start();
    }

    /// <summary>
    /// Push the current audio RMS level (0.0-1.0). Thread-safe; called from audio callback thread.
    /// Applies a perceptual remap: raw RMS for typical speech is ~0.02-0.10, which would map to
    /// invisible 1-5px bars at 50px height. sqrt-gain expands that range so speech visibly drives
    /// the meter while silence still reads as flat.
    /// </summary>
    public void PushLevel(float rmsLevel)
    {
        var clamped = Math.Clamp(rmsLevel, 0f, 1f);
        var display = MathF.Min(1f, MathF.Sqrt(clamped) * VisualGain);
        lock (_levelLock)
            _incomingLevel = display;
    }

    private const float VisualGain = 2.5f;

    private void UpdateBrush()
    {
        _fillBrush = new SolidColorBrush(BarColor);
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        float incoming;
        lock (_levelLock) incoming = _incomingLevel;

        // Smooth all bars with exponential decay
        for (int i = 0; i < BarCount - 1; i++)
            _bars[i] = _bars[i] * Decay + _bars[i + 1] * Attack;
        // Latest bar tracks the incoming level
        _bars[BarCount - 1] = _bars[BarCount - 1] * Decay + incoming * Attack;

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var barSlotWidth = bounds.Width / BarCount;
        var minH = Math.Max(2.0, bounds.Height * MinBarHeightFraction);
        var brush = _fillBrush;

        for (int i = 0; i < BarCount; i++)
        {
            var barH = Math.Max(minH, _bars[i] * bounds.Height);
            var x = i * barSlotWidth + 1; // 1px gutter
            var w = barSlotWidth - 2;     // 1px gutter each side
            var y = bounds.Height - barH;
            context.FillRectangle(brush, new Rect(x, y, w, barH));
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _renderTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }
}
