﻿using Axios.data;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Application = System.Windows.Application;
using Axios.Data;


namespace Axios
{
    public partial class RadioPage
    {
        public Player? AudioPlayer;
        private static Thread? _playerThread;
        private List<Tuple<string, string, string, string, int, string>> _radioStations;
        private Search _search = new();
        private List<Tuple<string, string, string, string, int, string>> _currentStations;
        private string _prevStationUrl = string.Empty;
        private string? _prevStationRowUUID;
        public static string CurrentStationRowUUID;
        private string _nextBtnStationRowUUID;
        private string _prevBtnStationRowUUID;
        private int _currentPage = 1;
        private bool _isLastPage;
        private string _resultsType;
        private readonly int _stationsPerPage = 18;
        private bool _favoriteStationsIsShowing;
        private static List<Tuple<string, string, string, string, int, string>> _favoriteStations;

        public RadioPage()
        {
            InitializeComponent();
            Data.Resources.ClearTempDir();
            Application.Current.MainWindow.IsEnabled = false;
            Application.Current.MainWindow.Opacity = 0.5;
            InitializeCache();
        }

        private async void InitializeCache()
        {
            var stw = new StationsCacheWindow();
            stw.Show();
            await stw.GrabStations();
            stw.Close();
            Application.Current.MainWindow.IsEnabled = true;
            Application.Current.MainWindow.Opacity = 1;
            InitializeUI();
        }

        private async void InitializeUI()
        {
            Dispatcher.Invoke(() =>
            {
                AudioVolumeLabel.Content = Player.DefaultVolume * 100;
                AudioSlider.Value = Player.DefaultVolume * 100;
                StopPlayerImg.Source = new BitmapImage(new Uri("Assets/play.png", UriKind.Relative));
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

        private async Task FormatResultsAsync(List<Tuple<string, string, string, string, int, string>> radioStations, int page = 1)
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

                if (_favoriteStationsIsShowing && radioStations != null)
                {
                    //StationsDataGrid.ItemsSource = radioStations.GetRange(startIndex, radioStations.Count);
                    StationsDataGrid.ItemsSource = radioStations;

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
                        //StationsDataGrid.ItemsSource = _radioStations.GetRange(startIndex, _radioStations.Count - startIndex);
                        StationsDataGrid.ItemsSource = radioStations;
                    }
                    else
                    {
                        //StationsDataGrid.ItemsSource = _radioStations.GetRange(startIndex, _stationsPerPage);
                        StationsDataGrid.ItemsSource = radioStations;
                    }
                }

                StationsDataGrid.IsEnabled = true;

            });
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
        }

        private async Task StartPlayerAsync(DataGridRow stationRow, bool autoPlay = true)
        {
            if (stationRow.Item == null) { return; }

            Dispatcher.Invoke(() => { StationsDataGrid.IsEnabled = false; });
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

            if (_prevStationUrl.Equals(url) || string.IsNullOrEmpty(url)) { return; }

            if (_playerThread != null && AudioPlayer != null)
            {
                AudioPlayer.EndPlaying();
                //_playerThread.Join();
            }

            try
            {
                await Task.Run(() => { AudioPlayer = new Player(url); }); 
                _playerThread = new Thread(AudioPlayer.StartPlaying);
                if (autoPlay)
                {
                    _playerThread.Start();
                }

                if (autoPlay)
                {
                    UpdatePlayerUiAsync(name, artUrl, true);
                }
                else
                {
                    UpdatePlayerUiAsync(name, artUrl);
                }

                _prevStationUrl = url;
                _prevStationRowUUID = CurrentStationRowUUID;
                CurrentStationRowUUID = uuid;
                await SetPrevAndNextRows();

                await UpdateStationBackgroundToCorrect(uuid);
            }
            catch (Exception)
            {
                MainWindow.NotifyIcon.ShowBalloonTip(500, "Axios", $"Cannot play {name} right now...", ToolTipIcon.None);

                UpdateStationBackgroundToFailed(stationRow);
                await UpdateStationBackgroundToCorrect(CurrentStationRowUUID);
                Dispatcher.Invoke(() =>
                {
                    StopPlayerImg.Source = new BitmapImage(new Uri("Assets/play.png", UriKind.Relative));
                    StationsDataGrid.IsEnabled = true;
                });

                EnablePlayerButtons();
            }
            finally
            {
                Dispatcher.Invoke(() => { StationsDataGrid.IsEnabled = true; });
                EnablePlayerButtons();
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

        private Task UpdateStationBackgroundToCorrect(string UUID)
        {
            if (string.IsNullOrEmpty(UUID)) { return Task.CompletedTask; }

            DataGridRow? correctRow = GetSelectedRowByUUID(UUID);

            if (correctRow == null) { return Task.CompletedTask; }

            Dispatcher.Invoke(() => { correctRow.Background = new SolidColorBrush(Color.FromRgb(248, 168, 106)); });

            if (UUID != _prevStationRowUUID && _prevStationRowUUID != null)
            {
                DataGridRow? prevRow = GetSelectedRowByUUID(_prevStationRowUUID);
                if (prevRow != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        correctRow.Background = new SolidColorBrush(Color.FromRgb(248, 168, 106));
                        prevRow.Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));
                    });
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

        private async Task SetPrevAndNextRows()
        {
            var currentRow = GetSelectedRowByUUID(CurrentStationRowUUID);
            
            if (currentRow == null) { return; }
            int selectedIndex = StationsDataGrid.Items.IndexOf(currentRow.Item); 

            if (selectedIndex > 0)
            {
                var row = (DataGridRow)StationsDataGrid.ItemContainerGenerator.ContainerFromItem(StationsDataGrid.Items[selectedIndex - 1]);
                _prevBtnStationRowUUID = (row.Item as Tuple<string, string, string, string, int, string>).Item6;
            }

            if (selectedIndex < StationsDataGrid.Items.Count - 1)
            {
                var row = (DataGridRow)StationsDataGrid.ItemContainerGenerator.ContainerFromItem(StationsDataGrid.Items[selectedIndex + 1]);
                _nextBtnStationRowUUID = (row.Item as Tuple<string, string, string, string, int, string>).Item6;
            }
        }

        private DataGridRow? GetFirstRow()
        {
            return (DataGridRow)StationsDataGrid.ItemContainerGenerator.ContainerFromItem(StationsDataGrid.Items[0]) ?? null;
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

        private async void StopPlayerBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (AudioPlayer != null) { StopRadio(); }
            else
            {
                // If no station is selected manually
                DataGridRow? firstRow = GetFirstRow();
                if (firstRow == null) { return; }
                _ = StartPlayerAsync(firstRow);
            }

            if (!string.IsNullOrEmpty(CurrentStationRowUUID))
            {
                var currentRow = GetSelectedRowByUUID(CurrentStationRowUUID);
                if (currentRow != null)
                {
                    await SetPrevAndNextRows();
                    EnablePlayerButtons();
                }
            }
        }
        private void PrevStationBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (! string.IsNullOrEmpty(_prevBtnStationRowUUID))
            {
                _ = StartPlayerAsync(GetSelectedRowByUUID(_prevBtnStationRowUUID));
            }
        }

        private void NextStationBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (! string.IsNullOrEmpty(_nextBtnStationRowUUID))
            {
                _ = StartPlayerAsync(GetSelectedRowByUUID(_nextBtnStationRowUUID));
            }
        }

        private void AudioSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Dispatcher.Invoke(() => { AudioVolumeLabel.Content = AudioSlider.Value; });

            AudioPlayer?.SetVolume((float)AudioSlider.Value / 100);
        }

        // -- Stations and UI
        private async void SearchButton_OnClick(object sender, RoutedEventArgs e)
        {
            FavoriteRemoveBtn.Visibility = Visibility.Collapsed;
            FavoriteAddBtn.Visibility = Visibility.Visible;
            _currentPage = 1;
            Dispatcher.Invoke(() => { CurrentDataGridPageLabel.Content = _currentPage; });
            await GatherStationsByNameAsync();
            await UpdateStationBackgroundToCorrect(CurrentStationRowUUID);
        }

        private async void Top100Btn_OnClick(object sender, RoutedEventArgs e)
        {
            FavoriteRemoveBtn.Visibility = Visibility.Collapsed;
            FavoriteAddBtn.Visibility = Visibility.Visible;
            _currentPage = 1;
            Dispatcher.Invoke(() => { CurrentDataGridPageLabel.Content = _currentPage; });
            await GatherStationsByVotesAsync();
            await UpdateStationBackgroundToCorrect(CurrentStationRowUUID);

        }

        private async void FavoriteBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (_favoriteStationsIsShowing)
            {
                _favoriteStationsIsShowing = !_favoriteStationsIsShowing;
                FavoriteBtn.Background = new SolidColorBrush(Color.FromRgb(230, 151, 55));
                FavoriteRemoveBtn.Visibility = Visibility.Collapsed;
                FavoriteAddBtn.Visibility = Visibility.Visible;
                await FormatResultsAsync(_currentStations, _currentPage);
                await UpdateStationBackgroundToCorrect(CurrentStationRowUUID);
            }
            else
            {
                _favoriteStationsIsShowing = !_favoriteStationsIsShowing;
                FavoriteBtn.Background = new SolidColorBrush(Color.FromRgb(115, 14, 2));
                _currentStations = GetCurrentDataGridAsTuple();
                await FormatResultsAsync(_favoriteStations);
                FavoriteRemoveBtn.Visibility = Visibility.Visible;
                FavoriteAddBtn.Visibility = Visibility.Collapsed;
            }
        }

        private void StationsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridRow? row = GetSelectedRow(sender);

            if (row != null)
            {
                _ = StartPlayerAsync(row);
                EnablePlayerButtons();
            }
        }

        private void StationsDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) { return; }
            e.Handled = true;
            DataGridRow? row = GetSelectedRow(sender);
            
            if (row != null)
            {
                _ = StartPlayerAsync(row);
                EnablePlayerButtons();
            }
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
