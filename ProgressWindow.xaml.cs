using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace SmilezStrap
{
    public partial class ProgressWindow : Window
    {
        private readonly HttpClient httpClient = new HttpClient();
        private CancellationTokenSource cancellationTokenSource;
        private bool isCompleted = false;
        private bool isStudio = false;

        private const string ROBLOX_DOWNLOAD_URL = "https://www.roblox.com/download/client?os=win";
        private const string STUDIO_DOWNLOAD_URL = "https://setup.rbxcdn.com/RobloxStudioInstaller.exe";

        public ProgressWindow(bool launchStudio = false)
        {
            InitializeComponent();
            isStudio = launchStudio;
            cancellationTokenSource = new CancellationTokenSource();
            
            SubtitleText.Text = isStudio ? "Launching Roblox Studio" : "Launching Roblox";
            
            // Start the launch process after window is loaded
            Loaded += async (s, e) => await StartLaunchProcess();
        }

        private void UpdateStatus(string status, string detail = "")
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = status;
                DetailText.Text = detail;
            });
        }

        private void SetProgress(int percent)
        {
            Dispatcher.Invoke(() =>
            {
                PercentText.Text = $"{percent}%";
                
                // Simple width animation - container should be 400px wide
                var targetWidth = 400.0 * (percent / 100.0);
                
                var animation = new DoubleAnimation
                {
                    To = targetWidth,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                ProgressBarFill.BeginAnimation(FrameworkElement.WidthProperty, animation);
            });
        }singMode = EasingMode.EaseOut }
                        };

                        ProgressBarFill.BeginAnimation(FrameworkElement.WidthProperty, animation);
                    }
                }, System.Windows.Threading.DispatcherPriority.Render);
            });
        }

        private void ShowCompletion(bool success, string message = "")
        {
            Dispatcher.Invoke(() =>
            {
                isCompleted = true;
                CancelButton.Visibility = Visibility.Collapsed;
                CloseButton.Visibility = Visibility.Visible;

                if (success)
                {
                    StatusText.Text = isStudio ? "Studio launched successfully!" : "Roblox launched successfully!";
                    SetProgress(100);
                }
                else
                {
                    StatusText.Text = "Error occurred";
                    DetailText.Text = message;
                }
            });
        }

        private async Task StartLaunchProcess()
        {
            try
            {
                if (isStudio)
                    await LaunchStudio();
                else
                    await LaunchRoblox();
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus("Cancelled", "Launch process was cancelled by user");
                    CancelButton.Visibility = Visibility.Collapsed;
                    CloseButton.Visibility = Visibility.Visible;
                });
            }
            catch (Exception ex)
            {
                ShowCompletion(false, ex.Message);
            }
        }

        private async Task LaunchRoblox()
        {
            var token = cancellationTokenSource.Token;

            UpdateStatus("Checking for updates...");
            SetProgress(5);
            await Task.Delay(500, token);
            token.ThrowIfCancellationRequested();

            UpdateStatus("Checking Roblox version...");
            SetProgress(10);
            token.ThrowIfCancellationRequested();

            string installedVersion = GetInstalledRobloxVersion();
            string latestVersion = await GetLatestRobloxVersion();

            bool needsUpdate = installedVersion == null || installedVersion != latestVersion;

            if (needsUpdate)
            {
                UpdateStatus("Downloading Roblox...", "This may take a few minutes");
                string tempPath = Path.Combine(Path.GetTempPath(), "SmilezStrap", "RobloxPlayerInstaller.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

                var downloadProgress = new Progress<int>(p =>
                {
                    UpdateStatus($"Downloading Roblox... {p}%");
                    SetProgress(10 + (p * 40 / 100));
                });

                await DownloadFile(ROBLOX_DOWNLOAD_URL, tempPath, downloadProgress, token);
                token.ThrowIfCancellationRequested();

                UpdateStatus("Installing Roblox...", "Please wait while Roblox is being installed");
                SetProgress(55);

                var installTask = RunInstallerSilently(tempPath);

                int simProgress = 55;
                while (!installTask.IsCompleted && simProgress < 90)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Delay(500, token);
                    simProgress += 2;
                    SetProgress(simProgress);
                }

                await installTask;
                token.ThrowIfCancellationRequested();
                
                SetProgress(90);
                await Task.Delay(2000, token);

                try { File.Delete(tempPath); } catch { }

                installedVersion = GetInstalledRobloxVersion();
                if (installedVersion == null)
                    throw new Exception("Installation failed to register.");
            }

            token.ThrowIfCancellationRequested();
            UpdateStatus("Launching Roblox...");
            SetProgress(95);

            // If Roblox was just installed, the installer already launched it
            if (needsUpdate)
            {
                // Wait for Roblox to start from installer
                await Task.Delay(2000, token);
                
                // Remove desktop shortcuts
                RemoveDesktopShortcuts();
                
                ShowCompletion(true);
                await Task.Delay(2000);
                this.Close();
            }
            else
            {
                // Only launch manually if we didn't install
                await Task.Delay(500, token);
                
                string exePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Roblox", "Versions", installedVersion, "RobloxPlayerBeta.exe");

                if (!File.Exists(exePath))
                    throw new Exception("Roblox executable not found.");

                Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                SetProgress(100);

                await Task.Delay(1000, token);
                RemoveDesktopShortcuts();

                ShowCompletion(true);
                await Task.Delay(2000);
                this.Close();
            }
        }

        private async Task LaunchStudio()
        {
            var token = cancellationTokenSource.Token;

            UpdateStatus("Checking for updates...");
            SetProgress(5);
            await Task.Delay(500, token);
            token.ThrowIfCancellationRequested();

            UpdateStatus("Checking Studio version...");
            SetProgress(10);
            token.ThrowIfCancellationRequested();

            string installedVersion = GetInstalledStudioVersion();
            string latestVersion = await GetLatestStudioVersion();

            bool needsUpdate = installedVersion == null || installedVersion != latestVersion;

            if (needsUpdate)
            {
                UpdateStatus("Downloading Roblox Studio...", "This may take a few minutes");
                string tempPath = Path.Combine(Path.GetTempPath(), "SmilezStrap", "RobloxStudioInstaller.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

                var downloadProgress = new Progress<int>(p =>
                {
                    UpdateStatus($"Downloading Studio... {p}%");
                    SetProgress(10 + (p * 40 / 100));
                });

                await DownloadFile(STUDIO_DOWNLOAD_URL, tempPath, downloadProgress, token);
                token.ThrowIfCancellationRequested();

                UpdateStatus("Installing Roblox Studio...", "Please wait while Studio is being installed");
                SetProgress(55);

                var installTask = RunInstallerSilently(tempPath);

                int simProgress = 55;
                while (!installTask.IsCompleted && simProgress < 90)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Delay(500, token);
                    simProgress += 2;
                    SetProgress(simProgress);
                }

                await installTask;
                token.ThrowIfCancellationRequested();
                
                SetProgress(90);
                await Task.Delay(2000, token);

                try { File.Delete(tempPath); } catch { }

                for (int i = 0; i < 5; i++)
                {
                    token.ThrowIfCancellationRequested();
                    installedVersion = GetInstalledStudioVersion();
                    if (installedVersion != null) break;
                    await Task.Delay(1000, token);
                }

                if (installedVersion == null)
                    throw new Exception("Studio installation completed but version not detected.");
            }

            token.ThrowIfCancellationRequested();
            UpdateStatus("Launching Roblox Studio...");
            SetProgress(95);

            // If Studio was just installed, the installer already launched it
            if (needsUpdate)
            {
                // Wait for Studio to start from installer
                await Task.Delay(2000, token);
                
                // Remove desktop shortcuts
                RemoveDesktopShortcuts();
                
                ShowCompletion(true);
                await Task.Delay(2000);
                this.Close();
            }
            else
            {
                // Only launch manually if we didn't install
                await Task.Delay(500, token);

                string exePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Roblox", "Versions", installedVersion, "RobloxStudioBeta.exe");

                if (!File.Exists(exePath))
                    throw new Exception("Studio executable not found.");

                Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                SetProgress(100);

                await Task.Delay(1000, token);
                RemoveDesktopShortcuts();

                ShowCompletion(true);
                await Task.Delay(2000);
                this.Close();
            }
        }

        private void RemoveDesktopShortcuts()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                
                // Remove Roblox Player shortcut
                string playerShortcut = Path.Combine(desktopPath, "Roblox Player.lnk");
                if (File.Exists(playerShortcut))
                {
                    File.Delete(playerShortcut);
                }

                // Remove Roblox Studio shortcut
                string studioShortcut = Path.Combine(desktopPath, "Roblox Studio.lnk");
                if (File.Exists(studioShortcut))
                {
                    File.Delete(studioShortcut);
                }
            }
            catch (Exception ex)
            {
                // Silently fail - shortcuts aren't critical
                Console.WriteLine($"Failed to remove shortcuts: {ex.Message}");
            }
        }

        // Helper methods
        private async Task<string> GetLatestRobloxVersion()
        {
            var response = await httpClient.GetStringAsync("https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer");
            var json = JsonDocument.Parse(response);
            return json.RootElement.GetProperty("clientVersionUpload").GetString();
        }

        private async Task<string> GetLatestStudioVersion()
        {
            var response = await httpClient.GetStringAsync("https://clientsettingscdn.roblox.com/v2/client-version/WindowsStudio64");
            var json = JsonDocument.Parse(response);
            return json.RootElement.GetProperty("clientVersionUpload").GetString();
        }

        private string GetInstalledRobloxVersion()
        {
            try
            {
                string versionsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Roblox", "Versions");

                if (!Directory.Exists(versionsPath)) return null;

                var versionDirs = Directory.GetDirectories(versionsPath)
                    .Where(d => File.Exists(Path.Combine(d, "RobloxPlayerBeta.exe")))
                    .OrderByDescending(d => Directory.GetCreationTime(d))
                    .ToList();

                return versionDirs.Any() ? Path.GetFileName(versionDirs.First()) : null;
            }
            catch
            {
                return null;
            }
        }

        private string GetInstalledStudioVersion()
        {
            try
            {
                string versionsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Roblox", "Versions");

                if (!Directory.Exists(versionsPath)) return null;

                var studioDirs = Directory.GetDirectories(versionsPath)
                    .Where(d => File.Exists(Path.Combine(d, "RobloxStudioBeta.exe")))
                    .OrderByDescending(d => Directory.GetLastWriteTime(d))
                    .ToList();

                return studioDirs.Any() ? Path.GetFileName(studioDirs.First()) : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task DownloadFile(string url, string destination, IProgress<int> progress, CancellationToken token)
        {
            using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    long totalRead = 0L;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) != 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                        totalRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            var percent = (int)((totalRead * 100L) / totalBytes);
                            progress?.Report(percent);
                        }
                    }
                }
            }
        }

        private async Task<bool> RunInstallerSilently(string installerPath)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process != null)
                    {
                        await Task.Run(() => process.WaitForExit());
                        return process.ExitCode == 0 || process.ExitCode == 1;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isCompleted)
            {
                cancellationTokenSource?.Cancel();
                UpdateStatus("Cancelling...", "Please wait...");
                CancelButton.IsEnabled = false;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
