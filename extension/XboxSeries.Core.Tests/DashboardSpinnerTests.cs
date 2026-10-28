using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // The Dashboard draws the console's loading spinner in its own XAML, as Playnite's ProgressIndicator, since it can't
    // use the core's Spinner. Its keyframes are the core's spinner sampled at a fixed rate; these checks keep the copy
    // from drifting from the model, and print the keyframes to paste in when it does.
    public class DashboardSpinnerTests
    {
        // Samples per turn: 100 a second, so the chords between them can't be seen at the top bar's 26 px.
        private const int Samples = 120;
        private const double Canvas = 100;
        private const double Tolerance = 0.01;

        private static readonly string MediaPath = Path.Combine(FindRepoRoot(), "theme", "XboxSeries.Dashboard", "Media.xaml");
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

        [Fact]
        public void The_progress_indicator_turns_through_the_cores_spinner()
        {
            var style = ProgressIndicator();
            var thickness = Thickness(style);
            var storyboard = style.Descendants().Single(e => e.Name.LocalName == "Storyboard");
            var expected = Expected(thickness);

            var message = "The Dashboard's spinner has drifted from Spinner.At. Paste this as the Storyboard's contents:\n\n"
                          + KeyframesXaml(expected);

            Assert.True((string)storyboard.Attribute("Duration") == Format(Spinner.Period), message);
            Assert.True(storyboard.Attribute("RepeatBehavior")?.Value == "Forever", message);

            foreach (var track in expected.Points)
            {
                var animation = storyboard.Elements().SingleOrDefault(e => e.Name.LocalName == "PointAnimationUsingKeyFrames"
                    && (string)e.Attribute("Storyboard.TargetName") == track.Target
                    && (string)e.Attribute("Storyboard.TargetProperty") == track.Property);
                Assert.True(animation != null, message);

                var keyframes = animation.Elements()
                    .Select(k => (Time: TimeSpan.Parse((string)k.Attribute("KeyTime"), CultureInfo.InvariantCulture), Value: ParsePoint((string)k.Attribute("Value"))))
                    .ToList();
                Assert.True(keyframes.Count > 0 && keyframes[0].Time == TimeSpan.Zero, message);
                Assert.True(animation.Elements().All(k => k.Name.LocalName == "LinearPointKeyFrame"), message);

                for (var i = 0; i < Samples; i++)
                {
                    var actual = Evaluate(keyframes, SampleTime(i));
                    Assert.True(Math.Abs(actual.X - track.Values[i].X) <= Tolerance && Math.Abs(actual.Y - track.Values[i].Y) <= Tolerance,
                        $"{track.Target} {track.Property} at {SampleTime(i)}: {actual} rather than {track.Values[i]}.\n{message}");
                }
            }

            foreach (var track in expected.LargeArcs)
            {
                var animation = storyboard.Elements().SingleOrDefault(e => e.Name.LocalName == "BooleanAnimationUsingKeyFrames"
                    && (string)e.Attribute("Storyboard.TargetName") == track.Target
                    && (string)e.Attribute("Storyboard.TargetProperty") == track.Property);
                Assert.True(animation != null, message);

                var keyframes = animation.Elements()
                    .Select(k => (Time: TimeSpan.Parse((string)k.Attribute("KeyTime"), CultureInfo.InvariantCulture), Value: bool.Parse((string)k.Attribute("Value"))))
                    .ToList();
                Assert.True(keyframes.Count > 0 && keyframes[0].Time == TimeSpan.Zero, message);
                Assert.True(animation.Elements().All(k => k.Name.LocalName == "DiscreteBooleanKeyFrame"), message);

                for (var i = 0; i < Samples; i++)
                {
                    var actual = keyframes.Last(k => k.Time <= SampleTime(i)).Value;
                    Assert.True(actual == track.Values[i], $"{track.Target} {track.Property} at {SampleTime(i)}: {actual}.\n{message}");
                }
            }
        }

        [Fact]
        public void The_progress_indicator_rests_where_a_turn_starts_until_it_spins()
        {
            var style = ProgressIndicator();
            var expected = Expected(Thickness(style));

            foreach (var name in new[] { "Tail", "Head" })
            {
                var path = style.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);
                var figure = path.Descendants().Single(e => e.Name.LocalName == "PathFigure");
                var arc = figure.Descendants().Single(e => e.Name.LocalName == "ArcSegment");

                AssertNear(expected.Points.Single(t => t.Target == name && t.Property.EndsWith("(PathFigure.StartPoint)")).Values[0], ParsePoint((string)figure.Attribute("StartPoint")));
                AssertNear(expected.Points.Single(t => t.Target == name && t.Property.EndsWith("(ArcSegment.Point)")).Values[0], ParsePoint((string)arc.Attribute("Point")));
                Assert.Equal("Round", (string)path.Attribute("StrokeStartLineCap"));
                Assert.Equal("Round", (string)path.Attribute("StrokeEndLineCap"));
            }
        }

        [Fact]
        public void The_progress_indicator_spins_only_while_it_is_visible()
        {
            var style = ProgressIndicator();
            var trigger = style.Descendants().Single(e => e.Name.LocalName == "Trigger" && (string)e.Attribute("Property") == "IsVisible");
            var begin = trigger.Descendants().Single(e => e.Name.LocalName == "BeginStoryboard");
            var remove = trigger.Descendants().Single(e => e.Name.LocalName == "RemoveStoryboard");

            Assert.Equal("True", (string)trigger.Attribute("Value"));
            Assert.Equal("EnterActions", begin.Parent.Name.LocalName.Split('.').Last());
            Assert.Equal("ExitActions", remove.Parent.Name.LocalName.Split('.').Last());
            Assert.Equal((string)begin.Attribute(X + "Name") ?? (string)begin.Attribute("Name"), (string)remove.Attribute("BeginStoryboardName"));
        }

        private static XElement ProgressIndicator()
        {
            var media = XDocument.Load(MediaPath);
            var style = media.Root.Elements().Single(e => e.Name.LocalName == "Style" && (string)e.Attribute(X + "Key") == "ProgressIndicator");
            Assert.Equal("ContentControl", (string)style.Attribute("TargetType"));
            return style;
        }

        private static double Thickness(XElement style)
        {
            var thicknesses = new[] { "Tail", "Head" }
                .Select(name => style.Descendants().Single(e => (string)e.Attribute(X + "Name") == name))
                .Select(path => double.Parse((string)path.Attribute("StrokeThickness"), CultureInfo.InvariantCulture))
                .Distinct()
                .ToList();
            Assert.Single(thicknesses);
            return thicknesses[0];
        }

        private static TimeSpan SampleTime(int i) => TimeSpan.FromTicks(Spinner.Period.Ticks * i / Samples);

        private sealed class PointTrack
        {
            public string Target;
            public string Property;
            public (double X, double Y)[] Values;
        }

        private sealed class BoolTrack
        {
            public string Target;
            public string Property;
            public bool[] Values;
        }

        private sealed class Tracks
        {
            public PointTrack[] Points;
            public BoolTrack[] LargeArcs;
        }

        private const string StartPoint = "(Path.Data).(PathGeometry.Figures)[0].(PathFigure.StartPoint)";
        private const string ArcPoint = "(Path.Data).(PathGeometry.Figures)[0].(PathFigure.Segments)[0].(ArcSegment.Point)";
        private const string LargeArc = "(Path.Data).(PathGeometry.Figures)[0].(PathFigure.Segments)[0].(ArcSegment.IsLargeArc)";

        /// <summary>The spinner sampled <see cref="Samples"/> times a turn, on a ring that fills the canvas with its stroke inside it.</summary>
        private static Tracks Expected(double thickness)
        {
            var radius = (Canvas - thickness) / 2;
            var arcs = Enumerable.Range(0, Samples).Select(i => Spinner.At(SampleTime(i))).ToArray();
            (double, double) At(double degrees)
            {
                var radians = degrees * Math.PI / 180;
                return (Math.Round(Canvas / 2 + radius * Math.Sin(radians), 3), Math.Round(Canvas / 2 - radius * Math.Cos(radians), 3));
            }

            return new Tracks
            {
                Points = new[]
                {
                    new PointTrack { Target = "Tail", Property = StartPoint, Values = arcs.Select(a => At(a.GreenTail)).ToArray() },
                    new PointTrack { Target = "Tail", Property = ArcPoint, Values = arcs.Select(a => At(a.WhiteTail)).ToArray() },
                    new PointTrack { Target = "Head", Property = StartPoint, Values = arcs.Select(a => At(a.WhiteTail)).ToArray() },
                    new PointTrack { Target = "Head", Property = ArcPoint, Values = arcs.Select(a => At(a.Head)).ToArray() },
                },
                LargeArcs = new[]
                {
                    new BoolTrack { Target = "Tail", Property = LargeArc, Values = arcs.Select(a => a.WhiteTail - a.GreenTail > 180).ToArray() },
                    new BoolTrack { Target = "Head", Property = LargeArc, Values = arcs.Select(a => a.Head - a.WhiteTail > 180).ToArray() },
                },
            };
        }

        /// <summary>The Storyboard's animations for <paramref name="tracks"/>, with keyframes inside a still stretch left out.</summary>
        private static string KeyframesXaml(Tracks tracks)
        {
            var xaml = new StringBuilder();
            foreach (var track in tracks.Points)
            {
                xaml.AppendLine($"<PointAnimationUsingKeyFrames Storyboard.TargetName=\"{track.Target}\" Storyboard.TargetProperty=\"{track.Property}\">");
                for (var i = 0; i < Samples; i++)
                {
                    var still = i > 0 && track.Values[i] == track.Values[i - 1]
                                && (i == Samples - 1 || track.Values[i] == track.Values[i + 1]);
                    if (!still)
                        xaml.AppendLine($"    <LinearPointKeyFrame KeyTime=\"{Format(SampleTime(i))}\" Value=\"{FormatPoint(track.Values[i])}\" />");
                }
                xaml.AppendLine("</PointAnimationUsingKeyFrames>");
            }
            foreach (var track in tracks.LargeArcs)
            {
                xaml.AppendLine($"<BooleanAnimationUsingKeyFrames Storyboard.TargetName=\"{track.Target}\" Storyboard.TargetProperty=\"{track.Property}\">");
                for (var i = 0; i < Samples; i++)
                {
                    if (i == 0 || track.Values[i] != track.Values[i - 1])
                        xaml.AppendLine($"    <DiscreteBooleanKeyFrame KeyTime=\"{Format(SampleTime(i))}\" Value=\"{track.Values[i]}\" />");
                }
                xaml.AppendLine("</BooleanAnimationUsingKeyFrames>");
            }
            return xaml.ToString();
        }

        private static (double X, double Y) Evaluate(List<(TimeSpan Time, (double X, double Y) Value)> keyframes, TimeSpan time)
        {
            var before = keyframes.Last(k => k.Time <= time);
            var after = keyframes.FirstOrDefault(k => k.Time > time);
            if (after.Time == default)
                return before.Value;
            var f = (time - before.Time).Ticks / (double)(after.Time - before.Time).Ticks;
            return (before.Value.X + f * (after.Value.X - before.Value.X), before.Value.Y + f * (after.Value.Y - before.Value.Y));
        }

        private static void AssertNear((double X, double Y) expected, (double X, double Y) actual)
        {
            Assert.True(Math.Abs(actual.X - expected.X) <= Tolerance && Math.Abs(actual.Y - expected.Y) <= Tolerance, $"{actual} rather than {expected}");
        }

        private static string Format(TimeSpan time) => "0:0:" + time.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);

        private static string FormatPoint((double X, double Y) point) =>
            point.X.ToString("0.###", CultureInfo.InvariantCulture) + "," + point.Y.ToString("0.###", CultureInfo.InvariantCulture);

        private static (double X, double Y) ParsePoint(string value)
        {
            var parts = value.Split(',');
            return (double.Parse(parts[0], CultureInfo.InvariantCulture), double.Parse(parts[1], CultureInfo.InvariantCulture));
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
