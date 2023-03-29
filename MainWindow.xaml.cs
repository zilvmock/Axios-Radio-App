using System.Collections.Generic;
using System;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Specialized;
using Size = System.Drawing.Size;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace Axios
{
    public partial class MainWindow : Window
    {
        public static NotifyIcon NotifyIcon { get; set; }
        private bool _runInBackgroundShowed;
        private bool _isExiting;
        private RadioPage RP;
        public List<Tuple<string, string, string, string, int>> FavoriteStations { get; set; }


        public MainWindow()
        {
            InitializeComponent();
            System.Windows.Application.Current.Exit += OnApplicationExit;
            Closing += Window_Closing;
            RP = new RadioPage();
            try
            {
                System.Windows.Application.Current.MainWindow.Content = RP;
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
            if (FavoriteStations.Count < 1)
            {
                Properties.Settings settings = new Properties.Settings();
                StringCollection favoriteStationsCollection = new StringCollection();
                foreach (var station in FavoriteStations)
                {
                    favoriteStationsCollection.Add(
                        $"{station.Item1},{station.Item2},{station.Item3},{station.Item4},{station.Item5}"
                    );
                }

                Properties.Settings.Default.FavoriteStations = favoriteStationsCollection;
                Properties.Settings.Default.Save();
            }

            // Save last station
            Properties.Settings.Default.LastStation = RadioPage.GetCurrentStationAsCollection();
            Properties.Settings.Default.Save();

            NotifyIcon.Dispose();
        }

        private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Properties.Settings.Default.MinimizeOnExit)
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
            RP.StopRadio();
        }

        private void OnVolumeUpClick(object? sender, EventArgs e)
        {
            if (RP.AudioPlayer != null) { RP.AudioSlider.Value += 2; }
        }

        private void OnVolumeDownClick(object? sender, EventArgs e)
        {
            if (RP.AudioPlayer != null) { RP.AudioSlider.Value -= 2; }
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}