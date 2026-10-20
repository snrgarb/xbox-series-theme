using System.Windows;
using System.Windows.Controls;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// One Idle screen card: a game's art or an achievement's icon, a title line and a detail line. It holds no logic
    /// of what a card says; whoever holds it fades it in and out.
    /// </summary>
    public partial class IdleCardControl : UserControl
    {
        /// <summary>The art's decode width: the card's 640 at up to 4K's twice 1080p, rather than the hero's full screen.</summary>
        internal const int ArtDecodeWidth = 1280;

        /// <summary>A cover's decode width, standing on its blurred self: up to its 288 square at up to 4K.</summary>
        internal const int CoverDecodeWidth = 576;

        /// <summary>The icon's decode width: its 120 square at up to 4K.</summary>
        internal const int IconDecodeWidth = 240;

        private readonly HeroLayer art = new HeroLayer(ArtDecodeWidth);

        public IdleCardControl()
        {
            InitializeComponent();
            art.Root.Opacity = 1;
            Art.Children.Insert(0, art.Root);
        }

        /// <summary>Shows <paramref name="card"/>; for none, keeps the last, to fade out as it is.</summary>
        public void Show(IdleCard card)
        {
            if (card == null)
            {
                return;
            }

            art.Show(card.Art);
            Art.Visibility = card.Art != null ? Visibility.Visible : Visibility.Collapsed;
            var isCover = card.Art?.IsCover == true;
            CoverImage.Source = isCover ? DecodedImageConverter.Load(card.Art.Image, CoverDecodeWidth) : null;
            Cover.Visibility = isCover ? Visibility.Visible : Visibility.Collapsed;

            // An achievement's icon, or its trophy for none; not beside art, and never on a tip.
            var hasIcon = card.Art == null && (card.Icon != null || card.Kind == IdleCardKind.RecentUnlock);
            IconImage.ImageSource = hasIcon ? DecodedImageConverter.Load(card.Icon, IconDecodeWidth) : null;
            Icon.Visibility = hasIcon ? Visibility.Visible : Visibility.Collapsed;

            // A tip is read whole, as a prompt's question; a name is cut short on one line, as a menu's title.
            var isTip = card.Kind == IdleCardKind.Tip;
            Title.Text = card.Title;
            Title.TextWrapping = isTip ? TextWrapping.Wrap : TextWrapping.NoWrap;
            Detail.Text = card.Description;
            Detail.Visibility = string.IsNullOrEmpty(card.Description) ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
