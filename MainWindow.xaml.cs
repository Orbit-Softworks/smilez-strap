using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;  // ADD THIS LINE - This is what's missing!
using System.Windows.Input;
using System.Windows.Media;     // ADD THIS LINE - For System.Windows.Media.Brushes
using System.Windows.Navigation;
using System.Text.Json;
using System.Text.RegularExpressions;

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
            VersionText.Text = $"v{VERSION}";
            InitializeApp();
            LoadSettings();
            
            // Set initial view
            HomeButton_Click(null, null);
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

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            HomeView.Visibility = Visibility.Visible;
            SettingsView.Visibility = Visibility.Collapsed;
            UpdateMenuButtonState(HomeButton, SettingsButton);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            HomeView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
            UpdateMenuButtonState(SettingsButton, HomeButton);
        }

        private void UpdateMenuButtonState(Button activeButton, Button inactiveButton)
        {
            // Simplify this method - it has some redundant code
            activeButton.Background = Brushes.Transparent;
            activeButton.Foreground = Brushes.White;
            
            inactiveButton.Background = Brushes.Transparent;
            inactiveButton.Foreground = Brushes.White;
            
            // FindResource might not exist, let's use a simpler approach
            try
            {
                // Try to get the button background from resources
                if (this.Resources["ButtonBackground"] is Brush buttonBackground)
                {
                    activeButton.Background = buttonBackground;
                }
                else
                {
                    activeButton.Background = Brushes.Transparent;
                }
            }
            catch
            {
                activeButton.Background = Brushes.Transparent;
            }
            
            activeButton.Foreground = Brushes.White;
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

            // Apply FPS limit before launching if enabled
            if (config?.AutoApplyFpsLimit ?? true)
            {
                ApplyFpsLimit();
            }

            this.Hide();

            var progressWindow = new ProgressWindow(false);
            progressWindow.Show();
        }

        private async void LaunchStudio_Click(object sender, RoutedEventArgs e)
        {
            bool canContinue = await CheckForAppUpdate();
            if (!canContinue) return;

            bool bootstrapOk = await CheckForBootstrapperUpdate();
            if (!bootstrapOk) return;

            // Apply FPS limit before launching if enabled
            if (config?.AutoApplyFpsLimit ?? true)
            {
                ApplyFpsLimit();
            }

            this.Hide();

            var progressWindow = new ProgressWindow(true);
            progressWindow.Show();
        }

        private void SetFpsButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(FpsLimitTextBox.Text, out int fpsLimit) && fpsLimit >= 1 && fpsLimit <= 999)
            {
                if (config != null)
                {
                    config.FpsLimit = fpsLimit;
                    SaveConfig();
                    
                    // Apply the FPS limit immediately
                    ApplyFpsLimit();
                    
                    MessageBox.Show($"FPS limit set to {fpsLimit}. This will be applied to Roblox.", 
                                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Please enter a valid FPS limit between 1 and 999.", 
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                FpsLimitTextBox.Text = config?.FpsLimit.ToString() ?? "60";
            }
        }

        private void ApplyFpsLimit()
        {
            if (config?.FpsLimit > 0)
            {
                try
                {
                    string robloxSettingsPath = GetRobloxSettingsPath();
                    if (!string.IsNullOrEmpty(robloxSettingsPath) && File.Exists(robloxSettingsPath))
                    {
                        SetFpsLimitInSettings(robloxSettingsPath, config.FpsLimit);
                        Console.WriteLine($"Applied FPS limit {config.FpsLimit} to {robloxSettingsPath}");
                    }
                    else
                    {
                        // Create new settings file if it doesn't exist
                        CreateRobloxSettingsWithFpsLimit(config.FpsLimit);
                        Console.WriteLine($"Created new Roblox settings with FPS limit {config.FpsLimit}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to apply FPS limit: {ex.Message}");
                }
            }
        }

        private string? GetRobloxSettingsPath()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string robloxPath = Path.Combine(localAppData, "Roblox");
                
                if (Directory.Exists(robloxPath))
                {
                    var files = Directory.GetFiles(robloxPath, "GlobalSettings_*.xml");
                    if (files.Length > 0)
                    {
                        return files[0]; // Return the first settings file
                    }
                }
            }
            catch { }
            
            return null;
        }

        private void SetFpsLimitInSettings(string settingsPath, int fpsLimit)
        {
            try
            {
                string content = File.ReadAllText(settingsPath);
                
                // Check if FramerateManager exists
                if (content.Contains("<FramerateManager>"))
                {
                    // Update existing FPS limit
                    string pattern = @"<FramerateLimit>\d+</FramerateLimit>";
                    string replacement = $"<FramerateLimit>{fpsLimit}</FramerateLimit>";
                    
                    if (Regex.IsMatch(content, pattern))
                    {
                        content = Regex.Replace(content, pattern, replacement);
                    }
                    else
                    {
                        // Insert FPS limit into FramerateManager
                        content = content.Replace("</FramerateManager>", 
                            $"<FramerateLimit>{fpsLimit}</FramerateLimit></FramerateManager>");
                    }
                }
                else
                {
                    // Create new FramerateManager section
                    string framerateSection = $"\n  <FramerateManager>\n    <FramerateLimit>{fpsLimit}</FramerateLimit>\n  </FramerateManager>";
                    
                    // Insert before closing GlobalSettings tag
                    if (content.Contains("</GlobalSettings>"))
                    {
                        content = content.Replace("</GlobalSettings>", $"{framerateSection}\n</GlobalSettings>");
                    }
                    else
                    {
                        content += framerateSection;
                    }
                }
                
                File.WriteAllText(settingsPath, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting FPS limit: {ex.Message}");
            }
        }

        private void CreateRobloxSettingsWithFpsLimit(int fpsLimit)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string robloxPath = Path.Combine(localAppData, "Roblox");
                Directory.CreateDirectory(robloxPath);
                
                string settingsPath = Path.Combine(robloxPath, "GlobalSettings_SmilezStrap.xml");
                
                string content = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<GlobalSettings>
  <FramerateManager>
    <FramerateLimit>{fpsLimit}</FramerateLimit>
  </FramerateManager>
</GlobalSettings>";
                
                File.WriteAllText(settingsPath, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating Roblox settings: {ex.Message}");
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            this.Close();
        }

        private void InitializeApp()
        {
            appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmilezStrap");
            Directory.CreateDirectory(appDataPath!);
            LoadConfig();
        }

        private void LoadSettings()
        {
            if (config != null)
            {
                FpsLimitTextBox.Text = config.FpsLimit.ToString();
                AutoRemoveCheckBox.IsChecked = config.AutoRemoveShortcuts;
                AutoUpdateCheckBox.IsChecked = config.AutoCheckUpdates;
                AutoFpsCheckBox.IsChecked = config.AutoApplyFpsLimit;
            }
        }

        private void SaveSettings()
        {
            if (config != null)
            {
                if (int.TryParse(FpsLimitTextBox.Text, out int fpsLimit) && fpsLimit >= 1 && fpsLimit <= 999)
                {
                    config.FpsLimit = fpsLimit;
                }
                config.AutoRemoveShortcuts = AutoRemoveCheckBox.IsChecked ?? true;
                config.AutoCheckUpdates = AutoUpdateCheckBox.IsChecked ?? true;
                config.AutoApplyFpsLimit = AutoFpsCheckBox.IsChecked ?? true;
                SaveConfig();
            }
        }

        private void LoadConfig()
        {
            string configPath = Path.Combine(appDataPath!, "config.json");
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    config = JsonSerializer.Deserialize<Config>(json) ?? new Config();
                }
                catch
                {
                    config = new Config();
                }
            }
            else
            {
                config = new Config();
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            try
            {
                string configPath = Path.Combine(appDataPath!, "config.json");
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
            }
        }

        private async Task<bool> CheckForAppUpdate()
        {
            if (!(config?.AutoCheckUpdates ?? true))
                return true;

            try
            {
                var response = await httpClient.GetStringAsync($"https://api.github.com/repos/{GITHUB_REPO}/releases/latest");
                var releaseInfo = JsonDocument.Parse(response);
                string? latestVersion = releaseInfo.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "1.0.0";
                
                if (string.IsNullOrEmpty(latestVersion))
                    return true;
                
                if (Version.TryParse(VERSION, out Version? currentVersion) && 
                    Version.TryParse(latestVersion, out Version? latestVersionObj))
                {
                    if (latestVersionObj <= currentVersion)
                        return true;
                }
                else
                {
                    return true;
                }

                var result = MessageBox.Show(
                    $"SmilezStrap v{latestVersion} is available!\n\nCurrent version: v{VERSION}\n\nDownload and install automatically? (App will restart)",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information
                );
                if (result != MessageBoxResult.Yes)
                    return true;

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
                    return true;
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
            return true;
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveSettings();
            base.OnClosed(e);
        }
    }

    public class Config
    {
        public string RobloxVersion { get; set; } = string.Empty;
        public string StudioVersion { get; set; } = string.Empty;
        public int FpsLimit { get; set; } = 60;
        public bool AutoRemoveShortcuts { get; set; } = true;
        public bool AutoCheckUpdates { get; set; } = true;
        public bool AutoApplyFpsLimit { get; set; } = true;
    }
}
