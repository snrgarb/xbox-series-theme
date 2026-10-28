using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>See all at the end of a Group on Home opens My games &amp; apps on Full library, as the Play history tile opens Play history.</summary>
    public class SeeAllTests
    {
        private static readonly string ExtensionDir = Path.Combine(FindRepoRoot(), "extension", "XboxSeries.Extension");

        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public SeeAllTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0)), windows, playnite, new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(), new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads());
        }

        private void Library(FakeLibrary library)
        {
            playnite.Library = library.Snapshot();
            core.OnLibraryChanged(playnite.Library);
        }

        private static FakeLibrary Games() => new FakeLibrary()
            .WithGame("Halo Infinite", playtime: 500)
            .WithGame("Hades", playtime: 9000, favorite: true)
            .WithGame("Forza Horizon 5", playtime: 20, favorite: true)
            .WithGame("The Witness");

        [Fact]
        public void See_all_on_a_presets_Group_opens_Full_library_with_that_presets_tab_selected_and_its_games_in_its_sort()
        {
            var library = Games()
                .WithFilterPreset("Couch co-op", showInFullscreenQuickSelection: true, games: new[] { "The Witness" })
                .WithFilterPreset("Most played", showInFullscreenQuickSelection: true,
                    games: new[] { "Halo Infinite", "Hades", "Forza Horizon 5" }, sort: GameSort.Playtime, direction: SortDirection.Descending);
            Library(library);

            core.ChooseSeeAll(core.Home.Groups.Single(g => g.Name == "Most played"));

            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.FullLibrary, core.MyGames.Item);
            Assert.Equal(library.PresetId("Most played"), playnite.SelectedPreset);
            Assert.Equal(new[] { "Hades", "Halo Infinite", "Forza Horizon 5" }, playnite.MainList());
            Assert.Same(LibrarySortOption.Playtime, core.MyGames.LibrarySort);
            Assert.Equal(3, core.MyGames.FullLibraryCount);
        }

        [Fact]
        public void See_all_on_a_preset_without_a_sort_of_its_own_keeps_the_grids_sort()
        {
            var library = Games()
                .WithFilterPreset("Shooters", showInFullscreenQuickSelection: true, games: new[] { "Halo Infinite", "Hades" });
            Library(library);

            core.ChooseSeeAll(core.Home.Groups.Single());

            Assert.Equal(library.PresetId("Shooters"), playnite.SelectedPreset);
            Assert.Equal(new[] { "Hades", "Halo Infinite" }, playnite.MainList());
            Assert.Same(LibrarySortOption.AToZ, core.MyGames.LibrarySort);
        }

        [Fact]
        public void See_all_on_the_Favourites_Group_opens_Full_library_under_the_users_own_filter_with_no_preset_selected()
        {
            Library(Games());

            var favourites = core.Home.Groups.Single();
            Assert.Null(favourites.FilterPresetId);
            core.ChooseSeeAll(favourites);

            Assert.True(core.MyGames.IsShown);
            Assert.Equal(MyGamesItem.FullLibrary, core.MyGames.Item);
            Assert.Null(playnite.SelectedPreset);
            Assert.True(playnite.ShowsUsersOwnFilter);
            Assert.Equal(new[] { "Forza Horizon 5", "Hades", "Halo Infinite", "The Witness" }, playnite.MainList());
            Assert.Equal(4, core.MyGames.FullLibraryCount);
        }

        [Fact]
        public void B_on_Full_library_opened_by_See_all_goes_back_to_Home()
        {
            Library(Games().WithFilterPreset("Shooters", showInFullscreenQuickSelection: true, games: new[] { "Halo Infinite" }));
            core.ChooseSeeAll(core.Home.Groups.Single());

            core.LeaveMyGames();

            Assert.False(core.MyGames.IsShown);
        }

        [Fact]
        public void A_Enter_and_a_click_on_See_all_tell_the_core_which_Group_it_ends()
        {
            var xaml = XDocument.Load(Path.Combine(ExtensionDir, "GroupsControl.xaml"));
            var seeAll = xaml.Descendants().Single(e => e.Name.LocalName == "Button" && (string)e.Attribute("Tag") == "See all");

            // Enter and the mouse click it; the pad's A arrives as KeyDown only, never PreviewKeyDown.
            Assert.Equal("OnSeeAllClick", (string)seeAll.Attribute("Click"));
            Assert.Equal("OnSeeAllKeyDown", (string)seeAll.Attribute("KeyDown"));
            Assert.Null(seeAll.Attribute("PreviewKeyDown"));

            var code = File.ReadAllText(Path.Combine(ExtensionDir, "GroupsControl.xaml.cs"));
            Assert.Matches(@"void OnSeeAllClick\([^)]*\)\s*\{\s*core\.ChooseSeeAll\(\(HomeGroup\)", code);
            Assert.Matches(new Regex(@"void OnSeeAllKeyDown\([^)]*\)\s*\{\s*if \(PlayniteInput\.PadButtonOf\(e\) == ControllerInput\.A\)\s*\{\s*e\.Handled = true;\s*core\.ChooseSeeAll\(\(HomeGroup\)", RegexOptions.Singleline), code);
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
