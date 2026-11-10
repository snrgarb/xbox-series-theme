using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Attached to a row of tiles (an ItemsControl with a ScrollViewer in its template): the row slides sideways to bring
    /// the tile taking focus wholly into view, rather than jumping there.
    /// </summary>
    internal static class SlidingRow
    {
        public static readonly DependencyProperty SlidesProperty = DependencyProperty.RegisterAttached(
            "Slides", typeof(bool), typeof(SlidingRow), new PropertyMetadata(false, OnSlidesChanged));

        public static bool GetSlides(DependencyObject element) => (bool)element.GetValue(SlidesProperty);

        public static void SetSlides(DependencyObject element, bool value) => element.SetValue(SlidesProperty, value);

        /// <summary>How long the row takes to slide to the tile, as Home's rows and Groups take to move.</summary>
        private static readonly Duration SlideDuration = TimeSpan.FromMilliseconds(300);

        /// <summary>
        /// The row's ScrollViewer's offset as it slides. The ScrollViewer's own HorizontalOffset can't be animated, so
        /// this is, and each step of it scrolls the row there.
        /// </summary>
        private static readonly DependencyProperty AnimatedOffsetProperty = DependencyProperty.RegisterAttached(
            "AnimatedOffset", typeof(double), typeof(SlidingRow), new PropertyMetadata(0.0, OnAnimatedOffsetChanged));

        /// <summary>A sliding row's own state, held on the row and on its ScrollViewer's content.</summary>
        private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
            "State", typeof(Row), typeof(SlidingRow), new PropertyMetadata(null));

        private static void OnSlidesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is ItemsControl row))
            {
                return;
            }

            row.Loaded -= OnLoaded;
            row.Unloaded -= OnUnloaded;
            Detach(row);
            if ((bool)e.NewValue)
            {
                row.Loaded += OnLoaded;
                row.Unloaded += OnUnloaded;
                if (row.IsLoaded)
                {
                    Attach(row);
                }
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e) => Attach((ItemsControl)sender);

        private static void OnUnloaded(object sender, RoutedEventArgs e) => Detach((ItemsControl)sender);

        private static void OnAnimatedOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scroller)
            {
                scroller.ScrollToHorizontalOffset((double)e.NewValue);
            }
        }

        /// <summary>
        /// Takes the row's requests to bring a tile into view over from its ScrollViewer. They are caught on the
        /// ScrollViewer's content as they bubble up from the tile, before they reach the ScrollViewer, which would jump.
        /// </summary>
        private static void Attach(ItemsControl row)
        {
            Detach(row);
            row.ApplyTemplate();
            var scroller = FindScroller(row);
            if (!(scroller?.Content is FrameworkElement content))
            {
                return;
            }

            var state = new Row(row, scroller, content);
            row.SetValue(StateProperty, state);
            content.SetValue(StateProperty, state);
            content.AddHandler(FrameworkElement.RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler(OnRequestBringIntoView));
            content.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnKeyDown), true);
            ((INotifyCollectionChanged)row.Items).CollectionChanged += state.OnItemsChanged;
        }

        /// <summary>Lets go of the row's requests, leaving it where its slide had got to.</summary>
        private static void Detach(ItemsControl row)
        {
            if (!(row.GetValue(StateProperty) is Row state))
            {
                return;
            }

            row.ClearValue(StateProperty);
            state.Content.ClearValue(StateProperty);
            state.Content.RemoveHandler(FrameworkElement.RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler(OnRequestBringIntoView));
            state.Content.RemoveHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnKeyDown));
            ((INotifyCollectionChanged)row.Items).CollectionChanged -= state.OnItemsChanged;
            state.Stop();
        }

        /// <summary>
        /// Notes where the row is as a key or pad button reaches it, before the ListBox moves focus along it. Moving onto a
        /// tile that isn't built or isn't in view yet, the ListBox first scrolls the row on a line at a time, at once,
        /// until the tile is built and partly in view, and only then focuses it; the slide starts from where the row was
        /// as the button was pressed, so that jump never shows.
        /// </summary>
        private static void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (((DependencyObject)sender).GetValue(StateProperty) is Row state)
            {
                state.NotePress();
            }
        }

        /// <summary>
        /// Slides the row to bring the tile asked for wholly into view, rather than letting the ScrollViewer jump there,
        /// then passes the request on from the ScrollViewer to any ScrollViewer round the row, as the ScrollViewer would.
        /// </summary>
        private static void OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            if (!(((DependencyObject)sender).GetValue(StateProperty) is Row state) || !(e.TargetObject is Visual target)
                || target == sender || !target.IsDescendantOf(state.Scroller) || state.Scroller.ActualWidth <= 0)
            {
                return;
            }

            e.Handled = true;
            var shown = state.SlideTo(target);
            if (!shown.IsEmpty)
            {
                state.Scroller.BringIntoView(shown);
            }
        }

        /// <summary>The ScrollViewer in the row's template: the first one down its visual tree.</summary>
        private static ScrollViewer FindScroller(DependencyObject node)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if ((child as ScrollViewer ?? FindScroller(child)) is ScrollViewer scroller)
                {
                    return scroller;
                }
            }

            return null;
        }

        /// <summary>One sliding row: its ScrollViewer, that ScrollViewer's content, and the slide it is on, if any.</summary>
        private sealed class Row
        {
            public readonly ItemsControl Items;
            public readonly ScrollViewer Scroller;
            public readonly FrameworkElement Content;

            /// <summary>The slide the row is on, and where to, while it slides.</summary>
            private DoubleAnimation slide;
            private double slidingTo;

            /// <summary>Where the row was as the key or pad button being handled now was pressed, while one is.</summary>
            private double? pressedAt;
            private object press;

            public Row(ItemsControl items, ScrollViewer scroller, FrameworkElement content)
            {
                Items = items;
                Scroller = scroller;
                Content = content;
            }

            /// <summary>Notes where the row is for the press being handled, forgotten once it has been.</summary>
            public void NotePress()
            {
                var thisPress = new object();
                press = thisPress;
                pressedAt = Scroller.HorizontalOffset;
                Scroller.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    if (press == thisPress)
                    {
                        press = null;
                        pressedAt = null;
                    }
                }));
            }

            /// <summary>
            /// Starts the row sliding, from where it is now, to the nearest offset that shows <paramref name="target"/>'s
            /// tile wholly, so it always comes to rest a whole number of tiles along. Going right, the end of the tile's
            /// slot, the margin after it included, meets the row's right edge; going left, the tile's left edge comes to
            /// the room the row leaves for the ring, in from its left edge. A tile already wholly in view leaves the row
            /// where it is, or mid-slide, where it is sliding to. Returns where the tile will be in the ScrollViewer once
            /// the row is there.
            /// </summary>
            public Rect SlideTo(Visual target)
            {
                var tile = Items.ContainerFromElement(target) as FrameworkElement ?? target as FrameworkElement;
                if (tile == null || !tile.IsDescendantOf(Scroller))
                {
                    return Rect.Empty;
                }

                var now = Scroller.HorizontalOffset;
                var at = slide != null ? slidingTo : now;

                // Where the tile is in the ScrollViewer with the row where it is going.
                var bounds = tile.TransformToAncestor(Scroller).TransformBounds(new Rect(0, 0, tile.ActualWidth, tile.ActualHeight));
                bounds.Offset(now - at, 0);
                var slotEnd = bounds.Right + tile.Margin.Right;
                var room = Content.Margin.Left;

                var to = at;
                if (bounds.Left < room)
                {
                    to = at + bounds.Left - room;
                }
                else if (slotEnd > Scroller.ActualWidth)
                {
                    to = at + slotEnd - Scroller.ActualWidth;
                }

                to = Math.Max(0, Math.Min(Scroller.ScrollableWidth, to));

                var from = now;
                if (pressedAt is double pressed)
                {
                    from = pressed;
                    pressedAt = null;
                    press = null;
                }

                // No distance to slide, or Reduce motion on: the row is there at once.
                if (Math.Abs(to - from) < 0.5 || Motion.Reduced)
                {
                    Stop();
                    Scroller.ScrollToHorizontalOffset(to);
                }
                else
                {
                    Start(from, to);
                }

                bounds.Offset(at - to, 0);
                return bounds;
            }

            /// <summary>Slides the row from <paramref name="from"/> to <paramref name="to"/>, taking over any slide it was on.</summary>
            private void Start(double from, double to)
            {
                var animation = new DoubleAnimation(from, to, SlideDuration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                animation.Completed += (_, __) =>
                {
                    if (slide == animation)
                    {
                        slide = null;
                    }
                };

                slide = animation;
                slidingTo = to;
                Scroller.BeginAnimation(AnimatedOffsetProperty, animation);
            }

            /// <summary>
            /// A new list in the row, as when Search's text changes: it starts at its first tile, with no slide or offset
            /// left over from the list before.
            /// </summary>
            public void OnItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                if (e.Action == NotifyCollectionChangedAction.Reset)
                {
                    Stop();
                    Scroller.ScrollToHorizontalOffset(0);
                }
            }

            /// <summary>Stops any slide where it has got to, leaving the row there.</summary>
            public void Stop()
            {
                slide = null;

                // Its resting value set to where the row is first, so taking the animation off doesn't scroll the row away.
                Scroller.SetValue(AnimatedOffsetProperty, Scroller.HorizontalOffset);
                Scroller.BeginAnimation(AnimatedOffsetProperty, null);
            }
        }
    }
}
