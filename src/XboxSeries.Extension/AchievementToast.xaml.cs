using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using XboxSeries.Core;
using static XboxSeries.Extension.AchievementPages;

namespace XboxSeries.Extension
{
    /// <summary>
    /// An achievement's Toast: the console's rare achievement pill, blue with its spinning diamond for a rare one and green
    /// with the achievement's icon for any other (see AchievementToast.xaml). The Toast's window plays it in place of its
    /// banner for <see cref="ToastKind.AchievementUnlocked"/> and <see cref="ToastKind.RareAchievementUnlocked"/>:
    /// <see cref="Play"/> as the Toast shows, its timeline the console's own (frame by frame from a capture of its rare
    /// one), paging through the whole description two lines at a time (<see cref="AchievementPages"/>), and
    /// <see cref="Go"/> as it hides. Everything moves by transforms and opacity alone, so the TV's graphics card does the
    /// work, and the diamond's spin is its facets squashed, skewed and moved round as a turning gem's would be, not a picture.
    /// </summary>
    public partial class AchievementToast : UserControl
    {
        // The intro, in seconds from the Toast showing, when the sound starts: the console draws its first frame 0.117 s
        // into its sound.
        private const double PopAt = 0.117, PopEnd = 0.217, LogoIn = 0.167;
        private const double SwapAt = 1.052, SwapEnd = 1.085, InsideFades = 1.25, FlyEnd = 1.485;
        private const double UnfurlAt = 1.485, UnfurlEnd = 2.385;
        private const double TextInAt = 1.786;
        private const double FirstShine = 2.954, SecondShine = 5.222, ShineFadesIn = 0.3, ShineDuration = 1.47;

        // The text's fade in ends at TextInEnd, and its pages change on AchievementPages' times: the console's own for
        // the header and one page of description, PushAt to CutAt, and the prompt, PromptAt to PromptEnd.

        // The going, in seconds from the Toast hiding.
        private const double CollapseEnd = 0.5, TextOutAt = 0.1, TextOut = 0.27, BrightOutAt = 0.4, LogoBackEnd = 0.57;
        private const double SwellAt = 0.633, SwellEnd = 0.813, CloseEnd = 0.947, DotEnd = 1.097, GoneEnd = 1.12;

        /// <summary>How far the circle and the pill's ends start in from their places: to the pill's centre.</summary>
        private const double Centre = (778 - 128) / 2.0;

        /// <summary>How far the band of light moves, at the start and end of its sweep: its middle from 185 to 590 across the pill.</summary>
        private const double ShineFrom = -95, ShineTo = 310;

        /// <summary>How far a page goes up as it leaves, and how far below the next starts.</summary>
        private const double PushUp = -24, PushFrom = 58;

        /// <summary>One facet's turn to the next: the gem's eight-fold symmetry repeats every 0.66 s, as the console's.</summary>
        private const double SpinPeriod = 0.66;

        // The diamond's shape, as CrownFacets and PavilionFacets draw it: the table's and girdle's half-widths, and the
        // crown's and pavilion's heights.
        private const double Table = 23, Girdle = 40, CrownHeight = 26, PavilionHeight = 53;

        private Storyboard intro;
        private Storyboard outro;

        /// <summary>The description's pages, one TextBlock each (DescriptionPage), kept for the next Toast's.</summary>
        private readonly List<TextBlock> descriptionPages = new List<TextBlock>();

        public AchievementToast()
        {
            InitializeComponent();
            BuildDiamond();
        }

        /// <summary>
        /// The Toast showing: from the circle popping in to the prompt, held there until <see cref="Go"/>; the description's
        /// pages only for an achievement with one. It starts afresh, whatever it was doing.
        /// </summary>
        internal void Play()
        {
            Stop();
            var toast = DataContext as ToastState;
            intro = Intro(toast?.Kind == ToastKind.RareAchievementUnlocked, Describe(toast?.Description));
            intro.Begin(this, HandoffBehavior.SnapshotAndReplace, true);
        }

        /// <summary>
        /// The description's pages, its text split two lines to a page as a page wraps it at the text column's width, in
        /// its font, so all of it is seen; the pages it needs shown, with their text, and the rest hidden.
        /// </summary>
        private List<TextBlock> Describe(string description)
        {
            var column = Pill.Width - Pages.Margin.Left - Pages.Margin.Right;
            var typeface = new Typeface(Header.FontFamily, Header.FontStyle, Header.FontWeight, Header.FontStretch);
            var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            FormattedText Laid(string text) => new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, typeface, Header.FontSize, Brushes.White, pixelsPerDip);
            var line = Laid("X").Height;

            // A hair narrower than the column, so a page measured to fit never wraps once more when laid out.
            int LinesOf(string text)
            {
                var laid = Laid(text);
                laid.MaxTextWidth = column - 1;
                return (int)Math.Round(laid.Height / line);
            }

            var texts = AchievementPages.Split(description, LinesOf);
            while (descriptionPages.Count < texts.Count)
            {
                var page = (TextBlock)((DataTemplate)Resources["DescriptionPage"]).LoadContent();
                page.RenderTransform = new TranslateTransform(0, PushFrom);
                Descriptions.Children.Add(page);
                descriptionPages.Add(page);
            }

            for (var index = 0; index < descriptionPages.Count; index++)
            {
                var shown = index < texts.Count;
                descriptionPages[index].Text = shown ? texts[index] : "";
                descriptionPages[index].Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
            }

            return descriptionPages.GetRange(0, texts.Count);
        }

        /// <summary>
        /// The Toast hiding: the pill collapsing into the circle, which turns dark with the logo, rings bright and shrinks
        /// to a dot, from wherever it was; then <paramref name="gone"/>.
        /// </summary>
        internal void Go(Action gone)
        {
            outro?.Stop(this);
            var going = outro = Outro();
            going.Completed += (_, __) =>
            {
                if (outro == going)
                {
                    gone();
                }
            };
            going.Begin(this, HandoffBehavior.SnapshotAndReplace, true);
        }

        /// <summary>Stops it where it is, back to its unseen start, as another kind of Toast takes the window.</summary>
        internal void Stop()
        {
            outro?.Stop(this);
            outro = null;
            intro?.Stop(this);
            intro = null;
        }

        private Storyboard Intro(bool rare, List<TextBlock> descriptions)
        {
            var board = new Storyboard();
            var quintic = new QuinticEase { EasingMode = EasingMode.EaseOut };
            var cubic = new CubicEase { EasingMode = EasingMode.EaseOut };

            // The circle pops in at the pill's centre, dark at first, brightening as it grows; the logo comes in with it.
            Animate(board, Circle, OpacityProperty, Jump(0, 0), Jump(PopAt, 0), To(PopEnd, 1));
            Animate(board, CircleScale, ScaleTransform.ScaleXProperty, Jump(0, 0.4), Jump(PopAt, 0.4), To(PopEnd, 1, cubic));
            Animate(board, CircleScale, ScaleTransform.ScaleYProperty, Jump(0, 0.4), Jump(PopAt, 0.4), To(PopEnd, 1, cubic));
            Animate(board, Dark, OpacityProperty, Jump(0, 1), Jump(PopEnd, 0));
            Animate(board, DarkScale, ScaleTransform.ScaleXProperty, Jump(0, 1));
            Animate(board, DarkScale, ScaleTransform.ScaleYProperty, Jump(0, 1));
            Animate(board, Bright, OpacityProperty, Jump(0, 0), Jump(PopAt, 0), To(PopEnd, 1));
            Animate(board, Ring, OpacityProperty, Jump(0, 0));
            Animate(board, RingScale, ScaleTransform.ScaleXProperty, Jump(0, 1));
            Animate(board, RingScale, ScaleTransform.ScaleYProperty, Jump(0, 1));
            Animate(board, Halo, OpacityProperty, Jump(0, 0));
            Animate(board, HaloScale, ScaleTransform.ScaleXProperty, Jump(0, 1));
            Animate(board, HaloScale, ScaleTransform.ScaleYProperty, Jump(0, 1));
            Animate(board, LogoScale, ScaleTransform.ScaleXProperty, Jump(0, 1));
            Animate(board, LogoScale, ScaleTransform.ScaleYProperty, Jump(0, 1));

            // The logo gives way: for a rare achievement to the inside of the gem, the diamond flying out of it and
            // spinning; for any other to its icon, or the trophy, zooming out from close up into its place, still.
            Animate(board, Logo, OpacityProperty, Jump(0, 0), Jump(LogoIn, 0), To(PopEnd, 1), Jump(SwapAt, 1), To(SwapEnd, 0));
            if (rare)
            {
                Spin(board, SwapAt);
                Animate(board, Icon, OpacityProperty, Jump(0, 0));
                Animate(board, Gem, OpacityProperty, Jump(0, 0), Jump(SwapAt, 0), To(SwapEnd, 1));
                Animate(board, Inside, OpacityProperty, Jump(0, 1), Jump(InsideFades, 1), To(FlyEnd, 0));
                Animate(board, DiamondZoom, ScaleTransform.ScaleXProperty, Jump(0, 3), Jump(SwapAt, 3), To(FlyEnd, 1, quintic));
                Animate(board, DiamondZoom, ScaleTransform.ScaleYProperty, Jump(0, 3), Jump(SwapAt, 3), To(FlyEnd, 1, quintic));
                Animate(board, GemOpening, ScaleTransform.ScaleXProperty, Jump(0, 1), Jump(SwapAt, 1), To(FlyEnd, 1.5, quintic));
                Animate(board, GemOpening, ScaleTransform.ScaleYProperty, Jump(0, 1), Jump(SwapAt, 1), To(FlyEnd, 1.5, quintic));
            }
            else
            {
                Animate(board, Gem, OpacityProperty, Jump(0, 0));
                Animate(board, Icon, OpacityProperty, Jump(0, 0), Jump(SwapAt, 0), To(SwapEnd, 1));
                Animate(board, IconZoom, ScaleTransform.ScaleXProperty, Jump(0, 3), Jump(SwapAt, 3), To(FlyEnd, 1, quintic));
                Animate(board, IconZoom, ScaleTransform.ScaleYProperty, Jump(0, 3), Jump(SwapAt, 3), To(FlyEnd, 1, quintic));
            }

            // The pill unfurls from behind the circle, which rides its left end out.
            Animate(board, Body, OpacityProperty, Jump(0, 0), Jump(UnfurlAt, 1));
            Animate(board, SpanWidth, ScaleTransform.ScaleXProperty, Jump(0, 0), Jump(UnfurlAt, 0), To(UnfurlEnd, 1, quintic));
            Animate(board, LeftEndShift, TranslateTransform.XProperty, Jump(0, Centre), Jump(UnfurlAt, Centre), To(UnfurlEnd, 0, quintic));
            Animate(board, RightEndShift, TranslateTransform.XProperty, Jump(0, -Centre), Jump(UnfurlAt, -Centre), To(UnfurlEnd, 0, quintic));
            Animate(board, CircleShift, TranslateTransform.XProperty, Jump(0, Centre), Jump(UnfurlAt, Centre), To(UnfurlEnd, 0, quintic));

            // The first page fades in as it unfurls, drifting a little left into place.
            Animate(board, Pages, OpacityProperty, Jump(0, 0), Jump(TextInAt, 0), To(TextInEnd, 1));
            Animate(board, PagesDrift, TranslateTransform.XProperty, Jump(0, 4), Jump(TextInAt, 4), To(TextInEnd, 0, cubic));

            // Pushed up for each page of the description in turn, the last cut for the prompt; without one, the first page is.
            var times = AchievementPages.Plan(descriptions.Count);
            var pages = new List<KeyValuePair<UIElement, TranslateTransform>> { new KeyValuePair<UIElement, TranslateTransform>(Page1, Page1Shift) };
            foreach (var description in descriptions)
            {
                pages.Add(new KeyValuePair<UIElement, TranslateTransform>(description, (TranslateTransform)description.RenderTransform));
            }

            for (var index = 0; index < pages.Count; index++)
            {
                var page = pages[index].Key;
                var pageShift = pages[index].Value;
                var opacity = new List<Key>();
                var place = new List<Key>();
                if (index == 0)
                {
                    opacity.Add(Jump(0, 1));
                    place.Add(Jump(0, 0));
                }
                else
                {
                    // In from below as the page before leaves.
                    var comes = times.Pushes[index - 1] + (PushInAt - PushAt);
                    opacity.AddRange(new[] { Jump(0, 0), Jump(comes, 1) });
                    place.AddRange(new[] { Jump(0, PushFrom), Jump(comes, PushFrom), To(comes + PushIn, 0, new CubicEase { EasingMode = EasingMode.EaseOut }) });
                }

                if (index < times.Pushes.Length)
                {
                    // Up and out for the next.
                    var leaves = times.Pushes[index];
                    opacity.Add(Jump(leaves + PushOut, 0));
                    place.AddRange(new[] { Jump(leaves, 0), To(leaves + PushOut, PushUp, new CubicEase { EasingMode = EasingMode.EaseIn }) });
                }
                else
                {
                    opacity.Add(Jump(times.Cut, 0));
                }

                Animate(board, page, OpacityProperty, opacity.ToArray());
                Animate(board, pageShift, TranslateTransform.YProperty, place.ToArray());
            }
            Animate(board, Prompt, OpacityProperty, Jump(0, 0), Jump(times.PromptAt, 0), To(times.PromptEnd, 1));

            // The band of light, once across the first page and once across the description's first, as long after it
            // settles as the console's.
            var shines = new List<double> { FirstShine };
            if (times.Pushes.Length > 0)
            {
                shines.Add(times.Pushes[0] + (SecondShine - PushAt));
            }

            var shine = new List<Key> { Jump(0, 0) };
            var shift = new List<Key> { Jump(0, ShineFrom) };
            foreach (var at in shines)
            {
                shine.AddRange(new[] { Jump(at, 0), To(at + ShineFadesIn, 1), Jump(at + ShineDuration - ShineFadesIn, 1), To(at + ShineDuration, 0) });
                shift.AddRange(new[] { Jump(at, ShineFrom), To(at + ShineDuration, ShineTo) });
            }
            Animate(board, Shine, OpacityProperty, shine.ToArray());
            Animate(board, ShineShift, TranslateTransform.XProperty, shift.ToArray());
            return board;
        }

        private Storyboard Outro()
        {
            var board = new Storyboard();
            var quintic = new QuinticEase { EasingMode = EasingMode.EaseIn };
            var cubicOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var cubicIn = new CubicEase { EasingMode = EasingMode.EaseIn };

            // The pill collapses into the circle at its centre as the text fades; the bright circle and its diamond or
            // icon fade last, leaving the dark one.
            Animate(board, SpanWidth, ScaleTransform.ScaleXProperty, To(CollapseEnd, 0, quintic));
            Animate(board, LeftEndShift, TranslateTransform.XProperty, To(CollapseEnd, Centre, quintic));
            Animate(board, RightEndShift, TranslateTransform.XProperty, To(CollapseEnd, -Centre, quintic));
            Animate(board, CircleShift, TranslateTransform.XProperty, To(CollapseEnd, Centre, quintic));
            Animate(board, Body, OpacityProperty, Jump(CollapseEnd, 0));
            Animate(board, Pages, OpacityProperty, Jump(TextOutAt, Pages.Opacity), To(TextOutAt + TextOut, 0));
            Animate(board, Shine, OpacityProperty, To(TextOutAt, 0));
            Animate(board, Dark, OpacityProperty, Jump(0, 1));
            Animate(board, Bright, OpacityProperty, Jump(BrightOutAt, Bright.Opacity), To(CollapseEnd, 0));
            Animate(board, Gem, OpacityProperty, Jump(BrightOutAt, Gem.Opacity), To(CollapseEnd, 0));
            Animate(board, Icon, OpacityProperty, Jump(BrightOutAt, Icon.Opacity), To(CollapseEnd, 0));

            // The logo on the dark circle, which swells as a bright ring closes in on it.
            Animate(board, Logo, OpacityProperty, Jump(0, 0), Jump(CollapseEnd, 0), To(LogoBackEnd, 1));
            Animate(board, Ring, OpacityProperty, Jump(0, 1));
            Animate(board, CircleScale, ScaleTransform.ScaleXProperty, Jump(SwellAt, 1), To(SwellEnd, 1.11, cubicOut), To(CloseEnd, 1));
            Animate(board, CircleScale, ScaleTransform.ScaleYProperty, Jump(SwellAt, 1), To(SwellEnd, 1.11, cubicOut), To(CloseEnd, 1));
            Animate(board, DarkScale, ScaleTransform.ScaleXProperty, Jump(0, 1), Jump(SwellAt, 1), To(SwellEnd, 0.82, cubicOut), To(CloseEnd, 0, cubicIn));
            Animate(board, DarkScale, ScaleTransform.ScaleYProperty, Jump(0, 1), Jump(SwellAt, 1), To(SwellEnd, 0.82, cubicOut), To(CloseEnd, 0, cubicIn));
            Animate(board, LogoScale, ScaleTransform.ScaleXProperty, Jump(0, 1), Jump(SwellEnd, 1), To(CloseEnd, 0, cubicIn));
            Animate(board, LogoScale, ScaleTransform.ScaleYProperty, Jump(0, 1), Jump(SwellEnd, 1), To(CloseEnd, 0, cubicIn));

            // The dark disc gone, the bright one shrinks to a dot inside a dark halo, and it is gone.
            Animate(board, RingScale, ScaleTransform.ScaleXProperty, Jump(0, 1), Jump(SwellEnd, 1), To(CloseEnd, 0.8), To(DotEnd, 0.08, cubicIn));
            Animate(board, RingScale, ScaleTransform.ScaleYProperty, Jump(0, 1), Jump(SwellEnd, 1), To(CloseEnd, 0.8), To(DotEnd, 0.08, cubicIn));
            Animate(board, Halo, OpacityProperty, Jump(0, 0), Jump(SwellEnd, 0), To(CloseEnd, 0.85), To(DotEnd, 0.6));
            Animate(board, HaloScale, ScaleTransform.ScaleXProperty, Jump(0, 1), Jump(CloseEnd, 1), To(DotEnd, 0.85));
            Animate(board, HaloScale, ScaleTransform.ScaleYProperty, Jump(0, 1), Jump(CloseEnd, 1), To(DotEnd, 0.85));
            Animate(board, Circle, OpacityProperty, Jump(DotEnd, 1), To(GoneEnd, 0));
            return board;
        }

        /// <summary>The five slices of the diamond's crown and pavilion that can face us, each turning to the next's place.</summary>
        private readonly List<Slice> slices = new List<Slice>();

        private sealed class Slice
        {
            public int Index;
            public bool Pavilion;
            public ScaleTransform Squash;
            public SkewTransform Lean;
            public TranslateTransform Place;
            public readonly List<KeyValuePair<UIElement, double>> Glints = new List<KeyValuePair<UIElement, double>>();
            public UIElement Shade;
        }

        /// <summary>The diamond: five slices of crown over five of pavilion, drawn by CrownFacets and PavilionFacets.</summary>
        private void BuildDiamond()
        {
            for (var index = 0; index < 5; index++)
            {
                foreach (var pavilion in new[] { false, true })
                {
                    var facets = (Canvas)((DataTemplate)Resources[pavilion ? "PavilionFacets" : "CrownFacets"]).LoadContent();
                    var slice = new Slice
                    {
                        Index = index,
                        Pavilion = pavilion,
                        Squash = new ScaleTransform(),
                        Lean = new SkewTransform(),
                        Place = new TranslateTransform(0, pavilion ? CrownHeight : 0),
                    };
                    var transform = new TransformGroup();
                    transform.Children.Add(slice.Squash);
                    transform.Children.Add(slice.Lean);
                    transform.Children.Add(slice.Place);
                    facets.RenderTransform = transform;
                    foreach (UIElement facet in facets.Children)
                    {
                        var tag = (facet as Shape)?.Tag as string;
                        if (tag == "Shade")
                        {
                            slice.Shade = facet;
                        }
                        else if (tag != null && tag.StartsWith("Glint "))
                        {
                            slice.Glints.Add(new KeyValuePair<UIElement, double>(facet, double.Parse(tag.Substring(6), System.Globalization.CultureInfo.InvariantCulture)));
                        }
                    }
                    Diamond.Children.Add(facets);
                    slices.Add(slice);
                    Place(slice, 0);
                }
            }
        }

        /// <summary>
        /// The diamond's spin from <paramref name="at"/>, forever: each slice turning 45 degrees, from its place to the
        /// next's, in <see cref="SpinPeriod"/>, so the loop is seamless.
        /// </summary>
        private void Spin(Storyboard board, double at)
        {
            const int steps = 16;
            foreach (var slice in slices)
            {
                var squash = new Key[steps + 1];
                var lean = new Key[steps + 1];
                var place = new Key[steps + 1];
                var shade = new Key[steps + 1];
                var glints = new Key[slice.Glints.Count][];
                for (var g = 0; g < glints.Length; g++)
                {
                    glints[g] = new Key[steps + 1];
                }

                for (var step = 0; step <= steps; step++)
                {
                    var time = SpinPeriod * step / steps;
                    var view = View(slice, (double)step / steps);
                    squash[step] = To(time, view.Squash);
                    lean[step] = To(time, view.Lean);
                    place[step] = To(time, view.Place);
                    shade[step] = To(time, view.Shade);
                    for (var g = 0; g < glints.Length; g++)
                    {
                        glints[g][step] = To(time, Glint(view.Middle, slice.Glints[g].Value));
                    }
                }

                Loop(Animate(board, slice.Squash, ScaleTransform.ScaleXProperty, squash), at);
                Loop(Animate(board, slice.Lean, SkewTransform.AngleXProperty, lean), at);
                Loop(Animate(board, slice.Place, TranslateTransform.XProperty, place), at);
                Loop(Animate(board, slice.Shade, OpacityProperty, shade), at);
                for (var g = 0; g < glints.Length; g++)
                {
                    Loop(Animate(board, slice.Glints[g].Key, OpacityProperty, glints[g]), at);
                }
            }
        }

        private static void Loop(Timeline timeline, double at)
        {
            timeline.BeginTime = TimeSpan.FromSeconds(at);
            timeline.Duration = TimeSpan.FromSeconds(SpinPeriod);
            timeline.RepeatBehavior = RepeatBehavior.Forever;
        }

        /// <summary>The diamond still, as it is at the spin's start, for before the spin begins.</summary>
        private static void Place(Slice slice, double turn)
        {
            var view = View(slice, turn);
            slice.Squash.ScaleX = view.Squash;
            slice.Lean.AngleX = view.Lean;
            slice.Place.X = view.Place;
            slice.Shade.Opacity = view.Shade;
            foreach (var glint in slice.Glints)
            {
                glint.Key.Opacity = Glint(view.Middle, glint.Value);
            }
        }

        private struct SliceView
        {
            public double Middle, Squash, Lean, Place, Shade;
        }

        /// <summary>
        /// Where a slice is seen, <paramref name="turn"/> of the way through a facet's turn: its edges at angles φ1 and
        /// φ2 round the gem are seen at sin φ across it, so the slice is its drawn shape squashed to half their distance,
        /// skewed so its wider girdle sits under its narrower table (or over the pavilion's point), and moved to their
        /// middle. A slice turned away is squashed to nothing.
        /// </summary>
        private static SliceView View(Slice slice, double turn)
        {
            var from = -135 + 45 * slice.Index + 45 * turn;
            var middle = from + 22.5;
            double Sin(double degrees) => Math.Sin(degrees * Math.PI / 180);
            var half = (Sin(from + 45) - Sin(from)) / 2;
            var centre = (Sin(from) + Sin(from + 45)) / 2;
            var facing = Math.Cos(middle * Math.PI / 180) > 0;
            double lean, place;
            if (slice.Pavilion)
            {
                lean = Math.Atan(-Girdle * centre / PavilionHeight);
                place = Girdle * centre;
            }
            else
            {
                lean = Math.Atan((Girdle - Table) * centre / CrownHeight);
                place = Table * centre;
            }

            return new SliceView
            {
                Middle = middle,
                Squash = facing ? half : 0,
                Lean = lean * 180 / Math.PI,
                Place = Girdle + place,
                Shade = 0.55 * (1 - Math.Cos(middle * Math.PI / 180)),
            };
        }

        /// <summary>A facet's glint, flashing as its slice turns past <paramref name="angle"/> from facing us.</summary>
        private static double Glint(double middle, double angle) =>
            0.06 + 0.8 * Math.Pow(Math.Max(0, Math.Cos((middle - angle) * Math.PI / 90)), 10);

        private struct Key
        {
            public double At;
            public double Value;
            public bool Jumps;
            public IEasingFunction Ease;
        }

        /// <summary>At <paramref name="at"/> seconds, <paramref name="value"/> at once.</summary>
        private static Key Jump(double at, double value) => new Key { At = at, Value = value, Jumps = true };

        /// <summary>By <paramref name="at"/> seconds, <paramref name="value"/>, eased from the last key (linear for none).</summary>
        private static Key To(double at, double value, IEasingFunction ease = null) => new Key { At = at, Value = value, Ease = ease };

        /// <summary>
        /// The names the boards find their targets by: a storyboard given a transform itself, rather than its name in the
        /// control's name scope, leaves it where it is.
        /// </summary>
        private readonly Dictionary<DependencyObject, string> targets = new Dictionary<DependencyObject, string>();

        private string NameOf(DependencyObject target)
        {
            if (!targets.TryGetValue(target, out var name))
            {
                name = "Animated" + targets.Count;
                RegisterName(name, target);
                targets.Add(target, name);
            }

            return name;
        }

        /// <summary>An animation of <paramref name="property"/> on <paramref name="target"/> through <paramref name="keys"/>, on the board.</summary>
        private Timeline Animate(Storyboard board, DependencyObject target, DependencyProperty property, params Key[] keys)
        {
            // From where it is now, held there until its first key: a board taking over from another drops the other's
            // value at once, so the value it was at, not the one in the XAML, has to be its own.
            var animation = new DoubleAnimationUsingKeyFrames();
            if (keys[0].At > 0)
            {
                animation.KeyFrames.Add(new DiscreteDoubleKeyFrame((double)target.GetValue(property), KeyTime.FromTimeSpan(TimeSpan.Zero)));
            }

            foreach (var key in keys)
            {
                var time = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(key.At));
                animation.KeyFrames.Add(key.Jumps
                    ? (DoubleKeyFrame)new DiscreteDoubleKeyFrame(key.Value, time)
                    : new EasingDoubleKeyFrame(key.Value, time, key.Ease));
            }

            Storyboard.SetTargetName(animation, NameOf(target));
            Storyboard.SetTargetProperty(animation, new PropertyPath(property));
            board.Children.Add(animation);
            return animation;
        }
    }
}
