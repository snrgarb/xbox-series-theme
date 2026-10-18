namespace XboxSeries.Core
{
    /// <summary>
    /// Full library's Sort dropdown and its filter by name. Both act on Playnite's main list under the user's own filter,
    /// with Playnite's own sort and name filter, so the user's filter kept for Games and given back as the page goes keeps
    /// the sort chosen here, as it keeps the tab. The name is only Full library's: it goes as the page moves to another
    /// nav item or closes, before anything keeps the user's filter, so it never outlives the page.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The sort the user chose on Full library's dropdown while the page is up; null until they choose one.</summary>
        private LibrarySortOption chosenLibrarySort;

        /// <summary>
        /// Sorts Full library's grid by <paramref name="option"/>, in place. The choice stays as the user switches tabs,
        /// whose presets can carry a sort of their own, until the page closes.
        /// </summary>
        public void SortFullLibrary(LibrarySortOption option)
        {
            if (!MyGames.ShowsFullLibrary || option == null)
            {
                return;
            }

            chosenLibrarySort = option;
            FollowLibrarySort();
        }

        /// <summary>
        /// Narrows Full library's grid to games whose name holds <paramref name="name"/>, as the user types it, on top of
        /// the tab; empty shows the whole tab again. Its count follows.
        /// </summary>
        public void FilterFullLibraryByName(string name)
        {
            name = name ?? string.Empty;
            if (!MyGames.ShowsFullLibrary || name == MyGames.NameFilter)
            {
                return;
            }

            playnite.FilterMainListByName(name);
            MyGames.NameFilter = name;
            RefreshFullLibraryCount();
        }

        /// <summary>
        /// The pad's A on Full library's name box: Playnite's on-screen keyboard, which narrows the grid as the user
        /// types, and leaves it as it was if they cancel.
        /// </summary>
        public void TypeFullLibraryName()
        {
            if (!MyGames.ShowsFullLibrary)
            {
                return;
            }

            MyGames.NameFilter = playnite.TypeMainListName();
            RefreshFullLibraryCount();
        }

        /// <summary>Clears Full library's name filter, B in its name box: the whole tab shows again.</summary>
        public void ClearFullLibraryName() => FilterFullLibraryByName(string.Empty);

        /// <summary>
        /// Full library's grid in the sort the user chose, put back if a tab's preset brought its own; with none chosen,
        /// the dropdown names Playnite's own sort.
        /// </summary>
        private void FollowLibrarySort()
        {
            if (!MyGames.ShowsFullLibrary)
            {
                return;
            }

            var chosen = chosenLibrarySort;
            if (chosen != null
                && (playnite.MainListSort != chosen.Sort || playnite.MainListSortDirection != chosen.Direction))
            {
                playnite.SortMainList(chosen.Sort, chosen.Direction);
            }

            MyGames.LibrarySort = chosen ?? LibrarySortOption.Of(playnite.MainListSort, playnite.MainListSortDirection);
        }

        /// <summary>
        /// Full library's name filter goes, off the main list: before the page moves to another nav item, which keeps
        /// the user's filter as it is, and as it closes, which gives it back for good.
        /// </summary>
        private void ForgetLibraryName()
        {
            if (MyGames.HasNameFilter && mainListFilter == null)
            {
                playnite.FilterMainListByName(string.Empty);
            }

            MyGames.NameFilter = string.Empty;
        }
    }
}
