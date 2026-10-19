using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// A row of tiles that slides sideways to the tile taking focus, as the console's do, rather than jumping there:
    /// it takes the request to bring the tile into view over from its ScrollViewer and animates the row's offset there.
    /// </summary>
    public class SlidingRowTests
    {
        private static readonly string Code = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "XboxSeries.Extension", "SlidingRow.cs"));

        [Fact]
        public void The_row_takes_the_request_to_bring_a_tile_into_view_and_marks_it_handled()
        {
            Assert.Matches(@"AddHandler\(\s*FrameworkElement\.RequestBringIntoViewEvent", Code);

            var handler = Regex.Match(Code, @"void (\w+)\(object sender, RequestBringIntoViewEventArgs e\)\s*\{.*?\n        \}", RegexOptions.Singleline);
            Assert.True(handler.Success, "no RequestBringIntoView handler");
            Assert.Contains("e.Handled = true;", handler.Value);
            Assert.Matches(@"AddHandler\(\s*FrameworkElement\.RequestBringIntoViewEvent,\s*new RequestBringIntoViewEventHandler\(" + handler.Groups[1].Value + @"\)", Code);
        }

        [Fact]
        public void The_row_lets_go_of_the_request_when_it_stops_sliding_or_unloads()
        {
            Assert.Matches(@"RemoveHandler\(\s*FrameworkElement\.RequestBringIntoViewEvent", Code);
            Assert.Matches(@"\.Unloaded \+= ", Code);
            Assert.Matches(@"\.Loaded \+= ", Code);
        }

        [Fact]
        public void The_row_slides_over_300_ms_easing_out_as_Homes_rows_do()
        {
            var duration = Regex.Match(Code, @"Duration (\w+) = TimeSpan\.FromMilliseconds\(300\);");
            Assert.True(duration.Success, "no 300 ms duration");
            Assert.Matches(@"new DoubleAnimation\([^;]*" + duration.Groups[1].Value + @"\)\s*\{[^}]*EasingFunction = new CubicEase \{ EasingMode = EasingMode\.EaseOut \}", Code);
        }

        [Fact]
        public void The_slide_scrolls_the_row_from_an_attached_animatable_offset()
        {
            var property = Regex.Match(Code, @"DependencyProperty (\w+) = DependencyProperty\.RegisterAttached\(\s*""\w+"",\s*typeof\(double\),\s*typeof\(SlidingRow\),\s*new PropertyMetadata\([^,]+,\s*(\w+)\)\)");
            Assert.True(property.Success, "no attached double with a change callback");

            var callback = Regex.Match(Code, @"void " + property.Groups[2].Value + @"\(DependencyObject d, DependencyPropertyChangedEventArgs e\)\s*\{.*?\n        \}", RegexOptions.Singleline).Value;
            Assert.Contains("ScrollToHorizontalOffset(", callback);
            Assert.Matches(@"\.BeginAnimation\(\s*" + property.Groups[1].Value + @",", Code);
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "XboxSeries.slnx")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
        }
    }
}
