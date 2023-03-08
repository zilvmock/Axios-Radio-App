using System.Collections.Generic;
using System.Threading;
using System;
using System.Windows;
using Axios.data;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Forms;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Collections.Specialized;
using Size = System.Drawing.Size;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace Axios
{
    public partial class MainWindow : Window
    {
        private Player _player;
        private static Thread? PlayerThread;
        private Search _search;
        private string _prevStationURL = string.Empty;
        private string _prevStationArtURL = string.Empty;
        private string _prevStationName = string.Empty;
        private ImageSource? _prevStationImageSource;
        private DataGridRow _prevStationRow;
        private object _backgroundLock = new object();
        private NotifyIcon _notifyIcon;
        private bool _RunInBackgroundShowed = false;
        private bool _isExiting = false;
        private List<Tuple<string, string, string, string, int>> _favoriteStations;


        public MainWindow()
        {
            InitializeComponent();
            System.Windows.Application.Current.Exit += OnApplicationExit;
            Closing += Window_Closing;
            InitializeUI();
            StationArt.ClearTemp();
            InitializeSystemTray();
        }

        private void OnApplicationExit(object sender, ExitEventArgs e)
        {
            if (_notifyIcon != null) { _notifyIcon.Dispose(); }
            _isExiting = true;

            // Save favorite stations
            if (_favoriteStations != null)
            {
                Properties.Settings settings = new Properties.Settings();
                StringCollection favoriteStationsCollection = new StringCollection();
                foreach (var station in _favoriteStations)
                {
                    favoriteStationsCollection.Add(
                        string.Format("{0},{1},{2},{3},{4}", station.Item1, station.Item2, station.Item3, station.Item4, station.Item5)
                    );
                }

                Properties.Settings.Default.FavoriteStations = favoriteStationsCollection;
                Properties.Settings.Default.Save();
            }
        }

        private async void Window_Closing([AllowNull] object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (SettingsWindow.MinimizeWhenClosing == true)
            {
                e.Cancel = true;
                Hide();
                if (_RunInBackgroundShowed == false)
                {
                    await Task.Delay(1000);
                    await Task.Run(() =>
                    {
                        if (_isExiting == false) 
                        {
                            _notifyIcon.ShowBalloonTip(500, "Axios", "Axios will continue to run in the background.", ToolTipIcon.Info);
                            _RunInBackgroundShowed = true;
                        }
                    });
                }
            }
        }

        private void InitializeUI()
        {
            Audio_Volume_Label.Content = Player.defaultVolume * 100;
            AudioSlider.Value = Player.defaultVolume * 100;
            Stop_Radio_Button.Content = "▶";
            Stop_Radio_Button.IsEnabled = false;
            Now_Playing_Label.Visibility = Visibility.Hidden;
            Favorite_Remove_Button.Visibility = Visibility.Collapsed;
            _ = GatherStationsByVotesAsync();

            // Grab favorite stations
            if (Properties.Settings.Default.FavoriteStations == null) { return; }
            if (Properties.Settings.Default.FavoriteStations.Count > 0)
            {
                _favoriteStations = new List<Tuple<string, string, string, string, int>>();
                foreach (var station in Properties.Settings.Default.FavoriteStations)
                {
                    if (station == null) { continue; }
                    string[] stationData = station.Split(',');
                    _favoriteStations.Add(new Tuple<string, string, string, string, int>(stationData[0], stationData[1], stationData[2], stationData[3], int.Parse(stationData[4])));
                }
            }
            else
            {
                _favoriteStations = new List<Tuple<string, string, string, string, int>>();
            }
        }

        private void InitializeSystemTray()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = new Icon("Axios_icon.ico");
            _notifyIcon.Text = "Axios";
            _notifyIcon.MouseClick += NotifyIcon_Click;
            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();

            _notifyIcon.ContextMenuStrip.AutoSize = false;
            _notifyIcon.ContextMenuStrip.Size = new Size(150, 115);
            _notifyIcon.ContextMenuStrip.ImageScalingSize = new Size(0, 0);

            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripButton("Play/Pause", null, OnPlayPauseClick)
            { AutoSize = false, Dock = DockStyle.Left, Width = 110, TextAlign = ContentAlignment.MiddleLeft });

            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripButton("Volume Up (+2)", null, OnVolumeUpClick)
            { AutoSize = false, Dock = DockStyle.Left, Width = 110, TextAlign = ContentAlignment.MiddleLeft });

            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripButton("Volume Down (-2)", null, OnVolumeDownClick)
            { AutoSize = false, Dock = DockStyle.Left, Width = 110, TextAlign = ContentAlignment.MiddleLeft });

            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripButton("Exit", null, OnExitClick)
            { AutoSize = false, Dock = DockStyle.Left, Width = 110, TextAlign = ContentAlignment.MiddleLeft });

            _notifyIcon.Visible = true;
        }

        private async Task FormatResultsAsync(List<Tuple<string, string, string, string, int>> RadioStations)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                Stations_DataGrid.IsEnabled = false;

                if (Stations_DataGrid.ItemsSource == null)
                {
                    DataGridTextColumn column1 = new DataGridTextColumn
                    {
                        Header = "CC",
                        Binding = new System.Windows.Data.Binding("Item4")
                    };

                    DataGridTextColumn column2 = new DataGridTextColumn
                    {
                        Header = "Votes",
                        Binding = new System.Windows.Data.Binding("Item5")
                    };

                    DataGridTextColumn column3 = new DataGridTextColumn
                    {
                        Header = "Station Name",
                        Binding = new System.Windows.Data.Binding("Item2"),
                        Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                    };

                    Stations_DataGrid.Columns.Add(column1);
                    Stations_DataGrid.Columns.Add(column2);
                    Stations_DataGrid.Columns.Add(column3);
                }

                Stations_DataGrid.ItemsSource = RadioStations;
                Stations_DataGrid.IsEnabled = true;

            });
        }

        private async Task GatherStationsByNameAsync()
        {
            if (_search == null) { _search = new Search(); }
            List<Tuple<string, string, string, string, int>> RadioStations = await _search.GetByName(Station_Search_TextBox.Text);
            await FormatResultsAsync(RadioStations);
        }

        private async Task GatherStationsByVotesAsync()
        {
            if (_search == null) { _search = new Search(); }
            List<Tuple<string, string, string, string, int>> RadioStations = await _search.GetByVotes();
            await FormatResultsAsync(RadioStations);
        }

        private async Task StartPlayerAsync(DataGridRow stationRow)
        {
            if (stationRow == null) { return; }

            string url = string.Empty;
            string name = string.Empty;
            string artURL = string.Empty;

            try
            {
                var selectedStation = (Tuple<string, string, string, string, int>)stationRow.DataContext;
                url = selectedStation.Item1;
                name = selectedStation.Item2;
                artURL = selectedStation.Item3;
            }
            catch (Exception) { return; }

            if (_prevStationURL.Equals(url) || string.IsNullOrEmpty(url)) { return; }

            if (PlayerThread != null)
            {
                _player.EndPlaying();
                PlayerThread.Join();
            }

            try
            {
                await Task.Run(() =>
                {
                    _player = new Player(url);
                    PlayerThread = new Thread(_player.StartPlaying);
                    PlayerThread.Start();
                });

                UpdatePlayerUIAsync(name, artURL);

                UpdateStationBackgroundToCorrect(stationRow);
            }
            catch (Exception)
            {
                PlayerThread?.Join();
                _notifyIcon.ShowBalloonTip(500, "Axios", $"Cannot play {name} right now...", ToolTipIcon.None);

                UpdateStationBackgroundToFailed(stationRow);

                if (_prevStationRow != null)
                {
                    _player.ResumePlaying();
                    _ = StartPlayerAsync(_prevStationRow);
                    return;
                }
            }
            finally
            {
                _prevStationURL = url;
                _prevStationArtURL = artURL;
                _prevStationName = name;
                _prevStationRow = stationRow;
            }
        }

        private void StopRadio()
        {
            if (_player == null) { return; }
            if (_player.IsPlaying())
            {
                _player.PausePlaying();
                Stop_Radio_Button.Dispatcher.Invoke(() =>
                {
                    Stop_Radio_Button.Content = "▶";
                    Stop_Radio_Button.ToolTip = "Resume";
                });
            }
            else
            {
                _player.ResumePlaying();
                Stop_Radio_Button.Dispatcher.Invoke(() =>
                {
                    Stop_Radio_Button.Content = "◼️";
                    Stop_Radio_Button.ToolTip = "Pause";
                });
            }
        }

        private async void UpdatePlayerUIAsync(string name, string artURL)
        {
            Stop_Radio_Button.Dispatcher.Invoke(() =>
            {
                _player.SetVolume((float)AudioSlider.Value / 100);
                Audio_Volume_Label.Content = AudioSlider.Value;
                Audio_Volume_Label.Visibility = Visibility.Visible;
                double volume = Math.Round((float)_player.GetVolume() * 100);
                Audio_Volume_Label.Content = volume;
                AudioSlider.Value = volume;

                Stop_Radio_Button.Visibility = Visibility.Visible;
                Stop_Radio_Button.Content = "◼️";
                Stop_Radio_Button.ToolTip = "Pause";
                Stop_Radio_Button.IsEnabled = true;

                Now_Playing_Label.Content = name;
                Now_Playing_Label.Visibility = Visibility.Visible;
            });

            if (artURL == string.Empty)
            {
                Station_FavIcon_Image.Dispatcher.Invoke(() =>
                {
                    Station_FavIcon_Image.Visibility = Visibility.Collapsed;
                });

                return;
            }

            ImageSource? imageSource = await new StationArt(artURL).GetImageAsync();
            if (imageSource == null)
            {
                Station_FavIcon_Image.Dispatcher.Invoke(() =>
                {
                    Station_FavIcon_Image.Visibility = Visibility.Collapsed;
                });
            }
            else
            {
                Station_FavIcon_Image.Dispatcher.Invoke(() =>
                {
                    Station_FavIcon_Image.Visibility = Visibility.Visible;
                    Station_FavIcon_Image.Source = imageSource;
                });
                _prevStationImageSource = imageSource;
            }
        }

        private void UpdateStationBackgroundToCorrect(DataGridRow stationRow)
        {
            if (stationRow == null) { return; }

            lock (_backgroundLock)
            {
                if (_prevStationRow == null)
                {
                    stationRow.Dispatcher.Invoke(() =>
                    {
                        stationRow.Background = new SolidColorBrush(Colors.Orange);
                    });
                }
                if (stationRow != _prevStationRow && _prevStationRow != null)
                {
                    _prevStationRow.Dispatcher.Invoke(() =>
                    {
                        _prevStationRow.Background = new SolidColorBrush(Colors.White);
                    });
                    stationRow.Dispatcher.Invoke(() =>
                    {
                        stationRow.Background = new SolidColorBrush(Colors.Orange);
                    });
                }

                foreach (object item in Stations_DataGrid.Items)
                {
                    var row = Stations_DataGrid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
                    if (row != null &&
                        item is Tuple<string, string, string, string, int> tuple &&
                        row != stationRow &&
                        row.Background != System.Windows.Media.Brushes.Yellow)
                    {
                        row.Dispatcher.Invoke(() =>
                        {
                            row.Background = System.Windows.Media.Brushes.White;
                        });
                    }
                }
            }
        }

        private void UpdateStationBackgroundToFailed(DataGridRow stationRow)
        {
            stationRow.Dispatcher.Invoke(() =>
            {
                stationRow.Background = new SolidColorBrush(Colors.Yellow);
            });
        }

        private DataGridRow? GetSelectedRow(object sender)
        {
            if (!(sender is DataGrid dataGrid)) { return null; }
            if (Stations_DataGrid.SelectedItem == null) { return null; }
            object selected = ((DataGrid)sender).SelectedItem;
            return (DataGridRow)((DataGrid)sender).ItemContainerGenerator.ContainerFromItem(selected);
        }


        // EVENTS
        // -- Tray Icon
        private void NotifyIcon_Click([AllowNull] object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                WindowState = WindowState.Normal;
                Show();
                ShowInTaskbar = true;
                Activate();
            }
        }

        private void OnPlayPauseClick([AllowNull] object sender, EventArgs e)
        {
            StopRadio();
        }

        private void OnVolumeUpClick([AllowNull] object sender, EventArgs e)
        {
            if (_player != null) { AudioSlider.Value += 2; }
        }

        private void OnVolumeDownClick([AllowNull] object sender, EventArgs e)
        {
            if (_player != null) { AudioSlider.Value -= 2; }
        }

        private void OnExitClick([AllowNull] object sender, EventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }


        // -- Player
        private void Stop_Radio_Button_Click(object sender, RoutedEventArgs e)
        {
            StopRadio();
        }

        private void AudioSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Audio_Volume_Label.Dispatcher.Invoke(() =>
            {
                Audio_Volume_Label.Content = AudioSlider.Value;
            });
            
            if (_player != null) { _player.SetVolume((float)AudioSlider.Value / 100); }
        }


        // -- Stations and UI
        private void Search_Button_Click(object sender, RoutedEventArgs e)
        {
            Favorite_Remove_Button.Visibility = Visibility.Collapsed;
            Favorite_Add_Button.Visibility = Visibility.Visible;
            _ = GatherStationsByNameAsync();
        }

        private void Top100_Button_Click(object sender, RoutedEventArgs e)
        {
            Favorite_Remove_Button.Visibility = Visibility.Collapsed;
            Favorite_Add_Button.Visibility = Visibility.Visible;
            _ = GatherStationsByVotesAsync();
        }

        private void Settings_Button_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = new SettingsWindow();
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            if (mainWindow == null) { return; }
            settingsWindow.Owner = mainWindow;
            mainWindow.IsEnabled = false;
            settingsWindow.Closed += (s, args) => mainWindow.IsEnabled = true;
            settingsWindow.Show();
        }

        private void Favorite_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_favoriteStations != null) 
            {
                _ = FormatResultsAsync(_favoriteStations); 
                Favorite_Remove_Button.Visibility = Visibility.Visible; 
                Favorite_Add_Button.Visibility = Visibility.Collapsed; 
            }
        }

        private void Stations_DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridRow? row = GetSelectedRow(sender);
            if (row != null) { _ = StartPlayerAsync(row); }
        }

        private void Stations_DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) { return; }
            e.Handled = true;
            DataGridRow? row = GetSelectedRow(sender);
            if (row != null) { _ = StartPlayerAsync(row); }
        }

        private void MenuItem_Add_Click(object sender, RoutedEventArgs e)
        {
            if (_favoriteStations == null) { _favoriteStations = new List<Tuple<string, string, string, string, int>>(); }
            var row = Stations_DataGrid.SelectedItem as Tuple<string, string, string, string, int>;
            if (row != null && ! _favoriteStations.Contains(row)) { _favoriteStations.Add(row); }
        }

        private void MenuItem_Remove_Click(object sender, RoutedEventArgs e)
        {
            if (_favoriteStations == null) { return; }
            var row = Stations_DataGrid.SelectedItem as Tuple<string, string, string, string, int>;
            if (row != null && _favoriteStations.Contains(row)) 
            { 
                _favoriteStations.Remove(row);
                Stations_DataGrid.Items.Refresh(); 
            }
        }

        private void Station_Search_TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { _ = GatherStationsByNameAsync(); }
        }

        private void Stations_DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (!e.Row.IsSelected) { e.Row.Background = System.Windows.Media.Brushes.White; }
        }

    }
}