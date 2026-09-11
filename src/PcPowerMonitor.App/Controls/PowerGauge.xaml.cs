using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace PcPowerMonitor.App.Controls;

/// <summary>
/// Minimal power gauge: a big number plus a proportional bar. The displayed value
/// eases to a new reading over 300ms, but only when the change is worth animating
/// (&gt; 5W) — small jitter snaps instantly to keep the UI idle-cheap.
/// </summary>
public partial class PowerGauge : UserControl
{
    private const double AnimateThresholdW = 5d;

    public static readonly DependencyProperty WattProperty = DependencyProperty.Register(
        nameof(Watt), typeof(double), typeof(PowerGauge),
        new PropertyMetadata(0d, OnWattChanged));

    public static readonly DependencyProperty DisplayWattProperty = DependencyProperty.Register(
        nameof(DisplayWatt), typeof(double), typeof(PowerGauge), new PropertyMetadata(0d));

    public static readonly DependencyProperty MaxWattProperty = DependencyProperty.Register(
        nameof(MaxWatt), typeof(double), typeof(PowerGauge), new PropertyMetadata(600d));

    public PowerGauge() => InitializeComponent();

    /// <summary>Live reading pushed by the view-model.</summary>
    public double Watt
    {
        get => (double)GetValue(WattProperty);
        set => SetValue(WattProperty, value);
    }

    /// <summary>Eased value actually shown (bound by the template).</summary>
    public double DisplayWatt
    {
        get => (double)GetValue(DisplayWattProperty);
        set => SetValue(DisplayWattProperty, value);
    }

    /// <summary>Full-scale value for the bar.</summary>
    public double MaxWatt
    {
        get => (double)GetValue(MaxWattProperty);
        set => SetValue(MaxWattProperty, value);
    }

    private static void OnWattChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var gauge = (PowerGauge)d;
        var target = (double)e.NewValue;
        var current = gauge.DisplayWatt;

        if (Math.Abs(target - current) <= AnimateThresholdW)
        {
            gauge.BeginAnimation(DisplayWattProperty, null);
            gauge.DisplayWatt = target;
            return;
        }

        var animation = new DoubleAnimation(current, target, new Duration(TimeSpan.FromMilliseconds(300)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        gauge.BeginAnimation(DisplayWattProperty, animation);
    }
}
