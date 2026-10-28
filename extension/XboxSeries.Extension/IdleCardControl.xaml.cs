using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// One Idle screen card, the console's idle panel over the right half of the screen: its top line, title, label and
    /// description, a game's art or an achievement's icon in the lower half, and the Y prompt. It holds no logic of what a
    /// card says, and shows nothing the card leaves null; whoever holds it flies it in and fades it.
    /// </summary>
    public partial class IdleCardControl : UserControl
    {
        /// <summary>The art's decode width: the panel's half of the screen, 962 at 1080p, at up to 4K's twice that.</summary>
        internal const int ArtDecodeWidth = 1920;

        /// <summary>The cover's and the icon's decode width: up to their 320 square at up to 4K.</summary>
        internal const int PictureDecodeWidth = 640;

        /// <summary>A game's or an achievement's panel, as the console's (#303030 in its captures).</summary>
        internal static readonly SolidColorBrush GamePanel = Frozen(Color.FromRgb(0x30, 0x30, 0x30));

        /// <summary>A tip's panel, slate blue as the console's (#647184 in its capture).</summary>
        internal static readonly SolidColorBrush TipPanel = Frozen(Color.FromRgb(0x64, 0x71, 0x84));

        private readonly HeroLayer art = new HeroLayer(ArtDecodeWidth);

        public IdleCardControl()
        {
            InitializeComponent();
            art.Root.Opacity = 1;
            Art.Children.Insert(0, art.Root);
            PromptGlyph.Child = ButtonGlyphs.Create(ControllerButton.Y, 72, Brushes.White);
        }

        /// <summary>Shows <paramref name="card"/>; for none, keeps the last, to fade out as it is.</summary>
        public void Show(IdleCard card)
        {
            if (card == null)
            {
                return;
            }

            var isTip = card.Kind == IdleCardKind.Tip;
            Panel.Background = isTip ? TipPanel : GamePanel;

            // The top line: the Score on the left, with the percentage on the right, or the header there; with no Score,
            // the header on the left, light, as the console's "Just now". The bar under it for a progress.
            var hasScore = card.Score.HasValue;
            ScoreRow.Visibility = Seen(hasScore);
            Rare.Visibility = Seen(hasScore && card.IsRare);
            Score.Text = hasScore ? card.Score.Value.ToString(CultureInfo.CurrentCulture) : null;
            var hasProgress = card.Progress.HasValue;
            var progress = hasProgress ? Math.Max(0, Math.Min(1, card.Progress.Value)) : 0;
            Percent.Text = hasProgress ? Math.Round(progress * 100).ToString(CultureInfo.CurrentCulture) + "%" : null;
            Percent.Visibility = Seen(hasProgress);
            Header.Text = card.Header;
            Header.Visibility = Seen(!string.IsNullOrEmpty(card.Header) && !(hasScore && hasProgress));
            Header.HorizontalAlignment = hasScore ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            TopLine.Visibility = Seen(hasScore || hasProgress || Header.Visibility == Visibility.Visible);
            Bar.Visibility = Seen(hasProgress);
            Done.Width = new GridLength(progress, GridUnitType.Star);
            ToDo.Width = new GridLength(1 - progress, GridUnitType.Star);

            // A name in the console's large title, a few lines at most; a tip's text in the smaller, read whole.
            Title.FontSize = isTip ? 46 : 64;

            // A tip's buttons as their icons in its words, "{Y}" as the console's "(Y)".
            Title.Inlines.Clear();
            foreach (var part in isTip ? ButtonTokens.Parse(card.Title) : new[] { new ButtonTokens.Part(card.Title, null) })
            {
                Title.Inlines.Add(part.Button.HasValue
                    ? (Inline)ButtonGlyphs.Inline(part.Button.Value, Title.FontSize, Brushes.White)
                    : new Run(part.Text));
            }

            Title.LineHeight = isTip ? 60 : 76;
            Title.MaxHeight = isTip ? 600 : 228;
            Title.Margin = new Thickness(0, hasProgress ? 40 : 29, 37, 0);
            Label.Text = card.Label;
            Label.Visibility = Seen(!string.IsNullOrEmpty(card.Label));
            Description.Text = card.Description;
            Description.Visibility = Seen(!string.IsNullOrEmpty(card.Description));

            // The art rising out of the panel; over it, an achievement's icon, or else a cover standing in for art, whole.
            art.Show(card.Art);
            Art.Visibility = Seen(card.Art != null);
            var picture = card.Icon ?? (card.Art?.IsCover == true ? card.Art.Image : null);
            PictureImage.Source = DecodedImageConverter.Load(picture, PictureDecodeWidth);
            Picture.Width = card.Icon != null ? 320 : double.NaN;
            PictureImage.Stretch = card.Icon != null ? Stretch.UniformToFill : Stretch.Uniform;
            Picture.Visibility = Seen(PictureImage.Source != null);
            IconShade.Visibility = Seen(card.Icon != null);
            Controller.Visibility = Seen(isTip && card.Art == null);
            Highlight(isTip ? card.Buttons : new ControllerButton[0]);

            // A tip's prompt quieter, as the console's "Got it".
            PromptText.Text = card.Prompt;
            Prompt.Visibility = Seen(!string.IsNullOrEmpty(card.Prompt));
            Prompt.Opacity = isTip ? 0.6 : 1;
        }

        /// <summary>
        /// The controller's <paramref name="buttons"/> filled white with their marks, the rest outlined (see
        /// IdleCardControl.xaml): each button's part tagged Shape filled, one tagged Lit shown and one tagged Unlit hidden.
        /// </summary>
        private void Highlight(IReadOnlyList<ControllerButton> buttons)
        {
            var lit = new HashSet<string>(buttons.Select(ElementOf));
            foreach (var button in Controller.Children.OfType<Canvas>())
            {
                var on = lit.Contains(button.Name);
                foreach (var part in button.Children.OfType<FrameworkElement>())
                {
                    switch (part.Tag as string)
                    {
                        case "Shape":
                            ((Shape)part).Fill = on ? Brushes.White : null;
                            break;
                        case "Lit":
                            part.Visibility = Seen(on);
                            break;
                        case "Unlit":
                            part.Visibility = Seen(!on);
                            break;
                    }
                }
            }
        }

        /// <summary>The controller's element for <paramref name="button"/>, named as it is; the D-pad's four as one.</summary>
        internal static string ElementOf(ControllerButton button) =>
            button >= ControllerButton.DPadUp ? "DPad" : button.ToString();

        private static Visibility Seen(bool seen) => seen ? Visibility.Visible : Visibility.Collapsed;

        private static SolidColorBrush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
