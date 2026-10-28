using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The pad's buttons as monochrome icons, as the console writes them in its text, "and (Y) to launch": A, B, X and Y
    /// a letter in a ring, the Guide the Xbox sphere, Menu three lines in a ring, View two overlapping squares in a
    /// ring, the bumpers a rounded bar with LB or RB, the sticks a ring in a ring and the D-pad a plus with the way
    /// pressed filled. Each is drawn on a 64 square, its lines 4 wide, scaled to the size asked, so it stays sharp at
    /// any size. Built in code with only the extension's own XboxFont key, so it needs nothing of the theme's.
    /// </summary>
    internal static class ButtonGlyphs
    {
        private const double Design = 64;

        /// <summary>
        /// The Guide's sphere: Kenney's Input Prompts xbox_guide.svg (https://kenney.nl/assets/input-prompts, CC0 1.0,
        /// kenney.nl), on its 64 square, the sphere 48 across.
        /// </summary>
        internal const string GuideSphere =
            "M51.3 45.75 Q51.65 44.05 50.6 40.7 49.55 37.45 46.45 32.2 43.3 26.95 40.05 22.85 L38.7 21.1 40.15 19.75 Q42.85 17.3 44.9 16 45.75 15.5 46.8 15.1 L48.4 14.65 Q48.7 14.65 49.7 15.7 L51.6 18 Q54.5 22.05 55.6 27.3 55.95 28.9 56 31.8 L55.7 36.35 Q55.1 39.45 53.9 42 L52.7 44.3 51.4 46.25 51.15 46.45 51.3 45.8 51.3 45.75 " +
            "M29.95 13.85 Q28.5 13.1 26.45 12.4 24.45 11.65 23.15 11.45 L20.9 11.3 19.95 11.2 Q20.1 11 21.8 10.25 24.4 9 27.4 8.35 L32.05 8 36.65 8.35 Q38.6 8.75 40.65 9.5 42.65 10.25 43.9 11.1 L44.4 11.4 43.3 11.35 Q39.75 11.1 34.05 13.85 L31.95 14.8 29.95 13.85 " +
            "M29.6 55.9 Q23.9 55.35 18.95 52.1 L16.3 50.05 Q15.65 49.3 15.65 48.2 15.65 46.05 17.9 42.4 20.2 38.8 24.25 34.45 26.55 32 29.2 29.65 31.8 27.35 32.1 27.4 32.8 27.6 36.9 31.55 40.95 35.5 43 38 46.25 41.95 47.65 45 49 48.05 48.25 49.5 47.7 50.6 44.8 52.35 41.9 54.1 39.3 54.9 L34.5 55.85 29.6 55.9 " +
            "M12 45.2 Q10.3 42.6 9.4 39.85 8.45 37.15 8.1 33.5 L8 31.2 8.45 27.65 Q8.95 24.55 10.6 21.3 12.2 18 14.35 15.8 15.2 14.85 15.6 14.75 L17.1 15 Q19.55 15.85 23.75 19.6 L25.45 21.15 24.5 22.3 Q21.2 26.35 18.2 31.35 15.1 36.45 13.8 39.95 13.1 41.85 12.85 43.6 12.55 45.35 12.9 46.15 L12.9 46.5 12 45.2 Z";

        /// <summary><paramref name="button"/>'s icon, <paramref name="size"/> square, drawn in <paramref name="brush"/>.</summary>
        internal static FrameworkElement Create(ControllerButton button, double size, Brush brush)
        {
            var square = new Grid { Width = Design, Height = Design };
            switch (button)
            {
                case ControllerButton.A:
                case ControllerButton.B:
                case ControllerButton.X:
                case ControllerButton.Y:
                    square.Children.Add(Ring(brush));
                    square.Children.Add(Letters(button.ToString(), 36, brush, raise: 3));
                    break;
                case ControllerButton.Guide:
                    square.Children.Add(Shape(GuideSphere, brush, filled: true, scale: 1.25));
                    break;
                case ControllerButton.Start:
                    square.Children.Add(Ring(brush));
                    square.Children.Add(Shape("M 20,23 L 44,23 M 20,32 L 44,32 M 20,41 L 44,41", brush));
                    break;
                case ControllerButton.Back:
                    square.Children.Add(Ring(brush));
                    square.Children.Add(Shape("M 27,38 L 19,38 L 19,20 L 37,20 L 37,26 M 27,26 L 45,26 L 45,44 L 27,44 Z", brush));
                    break;
                case ControllerButton.LeftShoulder:
                case ControllerButton.RightShoulder:
                    square.Children.Add(Shape("M 4,50 L 4,30 C 4,20 13,14 22,14 L 42,14 C 51,14 60,20 60,30 L 60,50 Z", brush));
                    square.Children.Add(Letters(button == ControllerButton.LeftShoulder ? "LB" : "RB", 24, brush, raise: 14));
                    break;
                case ControllerButton.LeftStick:
                case ControllerButton.RightStick:
                    square.Children.Add(Ring(brush));
                    square.Children.Add(Shape("M 20,32 A 12,12 0 1 1 44,32 A 12,12 0 1 1 20,32 Z", brush));
                    break;
                default:
                    // The D-pad, its arm for the way pressed filled.
                    square.Children.Add(Shape(
                        "M 25,6 L 39,6 L 39,25 L 58,25 L 58,39 L 39,39 L 39,58 L 25,58 L 25,39 L 6,39 L 6,25 L 25,25 Z", brush));
                    square.Children.Add(Shape(Arm(button), brush, filled: true));
                    break;
            }

            return new Viewbox { Width = size, Height = size, Child = square };
        }

        /// <summary>
        /// <paramref name="button"/>'s icon inline in text of <paramref name="fontSize"/>, a little taller than its
        /// capitals and centred on them, as the console's "(Y)" in its tip.
        /// </summary>
        internal static InlineUIContainer Inline(ControllerButton button, double fontSize, Brush brush)
        {
            var size = Round(fontSize * 0.96);
            var glyph = Create(button, size, brush);

            // Sat on the baseline, then lowered so its middle is at the capitals' middle.
            glyph.RenderTransform = new TranslateTransform(0, Round(size / 2 - fontSize * 0.36));
            return new InlineUIContainer(glyph) { BaselineAlignment = BaselineAlignment.Baseline };
        }

        private static double Round(double value) => System.Math.Round(value);

        private static Path Ring(Brush brush) =>
            Shape("M 2,32 A 30,30 0 1 1 62,32 A 30,30 0 1 1 2,32 Z", brush);

        /// <summary>A line 4 wide, or a filled shape, scaled about the square's middle by <paramref name="scale"/>.</summary>
        private static Path Shape(string data, Brush brush, bool filled = false, double scale = 1)
        {
            var geometry = Geometry.Parse(data).Clone();
            if (scale != 1)
            {
                geometry.Transform = new ScaleTransform(scale, scale, Design / 2, Design / 2);
            }

            return filled
                ? new Path { Data = geometry, Fill = brush }
                : new Path { Data = geometry, Stroke = brush, StrokeThickness = 4, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        }

        /// <summary>Letters in the square's middle, raised by <paramref name="raise"/> to sit centred as capitals do.</summary>
        private static TextBlock Letters(string text, double fontSize, Brush brush, double raise)
        {
            var letters = new TextBlock
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, raise),
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Foreground = brush,
            };
            letters.SetResourceReference(TextBlock.FontFamilyProperty, "XboxFont");
            return letters;
        }

        private static string Arm(ControllerButton button)
        {
            switch (button)
            {
                case ControllerButton.DPadUp: return "M 25,6 L 39,6 L 39,25 L 25,25 Z";
                case ControllerButton.DPadDown: return "M 25,39 L 39,39 L 39,58 L 25,58 Z";
                case ControllerButton.DPadLeft: return "M 6,25 L 25,25 L 25,39 L 6,39 Z";
                default: return "M 39,25 L 58,25 L 58,39 L 39,39 Z";
            }
        }
    }
}
