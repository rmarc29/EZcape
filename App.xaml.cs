using System.Windows;
using System.Net.Http; // For HttpClient
using System.Reflection; // For Assembly.GetExecutingAssembly()
using System; // For Exception
using System.Diagnostics; // For Process.Start

namespace EZcape
{
    public partial class App : Application
    {
        // Use a static HttpClient to avoid socket exhaustion issues
        private static readonly HttpClient _httpClient = new HttpClient();

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // You can run this in the background, so it doesn't block UI startup too much
            _ = CheckForUpdatesAsync();

            // Continue with normal app startup here
            // e.g., if you had specific startup logic in App.xaml.cs, put it here.
            // If not, your MainWindow will be created as normal via App.xaml's StartupUri.
        }

        private async System.Threading.Tasks.Task CheckForUpdatesAsync()
        {
            // IMPORTANT: Replace this with the actual raw URL to your latest_version.txt file
            const string onlineVersionFileUrl = "https://gist.githubusercontent.com/rmarc29/0d0fb8662309e325f6aaffb94727f698/raw/1191fdc33cec378aae647c4c812c84355e9d2a24/ezcape_latest_version.txt";
            // IMPORTANT: Replace this with the URL to your GitHub Releases page
            const string githubReleasesUrl = "https://github.com/rmarc29/EZcape/releases";

            try
            {
                // 1. Get current application version
                Version? currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                if (currentVersion == null)
                {
                    Debug.WriteLine("Could not determine current application version.");
                    return;
                }

                // 2. Fetch latest version from online file
                string onlineVersionString = await _httpClient.GetStringAsync(onlineVersionFileUrl);
                Version? latestOnlineVersion = Version.Parse(onlineVersionString.Trim());

                // 3. Compare versions
                if (latestOnlineVersion > currentVersion)
                {
                    Debug.WriteLine($"New version available: {latestOnlineVersion} (current: {currentVersion})");

                    // Show update prompt on the UI thread
                    this.Dispatcher.Invoke(() =>
                    {
                        MessageBoxResult result = MessageBox.Show(
                            $"A new version of EZcape ({latestOnlineVersion}) is available! Your current version is {currentVersion}.\n\nWould you like to visit the download page?",
                            "Update Available!",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            // Open GitHub Releases page in default browser
                            Process.Start(new ProcessStartInfo(githubReleasesUrl) { UseShellExecute = true });
                        }
                    });
                }
                else
                {
                    Debug.WriteLine("No new update available.");
                }
            }
            catch (HttpRequestException httpEx)
            {
                // Network error, e.g., no internet connection, server unreachable
                Debug.WriteLine($"Network error checking for updates: {httpEx.Message}");
                // Optionally: this.Dispatcher.Invoke(() => MessageBox.Show("Could not check for updates. Please check your internet connection.", "Update Check Failed"));
            }
            catch (FormatException)
            {
                // Version string from file was not in correct format
                Debug.WriteLine("Online version file content is malformed.");
            }
            catch (Exception ex)
            {
                // General unexpected error
                Debug.WriteLine($"An unexpected error occurred during update check: {ex.Message}");
            }
        }
    }
}