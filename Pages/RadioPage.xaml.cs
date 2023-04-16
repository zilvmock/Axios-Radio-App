using Axios.data;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Application = System.Windows.Application;
using Axios.Data;
using Control = System.Windows.Controls.Control;
using System.Reflection.Emit;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using FontStyle = System.Windows.FontStyle;


namespace Axios
{
    public partial class RadioPage
    {
        public Player? AudioPlayer;
        private static Thread? _playerThread;
        private Search _search = new();
        private List<Tuple<string, string, string, string, int, string>> _radioStations;
        private List<Tuple<string, string, string, string, int, string>> _currentStations;
        //private string _prevStationUrl = string.Empty;
        
        private string? _prevStationRowUUID;
        public static string CurrentStationRowUUID;

        private string _prevRowUUID;
        private string _nextRowUUID;
        private int _failedInRow = 0;
        private bool _nextStationIsPressed;

        private int _currentPage = 1;
        private bool _isLastPage;
        private bool _isLastItemOnPage;
        private bool _isFirstItemOnPage;
        private string _resultsType;
        private readonly int _stationsPerPage = 18;

        private bool _favoriteStationsIsShowing;
        private bool _top100StationsIsShowing;

        private bool _dataGridColumnsLoaded;

        private static List<Tuple<string, string, string, string, int, string>> _favoriteStations;

        private static readonly SolidColorBrush _defaultRowColorBrush = new (Color.FromRgb(0xEE, 0xEE, 0xEE));
        private static readonly SolidColorBrush _correctRowColorBrush = new (Color.FromRgb(248, 168, 106));
        //private static readonly SolidColorBrush _failedRowColorBrush = new (Color.FromRgb(254, 244, 219));
        private static readonly SolidColorBrush _failedRowTextColorBrush = new (Color.FromRgb(0x99, 0x99, 0x99));

        private static readonly BitmapImage _playImg = new (new Uri("/Assets/play.png", UriKind.Relative));
        private static readonly BitmapImage _pauseImg = new (new Uri("/Assets/pause.png", UriKind.Relative));
        private static readonly BitmapImage _logoImg = new (new Uri("/Assets/logo.png", UriKind.Relative));
        private static readonly BitmapImage _volumeImg = new (new Uri("/Assets/volume.png", UriKind.Relative));
        private static readonly BitmapImage _volumeMuteImg = new (new Uri("/Assets/volumeMute.png", UriKind.Relative));
        private static readonly BitmapImage _searchBtnBImg = new (new Uri("/Assets/search.png", UriKind.Relative));
        private static readonly BitmapImage _searchBtnWImg = new (new Uri("/Assets/search_w.png", UriKind.Relative));

        public RadioPage()
        {
            InitializeComponent();
            Data.Resources.ClearTempDir();
            InitializeCache();
        }

        private async void InitializeCache()
        {
            await new StationsCacheWindow().InitializeStationsCache();
            InitializeUI();
        }

        private async void InitializeUI()
        {
            Dispatcher.Invoke(() =>
            {
                AudioVolumeLabel.Content = Player.DefaultVolume * 100;
                AudioSlider.Value = Player.DefaultVolume * 100;
                StopPlayerImg.Source = _playImg;
                PrevStationBtn.IsEnabled = false;
                NextStationBtn.IsEnabled = false;
                FavoriteRemoveBtn.Visibility = Visibility.Collapsed;
            });

            await GatherStationsByVotesAsync();

            // Grab last station
            if (MainWindow.AppSettings.LastStation != null && MainWindow.AppSettings.LastStation.Count > 0)
            {
                try
                {
                    StringCollection lastStation = MainWindow.AppSettings.LastStation;
                    string[]? items = lastStation[0]?.Split(',');
                    if (items != null)
                    {
                        var station = new Tuple<string, string, string, string, int, string>(items[0], items[1],
                            items[2], items[3], int.Parse(items[4]), items[5]);

                        try
                        {
                            Dispatcher.Invoke(() =>
                            {
                                GetSelectedRowByUUID(station.Item6).Background = new SolidColorBrush(Color.FromRgb(248, 168, 106));
                            });
                        }
                        catch (Exception) { }

                        DataGridRow lastStationRow = new DataGridRow { DataContext = station, Item = station };
                        CurrentStationRowUUID = station.Item6;
                        SetItemPositionValueOnPage(lastStationRow);
                        await StartPlayerAsync(lastStationRow, false);
                    }
                }
                catch (Exception)
                {
                    MainWindow.NotifyIcon.ShowBalloonTip(500, "Axios", "Failed to load Last Stations! If this happens again please report an issue.", ToolTipIcon.Error);
                }
            }

            // Grab favorite stations
            if (MainWindow.AppSettings.FavoriteStations != null && MainWindow.AppSettings.FavoriteStations.Count > 0)
            {
                try
                {
                    if (Settings.Default.FavoriteStations.Count > 0)
                    {
                        _favoriteStations = new List<Tuple<string, string, string, string, int, string>>();
                        foreach (var station in Settings.Default.FavoriteStations)
                        {
                            if (station == null) { continue; }
                            string[] stationData = station.Split(',');
                            _favoriteStations.Add(new Tuple<string, string, string, string, int, string>(stationData[0], stationData[1], stationData[2], stationData[3], int.Parse(stationData[4]), stationData[5]));
                        }
                    }
                    else
                    {
                        _favoriteStations = new List<Tuple<string, string, string, string, int, string>>();
                    }
                }
                catch (Exception)
                {
                    MainWindow.NotifyIcon.ShowBalloonTip(500, "Axios", "Failed to load Favorite Stations! If this happens again please report an issue.", ToolTipIcon.Error);
                }
            }
        }

        // TODO: rework this, because it's become unnecessary
        private Task FormatResultsAsync(List<Tuple<string, string, string, string, int, string>> radioStations, int page = 1)
        {
            Dispatcher.Invoke(() =>
            {
                StationsDataGrid.IsEnabled = false;

                if (! _dataGridColumnsLoaded)
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
                    _dataGridColumnsLoaded = true;
                }

                StationsDataGrid.ItemsSource = radioStations;
                StationsDataGrid.IsEnabled = true;
            });

            return Task.CompletedTask;
        }

        private async Task GatherStationsByNameAsync()
        {
            if (_search == null) { _search = new Search(); }
            _search.SearchPhrase = SearchTextBox.Text;
            _radioStations = _search.GetByName();
            int startIndex = (_stationsPerPage * _currentPage) - _stationsPerPage;
            int endIndex = _stationsPerPage * _currentPage;
            _radioStations = _search.GetPageOfStations(startIndex, endIndex, _radioStations);
            _resultsType = "search";
            await FormatResultsAsync(_radioStations);
        }

        private async Task GatherStationsByVotesAsync()
        {
            if (_search == null) { _search = new Search(); }
            _radioStations = _search.GetByVotes();
            int startIndex = (_stationsPerPage * _currentPage) - _stationsPerPage;
            int endIndex = _stationsPerPage * _currentPage;
            _radioStations = _search.GetPageOfStations(startIndex, endIndex, _radioStations);
            _resultsType = "votes";
            await FormatResultsAsync(_radioStations);

            _top100StationsIsShowing = true;
            Top100Btn.IsChecked = true;
        }

        private async Task StartPlayerAsync(DataGridRow stationRow, bool autoPlay = true)
        {
            if (stationRow.Item == null) { return; }

            Dispatcher.Invoke(() =>
            {
                StationsDataGrid.IsEnabled = false;
                PlayerStatusLabel.Content = "Loading...";
                PlayerStatusLabel.Foreground = Brushes.LightGray;
            });
            DisablePlayerButtons();

            string url;
            string name;
            string artUrl;
            string uuid;

            try
            {
                var selectedStation = (Tuple<string, string, string, string, int, string>)stationRow.DataContext;
                url = selectedStation.Item1;
                name = selectedStation.Item2;
                artUrl = selectedStation.Item3;
                uuid = selectedStation.Item6;
            }
            catch (Exception) { return; }

            if (_playerThread != null && AudioPlayer != null)
            {
                AudioPlayer.EndPlaying();
                //_playerThread.Join();
            }

            try
            {
                await Task.Run(() => { AudioPlayer = new Player(url); });
                _playerThread = new Thread(AudioPlayer.StartPlaying);
                if (autoPlay) { _playerThread.Start(); }

                UpdatePlayerUiAsync(name, artUrl, autoPlay);

                _prevStationUrl = url;
                _prevStationRowUUID = CurrentStationRowUUID;
                CurrentStationRowUUID = uuid;
                
                await UpdateStationBackgroundToCorrect(uuid);
                Dispatcher.Invoke(() => { PlayerStatusLabel.Content = ""; });
                
                _failedInRow = 0;
            }
            catch (Exception)
            {
                _failedInRow++;
                UpdateStationBackgroundToFailed(stationRow);
                await UpdateStationBackgroundToCorrect(CurrentStationRowUUID);
                
                MainWindow.NotifyIcon.ShowBalloonTip(200, "Axios", $"Cannot play {name} right now...", ToolTipIcon.None);
                Dispatcher.Invoke(() =>
                {
                    StopPlayerImg.Source = _playImg;
                    StationsDataGrid.IsEnabled = true;
                    PlayerStatusLabel.Content = "Failed";
                    PlayerStatusLabel.Foreground = Brushes.Red;
                });
            }
            finally
            {
                Dispatcher.Invoke(() => { StationsDataGrid.IsEnabled = true; });
                EnablePlayerButtons();
                SetPrevAndNextRowsFromCurrent();
                SetItemPositionValueOnPage(GetSelectedRowByUUID(CurrentStationRowUUID));
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
                    StopPlayerImg.Source = _playImg;
                    StopPlayerBtn.ToolTip = "Resume";
                });
            }
            else
            {
                AudioPlayer.ResumePlaying();
                StopPlayerBtn.Dispatcher.Invoke(() =>
                {
                    StopPlayerImg.Source = _pauseImg;
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
                    StopPlayerImg.Source = _pauseImg;
                    StopPlayerBtn.ToolTip = "Pause";
                }
                else
                {
                    StopPlayerImg.Source = _playImg;
                    StopPlayerBtn.ToolTip = "Resume";
                }
                StopPlayerBtn.IsEnabled = true;
                NowPlayingLabel.Text = name;
                NowPlayingLabel.Visibility = Visibility.Visible;
            });

            ImageSource? imageSource = await new StationArt(artUrl).GetImageAsync();
            if (imageSource == null)
            {
                Dispatcher.Invoke(() =>
                {
                    StationFavIconImg.Source = _logoImg;
                });
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    StationFavIconImg.Source = imageSource;
                });
            }
        }

        private Task UpdateStationBackgroundToCorrect(string UUID)
        {
            if (string.IsNullOrEmpty(UUID)) { return Task.CompletedTask; }
            DataGridRow? correctRow = GetSelectedRowByUUID(UUID);
            if (correctRow == null) { return Task.CompletedTask; }

            Dispatcher.Invoke(() => { correctRow.Background = _correctRowColorBrush; });

            if (UUID != _prevStationRowUUID && _prevStationRowUUID != null)
            {
                DataGridRow? prevRow = GetSelectedRowByUUID(_prevStationRowUUID);
                if (prevRow != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        correctRow.Background = _correctRowColorBrush;
                        prevRow.Background = _defaultRowColorBrush;
                    });
                }
            }

            return Task.CompletedTask;
        }

        private void UpdateStationBackgroundToFailed(DataGridRow stationRow)
        {
            Dispatcher.Invoke(() =>
            {
                //stationRow.Background = _failedRowColorBrush;
                stationRow.Foreground = _failedRowTextColorBrush;
            });
        }

        private DataGridRow? GetSelectedRowFromClick(object sender)
        {
            if (!(sender is DataGrid)) { return null; }
            if (StationsDataGrid.SelectedItem == null) { return null; }
            object selected = ((DataGrid)sender).SelectedItem;
            return (DataGridRow)((DataGrid)sender).ItemContainerGenerator.ContainerFromItem(selected);
        }

        private DataGridRow? GetSelectedRowByUUID(string UUID)
        {
            if (string.IsNullOrEmpty(UUID)) { return null; }

            var tuples = StationsDataGrid.ItemsSource as List<Tuple<string, string, string, string, int, string>>;
            DataGridRow? correctRow = null;

            StationsDataGrid.Dispatcher.Invoke(() =>
            {
                foreach (var tuple in tuples)
                {
                    if (tuple.Item6.Equals(UUID))
                    {
                        correctRow = (DataGridRow)StationsDataGrid.ItemContainerGenerator.ContainerFromItem(tuple);
                        break;
                    }
                }
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            return correctRow;
        }

        private void SetPrevAndNextRowsFromCurrent()
        {
            var currentRow = GetSelectedRowByUUID(CurrentStationRowUUID);

            if (currentRow == null) { return; }

            int selectedIndex;
            if (_nextStationIsPressed)
            {
                selectedIndex = StationsDataGrid.Items.IndexOf(currentRow.Item) + _failedInRow;
            }
            else
            {
                selectedIndex = StationsDataGrid.Items.IndexOf(currentRow.Item) - _failedInRow;
            }

            if (selectedIndex > 0)
            {
                var row = (DataGridRow)StationsDataGrid.ItemContainerGenerator.ContainerFromItem(StationsDataGrid.Items[selectedIndex - 1]);
                _prevRowUUID = (row.Item as Tuple<string, string, string, string, int, string>).Item6;
            }

            if (selectedIndex < StationsDataGrid.Items.Count - 1)
            {
                var row = (DataGridRow)StationsDataGrid.ItemContainerGenerator.ContainerFromItem(StationsDataGrid.Items[selectedIndex + 1]);
                _nextRowUUID = (row.Item as Tuple<string, string, string, string, int, string>).Item6;
            }
        }

        private DataGridRow GetFirstRowOnPage()
        {
            return (DataGridRow)StationsDataGrid.ItemContainerGenerator.ContainerFromItem(StationsDataGrid.Items[0]);
        }

        private DataGridRow GetLastRowOnPage()
        {
            return (DataGridRow)StationsDataGrid.ItemContainerGenerator.ContainerFromItem(StationsDataGrid.Items[_stationsPerPage - 1]);
        }
        private List<Tuple<string, string, string, string, int, string>> GetCurrentDataGridAsTuple()
        {
            return StationsDataGrid.Items.Cast<Tuple<string, string, string, string, int, string>>().ToList();
        }

        public static StringCollection GetCurrentStationAsCollection()
        {
            if (string.IsNullOrEmpty(CurrentStationRowUUID)) { return new StringCollection(); }
            DataGridRow? selectedRow = MainWindow.RadioPage.GetSelectedRowByUUID(CurrentStationRowUUID);
            if (selectedRow == null) { return new StringCollection(); }
            StringCollection currentStation = new StringCollection();
            var currentRowTuple = selectedRow.Item as Tuple<string?, string, string, string, int, string>;
            currentStation.Add($"{currentRowTuple.Item1},{currentRowTuple.Item2},{currentRowTuple.Item3},{currentRowTuple.Item4},{currentRowTuple.Item5},{currentRowTuple.Item6}");

            return currentStation;
        }

        public static StringCollection GetFavoriteStationsAsCollection()
        {
            if (_favoriteStations == null) { return new StringCollection(); }
            if (_favoriteStations.Count < 1) { return new StringCollection(); }
            StringCollection favoriteStations = new StringCollection();
            foreach (var station in _favoriteStations)
            {
                favoriteStations.Add($"{station.Item1},{station.Item2},{station.Item3},{station.Item4},{station.Item5},{station.Item6}");
            }

            return favoriteStations;
        }

        private void SetItemPositionValueOnPage(DataGridRow row)
        {
            if (StationsDataGrid.Items.Count < 1) { return; }
            
            int rowIndex = row.GetIndex();

            if (rowIndex == _stationsPerPage - 1)
            {
                _isLastItemOnPage = true;
                _isFirstItemOnPage = false;
            }
            else if (rowIndex == 0)
            {
                _isLastItemOnPage = false;
                _isFirstItemOnPage = true;
            }
            else
            {
                _isLastItemOnPage = false;
                _isFirstItemOnPage = false;
            }
        }

        // EVENTS
        // -- Player
        private void EnablePlayerButtons()
        {
            Dispatcher.Invoke(() =>
            {
                PrevStationBtn.IsEnabled = true;
                StopPlayerBtn.IsEnabled = true;
                NextStationBtn.IsEnabled = true;
            });
        }

        private void DisablePlayerButtons()
        {
            Dispatcher.Invoke(() =>
            {
                PrevStationBtn.IsEnabled = false;
                StopPlayerBtn.IsEnabled = false;
                NextStationBtn.IsEnabled = false;
            });
        }

        private void StopPlayerBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (AudioPlayer != null) { StopRadio(); }
            else
            {
                // If no station is selected manually
                DataGridRow? firstRow = GetFirstRowOnPage();
                if (firstRow == null) { return; }
                _ = StartPlayerAsync(firstRow);
            }

            if (!string.IsNullOrEmpty(CurrentStationRowUUID))
            {
                var currentRow = GetSelectedRowByUUID(CurrentStationRowUUID);
                if (currentRow != null)
                {
                    SetPrevAndNextRowsFromCurrent();
                    EnablePlayerButtons();
                }
            }
        }

        private async void PrevStationBtn_OnClick(object sender, RoutedEventArgs e)
        {
            _nextStationIsPressed = false;
            if (_isFirstItemOnPage)
            {
                PrevDataGridPageBtn_OnClick(sender, e);

                while (StationsDataGrid.Items.Count == 0)
                {
                    await Task.Delay(100);
                }

                var row = new DataGridRow();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    row = GetLastRowOnPage();
                    _ = StartPlayerAsync(row);
                });
                SetItemPositionValueOnPage(row);
                return;
            }

            if (!string.IsNullOrEmpty(_prevRowUUID))
            {
                var row = GetSelectedRowByUUID(_prevRowUUID);
                if (row != null)
                {
                    _ = StartPlayerAsync(row);
                    StationsDataGrid.SelectedIndex = row.GetIndex();
                }
            }
        }

        private async void NextStationBtn_OnClick(object sender, RoutedEventArgs e)
        {
            _nextStationIsPressed = true;
            if (_isLastItemOnPage)
            {
                NextDataGridPageBtn_OnClick(sender, e);

                while (StationsDataGrid.Items.Count == 0)
                {
                    await Task.Delay(100);
                }

                var row = new DataGridRow();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    row = GetFirstRowOnPage();
                    _ = StartPlayerAsync(row);
                });
                SetItemPositionValueOnPage(row);
                return;
            }

            if (!string.IsNullOrEmpty(_nextRowUUID))
            {
                var row = GetSelectedRowByUUID(_nextRowUUID);
                if (row != null)
                {
                    _ = StartPlayerAsync(row);
                    StationsDataGrid.SelectedIndex = row.GetIndex();
                }
            }
        }

        private void AudioSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Dispatcher.Invoke(() => { AudioVolumeLabel.Content = AudioSlider.Value; });

            AudioPlayer?.SetVolume((float)AudioSlider.Value / 100);
        }

        // -- Stations and UI
        private async void SearchBtn_OnClick(object sender, RoutedEventArgs e)
        {
            _currentPage = 1;
            _top100StationsIsShowing = false;
            _favoriteStationsIsShowing = false;

            Dispatcher.Invoke(() =>
            {
                CurrentDataGridPageLabel.Content = _currentPage;
                SearchBtnIcon.Source = _searchBtnWImg;
                FavoriteBtn.IsChecked = false;
                Top100Btn.IsChecked = false;
                FavoriteBtn.Content = "My Favorites";
                Top100Btn.Content = "Top 100";
                FavoriteRemoveBtn.Visibility = Visibility.Collapsed;
                FavoriteAddBtn.Visibility = Visibility.Visible;
            });
            await GatherStationsByNameAsync();
            await UpdateStationBackgroundToCorrect(CurrentStationRowUUID);
        }

        private async void Top100Btn_OnClick(object sender, RoutedEventArgs e)
        {
            _currentPage = 1;
            _top100StationsIsShowing = true;
            _favoriteStationsIsShowing = false;
            
            Dispatcher.Invoke(() =>
            {
                CurrentDataGridPageLabel.Content = _currentPage;
                FavoriteBtn.IsChecked = false;
                Top100Btn.IsChecked = true;
                SearchBtn.IsChecked = false;
                SearchBtnIcon.Source = _searchBtnBImg;
                FavoriteBtn.Content = "My Favorites";
                Top100Btn.Content = "• Top 100";
                FavoriteRemoveBtn.Visibility = Visibility.Collapsed;
                FavoriteAddBtn.Visibility = Visibility.Visible;
            });
            await GatherStationsByVotesAsync();
            await UpdateStationBackgroundToCorrect(CurrentStationRowUUID);
        }

        private async void FavoriteBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (_favoriteStationsIsShowing)
            {
                Dispatcher.Invoke(() =>
                {
                    FavoriteBtn.IsChecked = false;
                    FavoriteBtn.Content = "My Favorites";
                    if (_top100StationsIsShowing)
                    {
                        Top100Btn.IsChecked = true;
                        Top100Btn.Content = "• Top 100";
                    }
                    else
                    {
                        SearchBtn.IsChecked = true;
                        SearchBtnIcon.Source = _searchBtnWImg;
                    }
                    FavoriteRemoveBtn.Visibility = Visibility.Collapsed;
                    FavoriteAddBtn.Visibility = Visibility.Visible;
                });
                await FormatResultsAsync(_currentStations, _currentPage);
                await UpdateStationBackgroundToCorrect(CurrentStationRowUUID);
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    FavoriteBtn.IsChecked = true;
                    Top100Btn.IsChecked = false;
                    SearchBtn.IsChecked = false;
                    SearchBtnIcon.Source = _searchBtnBImg;
                    FavoriteBtn.Content = "• My Favorites";
                    Top100Btn.Content = "Top 100";
                    FavoriteAddBtn.Visibility = Visibility.Collapsed;
                    FavoriteRemoveBtn.Visibility = Visibility.Visible;
                });
                _currentStations = GetCurrentDataGridAsTuple();
                await FormatResultsAsync(_favoriteStations);
            }

            _favoriteStationsIsShowing = !_favoriteStationsIsShowing;
        }

        private void StationsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridRow? row = GetSelectedRowFromClick(sender);

            if (row != null)
            {
                _ = StartPlayerAsync(row);
                EnablePlayerButtons();
            }
        }

        private void StationsDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                DataGridRow? row = GetSelectedRowFromClick(sender);

                if (row != null)
                {
                    _ = StartPlayerAsync(row);
                    EnablePlayerButtons();
                }
            }
            else if (e.Key == Key.Left)
            {
                PrevDataGridPageBtn_OnClick(sender, e);
                StationsDataGrid.SelectedIndex = 0;
            }
            else if (e.Key == Key.Right)
            {
                NextDataGridPageBtn_OnClick(sender, e);
                StationsDataGrid.SelectedIndex = 0;
            }
            else if (e.Key == Key.Up)
            {
                if (StationsDataGrid.SelectedIndex > 0)
                {
                    StationsDataGrid.SelectedIndex--;
                }
            }
            else if (e.Key == Key.Down)
            {
                if (StationsDataGrid.SelectedIndex < StationsDataGrid.Items.Count - 1)
                {
                    StationsDataGrid.SelectedIndex++;
                }
            }

            Dispatcher.BeginInvoke(() =>
            {
                StationsDataGrid.Focus();
            });
            Keyboard.Focus(StationsDataGrid);
        }

        private void MenuItem_Add_Click(object sender, RoutedEventArgs e)
        {
            if (_favoriteStations == null) { _favoriteStations = new List<Tuple<string, string, string, string, int, string>>(); }
            if (_favoriteStations.Count < 1) { _favoriteStations = new List<Tuple<string, string, string, string, int, string>>(); }
            var row = StationsDataGrid.SelectedItem as Tuple<string, string, string, string, int, string>;
            if (row != null && !_favoriteStations.Contains(row)) { _favoriteStations.Add(row); }
        }

        private void MenuItem_Remove_Click(object sender, RoutedEventArgs e)
        {
            if (_favoriteStations.Count < 1) { return; }
            var row = StationsDataGrid.SelectedItem as Tuple<string, string, string, string, int, string>;
            if (row != null && _favoriteStations.Contains(row))
            {
                _favoriteStations.Remove(row);
                StationsDataGrid.ItemsSource = null;
                StationsDataGrid.ItemsSource = _favoriteStations;
            }
        }

        private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { _ = GatherStationsByNameAsync(); }
        }

        private void StationsDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (!e.Row.IsSelected) { e.Row.Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)); }
        }

        private async void PrevDataGridPageBtn_OnClick(object sender, RoutedEventArgs e)
        {
            _currentPage -= 1;
            
            if (_currentPage < 1)
            {
                _currentPage = 1;
                _isLastPage = false;
                return;
            }

            _isLastPage = false;
            
            if (_resultsType.Equals("votes"))
            {
                _radioStations = _search.GetByVotes();
            }
            else if (_resultsType.Equals("search"))
            {
                _radioStations = _search.GetByName();
            }

            CurrentDataGridPageLabel.Dispatcher.Invoke(() => { CurrentDataGridPageLabel.Content = _currentPage.ToString(); });
            int startIndex = (_stationsPerPage * _currentPage) - _stationsPerPage;
            int endIndex = _stationsPerPage * _currentPage;
            _radioStations = _search.GetPageOfStations(startIndex, endIndex, _radioStations);
            await FormatResultsAsync(_radioStations, _currentPage);
            await UpdateStationBackgroundToCorrect(CurrentStationRowUUID);
        }

        private async void NextDataGridPageBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (_isLastPage) { return; }
            _currentPage += 1;
            int startIndex = (_stationsPerPage * _currentPage) - _stationsPerPage;
            int endIndex = _stationsPerPage * _currentPage;
            
            if (_resultsType.Equals("votes"))
            {
                _radioStations = _search.GetByVotes();
            }
            else if (_resultsType.Equals("search"))
            {
                _radioStations = _search.GetByName();
            }

            int lastPageItemCount = _radioStations.Count - _stationsPerPage;
            _radioStations = _search.GetPageOfStations(startIndex, endIndex, _radioStations);
            
            if (startIndex >= lastPageItemCount)
            {
                _isLastPage = true;
            }
            else
            {
                _isLastPage = false;
            }

            CurrentDataGridPageLabel.Dispatcher.Invoke(() => { CurrentDataGridPageLabel.Content = _currentPage.ToString(); });
            await FormatResultsAsync(_radioStations, _currentPage);
            await UpdateStationBackgroundToCorrect(CurrentStationRowUUID);
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
                        AudioImg.Source = _volumeImg;
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
                        AudioImg.Source = _volumeMuteImg;
                        AudioVolumeLabel.Content = "0";
                    });
                }
            }
        }
    }
}
