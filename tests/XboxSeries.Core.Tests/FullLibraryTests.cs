using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>Full library's Sort dropdown and its filter by name.</summary>
    public class FullLibraryTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public FullLibraryTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads());
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 1), added: new DateTime(2024, 1, 1), playtime: 500, releaseDate: new DateTime(2021, 12, 8))
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 13), added: new DateTime(2025, 6, 1), playtime: 9000, releaseDate: new DateTime(2020, 9, 17))
                .WithGame("Forza Horizon 5", added: new DateTime(2026, 2, 1), playtime: 20, releaseDate: new DateTime(2021, 11, 9))
                .WithGame("The Witness", sortingName: "Witness", lastActivity: new DateTime(2025, 3, 3), added: new DateTime(2023, 5, 5))
                .WithGame("Hidden Game", hidden: true, playtime: 99999);
            playnite.Library = library.Snapshot();
            core.OnLibraryChanged(playnite.Library);
        }

        public static IEnumerable<object[]> SortOptions() => new[]
        {
            new object[] { "A–Z", new[] { "Forza Horizon 5", "Hades", "Halo Infinite", "The Witness" } },
            new object[] { "Last played", new[] { "Hades", "Halo Infinite", "The Witness", "Forza Horizon 5" } },
            new object[] { "Recently added", new[] { "Forza Horizon 5", "Hades", "Halo Infinite", "The Witness" } },
            new object[] { "Playtime", new[] { "Hades", "Halo Infinite", "Forza Horizon 5", "The Witness" } },
            new object[] { "Release date", new[] { "Halo Infinite", "Forza Horizon 5", "Hades", "The Witness" } },
        };

        [Theory]
        [MemberData(nameof(SortOptions))]
        public void Each_sort_option_orders_the_grid(string option, string[] order)
        {
            playnite.SwitchPreset(GameSort.Playtime, SortDirection.Ascending);
            core.OpenMyGames(MyGamesItem.FullLibrary);

            core.SortFullLibrary(Option(option));

            Assert.Equal(order, playnite.MainList());
            Assert.Equal("Sorted by " + option, core.MyGames.SortLabel);
        }

        [Fact]
        public void The_dropdown_offers_the_consoles_sorts_in_order()
        {
            Assert.Equal(new[] { "A–Z", "Last played", "Recently added", "Playtime", "Release date" }, core.MyGames.SortOptions.Select(o => o.Name));
        }

        [Fact]
        public void The_dropdown_names_Playnites_own_sort_until_one_is_chosen()
        {
            playnite.SwitchPreset(GameSort.LastActivity, SortDirection.Descending);

            core.OpenMyGames(MyGamesItem.FullLibrary);

            Assert.Same(LibrarySortOption.LastPlayed, core.MyGames.LibrarySort);
            Assert.Equal(0, playnite.MainListSorts);
        }

        [Fact]
        public void A_sort_the_dropdown_doesnt_offer_reads_just_Sort()
        {
            playnite.SwitchPreset(GameSort.PlayCount, SortDirection.Descending);

            core.OpenMyGames(MyGamesItem.FullLibrary);

            Assert.Null(core.MyGames.LibrarySort);
            Assert.Equal("Sort", core.MyGames.SortLabel);
        }

        [Fact]
        public void The_chosen_sort_survives_switching_tabs()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.SortFullLibrary(LibrarySortOption.Playtime);

            // A preset of the user's with a sort of its own, then one without.
            playnite.SwitchPreset(GameSort.Name);
            core.OnMainListChanged();
            Assert.Equal(new[] { "Hades", "Halo Infinite", "Forza Horizon 5", "The Witness" }, playnite.MainList());
            playnite.SwitchPreset(null);
            core.OnMainListChanged();

            Assert.Equal(new[] { "Hades", "Halo Infinite", "Forza Horizon 5", "The Witness" }, playnite.MainList());
            Assert.Same(LibrarySortOption.Playtime, core.MyGames.LibrarySort);
        }

        [Fact]
        public void The_chosen_sort_survives_moving_along_the_nav_and_back()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.SortFullLibrary(LibrarySortOption.RecentlyAdded);

            core.OpenMyGames(MyGamesItem.Games);
            core.OpenMyGames(MyGamesItem.FullLibrary);

            Assert.Equal(new[] { "Forza Horizon 5", "Hades", "Halo Infinite", "The Witness" }, playnite.MainList());
            Assert.Same(LibrarySortOption.RecentlyAdded, core.MyGames.LibrarySort);
        }

        [Fact]
        public void A_tab_switch_doesnt_resort_a_grid_already_in_the_chosen_sort()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.SortFullLibrary(LibrarySortOption.Playtime);
            var sorts = playnite.MainListSorts;

            playnite.SwitchPreset(null);
            core.OnMainListChanged();

            Assert.Equal(sorts, playnite.MainListSorts);
        }

        [Fact]
        public void Only_Full_library_sorts_or_filters_by_name()
        {
            core.OpenMyGames(MyGamesItem.Games);

            core.SortFullLibrary(LibrarySortOption.Playtime);
            core.FilterFullLibraryByName("ha");
            core.TypeFullLibraryName();

            Assert.Equal(new[] { "Forza Horizon 5", "Hades", "Halo Infinite", "The Witness" }, playnite.MainList());
            Assert.Equal(string.Empty, core.MyGames.NameFilter);
        }

        [Fact]
        public void Typing_a_name_narrows_the_grid_to_the_games_that_match()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);

            core.FilterFullLibraryByName("o");
            Assert.Equal(new[] { "Forza Horizon 5", "Halo Infinite" }, playnite.MainList());
            core.FilterFullLibraryByName("ha");

            Assert.Equal(new[] { "Hades", "Halo Infinite" }, playnite.MainList());
            Assert.Equal("ha", core.MyGames.NameFilter);
            Assert.True(core.MyGames.HasNameFilter);
        }

        [Fact]
        public void The_name_narrows_the_tab_in_the_chosen_sort()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.SortFullLibrary(LibrarySortOption.LastPlayed);

            core.FilterFullLibraryByName("ha");

            Assert.Equal(new[] { "Hades", "Halo Infinite" }, playnite.MainList());
        }

        [Fact]
        public void Clearing_the_name_shows_the_whole_tab_again()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.FilterFullLibraryByName("ha");

            core.FilterFullLibraryByName(string.Empty);

            Assert.Equal(new[] { "Forza Horizon 5", "Hades", "Halo Infinite", "The Witness" }, playnite.MainList());
            Assert.False(core.MyGames.HasNameFilter);
        }

        [Fact]
        public void B_shows_the_whole_tab_again_before_it_leaves_the_page()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.FilterFullLibraryByName("ha");

            core.LeaveMyGames();

            Assert.True(core.MyGames.ShowsFullLibrary);
            Assert.Equal(new[] { "Forza Horizon 5", "Hades", "Halo Infinite", "The Witness" }, playnite.MainList());
            core.LeaveMyGames();
            Assert.False(core.MyGames.IsShown);
        }

        [Fact]
        public void The_on_screen_keyboard_narrows_the_grid_to_the_name_typed()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);
            playnite.NameToType = "halo";

            core.TypeFullLibraryName();

            Assert.Equal(new[] { "Halo Infinite" }, playnite.MainList());
            Assert.Equal("halo", core.MyGames.NameFilter);
            Assert.Equal(1, core.MyGames.FullLibraryCount);
        }

        [Fact]
        public void Cancelling_the_on_screen_keyboard_leaves_the_name_as_it_was()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.FilterFullLibraryByName("ha");
            playnite.NameToType = null;

            core.TypeFullLibraryName();

            Assert.Equal("ha", core.MyGames.NameFilter);
            Assert.Equal(new[] { "Hades", "Halo Infinite" }, playnite.MainList());
        }

        [Fact]
        public void The_count_is_the_games_the_grid_shows()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);
            Assert.Equal(4, core.MyGames.FullLibraryCount);

            core.FilterFullLibraryByName("ha");
            Assert.Equal(2, core.MyGames.FullLibraryCount);

            core.LeaveMyGames();
            Assert.Equal(4, core.MyGames.FullLibraryCount);
        }

        [Fact]
        public void The_name_goes_as_the_page_moves_to_another_nav_item()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.FilterFullLibraryByName("ha");

            core.OpenMyGames(MyGamesItem.Games);
            core.OpenMyGames(MyGamesItem.FullLibrary);

            Assert.Equal(string.Empty, core.MyGames.NameFilter);
            Assert.Equal(new[] { "Forza Horizon 5", "Hades", "Halo Infinite", "The Witness" }, playnite.MainList());
        }

        [Fact]
        public void The_users_own_filter_and_sort_are_restored_on_leaving_Fullscreen_without_the_name()
        {
            core.OpenMyGames(MyGamesItem.Games);
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.SortFullLibrary(LibrarySortOption.Playtime);
            core.FilterFullLibraryByName("ha");
            core.OpenMyGames(MyGamesItem.Games);

            core.OnApplicationStopped();

            // Full library is the user's own filter and sort: the sort chosen there is theirs, as a tab chosen there is.
            Assert.True(playnite.ShowsUsersOwnFilter);
            Assert.False(playnite.ReleasedUnderOwnFilter);
            Assert.Equal(GameSort.Playtime, playnite.MainListSort);
            Assert.Equal(SortDirection.Descending, playnite.MainListSortDirection);
            Assert.Equal(string.Empty, playnite.MainListName);
        }

        [Fact]
        public void Leaving_Fullscreen_on_Full_library_takes_the_name_off_the_users_filter()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.FilterFullLibraryByName("ha");

            core.OnApplicationStopped();

            Assert.Equal(string.Empty, playnite.MainListName);
        }

        [Fact]
        public void Leaving_the_page_takes_the_name_off_the_users_filter()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);
            playnite.NameToType = "halo";
            core.TypeFullLibraryName();

            core.OpenSettings();

            Assert.False(core.MyGames.IsShown);
            Assert.Equal(string.Empty, playnite.MainListName);
        }

        [Fact]
        public void The_chosen_sort_is_forgotten_once_the_page_closes()
        {
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.SortFullLibrary(LibrarySortOption.Playtime);
            core.LeaveMyGames();

            playnite.SwitchPreset(GameSort.Name);
            core.OpenMyGames(MyGamesItem.FullLibrary);

            Assert.Same(LibrarySortOption.AToZ, core.MyGames.LibrarySort);
            Assert.Equal(new[] { "Forza Horizon 5", "Hades", "Halo Infinite", "The Witness" }, playnite.MainList());
        }

        private static LibrarySortOption Option(string name) => LibrarySortOption.All.Single(o => o.Name == name);
    }

    /// <summary>Where Full library's Sort and Filter row sits, between the Dashboard's tabs and its grid.</summary>
    public class FullLibraryXamlTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

        [Fact]
        public void The_row_holds_Sort_then_the_name_box_at_the_left_and_the_count_then_Filter_at_the_right()
        {
            var row = Named(MyGames(), "LibraryBar");
            var children = row.Elements().Where(e => !e.Name.LocalName.Contains(".")).ToList();
            string Dock(XElement e) => (string)e.Attribute(XName.Get("DockPanel.Dock")) ?? "Left";

            Assert.Equal(new[] { "SortButton", "NameBox" }, children.Where(e => Dock(e) == "Left").Select(e => (string)e.Attribute(X + "Name")));

            // Docked right in turn, so the first is rightmost: Filter, with the count to its left.
            Assert.Equal(new[] { "FilterButton", "LibraryCount" }, children.Where(e => Dock(e) == "Right").Select(e => (string)e.Attribute(X + "Name")));
            Assert.Contains("FullLibraryCount", (string)Named(MyGames(), "LibraryCount").Attribute("Text"));
        }

        [Fact]
        public void The_row_sits_between_the_tabs_and_the_grid()
        {
            var myGames = MyGames();
            var main = XDocument.Load(Path.Combine(RepoRoot(), "src", "XboxSeries.Dashboard", "Views", "Main.xaml"));

            // The page's content: the header's row, then the row under it the tabs and the Sort and Filter row are in.
            var itemHost = Named(myGames, "ItemHost");
            var contentTop = Margin(itemHost)[1] + double.Parse((string)itemHost.Descendants().First(e => e.Name.LocalName == "RowDefinition").Attribute("Height"));
            var rowTop = contentTop + Margin(Named(myGames, "LibraryBar"))[1];
            var rowBottom = rowTop + double.Parse((string)Named(myGames, "LibraryBar").Attribute("Height"));

            var tabs = Named(main, "PART_FilterPresetSelector").Parent;
            var tabsBottom = Margin(tabs)[1] + double.Parse((string)tabs.Attribute("Height"));
            Assert.Equal(Margin(tabs)[1], contentTop);
            Assert.Equal(contentTop, contentTop + Margin(Named(myGames, "TabsFocus")).ElementAtOrDefault(1));
            Assert.True(rowTop >= tabsBottom, $"the row ({rowTop}) starts under the tabs ({tabsBottom})");

            // Full library's list, from the top of the Dashboard's content row under its 200 high top bar row.
            var library = main.Descendants().Single(e => e.Name.LocalName == "DataTrigger"
                && (string)e.Attribute("Binding") == "{Binding Content.State.ShowsFullLibrary, ElementName=XboxSeries_MyGames}"
                && e.Parent.Parent.Attribute(X + "Key")?.Value == "XboxMainList");
            var listTop = 200 + Margin(library.Elements().Single(e => (string)e.Attribute("Property") == "Margin"), "Value")[1];
            Assert.True(listTop > rowBottom, $"the grid ({listTop}) starts under the row ({rowBottom})");
        }

        private static XDocument MyGames() =>
            XDocument.Load(Path.Combine(RepoRoot(), "src", "XboxSeries.Extension", "MyGamesControl.xaml"));

        private static XElement Named(XDocument doc, string name) =>
            doc.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);

        private static double[] Margin(XElement e, string attribute = "Margin") =>
            ((string)e.Attribute(attribute) ?? "0").Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();

        private static string RepoRoot()
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
