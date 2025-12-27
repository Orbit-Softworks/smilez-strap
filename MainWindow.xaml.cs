using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
            
            // Add User-Agent header to avoid 403 errors
            httpClient.DefaultRequestHeaders.Add("User-Agent", "SmilezStrap");
            
            InitializeApp();
            LoadSettings();
            
            // Set initial view
            HomeButton_Click(null, null);
            
            // Auto-check for updates on startup
            CheckForUpdatesOnStartup();
            
            // Load About content
            LoadAboutContent();
        }

        private async void CheckForUpdatesOnStartup()
        {
            if (config?.AutoCheckUpdates ?? true)
            {
                await CheckForAppUpdate(false); // false = don't show "no update" message
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            // Show About view instead of opening browser
            HomeView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Collapsed;
            AboutView.Visibility = Visibility.Visible;
            UpdateMenuButtonState(AboutButton, HomeButton, SettingsButton);
        }

        private void VisitGitHub_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/Orbit-Softworks/smilez-strap") { UseShellExecute = true });
        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            // Show confirmation popup instead of directly opening
            DiscordPopup.Visibility = Visibility.Visible;
        }

        private void DiscordPopupYes_Click(object sender, RoutedEventArgs e)
        {
            DiscordPopup.Visibility = Visibility.Collapsed;
            Process.Start(new ProcessStartInfo("https://discord.gg/JSJcNC4Jv9") { UseShellExecute = true });
        }

        private void DiscordPopupNo_Click(object sender, RoutedEventArgs e)
        {
            DiscordPopup.Visibility = Visibility.Collapsed;
        }

        private void HomeButton_Click(object? sender, RoutedEventArgs? e)
        {
            HomeView.Visibility = Visibility.Visible;
            SettingsView.Visibility = Visibility.Collapsed;
            AboutView.Visibility = Visibility.Collapsed;
            UpdateMenuButtonState(HomeButton, SettingsButton, AboutButton);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            HomeView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
            AboutView.Visibility = Visibility.Collapsed;
            UpdateMenuButtonState(SettingsButton, HomeButton, AboutButton);
        }

        private void UpdateMenuButtonState(Button activeButton, params Button[] inactiveButtons)
        {
            // Set active button style
            activeButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A2A"));
            activeButton.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4444"));
            
            // Set inactive buttons style
            foreach (var button in inactiveButtons)
            {
                button.Background = Brushes.Transparent;
                button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999"));
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private async void LaunchRoblox_Click(object sender, RoutedEventArgs e)
        {
            // Apply FPS limit before launching (always apply)
            ApplyFpsLimit();

            this.Hide();

            var progressWindow = new ProgressWindow(false);
            progressWindow.Show();
        }

        private async void LaunchStudio_Click(object sender, RoutedEventArgs e)
        {
            // Apply FPS limit before launching (always apply)
            ApplyFpsLimit();

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
                    bool applied = ApplyFpsLimit();
                    
                    if (applied)
                    {
                        MessageBox.Show($"✓ FPS limit set to exactly {fpsLimit} FPS!\n\n" +
                                      "IMPORTANT: The FPS limit will take effect on your NEXT Roblox launch.\n\n" +
                                      "If Roblox is currently running:\n" +
                                      "1. Close ALL Roblox windows completely\n" +
                                      "2. Launch Roblox again from SmilezStrap\n\n" +
                                      "Your FPS will now be locked to exactly {fpsLimit} FPS (not rounded to 60/120/144/240).\n\n" +
                                      "The in-game 'Maximum Frame Rate' menu will be disabled automatically.", 
                                        "FPS Limit Applied Successfully", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"FPS limit set to {fpsLimit}, but could not apply settings automatically.\n" +
                                      "You may need to manually set FPS limit in Roblox settings.", 
                                      "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            else
            {
                MessageBox.Show("Please enter a valid FPS limit between 1 and 999.", 
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                FpsLimitTextBox.Text = config?.FpsLimit.ToString() ?? "60";
            }
        }

        private bool ApplyFpsLimit()
        {
            if (config?.FpsLimit > 0)
            {
                try
                {
                    // Method 1: Try new ClientSettings location (Roblox 2023+)
                    bool success = SetFpsLimitNewMethod(config.FpsLimit);
                    
                    if (!success)
                    {
                        // Method 2: Try old GlobalSettings location (fallback)
                        success = SetFpsLimitOldMethod(config.FpsLimit);
                    }
                    
                    if (success)
                    {
                        Console.WriteLine($"Applied FPS limit {config.FpsLimit}");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("Failed to apply FPS limit - could not find Roblox settings");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to apply FPS limit: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        private bool SetFpsLimitNewMethod(int fpsLimit)
        {
            try
            {
                // New location: %LOCALAPPDATA%\Roblox\Versions\[version]\ClientSettings
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string robloxVersions = Path.Combine(localAppData, "Roblox", "Versions");
                
                bool anySuccess = false;
                
                if (Directory.Exists(robloxVersions))
                {
                    var versionDirs = Directory.GetDirectories(robloxVersions);
                    
                    if (versionDirs.Length == 0)
                    {
                        Console.WriteLine("No Roblox version folders found!");
                        return false;
                    }
                    
                    foreach (var versionDir in versionDirs)
                    {
                        string clientSettingsDir = Path.Combine(versionDir, "ClientSettings");
                        Directory.CreateDirectory(clientSettingsDir);
                        
                        string clientSettingsPath = Path.Combine(clientSettingsDir, "ClientAppSettings.json");
                        
                        // Build the settings dictionary
                        var settingsDict = new Dictionary<string, object>();
                        
                        // Read existing settings if they exist
                        if (File.Exists(clientSettingsPath))
                        {
                            try
                            {
                                string existingJson = File.ReadAllText(clientSettingsPath);
                                var existingSettings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(existingJson);
                                
                                if (existingSettings != null)
                                {
                                    foreach (var kvp in existingSettings)
                                    {
                                        settingsDict[kvp.Key] = kvp.Value;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Warning: Could not read existing settings: {ex.Message}");
                            }
                        }
                        
                        // Set the FPS limit flags - THIS IS THE EXACT COMBINATION THAT WORKS!
                        settingsDict["DFIntTaskSchedulerTargetFps"] = fpsLimit;
                        settingsDict["FFlagGameBasicSettingsFramerateCap5"] = false;
                        settingsDict["FFlagTaskSchedulerLimitTargetFpsTo2402"] = false;
                        settingsDict["DFFlagTaskSchedulerLimitTargetFpsTo60"] = false;
                        
                        // Serialize with indentation for readability
                        var options = new JsonSerializerOptions 
                        { 
                            WriteIndented = true,
                            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        };
                        
                        string jsonContent = JsonSerializer.Serialize(settingsDict, options);
                        
                        File.WriteAllText(clientSettingsPath, jsonContent);
                        Console.WriteLine($"✓ Applied FPS limit {fpsLimit} to: {versionDir}");
                        Console.WriteLine($"  File: {clientSettingsPath}");
                        anySuccess = true;
                    }
                    
                    if (anySuccess)
                    {
                        Console.WriteLine($"Successfully applied FPS limit to {versionDirs.Length} version folder(s)");
                    }
                }
                else
                {
                    Console.WriteLine("Roblox Versions folder not found!");
                }
                
                return anySuccess;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error with new method: {ex.Message}");
                return false;
            }
        }

        private bool SetFpsLimitOldMethod(int fpsLimit)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string robloxPath = Path.Combine(localAppData, "Roblox");
                
                if (!Directory.Exists(robloxPath))
                    return false;
                
                // Try to find existing settings file
                var files = Directory.GetFiles(robloxPath, "GlobalSettings_*.xml");
                string settingsPath;
                
                if (files.Length > 0)
                {
                    settingsPath = files[0];
                }
                else
                {
                    // Create new file
                    settingsPath = Path.Combine(robloxPath, "GlobalSettings_SmilezStrap.xml");
                }
                
                string content;
                if (File.Exists(settingsPath))
                {
                    content = File.ReadAllText(settingsPath);
                    
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
                }
                else
                {
                    // Create new file
                    content = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<GlobalSettings>
  <FramerateManager>
    <FramerateLimit>{fpsLimit}</FramerateLimit>
  </FramerateManager>
</GlobalSettings>";
                }
                
                File.WriteAllText(settingsPath, content);
                Console.WriteLine($"Set FPS limit to {fpsLimit} in {settingsPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error with old method: {ex.Message}");
                return false;
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

        private async void LoadAboutContent()
        {
            try
            {
                // Fetch README from GitHub
                string readmeUrl = $"https://raw.githubusercontent.com/{GITHUB_REPO}/main/README.md";
                string readmeContent = await httpClient.GetStringAsync(readmeUrl);
                
                // Convert markdown to plain text (simple conversion)
                readmeContent = readmeContent.Replace("# ", "")
                                           .Replace("## ", "")
                                           .Replace("### ", "")
                                           .Replace("**", "")
                                           .Replace("*", "")
                                           .Replace("`", "");
                
                // Remove markdown links but keep text
                readmeContent = Regex.Replace(readmeContent, @"\[([^\]]+)\]\([^\)]+\)", "$1");
                
                AboutContentText.Text = readmeContent;
            }
            catch (Exception ex)
            {
                AboutContentText.Text = $"SmilezStrap - Roblox Bootstrapper\n\n" +
                                       $"Version: {VERSION}\n\n" +
                                       $"SmilezStrap is a custom Roblox bootstrapper that provides enhanced control over your Roblox installation.\n\n" +
                                       $"Features:\n" +
                                       $"• Custom FPS limiting\n" +
                                       $"• Automatic updates\n" +
                                       $"• Clean and modern interface\n" +
                                       $"• Easy Roblox and Studio launching\n\n" +
                                       $"Could not load README from GitHub. Please check your internet connection.";
                Console.WriteLine($"Error loading about content: {ex.Message}");
            }
        }

        private async Task<bool> CheckForAppUpdate(bool showNoUpdateMessage = true)
        {
            try
            {
                // Always check for updates when called
                var response = await httpClient.GetStringAsync($"https://api.github.com/repos/{GITHUB_REPO}/releases/latest");
                var releaseInfo = JsonDocument.Parse(response);
                string? latestVersion = releaseInfo.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "1.0.0";
                
                if (string.IsNullOrEmpty(latestVersion))
                {
                    if (showNoUpdateMessage)
                    {
                        MessageBox.Show("Could not check for updates. Please try again later.",
                                        "Update Check", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return true;
                }
                
                if (Version.TryParse(VERSION, out Version? currentVersion) && 
                    Version.TryParse(latestVersion, out Version? latestVersionObj))
                {
                    if (latestVersionObj <= currentVersion)
                    {
                        if (showNoUpdateMessage)
                        {
                            MessageBox.Show($"You are using the latest version (v{VERSION}).",
                                            "Up to Date", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        return true;
                    }
                }
                else
                {
                    if (showNoUpdateMessage)
                    {
                        MessageBox.Show("Could not check for updates. Please try again later.",
                                        "Update Check", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return true;
                }

                // Update available - ask user if they want to update
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
                    if (name != null && (name.EndsWith("SmilezStrap-Setup.exe", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("SmilezStrap.exe", StringComparison.OrdinalIgnoreCase)))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    MessageBox.Show("Update found, but no installer found in release assets.", 
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return true;
                }

                // Download the update
                string tempExePath = Path.Combine(Path.GetTempPath(), "SmilezStrap_Update.exe");
                using (var responseStream = await httpClient.GetStreamAsync(downloadUrl))
                using (var fileStream = new FileStream(tempExePath, FileMode.Create))
                {
                    await responseStream.CopyToAsync(fileStream);
                }

                string currentExePath = Process.GetCurrentProcess().MainModule?.FileName ??
                                        Path.Combine(AppContext.BaseDirectory, "SmilezStrap.exe");
                
                int currentProcessId = Process.GetCurrentProcess().Id;

                // Create update batch script with better process handling
                string batchPath = Path.Combine(Path.GetTempPath(), "update_smilezstrap.bat");
                string batchContent = $@"@echo off
echo Waiting for SmilezStrap to close...
timeout /t 1 /nobreak >nul

:waitloop
tasklist /FI ""PID eq {currentProcessId}"" 2>NUL | find ""{currentProcessId}"" >NUL
if NOT ERRORLEVEL 1 (
    timeout /t 1 /nobreak >nul
    goto waitloop
)

echo Updating SmilezStrap...
timeout /t 1 /nobreak >nul

del /f /q ""{currentExePath}"" 2>nul
if exist ""{currentExePath}"" (
    echo Failed to delete old file, retrying...
    timeout /t 2 /nobreak >nul
    del /f /q ""{currentExePath}""
)

copy /y ""{tempExePath}"" ""{currentExePath}""
if ERRORLEVEL 1 (
    echo Update failed! Press any key to exit.
    pause >nul
    exit
)

echo Starting SmilezStrap...
start """" ""{currentExePath}""

timeout /t 2 /nobreak >nul
del /f /q ""{tempExePath}"" 2>nul
del /f /q ""{batchPath}"" 2>nul
";
                File.WriteAllText(batchPath, batchContent);

                // Run the update script
                Process.Start(new ProcessStartInfo
                {
                    FileName = batchPath,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                // Give the batch file time to start
                await Task.Delay(500);

                // Close the current application
                Application.Current.Shutdown();
                return false;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP Error checking for updates: {ex.Message}");
                
                if (showNoUpdateMessage)
                {
                    MessageBox.Show($"Failed to check for updates.\n\nError: {ex.Message}\n\nThis may be due to GitHub API rate limiting or network issues.\n\nPlease try again in a few minutes or check your internet connection.",
                                    "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for updates: {ex.Message}");
                
                if (showNoUpdateMessage)
                {
                    MessageBox.Show($"Failed to check for updates: {ex.Message}\n\nPlease check your internet connection.",
                                    "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return true;
            }
        }

        // New method for Check Updates button
        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            // Show checking message
            var checkButton = sender as Button;
            if (checkButton != null)
            {
                string originalText = checkButton.Content.ToString();
                checkButton.Content = "Checking...";
                checkButton.IsEnabled = false;
                
                try
                {
                    await CheckForAppUpdate(true);
                }
                finally
                {
                    checkButton.Content = originalText;
                    checkButton.IsEnabled = true;
                }
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
        public bool AutoCheckUpdates { get; set; } = true;
    }
}
