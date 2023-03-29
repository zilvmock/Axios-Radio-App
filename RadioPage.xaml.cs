using Axios.data;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Axios.Properties;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;


namespace Axios
{
    public partial class RadioPage
    {
        public Player? AudioPlayer;
        private static Thread? _playerThread;
        private List<Tuple<string, string, string, string, int>> _radioStations;
        private Search? _search;
        private List<Tuple<string, string, string, string, int>> _currentStations;
        private string _prevStationUrl = string.Empty;
        private DataGridRow? _prevStationRow;
        public static DataGridRow CurrentStationRow;
        private DataGridRow _nextBtnStationRow;
        private DataGridRow _prevBtnStationRow;
        private int _currentPage = 1;
        private bool _isLastPage;
        private readonly int _stationsPerPage = 18;
        private object _backgroundLock = new();
        private bool _favoriteStationsIsShowing;
        private List<Tuple<string, string, string, string, int>> _favoriteStations;

        public RadioPage()
        {
            InitializeComponent();
            InitializeUI();
            StationArt.ClearTemp();
        }

        private async void InitializeUI()
        {
            AudioVolumeLabel.Content = Player.DefaultVolume * 100;
            AudioSlider.Value = Player.DefaultVolume * 100;
            StopPlayerImg.Source = new BitmapImage(new Uri("Assets/play.png", UriKind.Relative));
            PrevStationBtn.IsEnabled = false;
            NextStationBtn.IsEnabled = false;
            FavoriteRemoveBtn.Visibility = Visibility.Collapsed;
            await GatherStationsByVotesAsync();


            // Grab last station
            if (Settings.Default.LastStation.Count > 0)
            {
                StringCollection lastStation = Settings.Default.LastStation;
                string[]? items = lastStation[0]?.Split(',');
                if (items != null)
                {
                    Tuple<string, string, string, string, int> station = new Tuple<string, string, string, string, int>(items[0], items[1], items[2], items[3], int.Parse(items[4]));
                    DataGridRow lastStationRow = new DataGridRow { DataContext = station, Item = station };
                    CurrentStationRow = lastStationRow;
                    _ = StartPlayerAsync(lastStationRow, false);
                }
            }

            // Grab favorite stations
            if (Settings.Default.FavoriteStations == null) { return; }

            try
            {
                if (Settings.Default.FavoriteStations.Count > 0)
                {
                    _favoriteStations = new List<Tuple<string, string, string, string, int>>();
                    foreach (var station in Settings.Default.FavoriteStations)
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
            catch (Exception)
            {
                MainWindow.NotifyIcon.ShowBalloonTip(500, "Axios", "Failed to load Favorite Stations! If this happens again please report an issue.", ToolTipIcon.Error);
            }
        }

        private async Task FormatResultsAsync(List<Tuple<string, string, string, string, int>> radioStations, int page = 1)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                StationsDataGrid.IsEnabled = false;

                if (StationsDataGrid.ItemsSource == null)
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

                    StationsDataGrid.Columns.Add(column1);
                    StationsDataGrid.Columns.Add(column2);
                    StationsDataGrid.Columns.Add(column3);
                }

                int startIndex = (_stationsPerPage * page) - _stationsPerPage;

                if (_favoriteStationsIsShowing)
                {
                    StationsDataGrid.ItemsSource = radioStations.GetRange(startIndex, radioStations.Count);
                    FavoriteBtn.Dispatcher.Invoke(() =>
                    {
                        PrevDataGridPageBtn.Visibility = Visibility.Collapsed;
                        NextDataGridPageBtn.Visibility = Visibility.Collapsed;
                        CurrentDataGridPageLabel.Visibility = Visibility.Collapsed;
                    });
                }
                else
                {
                    FavoriteBtn.Dispatcher.Invoke(() =>
                    {
                        PrevDataGridPageBtn.Visibility = Visibility.Visible;
                        NextDataGridPageBtn.Visibility = Visibility.Visible;
                        CurrentDataGridPageLabel.Visibility = Visibility.Visible;
                    });

                    if (startIndex + _stationsPerPage >= _radioStations.Count)
                    {
                        StationsDataGrid.ItemsSource = _radioStations.GetRange(startIndex, _radioStations.Count - startIndex);
                    }
                    else
                    {
                        StationsDataGrid.ItemsSource = _radioStations.GetRange(startIndex, _stationsPerPage);
                    }
                }

                StationsDataGrid.IsEnabled = true;

            });
        }

        private async Task GatherStationsByNameAsync()
        {
            if (_search == null) { _search = new Search(); }
            _radioStations = await _search.GetByName(SearchTextBox.Text);
            await FormatResultsAsync(_radioStations);
        }

        private async Task GatherStationsByVotesAsync()
        {
            _search ??= new Search();
            _radioStations = await _search.GetByVotes();
            await FormatResultsAsync(_radioStations);
        }

        private async Task StartPlayerAsync(DataGridRow stationRow, bool autoPlay = true)
        {
            if (stationRow.Item == null) { return; }

            string url;
            string name;
            string artUrl;

            CurrentStationRow = stationRow;
            _ = SetPrevAndNextRows(stationRow);

            try
            {
                var selectedStation = (Tuple<string, string, string, string, int>)stationRow.DataContext;
                url = selectedStation.Item1;
                name = selectedStation.Item2;
                artUrl = selectedStation.Item3;
            }
            catch (Exception) { return; }

            if (_prevStationUrl.Equals(url) || string.IsNullOrEmpty(url)) { return; }

            if (_playerThread != null && AudioPlayer != null)
            {
                AudioPlayer.EndPlaying();
                //_playerThread.Join();
            }

            try
            {
                AudioPlayer = new Player(url);
                
                if (autoPlay)
                {
                    UpdatePlayerUiAsync(name, artUrl, true);
                }
                else
                {
                    UpdatePlayerUiAsync(name, artUrl);
                }

                await UpdateStationBackgroundToCorrect(stationRow);
                
                await Task.Run(() =>
                {
                    _playerThread = new Thread(AudioPlayer.StartPlaying);
                    if (autoPlay)
                    {
                        _playerThread.Start();
                    }
                });

            }
            catch (Exception)
            {
                _playerThread?.Join();
                MainWindow.NotifyIcon.ShowBalloonTip(500, "Axios", $"Cannot play {name} right now...", ToolTipIcon.None);

                UpdateStationBackgroundToFailed(stationRow);

                if (_prevStationRow != null)
                {
                    AudioPlayer?.ResumePlaying();
                    _ = StartPlayerAsync(_prevStationRow);
                }
            }
            finally
            {
                _prevStationUrl = url;
                _prevStationRow = stationRow;
            }
        }

        public void StopRadio()
        {
            if (AudioPlayer == null) { return; }
            if (AudioPlayer.IsPlaying())
            {
                AudioPlayer.PausePlaying();
                StopPlayerBtn.Dispatcher.Invoke(() =>
                {
                    StopPlayerImg.Source = new BitmapImage(new Uri("Assets/play.png", UriKind.Relative));
                    StopPlayerBtn.ToolTip = "Resume";
                });
            }
            else
            {
                AudioPlayer.ResumePlaying();
                StopPlayerBtn.Dispatcher.Invoke(() =>
                {
                    StopPlayerImg.Source = new BitmapImage(new Uri("Assets/pause.png", UriKind.Relative));
                    StopPlayerBtn.ToolTip = "Pause";
                });
            }
        }

        private async void UpdatePlayerUiAsync(string name, string artUrl, bool paused = false)
        {
            if (AudioPlayer == null) { return; }

            Dispatcher.Invoke(() =>
            {
                AudioPlayer.SetVolume((float)AudioSlider.Value / 100);
                AudioVolumeLabel.Content = AudioSlider.Value;
                AudioVolumeLabel.Visibility = Visibility.Visible;
                double volume = Math.Round((float)AudioPlayer.GetVolume() * 100);
                AudioVolumeLabel.Content = volume;
                AudioSlider.Value = volume;

                StopPlayerBtn.Visibility = Visibility.Visible;
                if (paused)
                {
                    StopPlayerImg.Source = new BitmapImage(new Uri("Assets/pause.png", UriKind.Relative));
                    StopPlayerBtn.ToolTip = "Pause";
                }
                else
                {
                    StopPlayerImg.Source = new BitmapImage(new Uri("Assets/play.png", UriKind.Relative));
                    StopPlayerBtn.ToolTip = "Resume";
                }
                StopPlayerBtn.IsEnabled = true;
                NowPlayingLabel.Content = name;
                NowPlayingLabel.Visibility = Visibility.Visible;
            });

            if (artUrl == string.Empty)
            {
                Dispatcher.Invoke(() =>
                {
                    StationFavIconImg.Source = new BitmapImage(new Uri("Axios_logo.png", UriKind.Relative));
                });

                return;
            }

            ImageSource? imageSource = await new StationArt(artUrl).GetImageAsync();
            if (imageSource == null)
            {
                StationFavIconImg.Dispatcher.Invoke(() =>
                {
                    StationFavIconImg.Source = new BitmapImage(new Uri("Axios_logo.png", UriKind.Relative));
                });
            }
            else
            {
                StationFavIconImg.Dispatcher.Invoke(() =>
                {
                    StationFavIconImg.Source = imageSource;
                });
            }
        }

        public Task UpdateStationBackgroundToCorrect(DataGridRow stationRow)
        {
            if (stationRow.Item == null)
            {
                return Task.CompletedTask;
            }

            lock (_backgroundLock)
            {
                if (_prevStationRow == null)
                {
                    Dispatcher.Invoke(() => { stationRow.Background = new SolidColorBrush(Color.FromRgb(248, 168, 106)); });
                }

                if (stationRow != _prevStationRow && _prevStationRow != null)
                {
                    Dispatcher.Invoke(() => { _prevStationRow.Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)); });
                    Dispatcher.Invoke(() => { stationRow.Background = new SolidColorBrush(Color.FromRgb(248, 168, 106)); });
                }
            }

            return Task.CompletedTask;
        }

        private void UpdateStationBackgroundToFailed(DataGridRow stationRow)
        {
            stationRow.Dispatcher.Invoke(() =>
            {
                stationRow.Background = new SolidColorBrush(Color.FromRgb(254, 244, 219));
            });
        }

        private DataGridRow? GetSelectedRow(object sender)
        {
            if (!(sender is DataGrid dataGrid)) { return null; }
            if (StationsDataGrid.SelectedItem == null) { return null; }
            object selected = ((DataGrid)sender).SelectedItem;
            return (DataGridRow)((DataGrid)sender).ItemContainerGenerator.ContainerFromItem(selected);
        }

        private async Task SetPrevAndNextRows(DataGridRow currentRow)
        {
            int selectedIndex = StationsDataGrid.Items.IndexOf(currentRow.Item);

            if (selectedIndex > 0)
            {
                _prevBtnStationRow =
                    (DataGridRow)StationsDataGrid.ItemContainerGenerator.ContainerFromItem(
                        StationsDataGrid.Items[selectedIndex - 1]);
            }

            if (selectedIndex < StationsDataGrid.Items.Count - 1)
            {
                _nextBtnStationRow = (DataGridRow)StationsDataGrid.ItemContainerGenerator.ContainerFromItem(
                    StationsDataGrid.Items[selectedIndex + 1]);
            }
        }

        private DataGridRow? GetFirstRow()
        {
            return (DataGridRow)StationsDataGrid.ItemContainerGenerator.ContainerFromItem(StationsDataGrid.Items[0]) ?? null;
        }

        private List<Tuple<string, string, string, string, int>> GetCurrentDataGrid()
        {
            return StationsDataGrid.Items.Cast<Tuple<string, string, string, string, int>>().ToList();
        }

        public static StringCollection GetCurrentStationAsCollection()
        {
            if (CurrentStationRow.Item == null) { return new StringCollection(); }
            StringCollection currentStation = new StringCollection();
            var currentRowTuple = CurrentStationRow.DataContext as Tuple<string?, string, string, string, int>;
            currentStation.Add($"{currentRowTuple.Item1},{currentRowTuple.Item2},{currentRowTuple.Item3},{currentRowTuple.Item4},{currentRowTuple.Item5}");

            return currentStation;
        }

        // EVENTS
        // -- Player
        private void StopPlayerBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (AudioPlayer != null) { StopRadio(); }
            else
            {
                // If no station is selected manually
                DataGridRow? firstRow = GetFirstRow();
                if (firstRow == null) { return; }
                _ = StartPlayerAsync(firstRow);
                Dispatcher.Invoke(() =>
                {
                    PrevStationBtn.IsEnabled = true;
                    NextStationBtn.IsEnabled = true;
                });
            }
        }

        private void AudioSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Dispatcher.Invoke(() => { AudioVolumeLabel.Content = AudioSlider.Value; });

            AudioPlayer?.SetVolume((float)AudioSlider.Value / 100);
        }

        // -- Stations and UI
        private void SearchButton_OnClick(object sender, RoutedEventArgs e)
        {
            FavoriteRemoveBtn.Visibility = Visibility.Collapsed;
            FavoriteAddBtn.Visibility = Visibility.Visible;
            _ = GatherStationsByNameAsync();
        }

        private void Top100Btn_OnClick(object sender, RoutedEventArgs e)
        {
            FavoriteRemoveBtn.Visibility = Visibility.Collapsed;
            FavoriteAddBtn.Visibility = Visibility.Visible;
            _ = GatherStationsByVotesAsync();
        }

        private void FavoriteBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (_favoriteStationsIsShowing)
            {
                FavoriteBtn.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x8C, 0x00));
                FavoriteRemoveBtn.Visibility = Visibility.Collapsed;
                FavoriteAddBtn.Visibility = Visibility.Visible;
                _ = FormatResultsAsync(_currentStations, _currentPage);
                _favoriteStationsIsShowing = !_favoriteStationsIsShowing;
            }
            else
            {
                FavoriteBtn.Background = new SolidColorBrush(Color.FromRgb(115, 14, 2));
                _currentStations = GetCurrentDataGrid();
                _ = FormatResultsAsync(_favoriteStations);
                FavoriteRemoveBtn.Visibility = Visibility.Visible;
                FavoriteAddBtn.Visibility = Visibility.Collapsed;
                _favoriteStationsIsShowing = !_favoriteStationsIsShowing;
            }
        }

        private void StationsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridRow? row = GetSelectedRow(sender);
            if (row != null) { _ = StartPlayerAsync(row); }
        }

        private void StationsDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) { return; }
            e.Handled = true;
            DataGridRow? row = GetSelectedRow(sender);
            if (row != null) { _ = StartPlayerAsync(row); }
        }

        private void MenuItem_Add_Click(object sender, RoutedEventArgs e)
        {
            if (_favoriteStations == null) { _favoriteStations = new List<Tuple<string, string, string, string, int>>(); }
            var row = StationsDataGrid.SelectedItem as Tuple<string, string, string, string, int>;
            if (row != null && !_favoriteStations.Contains(row)) { _favoriteStations.Add(row); }
        }

        private void MenuItem_Remove_Click(object sender, RoutedEventArgs e)
        {
            if (_favoriteStations == null) { return; }
            var row = StationsDataGrid.SelectedItem as Tuple<string, string, string, string, int>;
            if (row != null && _favoriteStations.Contains(row))
            {
                _favoriteStations.Remove(row);
                StationsDataGrid.Items.Refresh();
            }
        }

        private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { _ = GatherStationsByNameAsync(); }
        }

        private void StationsDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (!e.Row.IsSelected) { e.Row.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEE, 0xEE, 0xEE)); }
        }

        private void PrevStationBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (_prevBtnStationRow.Item != null) { _ = StartPlayerAsync(_prevBtnStationRow); }
        }

        private void NextStationBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (_nextBtnStationRow.Item != null) { _ = StartPlayerAsync(_nextBtnStationRow); }
        }

        private void PrevDataGridPageBtn_OnClick(object sender, RoutedEventArgs e)
        {
            _currentPage -= 1;
            
            if (_currentPage < 1)
            {
                _currentPage = 1; 
                return;
            }

            _isLastPage = false;
            CurrentDataGridPageLabel.Dispatcher.Invoke(() => { CurrentDataGridPageLabel.Content = _currentPage.ToString(); });
            _ = FormatResultsAsync(_currentStations, _currentPage);
        }

        private void NextDataGridPageBtn_OnClick(object sender, RoutedEventArgs e)
        {
            _currentPage += 1;
            int startIndex = (_stationsPerPage * _currentPage) - _stationsPerPage;
            
            if (_isLastPage)
            {
                _currentPage -= 1;
                return;
            }

            if (startIndex + _stationsPerPage >= _radioStations.Count)
            {
                _isLastPage = true;
            }

            CurrentDataGridPageLabel.Dispatcher.Invoke(() => { CurrentDataGridPageLabel.Content = _currentPage.ToString(); });
            _ = FormatResultsAsync(_currentStations, _currentPage);
        }

        private void AudioImg_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (AudioPlayer != null)
            {
                if (AudioPlayer.GetVolume().Equals(0))
                {
                    AudioPlayer.SetVolume(AudioPlayer.LastVolume);
                    AudioSlider.Value = AudioPlayer.LastVolume * 100;
                    Dispatcher.Invoke(() =>
                    {
                        AudioImg.Source = new BitmapImage(new Uri("Assets/volume.png", UriKind.Relative));
                        AudioVolumeLabel.Content = (int)Math.Round(AudioSlider.Value);
                    });
                }
                else
                {
                    AudioPlayer.LastVolume = AudioPlayer.GetVolume();
                    AudioSlider.Value = 0;
                    AudioPlayer.SetVolume(0);
                    Dispatcher.Invoke(() =>
                    {
                        AudioImg.Source = new BitmapImage(new Uri("Assets/volumeMute.png", UriKind.Relative));
                        AudioVolumeLabel.Content = "0";
                    });
                }
            }
        }
    }
}
