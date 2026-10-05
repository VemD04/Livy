using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Livy.Views;

/// <summary>
/// A smooth, high-performance rotating spinner control styled after modern Fluent design.
/// Automatically starts spinning when visible and pauses when collapsed or unloaded.
/// </summary>
public partial class LoadingSpinner : UserControl
{
    private static readonly DoubleAnimation SpinAnimation;

    static LoadingSpinner()
    {
        SpinAnimation = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = new Duration(TimeSpan.FromSeconds(0.85)),
            RepeatBehavior = RepeatBehavior.Forever
        };
        SpinAnimation.Freeze();
    }

    public static readonly DependencyProperty DiameterProperty =
        DependencyProperty.Register(
            nameof(Diameter),
            typeof(double),
            typeof(LoadingSpinner),
            new FrameworkPropertyMetadata(20.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(double),
            typeof(LoadingSpinner),
            new FrameworkPropertyMetadata(2.5, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ColorProperty =
        DependencyProperty.Register(
            nameof(Color),
            typeof(Brush),
            typeof(LoadingSpinner),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Diameter
    {
        get => (double)GetValue(DiameterProperty);
        set => SetValue(DiameterProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public Brush? Color
    {
        get => (Brush?)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public LoadingSpinner()
    {
        InitializeComponent();
    }

    private void Root_Loaded(object sender, RoutedEventArgs e) => UpdateAnimation();

    private void Root_Unloaded(object sender, RoutedEventArgs e) => StopAnimation();

    private void Root_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateAnimation();

    private void UpdateAnimation()
    {
        if (IsVisible && IsLoaded)
        {
            SpinnerTransform?.BeginAnimation(RotateTransform.AngleProperty, SpinAnimation);
        }
        else
        {
            StopAnimation();
        }
    }

    private void StopAnimation()
    {
        SpinnerTransform?.BeginAnimation(RotateTransform.AngleProperty, null);
    }
}
