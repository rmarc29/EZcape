using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows; // For Window, MessageBox, WindowState, RoutedEventArgs, SystemParameters
using System.Windows.Controls; // For Controls like Button, CheckBox, ComboBox, GroupBox, Label, ListView, ProgressBar, StackPanel, TextBlock, TextBox, ToggleButton, WrapPanel
using System.Windows.Data; // For IValueConverter, IMultiValueConverter
using System.Windows.Input; // For MouseButtonEventArgs, Cursors
using System.Windows.Navigation; // For RequestNavigateEventArgs

// IMPORTANT: Make sure your ThemeManager.cs file is in the EZcape namespace
// and that it correctly manages the dynamic resources.

namespace EZcape
{
    public partial class MainWindow : Window
    {
        private const string ApiUrl = "https://api.tarkov.dev/graphql";
        private static readonly HttpClient client = new HttpClient();
        private readonly string _saveFilePath;
        private readonly string _themeFilePath;
        private List<TaskItem>? _allTasks;

        public MainWindow()
        {
            InitializeComponent();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "EZcape TarkovApp/1.0");

            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string ezcapeFolder = Path.Combine(appDataFolder, "EZcape");
            Directory.CreateDirectory(ezcapeFolder);
            _saveFilePath = Path.Combine(ezcapeFolder, "tasks.json");
            _themeFilePath = Path.Combine(ezcapeFolder, "theme.txt");
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTheme();

            if (File.Exists(_saveFilePath))
            {
                LoadTasksFromDisk();
            }
            else
            {
                await FetchTasksFromServerAsync();
            }
            ApplyFilters();
        }

        private void LoadTheme()
        {
            if (File.Exists(_themeFilePath))
            {
                string themeName = File.ReadAllText(_themeFilePath);
                if (themeName == "Dark")
                {
                    ThemeManager.SetTheme("Dark");
                    ThemeToggleButton.IsChecked = true;
                }
                else
                {
                    ThemeManager.SetTheme("Light");
                    ThemeToggleButton.IsChecked = false;
                }
            }
            else
            {
                ThemeManager.SetTheme("Light");
                ThemeToggleButton.IsChecked = false;
            }
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            string themeName;
            if (ThemeToggleButton.IsChecked == true)
            {
                themeName = "Dark";
                ThemeManager.SetTheme(themeName);
            }
            else
            {
                themeName = "Light";
                ThemeManager.SetTheme(themeName);
            }

            try
            {
                File.WriteAllText(_themeFilePath, themeName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save theme: {ex.Message}");
            }
        }

        // Updated UpdateProgress method to include Kappa and Lightkeeper progress
        private void UpdateProgress()
        {
            if (_allTasks == null || !_allTasks.Any())
            {
                // Reset all progress indicators if no tasks
                ProgressTextBlock.Text = "0 / 0 Completed";
                CompletionProgressBar.Value = 0;
                ProgressPercentageTextBlock.Text = "0%";

                KappaProgressTextBlock.Text = "Kappa: 0 / 0";
                KappaCompletionProgressBar.Value = 0;
                KappaProgressPercentageTextBlock.Text = "0%";

                LightkeeperProgressTextBlock.Text = "Lightkeeper: 0 / 0";
                LightkeeperCompletionProgressBar.Value = 0;
                LightkeeperProgressPercentageTextBlock.Text = "0%";
                return;
            }

            // --- Overall Progress ---
            int totalCount = _allTasks.Count;
            int completedCount = _allTasks.Count(t => t.IsCompleted);
            double overallPercentage = (totalCount > 0) ? ((double)completedCount / totalCount) * 100 : 0;
            ProgressTextBlock.Text = $"{completedCount} / {totalCount} Completed";
            CompletionProgressBar.Value = overallPercentage;
            ProgressPercentageTextBlock.Text = $"{overallPercentage:F0}%";

            // --- Kappa Progress ---
            int totalKappaTasks = _allTasks.Count(t => t.KappaRequired);
            int completedKappaTasks = _allTasks.Count(t => t.KappaRequired && t.IsCompleted);
            double kappaPercentage = (totalKappaTasks > 0) ? ((double)completedKappaTasks / totalKappaTasks) * 100 : 0;
            KappaProgressTextBlock.Text = $"Kappa: {completedKappaTasks} / {totalKappaTasks}";
            KappaCompletionProgressBar.Value = kappaPercentage;
            KappaProgressPercentageTextBlock.Text = $"{kappaPercentage:F0}%";

            // --- Lightkeeper Progress ---
            int totalLightkeeperTasks = _allTasks.Count(t => t.LightkeeperRequired);
            int completedLightkeeperTasks = _allTasks.Count(t => t.LightkeeperRequired && t.IsCompleted);
            double lightkeeperPercentage = (totalLightkeeperTasks > 0) ? ((double)completedLightkeeperTasks / totalLightkeeperTasks) * 100 : 0;
            LightkeeperProgressTextBlock.Text = $"Lightkeeper: {completedLightkeeperTasks} / {totalLightkeeperTasks}";
            LightkeeperCompletionProgressBar.Value = lightkeeperPercentage;
            LightkeeperProgressPercentageTextBlock.Text = $"{lightkeeperPercentage:F0}%";
        }

        private void LoadTasksFromDisk()
        {
            try
            {
                StatusTextBlock.Text = "Loading saved progress...";
                string json = File.ReadAllText(_saveFilePath);
                _allTasks = JsonConvert.DeserializeObject<List<TaskItem>>(json);
                TasksListView.ItemsSource = _allTasks;
                StatusTextBlock.Text = $"Loaded {_allTasks?.Count ?? 0} tasks from local save file.";
                PopulateFilterComboBoxes();
                UpdateProgress();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Error loading saved file.";
                MessageBox.Show($"Could not load the task file. It might be corrupted.\n\nError: {ex.Message}", "Load Error");
            }
        }

        private async Task FetchTasksFromServerAsync()
        {
            StatusTextBlock.Text = "Fetching tasks from API for the first time...";
            LoadingOverlay.Visibility = Visibility.Visible;
            this.Cursor = Cursors.Wait;
            try
            {
                var graphQLQuery = @"
                    {
                        tasks {
                            map { name }
                            id
                            name
                            taskImageLink
                            trader { name }
                            objectives { description }
                            kappaRequired
                            lightkeeperRequired
                            startRewards { items { item { name } } }
                            finishRewards { items { item { name } } }
                            wikiLink
                        }
                    }";

                var payload = new { query = graphQLQuery };
                var jsonPayload = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(ApiUrl, content);
                string responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonConvert.DeserializeObject<ApiResponse>(responseContent);
                    if (apiResponse?.Data?.Tasks != null)
                    {
                        _allTasks = apiResponse.Data.Tasks;
                        TasksListView.ItemsSource = _allTasks;
                        StatusTextBlock.Text = $"Success! Found and saved {_allTasks.Count} tasks.";
                        SaveTasksToFile();
                        PopulateFilterComboBoxes();
                        UpdateProgress();
                    }
                    else if (apiResponse?.Errors != null && apiResponse.Errors.Any())
                    {
                        var errorMessages = string.Join("\n", apiResponse.Errors.Select(err => err.Message));
                        StatusTextBlock.Text = "API returned an error in the query.";
                        MessageBox.Show(errorMessages, "GraphQL API Error");
                    }
                }
                else
                {
                    StatusTextBlock.Text = $"Error: API returned status code {response.StatusCode}.";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"An error occurred: {ex.Message}";
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                this.Cursor = Cursors.Arrow;
            }
        }

        private void PopulateFilterComboBoxes()
        {
            if (_allTasks == null) return;

            var traders = _allTasks.Select(t => t.Trader.Name).Distinct().OrderBy(name => name).ToList();
            traders.Insert(0, "All Traders");
            TraderFilterComboBox.ItemsSource = traders;
            TraderFilterComboBox.SelectedIndex = 0;

            var maps = _allTasks.Where(t => t.Map != null).Select(t => t.Map.Name).Distinct().OrderBy(name => name).ToList();
            maps.Insert(0, "All Maps");
            MapFilterComboBox.ItemsSource = maps;
            MapFilterComboBox.SelectedIndex = 0;
        }

        private void ApplyFilters()
        {
            if (_allTasks == null) return;

            IEnumerable<TaskItem> filteredTasks = _allTasks;

            string nameFilter = TaskNameFilterTextBox.Text;
            if (!string.IsNullOrWhiteSpace(nameFilter))
            {
                filteredTasks = filteredTasks.Where(t => t.Name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (TraderFilterComboBox.SelectedIndex > 0)
            {
                string? traderFilter = TraderFilterComboBox.SelectedItem?.ToString();
                filteredTasks = filteredTasks.Where(t => t.Trader.Name == traderFilter);
            }

            if (MapFilterComboBox.SelectedIndex > 0)
            {
                string? mapFilter = MapFilterComboBox.SelectedItem?.ToString();
                filteredTasks = filteredTasks.Where(t => t.Map != null && t.Map.Name == mapFilter);
            }

            if (KappaFilterCheckBox.IsChecked == true)
            {
                filteredTasks = filteredTasks.Where(t => t.KappaRequired);
            }

            if (LightkeeperFilterCheckBox.IsChecked == true)
            {
                filteredTasks = filteredTasks.Where(t => t.LightkeeperRequired);
            }

            if (ShowCompletedCheckBox.IsChecked == false)
            {
                filteredTasks = filteredTasks.Where(t => !t.IsCompleted);
            }

            TasksListView.ItemsSource = filteredTasks.ToList();
            StatusTextBlock.Text = $"Displaying {filteredTasks.Count()} of {_allTasks.Count} tasks.";
        }

        private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyFilters();
        private void TaskNameFilter_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

        private void SaveTasksToFile()
        {
            if (_allTasks == null || !_allTasks.Any()) return;
            try
            {
                string json = JsonConvert.SerializeObject(_allTasks, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(_saveFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save progress.\n\nError: {ex.Message}", "Save Error");
            }
        }

        private void TaskCheckBox_Click(object sender, RoutedEventArgs e)
        {
            SaveTasksToFile();
            UpdateProgress();
            ApplyFilters();
        }

        private void ResetProgressButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to reset all task progress? This action cannot be undone.",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                if (_allTasks == null || !_allTasks.Any()) return;

                foreach (var task in _allTasks)
                {
                    task.IsCompleted = false;
                }

                SaveTasksToFile();
                UpdateProgress();
                ApplyFilters();

                StatusTextBlock.Text = "All task progress has been reset.";
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        // --- NEW: Custom Title Bar Event Handlers ---

        // Event handler for dragging the window by clicking and holding the custom title bar
        private void CustomTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Check if the left mouse button was pressed down
            if (e.ChangedButton == MouseButton.Left)
            {
                // Check if the window is currently maximized. If so, restore it before dragging.
                // This makes dragging a maximized window behave like Windows' default (restore and drag).
                if (this.WindowState == WindowState.Maximized)
                {
                    this.WindowState = WindowState.Normal;
                    // Adjust mouse position to prevent instant re-maximize if dragged from very top
                    var mousePosition = e.GetPosition(this);
                    this.Left = mousePosition.X - this.ActualWidth / 2;
                    this.Top = mousePosition.Y - SystemParameters.CaptionHeight / 2;
                }

                // Allows dragging the window
                this.DragMove();
            }
        }

        // Event handler for the Minimize button
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        // Event handler for the Maximize/Restore button
        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                // Optional: Change the button content to maximize symbol if it's currently restore
                // MaximizeRestoreButton.Content = ""; // Maximize symbol
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                // Optional: Change the button content to restore symbol if it's currently maximize
                // MaximizeRestoreButton.Content = ""; // Restore symbol ( is restore icon)
            }
        }

        // Event handler for the Close button
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    // --- Converter Classes (THESE MUST BE HERE, DIRECTLY IN THE NAMESPACE, NOT INSIDE MAINWINDOW CLASS) ---
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { throw new NotImplementedException(); }
    }

    public class BooleanOrToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            foreach (var value in values)
            {
                if (value is bool b && b) { return Visibility.Visible; }
            }
            return Visibility.Collapsed;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) { throw new NotImplementedException(); }
    }

    // --- API Response and Data Model Classes (These must also remain here, directly in the namespace) ---
    public class ApiResponse { [JsonProperty("data")] public ApiData? Data { get; set; } [JsonProperty("errors")] public List<GraphQLError>? Errors { get; set; } }
    public class GraphQLError { [JsonProperty("message")] public string Message { get; set; } = ""; }
    public class ApiData { [JsonProperty("tasks")] public List<TaskItem>? Tasks { get; set; } }
    public class TaskItem { [JsonProperty("id")] public string Id { get; set; } = ""; [JsonProperty("name")] public string Name { get; set; } = ""; [JsonProperty("taskImageLink")] public string? TaskImageLink { get; set; } [JsonProperty("kappaRequired")] public bool KappaRequired { get; set; } [JsonProperty("lightkeeperRequired")] public bool LightkeeperRequired { get; set; } [JsonProperty("wikiLink")] public string? WikiLink { get; set; } [JsonProperty("trader")] public Trader Trader { get; set; } = new Trader(); [JsonProperty("map")] public Map? Map { get; set; } [JsonProperty("objectives")] public List<Objective>? Objectives { get; set; } [JsonProperty("startRewards")] public TaskRewards? StartRewards { get; set; } [JsonProperty("finishRewards")] public TaskRewards? FinishRewards { get; set; } public bool IsCompleted { get; set; } }
    public class Trader { [JsonProperty("name")] public string Name { get; set; } = ""; }
    public class Map { [JsonProperty("name")] public string Name { get; set; } = ""; }
    public class Objective { [JsonProperty("description")] public string? Description { get; set; } }
    public class TaskRewards { [JsonProperty("items")] public List<RewardItem>? Items { get; set; } }
    public class RewardItem { [JsonProperty("item")] public Item Item { get; set; } = new Item(); }
    public class Item { [JsonProperty("name")] public string Name { get; set; } = ""; }
}