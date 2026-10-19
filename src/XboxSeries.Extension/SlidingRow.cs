using System.Windows;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Attached to a row of tiles (an ItemsControl with a ScrollViewer in its template): the row slides sideways to bring
    /// the tile taking focus wholly into view, rather than jumping there.
    /// </summary>
    internal static class SlidingRow
    {
        public static readonly DependencyProperty SlidesProperty = DependencyProperty.RegisterAttached(
            "Slides", typeof(bool), typeof(SlidingRow), new PropertyMetadata(false));

        public static bool GetSlides(DependencyObject element) => (bool)element.GetValue(SlidesProperty);

        public static void SetSlides(DependencyObject element, bool value) => element.SetValue(SlidesProperty, value);
    }
}
