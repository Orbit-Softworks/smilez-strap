using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Text.Json;

namespace SmilezStrap
{
    public partial class MainWindow : Window
    {
        private static readonly string VERSION = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
        private const string GITHUB_REPO = "Orbit-Softworks/smilez-strap";
        private readonly HttpClient httpClient = new HttpClient();
        private string appDataPath;
        private Config config;

        public MainWindow()
        {
            InitializeComponent();
            InitializeApp();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/Orbit-Softworks/smilez-strap") { UseShellExecute = true });
        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://discord.gg/yourinvite") { UseShellExecute = true });
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private async void LaunchRoblox_Click(object sender, RoutedEventArgs e)
        {
            bool canContinue = await CheckForBootstrapperUpdate();
            if (!canContinue) return;
            this.Hide();

            var progressWindow = new ProgressWindow(false);
            progressWindow.Closed += (s, args) =>
            {
                this.Show();
            };
            progressWindow.Show();
        }

        private async void LaunchStudio_Click(object sender, RoutedEventArgs e)
        {
            bool canContinue = await CheckForBootstrapperUpdate();
            if (!canContinue) return;
            this.Hide();

            var progressWindow = new ProgressWindow(true);
            progressWindow.Closed += (s, args) =>
            {
                this.Show();
            };
            progressWindow.Show();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Settings panel coming soon!", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void InitializeApp()
        {
            appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmilezStrap");
            Directory.CreateDirectory(appDataPath);
            LoadConfig();
        }

        private void LoadConfig()
        {
            string configPath = Path.Combine(appDataPath, "config.json");
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                config = JsonSerializer.Deserialize<Config>(json);
            }
            else
            {
                config = new Config();
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            string configPath = Path.Combine(appDataPath, "config.json");
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }

        private async Task<bool> CheckForBootstrapperUpdate()
        {
            try
            {
                var response = await httpClient.GetStringAsync($"https://api.github.com/repos/{GITHUB_REPO}/releases/latest");
                var releaseInfo = JsonDocument.Parse(response);
                string latestVersion = releaseInfo.RootElement.GetProperty("tag_name").GetString().TrimStart('v');
                if (new Version(latestVersion) > new Version(VERSION))
                {
                    string downloadUrl = releaseInfo.RootElement.GetProperty("html_url").GetString();
                    var result = MessageBox.Show(
                        $"SmilezStrap v{latestVersion} is available!\n\nCurrent version: v{VERSION}\n\nWould you like to download the update?",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information
                    );
                    if (result == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(downloadUrl) { UseShellExecute = true });
                        Application.Current.Shutdown();
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for updates: {ex.Message}");
                return true;
            }
        }
    }

    public class Config
    {
        public string RobloxVersion { get; set; } = string.Empty;
        public string StudioVersion { get; set; } = string.Empty;
    }
}
