using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Grows the focused one of a set of Home tiles, as on the console, and shrinks the rest back. Only the Recent
    /// row's tiles grow; the Groups' are only ringed and titled.
    /// </summary>
    internal sealed class TileGrowth
    {
        /// <summary>A tile's size at rest, and how much it grows when focused, at 1080p (the console captures, #7).</summary>
        public const double TileSize = 153;
        public const double FocusGrowth = 92;

        /// <summary>How quickly a tile eases toward its size: about 98% of the way in 0.2 s, as on the console.</summary>
        private static readonly TimeSpan GrowthTimeConstant = TimeSpan.FromMilliseconds(50);

        private readonly Func<List<Button>> tiles;

        // Each tile's share of the focus growth. Every frame each share eases toward 1 for the focused tile and 0 for the
        // rest by the same fraction, so the shares always add up to the same total and a row's width never changes:
        // only the tiles growing or shrinking move, however fast focus moves. Per-tile animations can't promise that
        // once one is interrupted mid-way.
        private readonly Dictionary<Button, double> growth = new Dictionary<Button, double>();
        private readonly Stopwatch frameClock = new Stopwatch();
        private bool isAnimating;

        /// <param name="tiles">Every tile in the set. Called every frame while tiles change size, so it shouldn't allocate.</param>
        public TileGrowth(Func<List<Button>> tiles)
        {
            this.tiles = tiles;
        }

        /// <summary>Starts easing the tiles toward their sizes, after focus has moved.</summary>
        public void Start()
        {
            if (!isAnimating)
            {
                isAnimating = true;
                frameClock.Restart();
                CompositionTarget.Rendering += OnFrame;
            }
        }

        public void Stop()
        {
            if (isAnimating)
            {
                isAnimating = false;
                CompositionTarget.Rendering -= OnFrame;
            }
        }

        /// <summary>Forgets tiles a rebuild has removed, keeping only <paramref name="current"/>.</summary>
        public void Forget(ICollection<Button> current)
        {
            foreach (var gone in growth.Keys.Except(current).ToList())
            {
                growth.Remove(gone);
            }
        }

        /// <summary>Eases every tile's share of the growth toward its target, and sizes the tiles from their shares.</summary>
        private void OnFrame(object sender, EventArgs e)
        {
            var elapsed = frameClock.Elapsed;
            frameClock.Restart();
            var step = 1 - Math.Exp(-elapsed.TotalMilliseconds / GrowthTimeConstant.TotalMilliseconds);

            // No allocations here: this runs every frame while tiles change size.
            var all = tiles();
            var settled = true;
            foreach (var tile in all)
            {
                if (Math.Abs(GrowthTarget(tile) - NextGrowth(tile, step)) >= 0.002)
                {
                    settled = false;
                }
            }

            // All tiles settle in the same frame, so snapping to the targets keeps the shares' total exact.
            foreach (var tile in all)
            {
                var share = settled ? GrowthTarget(tile) : NextGrowth(tile, step);
                growth[tile] = share;
                tile.Width = tile.Height = TileSize + FocusGrowth * share;
            }

            if (settled)
            {
                Stop();
            }
        }

        private static double GrowthTarget(Button tile) => tile.IsKeyboardFocused ? 1 : 0;

        private double NextGrowth(Button tile, double step)
        {
            growth.TryGetValue(tile, out var share);
            return share + (GrowthTarget(tile) - share) * step;
        }
    }
}
