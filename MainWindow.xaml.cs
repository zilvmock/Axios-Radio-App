using System;
using System.Collections.Specialized;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using Application = System.Windows.Application;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;
using Axios.Properties;
using System.Threading;
using Axios.data;
using MessageBox = System.Windows.MessageBox;
using Window = System.Windows.Window;
using Brushes = System.Drawing.Brushes;

namespace Axios
{
    public partial class MainWindow : Window
    {
        private static Mutex _mutex = null;

        public Window mainWindow => this;
        public static NotifyIcon NotifyIcon { get; set; }
        private bool _runInBackgroundShowed;
        private bool _isExiting;
        public static RadioPage RadioPage { get; set; }
        public static SettingsPage SettingsPage { get; set; }
        public static SidePanel SidePanel { get; set; }
        public static Settings AppSettings { get; set; }

        //private NotifyIcon NotifyIcon;
        private ToolStripMenuItem _playPauseMenuItem;
        private Bitmap _playIcon;
        private Bitmap _pauseIcon;

        public Frame MWContentFrame
        {
            get { return ContentFrame; }
            set { ContentFrame = value; }
        }

        public MainWindow()
        {
            _mutex = new Mutex(true, "AxiosMutex", out var createdNew);

            if (!createdNew)
            {
                MessageBox.Show("Another instance of the application is already running.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }

            Data.Resources.InitializeTempDir();
            InitializeComponent();
            MWContentFrame.NavigationUIVisibility = NavigationUIVisibility.Hidden;
            AppSettings = Settings.Default;
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

            NotifyIcon = new NotifyIcon();
            _playIcon = GetIconFromUnicode('\uE768');
            _pauseIcon = GetIconFromUnicode('\uE769');

            InitializeSystemTray();
        }

        private void OnApplicationExit(object sender, ExitEventArgs e)
        {
            _isExiting = true;

            Settings.Default.FavoriteStations = RadioPage.GetFavoriteStationsAsCollection();
            Settings.Default.LastStation = RadioPage.GetCurrentStationAsCollection();
            Settings.Default.FirstLaunch = false;
            Settings.Default.LastVoteTime = RadioPage.Search.LastVoteTime;
            Settings.Default.LastVoteUUIDs = RadioPage.Search.LastVoteUUIDs;
            Settings.Default.LastVolume = (int)RadioPage.AudioSlider.Value;
            Settings.Default.Save();

            RadioPage.StopRadio();
            NotifyIcon.Dispose();
        }

        private async void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (AppSettings.MinimizeOnExit)
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
            //NotifyIcon = new NotifyIcon();
            NotifyIcon.Icon = new Icon("Axios_icon.ico");
            NotifyIcon.Text = "Axios";
            NotifyIcon.MouseClick += NotifyIcon_Click;
            NotifyIcon.ContextMenuStrip = new ContextMenuStrip();
            var renderer = new CustomToolStripRenderer();
            NotifyIcon.ContextMenuStrip.Renderer = renderer;

            NotifyIcon.ContextMenuStrip.Items.Add(
                _playPauseMenuItem = new ToolStripMenuItem("Play/Pause", _playIcon, OnPlayPauseClick)
                {
                    DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                    ImageScaling = ToolStripItemImageScaling.SizeToFit
                });

            NotifyIcon.ContextMenuStrip.Items.Add(
                new ToolStripMenuItem("Volume Up (+2)", GetIconFromUnicode('\uE994'), OnVolumeUpClick)
                {
                    DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                    ImageScaling = ToolStripItemImageScaling.SizeToFit
                });

            NotifyIcon.ContextMenuStrip.Items.Add(
                new ToolStripMenuItem("Volume Down (-2)", GetIconFromUnicode('\uE993'), OnVolumeDownClick)
                {
                    DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                    ImageScaling = ToolStripItemImageScaling.SizeToFit
                });

            NotifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

            NotifyIcon.ContextMenuStrip.Items.Add(
                new ToolStripMenuItem("Exit", null, OnExitClick)
                {
                    DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                    ImageScaling = ToolStripItemImageScaling.SizeToFit,
                });

            NotifyIcon.Visible = true;
        }

        public Bitmap GetIconFromUnicode(char unicodeChar)
        {
            Bitmap icon = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(icon))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.DrawString(unicodeChar.ToString(), new Font("Segoe MDL2 Assets", 12), Brushes.Black, new PointF(0, 0));
            }
            return icon;
        }

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
            if (RadioPage.AudioPlayer != null)
            {
                _playPauseMenuItem.Image = RadioPage.AudioPlayer.IsPlaying() ? _playIcon : _pauseIcon;
            }
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

    public class CustomToolStripRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var menuItem = e.Item as ToolStripMenuItem;
            if (menuItem != null && menuItem.Selected)
            {
                e.Graphics.FillRectangle(Brushes.LightGray, e.Item.ContentRectangle);
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }
    }
}