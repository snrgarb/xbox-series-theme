using System.Windows;
using System.Windows.Controls;

namespace XboxSeries.Extension
{
    /// <summary>The status area: the controller's battery and the clock. It follows its <see cref="Core.StatusState"/> and holds no logic.</summary>
    public partial class StatusArea : UserControl
    {
        public static readonly DependencyProperty StackedProperty =
            DependencyProperty.Register(nameof(Stacked), typeof(bool), typeof(StatusArea), new PropertyMetadata(false));

        public StatusArea()
        {
            InitializeComponent();
        }

        /// <summary>The Guide's layout: the battery above a large clock, in place of Home's row.</summary>
        public bool Stacked
        {
            get => (bool)GetValue(StackedProperty);
            set => SetValue(StackedProperty, value);
        }
    }
}
