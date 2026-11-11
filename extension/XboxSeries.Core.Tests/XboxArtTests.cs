using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // The games' art from the Xbox store on their tiles, as the console shows it: a square tile its box art, a tall one
    // (a Group's cards) its poster, else the cover. Each library game is asked for once a session, the most recently
    // played first; art arriving shows on every tile at once; Personalisation's Xbox store art, on by default, turns it
    // all off and back on, saved and shared by every profile.
    public class XboxArtTests
    {
        private static readonly XboxArt HaloArt = new XboxArt(@"C:\art\halo.box.jpg", @"C:\art\halo.poster.jpg");

        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 11, 11, 20, 0, 0));
        private readonly FakeSecretStore secrets = new FakeSecretStore();
        private readonly FakeXboxArt xboxArt = new FakeXboxArt();
        private readonly Guid halo = Guid.NewGuid();
        private readonly Guid hades = Guid.NewGuid();
        private readonly Guid brotato = Guid.NewGuid();
        private ExtensionCore core;

        public XboxArtTests()
        {
            NewCore();
        }

        /// <summary>A core made afresh over the same store and port, as when Playnite starts again.</summary>
        private void NewCore()
        {
            var windows = new FakeWindowSystem();
            core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), new FakeSoundPlayer(), new FakeDrives(), new FakeUserAccount(),
                new FakeAudio(), new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), secrets, new FakeDownloads(),
                xboxArt: xboxArt);
        }

        /// <summary>Halo played last, then Hades, and Brotato never; all three favourites, so the Favourites Group shows them.</summary>
        private FakeLibrary Library(string haloName = "Halo Infinite") => new FakeLibrary()
            .WithGame(brotato, "Brotato", added: new DateTime(2026, 9, 1), coverImage: @"C:\covers\brotato.jpg", favorite: true)
            .WithGame(hades, "Hades", lastActivity: new DateTime(2026, 11, 1), coverImage: @"C:\covers\hades.jpg", favorite: true)
            .WithGame(halo, haloName, lastActivity: new DateTime(2026, 11, 10), coverImage: @"C:\covers\halo.jpg", favorite: true);

        private HomeTile RecentTile(Guid game) => core.Home.RecentRow.Single(t => t.GameId == game);

        private HomeTile GroupTile(Guid game) => core.Home.Groups.Single().Tiles.Single(t => t.GameId == game);

        private void Found(Guid game, XboxArt art) => core.OnXboxArt(new Dictionary<Guid, XboxArt> { [game] = art });

        [Fact]
        public void It_is_on_by_default_and_nothing_goes_to_the_store()
        {
            Assert.True(StartUpSettingsState.XboxArtByDefault);
            Assert.True(core.Settings.StartUp.XboxArt);
            Assert.False(secrets.Values.ContainsKey("StartUp"));
        }

        [Fact]
        public void Each_library_game_is_asked_for_the_most_recently_played_first_then_the_most_recently_added()
        {
            core.OnLibraryChanged(Library().Snapshot());

            Assert.Equal(new[] { (halo, "Halo Infinite"), (hades, "Hades"), (brotato, "Brotato") }, xboxArt.Looked);
        }

        [Fact]
        public void A_game_is_asked_for_once_a_session_however_often_the_library_is_pushed()
        {
            core.OnLibraryChanged(Library().Snapshot());
            core.OnLibraryChanged(Library().Snapshot());

            Assert.Equal(3, xboxArt.Looked.Count);
        }

        [Fact]
        public void A_game_renamed_is_asked_for_again_by_its_new_name()
        {
            core.OnLibraryChanged(Library().Snapshot());
            core.OnLibraryChanged(Library("Halo Infinite (Campaign)").Snapshot());

            Assert.Equal((halo, "Halo Infinite (Campaign)"), xboxArt.Looked.Last());
            Assert.Equal(4, xboxArt.Looked.Count);
        }

        [Fact]
        public void Without_Xbox_art_every_tile_shows_its_cover()
        {
            core.OnLibraryChanged(Library().Snapshot());

            Assert.Equal(@"C:\covers\halo.jpg", RecentTile(halo).Art);
            Assert.Equal(@"C:\covers\halo.jpg", GroupTile(halo).Art);
        }

        [Fact]
        public void Art_found_shows_at_once_box_art_on_square_tiles_and_the_poster_on_a_Groups_tall_cards()
        {
            core.OnLibraryChanged(Library().Snapshot());

            Found(halo, HaloArt);

            Assert.Equal(HaloArt.BoxArt, RecentTile(halo).Art);
            Assert.Equal(HaloArt.Poster, GroupTile(halo).Art);
            Assert.Equal(@"C:\covers\halo.jpg", RecentTile(halo).CoverImage);
            Assert.Equal(HaloArt.BoxArt, core.MyGames.RecentlyInstalled.Single(t => t.GameId == halo).Art);
            Assert.Equal(HaloArt.BoxArt, core.MyGames.PlayHistory.SelectMany(s => s.Entries).Single(e => e.Tile.GameId == halo).Tile.Art);

            // The others keep their covers.
            Assert.Equal(@"C:\covers\hades.jpg", RecentTile(hades).Art);
        }

        [Fact]
        public void A_game_with_only_one_shape_shows_its_cover_on_the_other()
        {
            core.OnLibraryChanged(Library().Snapshot());

            Found(halo, new XboxArt(HaloArt.BoxArt, null));

            Assert.Equal(HaloArt.BoxArt, RecentTile(halo).Art);
            Assert.Equal(@"C:\covers\halo.jpg", GroupTile(halo).Art);
        }

        [Fact]
        public void Searchs_tiles_show_the_box_art()
        {
            core.OnLibraryChanged(Library().Snapshot());
            core.OpenSearch();

            Found(halo, HaloArt);

            Assert.Equal(HaloArt.BoxArt, core.Search.RecentlyAdded.Single(t => t.GameId == halo).Art);
        }

        [Fact]
        public void Art_found_to_be_none_takes_the_tiles_back_to_the_cover()
        {
            core.OnLibraryChanged(Library().Snapshot());
            Found(halo, HaloArt);

            Found(halo, null);

            Assert.Equal(@"C:\covers\halo.jpg", RecentTile(halo).Art);
        }

        [Fact]
        public void Art_found_again_the_same_keeps_the_rows()
        {
            core.OnLibraryChanged(Library().Snapshot());
            Found(halo, HaloArt);
            var row = core.Home.RecentRow;
            var raised = 0;
            core.XboxArtChanged += () => raised++;

            Found(halo, new XboxArt(HaloArt.BoxArt, HaloArt.Poster));

            Assert.Same(row, core.Home.RecentRow);
            Assert.Equal(0, raised);
        }

        [Fact]
        public void Art_saved_on_an_earlier_start_is_on_the_first_tiles_built()
        {
            xboxArt.SavedArt[halo] = HaloArt;
            NewCore();

            core.OnLibraryChanged(Library().Snapshot());

            Assert.Equal(HaloArt.BoxArt, RecentTile(halo).Art);
            Assert.Equal(HaloArt.Poster, GroupTile(halo).Art);
        }

        [Fact]
        public void The_themes_tiles_read_the_Xbox_art_alone_of_their_shape_and_are_told_when_it_comes()
        {
            core.OnLibraryChanged(Library().Snapshot());
            var raised = 0;
            core.XboxArtChanged += () => raised++;
            Assert.Null(core.XboxArtFor(halo, TileShape.Square));

            Found(halo, HaloArt);

            Assert.Equal(1, raised);
            Assert.Equal(HaloArt.BoxArt, core.XboxArtFor(halo, TileShape.Square));
            Assert.Equal(HaloArt.Poster, core.XboxArtFor(halo, TileShape.Tall));
            Assert.Null(core.XboxArtFor(hades, TileShape.Square));
        }

        [Fact]
        public void Off_every_tile_shows_its_cover_at_once_and_the_store_is_asked_nothing_more()
        {
            core.OnLibraryChanged(Library().Snapshot());
            Found(halo, HaloArt);
            var raised = 0;
            core.XboxArtChanged += () => raised++;

            core.SetXboxArt(false);

            Assert.False(core.Settings.StartUp.XboxArt);
            Assert.Equal(@"C:\covers\halo.jpg", RecentTile(halo).Art);
            Assert.Equal(@"C:\covers\halo.jpg", GroupTile(halo).Art);
            Assert.Null(core.XboxArtFor(halo, TileShape.Square));
            Assert.Equal(1, raised);
            Assert.Equal(1, xboxArt.Stops);

            // A game added while it is off isn't asked for.
            var asked = xboxArt.Looked.Count;
            core.OnLibraryChanged(Library().WithGame("Cuphead", lastActivity: new DateTime(2026, 11, 11)).Snapshot());
            Assert.Equal(asked, xboxArt.Looked.Count);
        }

        [Fact]
        public void On_again_the_art_comes_back_and_every_game_is_asked_for_again()
        {
            core.OnLibraryChanged(Library().Snapshot());
            Found(halo, HaloArt);
            core.SetXboxArt(false);
            xboxArt.Looked.Clear();

            core.SetXboxArt(true);

            Assert.Equal(HaloArt.BoxArt, RecentTile(halo).Art);
            Assert.Equal(new[] { halo, hades, brotato }, xboxArt.Looked.Select(l => l.GameId));
        }

        [Fact]
        public void Art_found_while_it_is_off_waits_for_it_to_be_on()
        {
            core.OnLibraryChanged(Library().Snapshot());
            core.SetXboxArt(false);

            Found(halo, HaloArt);
            Assert.Equal(@"C:\covers\halo.jpg", RecentTile(halo).Art);

            core.SetXboxArt(true);
            Assert.Equal(HaloArt.BoxArt, RecentTile(halo).Art);
        }

        [Fact]
        public void Off_is_saved_and_kept_across_a_restart_asking_nothing()
        {
            core.SetXboxArt(false);
            NewCore();
            xboxArt.Looked.Clear();

            core.OnLibraryChanged(Library().Snapshot());

            Assert.False(core.Settings.StartUp.XboxArt);
            Assert.Empty(xboxArt.Looked);
        }

        [Fact]
        public void Set_again_to_what_was_chosen_saves_nothing()
        {
            core.SetXboxArt(false);
            var writes = secrets.Writes;

            core.SetXboxArt(false);

            Assert.Equal(writes, secrets.Writes);
        }
    }
}
