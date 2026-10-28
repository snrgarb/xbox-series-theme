using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>A menu or prompt, as the Guide and the tile menu both draw it. It follows its <see cref="Dialog"/> and holds no logic.</summary>
    public partial class MenuPanel : UserControl
    {
        public MenuPanel()
        {
            InitializeComponent();
        }

        /// <summary>The core the mouse reports to, in debug builds.</summary>
        internal ExtensionCore Core { get; set; }

        // Debug builds only: the mouse points at and picks options. Release keeps clicks to closing the menu (#10).
        private void OnOptionMouseEnter(object sender, MouseEventArgs e)
        {
#if DEBUG
            Core?.FocusOption((DialogOption)((FrameworkElement)sender).DataContext);
#endif
        }

        private void OnOptionClick(object sender, MouseButtonEventArgs e)
        {
#if DEBUG
            Core?.ChooseOption((DialogOption)((FrameworkElement)sender).DataContext);
#endif
        }
    }
}
