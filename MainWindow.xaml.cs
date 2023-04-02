using System;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using Application = System.Windows.Application;
using Size = System.Drawing.Size;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;
using Axios.Properties;

namespace Axios
{
    public partial class MainWindow : Window
    {
        private Window mainWindow;
        public static NotifyIcon NotifyIcon { get; set; }
        private bool _runInBackgroundShowed;
        private bool _isExiting;
        public static RadioPage RadioPage { get; set; }
        public static SettingsPage SettingsPage {get; set; }
        public static SidePanel SidePanel {get; set; }
        public static Settings AppSettings { get; set; }

        public Frame MWContentFrame
        {
            get { return ContentFrame; }
            set { ContentFrame = value; }
        }

        public MainWindow()
        {
            InitializeComponent();
            AppSettings = new Settings();
            mainWindow = this;
            MWContentFrame.NavigationUIVisibility = NavigationUIVisibility.Hidden;
            Application.Current.Exit += OnApplicationExit;
            Closing += Window_Closing;
            RadioPage = new RadioPage();
            SettingsPage = new SettingsPage();
            SidePanel = new SidePanel(this);
            try
            {
                ContentFrame.Content = RadioPage;
                SidePanelFrame.Content = SidePanel;
            }
            catch (Exception)
            {
                throw new Exception("Failed to load");
            }

            InitializeSystemTray();
        }

        private void OnApplicationExit(object sender, ExitEventArgs e)
        {
            _isExiting = true;

            // Save favorite stations
            AppSettings.FavoriteStations = RadioPage.GetFavoriteStationsAsCollection();
            Settings.Default.Save();

            // Save last station
            AppSettings.LastStation = RadioPage.GetCurrentStationAsCollection();
            AppSettings.Save();

            RadioPage.StopRadio();

            NotifyIcon.Dispose();
        }

        private async void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (Settings.Default.MinimizeOnExit)
            {
                e.Cancel = true;
                Hide();
                if (_runInBackgroundShowed == false)
                {
                    await Task.Delay(1000);
                    await Task.Run(() =>
                    {
                        if (_isExiting == false) 
                        {
                            NotifyIcon.ShowBalloonTip(500, "Axios", "Axios will continue to run in the background.", ToolTipIcon.Info);
                            _runInBackgroundShowed = true;
                        }
                    });
                }
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        private void InitializeSystemTray()
        {
            NotifyIcon = new NotifyIcon();
            NotifyIcon.Icon = new Icon("Axios_icon.ico");
            NotifyIcon.Text = "Axios";
            NotifyIcon.MouseClick += NotifyIcon_Click;
            NotifyIcon.ContextMenuStrip = new ContextMenuStrip();

            NotifyIcon.ContextMenuStrip.AutoSize = false;
            NotifyIcon.ContextMenuStrip.Size = new Size(150, 115);
            NotifyIcon.ContextMenuStrip.ImageScalingSize = new Size(0, 0);

            NotifyIcon.ContextMenuStrip.Items.Add(new ToolStripButton("Play/Pause", null, OnPlayPauseClick)
            { AutoSize = false, Dock = DockStyle.Left, Width = 110, TextAlign = ContentAlignment.MiddleLeft });

            NotifyIcon.ContextMenuStrip.Items.Add(new ToolStripButton("Volume Up (+2)", null, OnVolumeUpClick)
            { AutoSize = false, Dock = DockStyle.Left, Width = 110, TextAlign = ContentAlignment.MiddleLeft });

            NotifyIcon.ContextMenuStrip.Items.Add(new ToolStripButton("Volume Down (-2)", null, OnVolumeDownClick)
            { AutoSize = false, Dock = DockStyle.Left, Width = 110, TextAlign = ContentAlignment.MiddleLeft });

            NotifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

            NotifyIcon.ContextMenuStrip.Items.Add(new ToolStripButton("Exit", null, OnExitClick)
            { AutoSize = false, Dock = DockStyle.Left, Width = 110, TextAlign = ContentAlignment.MiddleLeft });

            NotifyIcon.Visible = true;
        }

        // EVENTS
        // -- Tray Icon
        private void NotifyIcon_Click(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                WindowState = WindowState.Normal;
                Show();
                ShowInTaskbar = true;
                Activate();
            }
        }

        private void OnPlayPauseClick(object? sender, EventArgs e)
        {
            RadioPage.StopRadio();
        }

        private void OnVolumeUpClick(object? sender, EventArgs e)
        {
            if (RadioPage.AudioPlayer != null) { RadioPage.AudioSlider.Value += 2; }
        }

        private void OnVolumeDownClick(object? sender, EventArgs e)
        {
            if (RadioPage.AudioPlayer != null) { RadioPage.AudioSlider.Value -= 2; }
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        // -- Custom Title Bar
        private void UIElement_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        { 
            if (e.ChangedButton == MouseButton.Left)
            {
                if (Application.Current.MainWindow != null) Application.Current.MainWindow.DragMove();
            }
        }

        private void MinimizeButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            mainWindow.WindowState = WindowState.Minimized;
        }

        private void CloseButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Window_Closing(sender, new CancelEventArgs());
        }
    }
}