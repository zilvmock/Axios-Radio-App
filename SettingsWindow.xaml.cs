using System.Windows;

namespace Axios
{
    public partial class SettingsWindow : Window
    {
        public static bool MinimizeWhenClosing { get; set; } = false;

        public SettingsWindow()
        {
            InitializeComponent();
            Minimize_On_Shutdown_Checkbox.IsChecked = Properties.Settings.Default.MinimizeOnExit;
            Minimize_On_Shutdown_Checkbox.IsChecked = MinimizeWhenClosing;
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            MinimizeWhenClosing = !MinimizeWhenClosing;
            Properties.Settings.Default.MinimizeOnExit = MinimizeWhenClosing;
            Properties.Settings.Default.Save();
        }
    }
}
