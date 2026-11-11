using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // The Game hub's header as the console's (#204, spec #201's Game hub > Header): the title alone on its line, and
    // under it one line of facts, Publisher • Year • Install size • Platform • Source, then the features, then the age
    // rating's box. A part with nothing to show takes its "•" with it, the line trims with an ellipsis rather than
    // wrapping, and the rating's box stays whole. Theme only: plain XAML and Playnite's own converters. The XAML isn't
    // run here; these read its structure.
    public class GameHubHeaderXamlTests
    {
        private const string Dot = " • ";

        // The facts in the line's order, by the binding of each part's text.
        private static readonly string[] Facts =
        {
            "Publishers", "ReleaseYear", "InstallSize", "Platforms", "Source.Name", "Features",
        };

        [Fact]
        public void The_title_is_alone_on_the_headers_first_line()
        {
            var row = InRow(0);

            var title = Assert.Single(row);
            Assert.Equal("TextBlock", title.Name.LocalName);
            Assert.Equal("{Binding DisplayName}", (string)title.Attribute("Text"));
            Assert.Equal("XboxHeaderFontSemibold", ResourceKey((string)title.Attribute("FontFamily")));
            Assert.Equal("52", (string)title.Attribute("FontSize"));
        }

        [Fact]
        public void The_facts_features_and_rating_share_the_line_under_the_title()
        {
            var line = Assert.Single(InRow(1));
            var facts = FactsText();
            var rating = Rating();

            Assert.Same(line, facts.Parent);
            Assert.Same(line, rating.Parent);
            Assert.Equal("{StaticResource XboxHubDimText}", (string)facts.Attribute("Style"));
            Assert.Equal(Facts, Values().Select(Bound).ToArray());
            Assert.Contains("AgeRatings", (string)rating.Descendants().Single(e => e.Name.LocalName == "TextBlock").Attribute("Text"));
        }

        [Fact]
        public void The_features_are_joined_by_the_same_dot()
        {
            var features = Values().Last();

            Assert.Contains($"ConverterParameter='{Dot}'", TextOf(features));
        }

        // The first part has none; each later one's "•" is " • " only while its own part shows and some part before
        // it does: blank when its part's text is empty, and blank when every earlier part's is.
        [Fact]
        public void No_part_leaves_a_leading_or_doubled_dot()
        {
            var runs = Runs();
            var values = Values();

            Assert.Equal("Value", Role(runs[0]));
            Assert.Equal(2 * values.Count - 1, runs.Count);
            for (var i = 1; i < values.Count; i++)
            {
                var dot = runs[2 * i - 1];
                Assert.Equal("Separator", Role(dot));
                Assert.Same(values[i], runs[2 * i]);

                var style = RunStyle(dot);
                Assert.Equal(Dot, Setter(style, "Text"));

                var own = style.Descendants().Where(e => e.Name.LocalName == "DataTrigger" && Watches(e, Name(values[i])))
                    .Where(t => (string)t.Attribute("Value") == "True").ToList();
                Assert.True(own.Count == 1, $"The dot before {Bound(values[i])} isn't blanked when its part is empty");
                Assert.Equal("", Setter(own[0], "Text"));

                var before = values.Take(i).Select(Name).OrderBy(n => n).ToArray();
                var none = style.Descendants().Where(e => e.Name.LocalName == "MultiDataTrigger").ToList();
                Assert.True(none.Count == 1, $"The dot before {Bound(values[i])} has no trigger for nothing before it");
                var conditions = none[0].Descendants().Where(e => e.Name.LocalName == "Condition").ToList();
                Assert.All(conditions, c => Assert.Equal("True", (string)c.Attribute("Value")));
                Assert.Equal(before, conditions.Select(c => ElementName((string)c.Attribute("Binding"))).OrderBy(n => n).ToArray());
                Assert.Equal("", Setter(none[0], "Text"));
            }
        }

        // An empty part is one whose text is empty, read by each dot from that part's own run.
        [Fact]
        public void A_dot_reads_whether_a_part_is_empty_from_its_text()
        {
            var bindings = Runs().Where(r => Role(r) == "Separator")
                .SelectMany(r => RunStyle(r).Descendants())
                .Where(e => e.Name.LocalName == "DataTrigger" || e.Name.LocalName == "Condition")
                .Select(e => (string)e.Attribute("Binding")).ToList();

            Assert.NotEmpty(bindings);
            Assert.All(bindings, b =>
            {
                Assert.StartsWith("{Binding Text, ElementName=", b);
                Assert.Contains("Converter={StaticResource StringNullOrEmptyToBoolConverter}", b);
            });
        }

        // Install size shows for an installed game only; its text is blank otherwise, so its dot goes too.
        [Fact]
        public void Install_size_is_blank_for_a_game_that_isnt_installed()
        {
            var size = Values().Single(v => Bound(v) == "InstallSize");
            var blank = RunStyle(size).Descendants()
                .Single(e => e.Name.LocalName == "DataTrigger" && (string)e.Attribute("Binding") == "{Binding IsInstalled}");

            Assert.Equal("False", (string)blank.Attribute("Value"));
            Assert.Equal("", Setter(blank, "Text"));
        }

        // Source is blank for a game with none, which Playnite gives an empty source id rather than none.
        [Fact]
        public void Source_is_blank_for_a_game_with_none()
        {
            var source = Values().Single(v => Bound(v) == "Source.Name");
            var blank = RunStyle(source).Descendants()
                .Single(e => e.Name.LocalName == "DataTrigger" && (string)e.Attribute("Binding") == "{Binding SourceId}");

            Assert.Equal("{x:Static sys:Guid.Empty}", (string)blank.Attribute("Value"));
            Assert.Equal("", Setter(blank, "Text"));
        }

        // A Run's text binds two ways by default, which a read-only property refuses.
        [Fact]
        public void Each_parts_text_binds_one_way()
        {
            Assert.All(Values(), v => Assert.Contains("Mode=OneWay", TextOf(v)));
        }

        [Fact]
        public void A_long_line_ends_in_an_ellipsis_before_the_rating_box()
        {
            var facts = FactsText();
            var rating = Rating();
            var line = (XElement)facts.Parent;
            var children = line.Elements().Where(e => !e.Name.LocalName.Contains(".")).ToList();

            Assert.Equal("CharacterEllipsis", (string)facts.Attribute("TextTrimming"));
            Assert.True(facts.Attribute("TextWrapping") == null || (string)facts.Attribute("TextWrapping") == "NoWrap");
            Assert.Null(facts.Attribute("MaxHeight"));
            Assert.Null(facts.Attribute("MaxWidth"));
            // The rating is docked first, so it is measured whole and the facts get what's left; the line keeps to its
            // content, so a short line has the rating straight after it.
            Assert.Equal("DockPanel", line.Name.LocalName);
            Assert.Equal("Right", (string)rating.Attribute("DockPanel.Dock"));
            Assert.Same(rating, children.First());
            Assert.Same(facts, children.Last());
            Assert.Equal("Left", (string)line.Attribute("HorizontalAlignment"));
            Assert.NotEqual("False", (string)line.Attribute("LastChildFill"));
        }

        // With no facts or features the rating starts the line: the gap before it is the facts' own, which go with them.
        [Fact]
        public void The_rating_leads_the_line_when_there_are_no_facts()
        {
            var facts = FactsText();
            var empty = facts.Elements().Single(e => e.Name.LocalName == "TextBlock.Style")
                .Descendants().Single(e => e.Name.LocalName == "MultiDataTrigger");
            var conditions = empty.Descendants().Where(e => e.Name.LocalName == "Condition").ToList();

            Assert.Equal(Values().Select(Name).OrderBy(n => n), conditions.Select(c => ElementName((string)c.Attribute("Binding"))).OrderBy(n => n));
            Assert.Equal("Collapsed", Setter(empty, "Visibility"));
            Assert.Null(Rating().Attribute("Margin"));
        }

        [Fact]
        public void Both_pages_show_the_same_header()
        {
            var hub = Hub();
            var pages = new[] { "HubCards", "DetailsCards" }
                .Select(n => hub.Descendants().Single(e => (string)e.Attribute(X + "Name") == n)).ToList();
            var header = InRow(0).Concat(InRow(1)).ToList();

            Assert.Equal(2, header.Count);
            Assert.All(header, h => Assert.DoesNotContain(pages, p => p.Descendants().Contains(h)));
            Assert.Single(hub.Descendants(), e => (string)e.Attribute("Text") == "{Binding DisplayName}");
            Assert.Single(hub.Descendants(), e => (string)e.Attribute(X + "Name") == "HeaderFacts");
            // The page toggle swaps the cards only.
            var swapped = hub.Descendants().Where(e => e.Name.LocalName == "Trigger" && (string)e.Attribute("SourceName") == "PageToggle")
                .SelectMany(t => t.Elements()).Select(s => (string)s.Attribute("TargetName")).ToList();
            Assert.Equal(new[] { "HubCards", "DetailsCards", "Cards" }, swapped);
        }

        [Fact]
        public void The_old_two_line_header_is_gone()
        {
            var hub = Hub();

            Assert.DoesNotContain(hub.Descendants(), e => (string)e.Attribute(X + "Key") == "XboxHubDot");
            Assert.DoesNotContain(hub.Descendants(), e => ((string)e.Attribute(X + "Name"))?.StartsWith("HeaderPublisher") == true);
        }

        private static XDocument Hub() => XDocument.Load(Path.Combine(RepoRoot, "theme", "XboxSeries.Dashboard", "Views", "GameDetails.xaml"));

        // The header's rows: what sits in the hub's main grid at Grid.Row n.
        private static List<XElement> InRow(int n)
        {
            var actions = Hub().Descendants().Single(e => (string)e.Attribute(X + "Name") == "Actions");
            return actions.Parent.Elements()
                .Where(e => ((string)e.Attribute("Grid.Row") ?? "0") == n.ToString() && !e.Name.LocalName.Contains("."))
                .ToList();
        }

        private static XElement FactsText() =>
            Hub().Descendants().Single(e => (string)e.Attribute(X + "Name") == "HeaderFacts");

        private static XElement Rating() =>
            Hub().Descendants().Single(e => (string)e.Attribute(X + "Name") == "HeaderRating");

        private static List<XElement> Runs() =>
            FactsText().Elements().Where(e => e.Name.LocalName == "Run").ToList();

        private static List<XElement> Values() => Runs().Where(r => Role(r) == "Value").ToList();

        // A run is a part's value when it binds the game, else the dot before one.
        private static string Role(XElement run) => TextOf(run).StartsWith("{Binding") ? "Value" : "Separator";

        // A run's text: its own, or its style's Text setter.
        private static string TextOf(XElement run) =>
            (string)run.Attribute("Text") ?? (RunStyle(run) is XElement s ? Setter(s, "Text") : "");

        private static XElement RunStyle(XElement run) =>
            run.Elements().SingleOrDefault(e => e.Name.LocalName == "Run.Style")?.Elements().Single();

        private static string Bound(XElement run)
        {
            var text = TextOf(run);
            var path = text.Substring("{Binding ".Length);
            var end = path.IndexOfAny(new[] { ',', '}' });
            return path.Substring(0, end).Trim();
        }

        private static string Name(XElement e) => (string)e.Attribute(X + "Name");

        private static string ElementName(string binding)
        {
            var at = binding.IndexOf("ElementName=") + "ElementName=".Length;
            var end = binding.IndexOfAny(new[] { ',', '}' }, at);
            return binding.Substring(at, end - at).Trim();
        }

        private static bool Watches(XElement trigger, string name) =>
            ((string)trigger.Attribute("Binding"))?.Contains($"ElementName={name},") == true;

        private static string Setter(XElement owner, string property) =>
            (string)owner.Elements().Single(s => s.Name.LocalName == "Setter" && (string)s.Attribute("Property") == property).Attribute("Value");
    }
}
