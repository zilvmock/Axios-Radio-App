using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Axios.data;
using Axios.Models;
using Axios.Services;
using Axios.Windows;
using Application = System.Windows.Application;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Axios.Properties;

namespace Axios.Pages
{
    public partial class RadioPage
    {
        public AudioPlayer? AudioPlayer { get; set; }
        public static RadioStationManager RadioStationManager = new();
        public static RadioStationService RadioStationService { get; } = new(RadioStationManager);
        public static RadioStationManagerService RadioStationManagerService { get; } = new(RadioStationManager);

        private List<Station> _radioStations;
        private List<Station> _currentStations;
        private static List<Station> _favoriteStations;
        private static Thread? _playerThread;
        private string _searchPhrase;
        private string? _prevStationRowUUID;
        private static string _currentStationRowUUID;
        private string _prevRowUUID;
        private string _nextRowUUID;
        private int _failedInRow;
        private bool _nextStationIsPressed;
        private bool _loading;
        private bool _isPageKeyHeldDown;
        private bool _columnHeaderClicked;
        private int _currentPage = 1;
        private bool _isLastPage;
        private bool _isLastItemOnPage;
        private bool _isFirstItemOnPage;
        private string _resultsType;
        private const int StationsPerPage = 18;
        private bool _favoriteStationsIsShowing;
        private bool _top100StationsIsShowing;
        private bool _dataGridColumnsLoaded;
        private bool _newPageLoaded;

        private static readonly SolidColorBrush DefaultRowColorBrush = new(Color.FromRgb(0xEE, 0xEE, 0xEE));
        private static readonly SolidColorBrush CorrectRowColorBrush = new(Color.FromRgb(248, 168, 106));
        private static readonly SolidColorBrush FailedRowTextColorBrush = new(Color.FromRgb(0x99, 0x99, 0x99));
        private static readonly BitmapImage PlayImg = new(new Uri("/Assets/play.png", UriKind.Relative));
        private static readonly BitmapImage PauseImg = new(new Uri("/Assets/pause.png", UriKind.Relative));
        private static readonly BitmapImage VolumeImg = new(new Uri("/Assets/volume.png", UriKind.Relative));
        private static readonly BitmapImage VolumeMuteImg = new(new Uri("/Assets/volumeMute.png", UriKind.Relative));
        private static readonly BitmapImage SearchBtnBImg = new(new Uri("/Assets/search.png", UriKind.Relative));
        private static readonly BitmapImage SearchBtnWImg = new(new Uri("/Assets/search_w.png", UriKind.Relative));

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

        /// <summary>
        /// Initializes the user interface with default values and loads the last played station and favorite stations.
        /// </summary>
        private async void InitializeUI()
        {
            // Setup initial UI
            Dispatcher.Invoke(() =>
            {
                AudioVolumeLabel.Content = AudioPlayer.DefaultVolume * 100;
                AudioSlider.Value = AudioPlayer.DefaultVolume * 100;
                StopPlayerImg.Source = PlayImg;
                PrevStationBtn.IsEnabled = false;
                NextStationBtn.IsEnabled = false;
                FavoriteRemoveBtn.Visibility = Visibility.Collapsed;
            });

            // Default on startup
            _top100StationsIsShowing = true;
            Top100Btn.IsChecked = true;
            await GatherStationsByVotesAsync();

            // Grab last station
            if (Settings.Default.LastStation != null && Settings.Default.LastStation.Count > 0)
            {
                try
                {
                    StringCollection lastStation = Settings.Default.LastStation;
                    string[]? items = lastStation[0]?.Split(',');
                    if (items != null)
                    {
                        var station = new Station(items[0], items[1], items[2], items[3], int.Parse(items[4]), items[5]);

                        var row = GetSelectedRowByUUID(station.Uuid);
                        if (row != null) { Dispatcher.Invoke(() => { row.Background = new SolidColorBrush(Color.FromRgb(248, 168, 106)); }); }

                        DataGridRow lastStationRow = new DataGridRow { DataContext = station, Item = station };
                        _currentStationRowUUID = station.Uuid;
                        SetItemPositionValueOnPage(lastStationRow);
                        await StartPlayerAsync(lastStationRow, false);
                    }
                }
                catch (Exception)
                {
                    MainWindow.NotifyIcon.ShowBalloonTip(500, "Axios", "Failed to load Last Station.", ToolTipIcon.Error);
                }
            }

            // Grab favorite stations
            if (Settings.Default.FavoriteStations != null && Settings.Default.FavoriteStations.Count > 0)
            {
                try
                {
                    if (Settings.Default.FavoriteStations.Count > 0)
                    {
                        _favoriteStations = new List<Station>();
                        foreach (var station in Settings.Default.FavoriteStations)
                        {
                            if (station == null) { continue; }
                            string[] stationData = station.Split(',');
                            _favoriteStations.Add(new Station(stationData[0], stationData[1], stationData[2], stationData[3], int.Parse(stationData[4]), stationData[5]));
                        }
                    }
                    else { _favoriteStations = new List<Station>(); }
                }
                catch (Exception) { MainWindow.NotifyIcon.ShowBalloonTip(500, "Axios", "Failed to load Favorite Stations.", ToolTipIcon.Error); }
            }

            // Set previous volume
            Dispatcher.Invoke(() =>
            {
                AudioVolumeLabel.Content = Settings.Default.LastVolume;
                AudioSlider.Value = Settings.Default.LastVolume;
            });
            AudioPlayer?.SetVolume((float)AudioSlider.Value / 100);

            // Set vote data
            RadioStationManager.LastVoteTime = Settings.Default.LastVoteTime;
            RadioStationManager.LastVoteUUIDs = Settings.Default.LastVoteUUIDs ?? new StringCollection();
        }

        /// <summary>
        /// Formats the results of a list of radio stations and displays them in a DataGrid control.
        /// </summary>
        /// <param name="radioStations">A list of radio stations to be displayed.</param>
        private void FormatResults(List<Station> radioStations)
        {
            Dispatcher.Invoke(() =>
            {
                StationsDataGrid.IsEnabled = false;

                if (! _dataGridColumnsLoaded)
                {
                    DataGridTextColumn column1 = new DataGridTextColumn
                    {
                        Header = "CC",
                        Binding = new System.Windows.Data.Binding("CountryCode")
                    };

                    DataGridTextColumn column2 = new DataGridTextColumn
                    {
                        Header = "Votes",
                        Binding = new System.Windows.Data.Binding("Votes")
                    };

                    DataGridTextColumn column3 = new DataGridTextColumn
                    {
                        Header = "Station Name",
                        Binding = new System.Windows.Data.Binding("Name"),
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

            EventManager.RegisterClassHandler(typeof(DataGridColumnHeader), DataGridColumnHeader.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(DataGridColumnHeader_PreviewMouseLeftButtonUp));
        }

        /// <summary>
        /// Calculates the starting and ending indices of radio stations to display based on the current page.
        /// </summary>
        /// <param name="currentPage">The current page number.</param>
        /// <returns>A tuple of integers representing the starting and ending indices of the radio stations to display.</returns>
        private static (int StartIndex, int EndIndex) GetPageRange(int currentPage)
        {
            int startIndex = (StationsPerPage * currentPage) - StationsPerPage;
            int endIndex = StationsPerPage * currentPage;
            return (StartIndex: startIndex, EndIndex: endIndex);
        }

        /// <summary>
        /// Retrieves radio stations containing the search phrase and displays them on the UI.
        /// </summary>
        private async Task GatherStationsByNameAsync()
        {
            _searchPhrase = SearchTextBox.Text;
            _radioStations = await RadioStationManagerService.GetStationsByNameAsync(SearchTextBox.Text);
            var pagesRange = GetPageRange(_currentPage);
            _radioStations = await RadioStationManagerService.GetPageOfStationsAsync(pagesRange.StartIndex, pagesRange.EndIndex, _radioStations);
            _resultsType = "search";
            FormatResults(_radioStations);
        }

        /// <summary>
        /// Retrieves radio stations ordered by votes and displays them on the UI.
        /// </summary>
        private async Task GatherStationsByVotesAsync()
        {
            _radioStations = await RadioStationManagerService.GetStationsByVotesAsync();
            var pagesRange = GetPageRange(_currentPage);
            _radioStations = await RadioStationManagerService.GetPageOfStationsAsync(pagesRange.StartIndex, pagesRange.EndIndex, _radioStations);
            _resultsType = "votes";
            FormatResults(_radioStations);
        }

        /// <summary>
        /// Starts playing the selected station on a separate thread using AudioPlayer and updates UI components
        /// </summary>
        /// <param name="stationRow">The DataGridRow that represents the station to play</param>
        /// <param name="autoPlay">Whether the station should start playing automatically or not</param>
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

            var selectedStation = (Station)stationRow.DataContext;
            string url = selectedStation.Url;
            string name = selectedStation.Name;
            string artUrl = selectedStation.IconUrl;
            string uuid = selectedStation.Uuid;

            if (_playerThread != null && AudioPlayer != null) { AudioPlayer.EndAndDispose(); }

            try
            {
                await Task.Run(() => { AudioPlayer = new AudioPlayer(url); });
                if (AudioPlayer != null) _playerThread = new Thread(AudioPlayer.StartPlaying);
                if (autoPlay) { _playerThread?.Start(); }
                UpdatePlayerUiAsync(name, artUrl, autoPlay);
                _prevStationRowUUID = _currentStationRowUUID;
                _currentStationRowUUID = uuid;
                UpdateStationBackgroundToCorrect(uuid);
                Dispatcher.Invoke(() => { PlayerStatusLabel.Content = ""; });
                _failedInRow = 0;
            }
            catch (Exception)
            {
                _failedInRow++;
                UpdateStationBackgroundToFailed(stationRow);
                UpdateStationBackgroundToCorrect(_currentStationRowUUID);
                MainWindow.NotifyIcon.ShowBalloonTip(200, "Axios", $"Cannot play {name} right now...", ToolTipIcon.None);
                Dispatcher.Invoke(() =>
                {
                    StopPlayerImg.Source = PlayImg;
                    StationsDataGrid.IsEnabled = true;
                    PlayerStatusLabel.Content = "Failed";
                    PlayerStatusLabel.Foreground = Brushes.Red;
                });
            }
            finally
            {
                await RadioStationService.CountStationClickAsync(_currentStationRowUUID);
                Dispatcher.Invoke(() => { StationsDataGrid.IsEnabled = true; StationsDataGrid.Focus(); });
                EnablePlayerButtons();
                SetPrevAndNextRowsFromCurrent();
                var row = GetSelectedRowByUUID(_currentStationRowUUID);
                if (row != null) { SetItemPositionValueOnPage(row); }
            }
        }

        /// <summary>
        /// Stops or resumes playing the radio station that is currently being played.
        /// </summary>
        public void StopRadio()
        {
            if (AudioPlayer == null) { return; }
            if (AudioPlayer.IsPlaying())
            {
                AudioPlayer.PausePlaying();
                Dispatcher.Invoke(() =>
                {
                    StopPlayerImg.Source = PlayImg;
                    StopPlayerBtn.ToolTip = "Resume";
                });
            }
            else
            {
                AudioPlayer.ResumePlaying();
                Dispatcher.Invoke(() =>
                {
                    StopPlayerImg.Source = PauseImg;
                    StopPlayerBtn.ToolTip = "Pause";
                });
            }
        }

        /// <summary>
        /// Asynchronously updates the player UI to reflect the currently playing station.
        /// </summary>
        /// <param name="name">The name of the currently playing station.</param>
        /// <param name="artUrl">The URL of the icon for the currently playing station.</param>
        /// <param name="paused">True if the player is currently paused, false otherwise.</param>
        private async void UpdatePlayerUiAsync(string name, string artUrl, bool paused = false)
        {
            if (AudioPlayer == null) { return; }
            Dispatcher.Invoke(() =>
            {
                AudioPlayer.SetVolume((float)AudioSlider.Value / 100);
                AudioVolumeLabel.Content = AudioSlider.Value;
                AudioVolumeLabel.Visibility = Visibility.Visible;
                double volume = Math.Round(AudioPlayer.GetVolume() * 100);
                AudioVolumeLabel.Content = volume;
                AudioSlider.Value = volume;

                StopPlayerBtn.Visibility = Visibility.Visible;
                if (paused)
                {
                    StopPlayerImg.Source = PauseImg;
                    StopPlayerBtn.ToolTip = "Pause";
                }
                else
                {
                    StopPlayerImg.Source = PlayImg;
                    StopPlayerBtn.ToolTip = "Resume";
                }
                StopPlayerBtn.IsEnabled = true;
                NowPlayingLabel.Text = name;
                NowPlayingLabel.Visibility = Visibility.Visible;
            });

            await Dispatcher.Invoke(async () => { StationFavIconImg.Source = await RadioStationService.GetStationIconAsync(artUrl); });
        }

        /// <summary>
        /// Updates the background color of a given station row to indicate it as correct and changes the previously selected row back to its default color.
        /// </summary>
        /// <param name="UUID">The UUID of the station row to update.</param>
        private void UpdateStationBackgroundToCorrect(string UUID)
        {
            if (string.IsNullOrEmpty(UUID)) { return; }
            DataGridRow? correctRow = GetSelectedRowByUUID(UUID);
            if (correctRow == null) { return; }
            Dispatcher.Invoke(() => { correctRow.Background = CorrectRowColorBrush; });
            if (UUID == _prevStationRowUUID || _prevStationRowUUID == null) { return; }
            DataGridRow? prevRow = GetSelectedRowByUUID(_prevStationRowUUID);
            if (prevRow == null) { return; }
            Dispatcher.Invoke(() =>
            {
                correctRow.Background = CorrectRowColorBrush;
                prevRow.Background = DefaultRowColorBrush;
            });
        }

        /// <summary>
        /// Updates the background color of a specified <see cref="DataGridRow"/> to indicate that the station associated with it failed to play.
        /// </summary>
        /// <param name="stationRow">The <see cref="DataGridRow"/> of the station that failed to play.</param>
        private void UpdateStationBackgroundToFailed(DataGridRow stationRow)
        {
            Dispatcher.Invoke(() => { stationRow.Foreground = FailedRowTextColorBrush; });
        }

        /// <summary>
        /// Gets the selected row from a DataGrid control based on a mouse click event.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <returns>The selected row in the DataGrid control.</returns>
        private DataGridRow? GetSelectedRowFromClick(object sender)
        {
            if (sender is not DataGrid) { return null; }
            if (StationsDataGrid.SelectedItem == null) { return null; }
            object selected = ((DataGrid)sender).SelectedItem;
            return (DataGridRow)((DataGrid)sender).ItemContainerGenerator.ContainerFromItem(selected);
        }

        /// <summary>
        /// Gets the row in the StationsDataGrid corresponding to the given UUID of a station.
        /// </summary>
        /// <param name="UUID">The UUID of the station to search for.</param>
        /// <returns>The DataGridRow corresponding to the station with the given UUID, or null if no such row exists.</returns>
        private DataGridRow? GetSelectedRowByUUID(string UUID)
        {
            if (string.IsNullOrEmpty(UUID)) { return null; }
            var stations = StationsDataGrid.ItemsSource as List<Station>;
            if (stations == null) { return null; }
            DataGridRow? correctRow = null;
            Dispatcher.Invoke(() =>
            {
                foreach (var station in stations)
                {
                    if (!station.Uuid.Equals(UUID)) { continue; }
                    correctRow = (DataGridRow)StationsDataGrid.ItemContainerGenerator.ContainerFromItem(station);
                    break;
                }
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            return correctRow;
        }

        /// <summary>
        /// Sets the previous and next row UUIDs based on the current selected row UUID.
        /// </summary>
        private void SetPrevAndNextRowsFromCurrent()
        {
            var currentRow = GetSelectedRowByUUID(_currentStationRowUUID);
            if (currentRow == null) { return; }
            
            int selectedIndex;
            if (_nextStationIsPressed)
            { selectedIndex = StationsDataGrid.Items.IndexOf(currentRow.Item) + _failedInRow; }
            else
            { selectedIndex = StationsDataGrid.Items.IndexOf(currentRow.Item) - _failedInRow; }

            if (selectedIndex > 0)
            {
                var row = (DataGridRow)StationsDataGrid.ItemContainerGenerator.ContainerFromItem(StationsDataGrid.Items[selectedIndex - 1]);
                _prevRowUUID = (row.Item as Station)?.Uuid ?? string.Empty;
            }

            if (selectedIndex < StationsDataGrid.Items.Count - 1)
            {
                var row = (DataGridRow)StationsDataGrid.ItemContainerGenerator.ContainerFromItem(StationsDataGrid.Items[selectedIndex + 1]);
                _nextRowUUID = (row.Item as Station)?.Uuid ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets the first row displayed on the current page of the data grid.
        /// </summary>
        /// <returns>The first row in the grid or null if the grid is empty.</returns>
        private DataGridRow? GetFirstRowOnPage()
        {
            if (StationsDataGrid.Items.Count < 1) { return null; }
            return (DataGridRow)StationsDataGrid.ItemContainerGenerator.ContainerFromItem(StationsDataGrid.Items[0]);
        }

        /// <summary>
        /// Gets the last row displayed on the current page of the data grid.
        /// </summary>
        /// <returns>The last row in the grid or null if the grid is empty.</returns>
        private DataGridRow? GetLastRowOnPage()
        {
            if (StationsDataGrid.Items.Count < 1) { return null; }
            return (DataGridRow)StationsDataGrid.ItemContainerGenerator.ContainerFromItem(StationsDataGrid.Items[StationsPerPage - 1]);
        }

        /// <summary>
        /// Gets the current data grid as a list of Station objects.
        /// </summary>
        /// <returns>List of Station objects.</returns>
        private List<Station> GetCurrentDataGridAsList()
        {
            return StationsDataGrid.Items.Cast<Station>().ToList();
        }

        /// <summary>
        /// Gets the current selected station as a <see cref="System.Collections.Specialized.StringCollection"/> containing the current selected station data.
        /// If the current selected station UUID is empty or null, an empty collection will be returned.
        /// </summary>
        /// <returns>A <see cref="System.Collections.Specialized.StringCollection"/> containing the current selected station data, or an empty collection if the UUID is empty or null.</returns>
        public static StringCollection GetCurrentStationAsCollection()
        {
            if (string.IsNullOrEmpty(_currentStationRowUUID)) { return new StringCollection(); }
            DataGridRow? selectedRow = MainWindow.RadioPage.GetSelectedRowByUUID(_currentStationRowUUID);
            if (selectedRow == null) { return new StringCollection(); }
            StringCollection currentStation = new();
            var currentRow = selectedRow.Item as Station;
            currentStation.Add($"{currentRow.Url},{currentRow.Name},{currentRow.IconUrl},{currentRow.CountryCode},{currentRow.Votes},{currentRow.Uuid}");

            return currentStation;
        }

        /// <summary>
        /// Gets the current list of favorite stations as a <see cref="System.Collections.Specialized.StringCollection"/> containing the favorite stations data.
        /// If there are no favorite stations, an empty string collection is returned.
        /// </summary>
        /// <returns>A string collection of favorite stations.</returns>
        public static StringCollection GetFavoriteStationsAsCollection()
        {
            if (_favoriteStations.Count < 1) { return new StringCollection(); }
            StringCollection favoriteStations = new();
            foreach (var station in _favoriteStations)
            {
                favoriteStations.Add($"{station.Url},{station.Name},{station.IconUrl},{station.CountryCode},{station.Votes},{station.Uuid}");
            }

            return favoriteStations;
        }


        /// <summary>
        /// Sets the values of the _isFirstItemOnPage and _isLastItemOnPage fields depending on the given DataGridRow's position on the page.
        /// If the DataGrid is empty or null, or the given DataGridRow is null, then the method returns without making any changes.
        /// </summary>
        /// <param name="row">The DataGridRow whose position on the page will be used to set the values of the _isFirstItemOnPage and _isLastItemOnPage fields.</param>
        private void SetItemPositionValueOnPage(DataGridRow? row)
        {
            if (StationsDataGrid.Items.Count < 1 || row == null) { return; }
            
            int rowIndex = row.GetIndex();

            if (rowIndex == StationsPerPage - 1)
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

        /// <summary>
        /// Refreshes the items on the page by retrieving the appropriate stations based on the current page, results type, and search phrase.
        /// The selected index is maintained across the refresh, and the station background is updated to reflect the currently playing station.
        /// </summary>
        public async void RefreshPageItems()
        {
            int selectedIndex = StationsDataGrid.SelectedIndex;
            StationsDataGrid.ItemsSource = null;
            StationsDataGrid.Items.Clear();
            var range = GetPageRange(_currentPage);

            if (_resultsType.Equals("votes"))
            {
                _radioStations = await RadioStationManagerService.GetStationsByVotesAsync();
                _radioStations = await RadioStationManagerService.GetPageOfStationsAsync(range.StartIndex, range.EndIndex, _radioStations);
            }
            else if (_resultsType.Equals("search"))
            {
                _radioStations = await RadioStationManager.GetStationsByNameAsync(_searchPhrase);
                _radioStations = await RadioStationManagerService.GetPageOfStationsAsync(range.StartIndex, range.EndIndex, _radioStations);
            }
            else
            {
                FormatResults(_favoriteStations);
                UpdateStationBackgroundToCorrect(_currentStationRowUUID);
                StationsDataGrid.IsEnabled = true;
                return;
            }

            FormatResults(_radioStations);
            UpdateStationBackgroundToCorrect(_currentStationRowUUID);

            if (selectedIndex != -1) { StationsDataGrid.SelectedIndex = selectedIndex; }
            StationsDataGrid.IsEnabled = true;
        }

        // -- AUDIO PLAYER COMPONENTS EVENTS --
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
            if (string.IsNullOrEmpty(_currentStationRowUUID))
            {
                DataGridRow? firstRow = GetFirstRowOnPage();
                if (firstRow == null) { return; }
                await StartPlayerAsync(firstRow);
            }
            else
            {
                StopRadio();
                var currentRow = GetSelectedRowByUUID(_currentStationRowUUID);
                if (currentRow == null) { return; }
                SetPrevAndNextRowsFromCurrent();
                EnablePlayerButtons();
            }
        }

        private async void PrevStationBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (_loading) { return; }
            _loading = true;
            _nextStationIsPressed = false;
            
            if (_isFirstItemOnPage && ! _favoriteStationsIsShowing)
            {
                PrevDataGridPageBtn_OnClick(sender, e);
                int waited = 0;
                while (_newPageLoaded == false)
                {
                    if (waited > 30) { break; }
                    await Task.Delay(100);
                    waited++;
                }
                DataGridRow? row = new();
                await Dispatcher.Invoke(async () =>
                {
                    row = GetLastRowOnPage();
                    if (row == null) { _loading = false; return; }
                    await StartPlayerAsync(row);
                });
                SetItemPositionValueOnPage(row);
                _loading = false;
                return;
            }

            if (!string.IsNullOrEmpty(_prevRowUUID))
            {
                var row = GetSelectedRowByUUID(_prevRowUUID);
                if (row == null) { _loading = false; return; }
                await StartPlayerAsync(row);
                StationsDataGrid.SelectedIndex = row.GetIndex();
            }

            _loading = false;
        }

        private async void NextStationBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (_loading) { return; }
            _loading = true;
            _nextStationIsPressed = true;
            
            if (_isLastItemOnPage && ! _favoriteStationsIsShowing)
            {
                NextDataGridPageBtn_OnClick(sender, e);
                int waited = 0;
                while (_newPageLoaded == false)
                {
                    if (waited > 30) { break; }
                    await Task.Delay(100);
                    waited++;
                }

                DataGridRow? row = new();
                await Dispatcher.Invoke(async () =>
                {
                    row = GetFirstRowOnPage();
                    if (row == null) { _loading = false; return; }
                    await StartPlayerAsync(row);
                });
                SetItemPositionValueOnPage(row);
                _loading = false;
                return;
            }

            if (!string.IsNullOrEmpty(_nextRowUUID))
            {
                var row = GetSelectedRowByUUID(_nextRowUUID);
                if (row == null) { _loading = false; return; }
                await StartPlayerAsync(row);
                StationsDataGrid.SelectedIndex = row.GetIndex();
            }

            _loading = false;
        }

        private void AudioImg_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (AudioPlayer == null) { return; }
            if (AudioPlayer.GetVolume().Equals(0))
            {
                AudioPlayer.SetVolume(AudioPlayer.LastVolume);
                AudioSlider.Value = AudioPlayer.LastVolume * 100;
                Dispatcher.Invoke(() =>
                {
                    AudioImg.Source = VolumeImg;
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
                    AudioImg.Source = VolumeMuteImg;
                    AudioVolumeLabel.Content = "0";
                });
            }
        }

        private void AudioSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Dispatcher.Invoke(() => { AudioVolumeLabel.Content = AudioSlider.Value; });
            AudioPlayer?.SetVolume((float)AudioSlider.Value / 100);
        }

        // -- DATA GRID COMPONENTS EVENTS --

        private async void StationsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_columnHeaderClicked)
            {
                _columnHeaderClicked = false;
                return;
            }
            DataGridRow? row = GetSelectedRowFromClick(sender);
            if (row == null) { return; }
            await StartPlayerAsync(row);
            EnablePlayerButtons();
        }

        private async void StationsDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_isPageKeyHeldDown) { return; }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                DataGridRow? row = GetSelectedRowFromClick(sender);

                if (row != null)
                {
                    await StartPlayerAsync(row);
                }
            }
            else if (e.Key == Key.Left && !_favoriteStationsIsShowing)
            {
                PrevDataGridPageBtn_OnClick(sender, e);
                StationsDataGrid.SelectedIndex = 0;
            }
            else if (e.Key == Key.Right && !_favoriteStationsIsShowing)
            {
                NextDataGridPageBtn_OnClick(sender, e);
                StationsDataGrid.SelectedIndex = 0;
            }
            else if (e.Key == Key.Up)
            {
                if (StationsDataGrid.SelectedIndex > 0)
                { StationsDataGrid.SelectedIndex--; }
            }
            else if (e.Key == Key.Down)
            {
                if (StationsDataGrid.SelectedIndex < StationsDataGrid.Items.Count - 1)
                { StationsDataGrid.SelectedIndex++; }
            }

            _isPageKeyHeldDown = true;

            await Dispatcher.BeginInvoke(() => { StationsDataGrid.Focus(); });
            Keyboard.Focus(StationsDataGrid);
        }

        private void StationsDataGrid_OnPreviewKeyUp(object sender, KeyEventArgs e)
        {
            _isPageKeyHeldDown = false;
        }

        private void StationsDataGrid_OnPreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (_isPageKeyHeldDown) { e.Handled = true; }
        }

        private async void DataGridColumnHeader_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _columnHeaderClicked = true;
            StationsDataGrid.IsEnabled = false;
            await Task.Delay(300);
            SetPrevAndNextRowsFromCurrent();
            SetItemPositionValueOnPage(GetSelectedRowByUUID(_currentStationRowUUID));
            UpdateStationBackgroundToCorrect(_currentStationRowUUID);
            StationsDataGrid.IsEnabled = true;
        }

        private void StationsDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (!e.Row.IsSelected) { e.Row.Background = DefaultRowColorBrush; }
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
            _newPageLoaded = false;
            _isLastPage = false;

            if (_resultsType.Equals("votes")) { _radioStations = await RadioStationManagerService.GetStationsByVotesAsync(); }
            else if (_resultsType.Equals("search")) { _radioStations = await RadioStationManagerService.GetStationsByNameAsync(_searchPhrase); }
            CurrentDataGridPageLabel.Dispatcher.Invoke(() => { CurrentDataGridPageLabel.Content = _currentPage.ToString(); });
            var range = GetPageRange(_currentPage);
            _radioStations = await RadioStationManagerService.GetPageOfStationsAsync(range.StartIndex, range.EndIndex, _radioStations);
            FormatResults(_radioStations);
            UpdateStationBackgroundToCorrect(_currentStationRowUUID);
            _newPageLoaded = true;
        }

        private async void NextDataGridPageBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (_isLastPage) { return; }
            _newPageLoaded = false;
            _currentPage += 1;
            var range = GetPageRange(_currentPage);

            if (_resultsType.Equals("votes")) { _radioStations = await RadioStationManagerService.GetStationsByVotesAsync(); }
            else if (_resultsType.Equals("search")) { _radioStations = await RadioStationManagerService.GetStationsByNameAsync(_searchPhrase); }
            int lastPageItemCount = _radioStations.Count - StationsPerPage;
            _radioStations = await RadioStationManagerService.GetPageOfStationsAsync(range.StartIndex, range.EndIndex, _radioStations);

            if (range.StartIndex >= lastPageItemCount) { _isLastPage = true; }
            else { _isLastPage = false; }

            CurrentDataGridPageLabel.Dispatcher.Invoke(() => { CurrentDataGridPageLabel.Content = _currentPage.ToString(); });
            FormatResults(_radioStations);
            UpdateStationBackgroundToCorrect(_currentStationRowUUID);
            _newPageLoaded = true;
        }

        private void MenuItem_Add_Click(object sender, RoutedEventArgs e)
        {
            if (_favoriteStations == null) { _favoriteStations = new List<Station>(); }
            if (_favoriteStations.Count < 1) { _favoriteStations = new List<Station>(); }
            if (_favoriteStations.Count > StationsPerPage) { return; }
            var row = StationsDataGrid.SelectedItem as Station;
            if (row == null)
            {
                StationsDataGrid.IsEnabled = true;
                return;
            }
            var uuid = row.Uuid;
            if (_favoriteStations.All(x => x.Uuid != uuid)) { _favoriteStations.Add(row); }
        }

        private void MenuItem_Remove_Click(object sender, RoutedEventArgs e)
        {
            if (_favoriteStations.Count < 1) { return; }
            var row = StationsDataGrid.SelectedItem as Station;
            if (row == null)
            {
                StationsDataGrid.IsEnabled = true;
                return;
            }
            if (_favoriteStations.Contains(row))
            {
                _favoriteStations.Remove(row);
                StationsDataGrid.ItemsSource = null;
                StationsDataGrid.ItemsSource = _favoriteStations;
            }
        }

        private async void MenuItem_Vote_Click(object sender, RoutedEventArgs e)
        {
            StationsDataGrid.IsEnabled = false;
            var row = StationsDataGrid.SelectedItem as Station;
            if (row == null)
            {
                StationsDataGrid.IsEnabled = true;
                return;
            }
            await RadioStationService.VoteForStationAsync(row.Uuid);
            RefreshPageItems();
        }

        // -- OTHER PAGE COMPONENTS EVENTS --

        private async void SearchBtn_OnClick(object sender, RoutedEventArgs e)
        {
            _currentPage = 1;
            _top100StationsIsShowing = false;
            _favoriteStationsIsShowing = false;

            Dispatcher.Invoke(() =>
            {
                CurrentDataGridPageLabel.Content = _currentPage;
                SearchBtnIcon.Source = SearchBtnWImg;
                FavoriteBtn.IsChecked = false;
                Top100Btn.IsChecked = false;
                SearchBtn.IsChecked = true;
                FavoriteBtn.Content = "My Favorites";
                Top100Btn.Content = "Top 100";
                FavoriteRemoveBtn.Visibility = Visibility.Collapsed;
                FavoriteAddBtn.Visibility = Visibility.Visible;
                PrevDataGridPageBtn.Visibility = Visibility.Visible;
                CurrentDataGridPageLabel.Visibility = Visibility.Visible;
                NextDataGridPageBtn.Visibility = Visibility.Visible;
            });
            _resultsType = "search";
            await GatherStationsByNameAsync();
            UpdateStationBackgroundToCorrect(_currentStationRowUUID);
        }

        private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { _ = GatherStationsByNameAsync(); }
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
                SearchBtnIcon.Source = SearchBtnBImg;
                FavoriteBtn.Content = "My Favorites";
                Top100Btn.Content = "• Top 100";
                FavoriteRemoveBtn.Visibility = Visibility.Collapsed;
                FavoriteAddBtn.Visibility = Visibility.Visible;
                PrevDataGridPageBtn.Visibility = Visibility.Visible;
                CurrentDataGridPageLabel.Visibility = Visibility.Visible;
                NextDataGridPageBtn.Visibility = Visibility.Visible;
            });
            _resultsType = "votes";
            await GatherStationsByVotesAsync();
            UpdateStationBackgroundToCorrect(_currentStationRowUUID);
        }

        private void FavoriteBtn_OnClick(object sender, RoutedEventArgs e)
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
                        _resultsType = "votes";
                    }
                    else
                    {
                        SearchBtn.IsChecked = true;
                        SearchBtnIcon.Source = SearchBtnWImg;
                        _resultsType = "search";
                    }
                    FavoriteRemoveBtn.Visibility = Visibility.Collapsed;
                    FavoriteAddBtn.Visibility = Visibility.Visible;
                    PrevDataGridPageBtn.Visibility = Visibility.Visible;
                    CurrentDataGridPageLabel.Visibility = Visibility.Visible;
                    NextDataGridPageBtn.Visibility = Visibility.Visible;
                });
                FormatResults(_currentStations);
                UpdateStationBackgroundToCorrect(_currentStationRowUUID);
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    FavoriteBtn.IsChecked = true;
                    Top100Btn.IsChecked = false;
                    SearchBtn.IsChecked = false;
                    SearchBtnIcon.Source = SearchBtnBImg;
                    FavoriteBtn.Content = "• My Favorites";
                    Top100Btn.Content = "Top 100";
                    FavoriteAddBtn.Visibility = Visibility.Collapsed;
                    FavoriteRemoveBtn.Visibility = Visibility.Visible;
                    PrevDataGridPageBtn.Visibility = Visibility.Hidden;
                    CurrentDataGridPageLabel.Visibility = Visibility.Hidden;
                    NextDataGridPageBtn.Visibility = Visibility.Hidden;
                });
                _resultsType = "favorites";
                _currentStations = GetCurrentDataGridAsList();

                FormatResults(_favoriteStations);
                UpdateStationBackgroundToCorrect(_currentStationRowUUID);
            }
            _favoriteStationsIsShowing = !_favoriteStationsIsShowing;
        }
    }
}
