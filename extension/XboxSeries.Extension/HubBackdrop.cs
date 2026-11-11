using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The Game hub's backdrop (<see cref="HubBackdropState"/>): as the hub shows, the page under it (Main's
    /// PART_ViewHost, which holds Home, My games &amp; apps, Search and every page in their place) is drawn once into a
    /// picture an eighth of its size, softened and frozen, before the hub is drawn over it; as the hub goes, it is let go.
    /// The page rather than Playnite's whole window: the hub, just made visible, hasn't been laid out yet, so the window
    /// would show it as it was last drawn, and what sits over the page (the hint bar, the Tile menu) isn't wanted behind.
    /// Each snapshot taken and freed is logged with the process's private bytes, so e2e can tell memory doesn't grow.
    /// </summary>
    internal sealed class HubBackdrop
    {
        /// <summary>The snapshot is this many times smaller than the page, each way.</summary>
        public const int Scale = 8;

        /// <summary>The blur, in the snapshot's pixels, drawn into it once: about 16 of the screen's at 1080p.</summary>
        private const double Soften = 2;

        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly HubBackdropState state;
        private readonly FrameworkElement hub;
        private readonly FrameworkElement page;

        private HubBackdrop(HubBackdropState state, FrameworkElement hub, FrameworkElement page)
        {
            this.state = state;
            this.hub = hub;
            this.page = page;
        }

        /// <summary>Follows <paramref name="hub"/> showing and hiding over <paramref name="page"/>; null if the theme has either missing.</summary>
        public static HubBackdrop Follow(HubBackdropState state, FrameworkElement hub, FrameworkElement page)
        {
            if (hub == null || page == null)
            {
                return null;
            }

            var backdrop = new HubBackdrop(state, hub, page);
            hub.IsVisibleChanged += backdrop.OnHubVisibleChanged;
            if (hub.IsVisible)
            {
                backdrop.Open();
            }

            return backdrop;
        }

        /// <summary>Stops following the hub, letting go of any snapshot held.</summary>
        public void Stop()
        {
            hub.IsVisibleChanged -= OnHubVisibleChanged;
            Close();
        }

        private void OnHubVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                Open();
            }
            else
            {
                Close();
            }
        }

        private void Open() => Guarded.Run("Taking the Game hub's backdrop", () =>
        {
            if (state.Open(Take) && state.Snapshot is BitmapSource taken)
            {
                logger.Info($"[Hub] backdrop snapshot taken ({taken.PixelWidth}x{taken.PixelHeight}), private bytes {PrivateMegabytes()} MB");
            }
        });

        private void Close() => Guarded.Run("Letting go of the Game hub's backdrop", () =>
        {
            if (state.Close())
            {
                logger.Info($"[Hub] backdrop snapshot freed, private bytes {PrivateMegabytes()} MB");
            }
        });

        /// <summary>
        /// The page as it is on screen, an eighth of its size and softened, frozen; null while it has no size. Drawn
        /// through a brush over exactly the page's own bounds, so what is flown off it (Home's other screen) stays out.
        /// The brush's area runs a blur's width past the picture's edges, so the blur has the page to soften into there
        /// rather than fading the edges out.
        /// </summary>
        private object Take()
        {
            var width = (int)Math.Ceiling(page.ActualWidth / Scale);
            var height = (int)Math.Ceiling(page.ActualHeight / Scale);
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            var brush = new VisualBrush(page)
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, page.ActualWidth, page.ActualHeight),
                Stretch = Stretch.Fill,
            };
            var visual = new DrawingVisual { Effect = new BlurEffect { Radius = Soften, KernelType = KernelType.Gaussian } };
            using (var context = visual.RenderOpen())
            {
                context.DrawRectangle(brush, null, new Rect(-Soften, -Soften, width + 2 * Soften, height + 2 * Soften));
            }

            var snapshot = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            snapshot.Render(visual);
            snapshot.Freeze();
            return snapshot;
        }

        private static string PrivateMegabytes()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return (process.PrivateMemorySize64 / (1024.0 * 1024.0)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
