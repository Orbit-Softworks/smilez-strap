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
        private static readonly string VERSION = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        private const string GITHUB_REPO = "Orbit-Softworks/smilez-strap";
        private readonly HttpClient httpClient = new HttpClient();
        private string? appDataPath;
        private Config? config;

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
            Process.Start(new ProcessStartInfo("https://discord.gg/JSJcNC4Jv9") { UseShellExecute = true });
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private async void LaunchRoblox_Click(object sender, RoutedEventArgs e)
        {
            bool canContinue = await CheckForAppUpdate();
            if (!canContinue) return;

            bool bootstrapOk = await CheckForBootstrapperUpdate();
            if (!bootstrapOk) return;

            this.Hide();

            var progressWindow = new ProgressWindow(false);
            // No Closed handler → no re-show
            progressWindow.Show();
        }

        private async void LaunchStudio_Click(object sender, RoutedEventArgs e)
        {
            bool canContinue = await CheckForAppUpdate();
            if (!canContinue) return;

            bool bootstrapOk = await CheckForBootstrapperUpdate();
            if (!bootstrapOk) return;

            this.Hide();

            var progressWindow = new ProgressWindow(true);
            // No Closed handler → no re-show
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
            string configPath = Path.Combine(appDataPath!, "config.json");
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                config = JsonSerializer.Deserialize<Config>(json) ?? new Config();
            }
            else
            {
                config = new Config();
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            string configPath = Path.Combine(appDataPath!, "config.json");
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }

        private async Task<bool> CheckForAppUpdate()
        {
            try
            {
                var response = await httpClient.GetStringAsync($"https://api.github.com/repos/{GITHUB_REPO}/releases/latest");
                var releaseInfo = JsonDocument.Parse(response);
                string? latestVersion = releaseInfo.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "1.0.0";
                
                if (string.IsNullOrEmpty(latestVersion))
                    return true;
                
                // Parse versions safely
                if (Version.TryParse(VERSION, out Version? currentVersion) && 
                    Version.TryParse(latestVersion, out Version? latestVersionObj))
                {
                    if (latestVersionObj <= currentVersion)
                        return true;
                }
                else
                {
                    return true; // If version parsing fails, continue anyway
                }

                var result = MessageBox.Show(
                    $"SmilezStrap v{latestVersion} is available!\n\nCurrent version: v{VERSION}\n\nDownload and install automatically? (App will restart)",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information
                );
                if (result != MessageBoxResult.Yes)
                    return false;

                string? downloadUrl = null;
                var assets = releaseInfo.RootElement.GetProperty("assets").EnumerateArray();
                foreach (var asset in assets)
                {
                    string? name = asset.GetProperty("name").GetString();
                    if (name != null && name.Equals("SmilezStrap.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    MessageBox.Show("Update found, but no SmilezStrap.exe in release assets.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                string tempExePath = Path.Combine(Path.GetTempPath(), "SmilezStrap_new.exe");
                using (var responseStream = await httpClient.GetStreamAsync(downloadUrl))
                using (var fileStream = new FileStream(tempExePath, FileMode.Create))
                {
                    await responseStream.CopyToAsync(fileStream);
                }

                string currentExePath = Process.GetCurrentProcess().MainModule?.FileName ??
                                        System.AppContext.BaseDirectory + "SmilezStrap.exe";

                string batchPath = Path.Combine(Path.GetTempPath(), "update_smilezstrap.bat");
                string batchContent = $@"
@echo off
timeout /t 2 /nobreak >nul
del /f /q ""{currentExePath}""
copy /y ""{tempExePath}"" ""{currentExePath}""
start """" ""{currentExePath}""
del /f /q ""{batchPath}""
";
                File.WriteAllText(batchPath, batchContent);

                Process.Start(new ProcessStartInfo
                {
                    FileName = batchPath,
                    UseShellExecute = true,
                    CreateNoWindow = true
                });

                Application.Current.Shutdown();
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for updates: {ex.Message}");
                return true;
            }
        }

        private async Task<bool> CheckForBootstrapperUpdate()
        {
            // Your original Roblox update check code here (unchanged)
            return true;
        }
    }

    public class Config
    {
        public string RobloxVersion { get; set; } = string.Empty;
        public string StudioVersion { get; set; } = string.Empty;
    }
}
