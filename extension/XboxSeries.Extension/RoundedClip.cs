using System.Windows;
using System.Windows.Media;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Clips an element to a rounded rectangle of its own size, kept up to date as it resizes, so a tile that grows
    /// and shrinks keeps the same corners. WPF has no such clip of its own, and an opacity mask doesn't follow resizes.
    /// </summary>
    internal static class RoundedClip
    {
        public static readonly DependencyProperty RadiusProperty = DependencyProperty.RegisterAttached(
            "Radius", typeof(double), typeof(RoundedClip), new PropertyMetadata(0.0, OnRadiusChanged));

        public static double GetRadius(DependencyObject element) => (double)element.GetValue(RadiusProperty);

        public static void SetRadius(DependencyObject element, double value) => element.SetValue(RadiusProperty, value);

        private static void OnRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                element.SizeChanged -= OnSizeChanged;
                element.SizeChanged += OnSizeChanged;
                Clip(element);
            }
        }

        private static void OnSizeChanged(object sender, SizeChangedEventArgs e) => Clip((FrameworkElement)sender);

        private static void Clip(FrameworkElement element)
        {
            var radius = GetRadius(element);
            element.Clip = new RectangleGeometry(new Rect(0, 0, element.ActualWidth, element.ActualHeight), radius, radius);
        }
    }
}
