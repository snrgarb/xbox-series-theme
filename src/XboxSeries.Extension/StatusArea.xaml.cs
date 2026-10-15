using System.Windows.Controls;

namespace XboxSeries.Extension
{
    /// <summary>The status area: the controller's battery and the clock. It follows its <see cref="Core.StatusState"/> and holds no logic.</summary>
    public partial class StatusArea : UserControl
    {
        public StatusArea()
        {
            InitializeComponent();
        }
    }
}
