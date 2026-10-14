using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>A wide dialog, as the Guide draws Power. It follows its <see cref="Dialog"/> and holds no logic.</summary>
    public partial class WideDialogPanel : UserControl
    {
        public WideDialogPanel()
        {
            InitializeComponent();
        }

        /// <summary>The core the mouse reports to, in debug builds.</summary>
        internal ExtensionCore Core { get; set; }

        // Debug builds only: the mouse points at and picks options. Release keeps clicks to closing the Guide (#10).
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
