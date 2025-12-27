using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SmilezStrap
{
    public partial class UpdateProgressWindow : Window
    {
        private readonly string downloadUrl;
        private readonly string newVersion;
        
        public UpdateProgressWindow(string downloadUrl, string newVersion)
        {
            InitializeComponent();
            this.downloadUrl = downloadUrl;
            this.newVersion = newVersion;
            
            Loaded += UpdateProgressWindow_Loaded;
        }

        private async void UpdateProgressWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await PerformUpdate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Update failed: {ex.Message}", "Update Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private async Task PerformUpdate()
        {
            // Step 1: Download update (0-50%)
            UpdateStatus("Downloading update...", 0);
            DetailsText.Text = $"Downloading SmilezStrap v{newVersion}";
            
            string tempExePath = Path.Combine(Path.GetTempPath(), "SmilezStrap_Update.exe");
            
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "SmilezStrap");
                
                using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress = totalBytes != -1;
                    
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempExePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        long totalRead = 0L;
                        int bytesRead;
                        
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;
                            
                            if (canReportProgress)
                            {
                                var progressPercentage = (int)((totalRead * 50) / totalBytes);
                                UpdateStatus("Downloading update...", progressPercentage);
                                DetailsText.Text = $"Downloaded {totalRead / 1024 / 1024} MB of {totalBytes / 1024 / 1024} MB";
                            }
                        }
                    }
                }
            }
            
            UpdateStatus("Download complete!", 50);
            DetailsText.Text = $"Successfully downloaded v{newVersion}";
            await Task.Delay(800);
            
            // Step 2: Verify download (50-55%)
            UpdateStatus("Verifying download...", 52);
            DetailsText.Text = "Checking file integrity...";
            
            if (!File.Exists(tempExePath))
            {
                throw new Exception("Downloaded file not found!");
            }
            
            var fileInfo = new FileInfo(tempExePath);
            if (fileInfo.Length < 1000)
            {
                throw new Exception("Downloaded file is too small!");
            }
            
            UpdateStatus("Verification successful", 55);
            DetailsText.Text = $"File size: {fileInfo.Length / 1024 / 1024} MB";
            await Task.Delay(600);
            
            // Step 3: Prepare update script (55-65%)
            UpdateStatus("Preparing update script...", 58);
            DetailsText.Text = "Creating installation batch file...";
            
            string currentExePath = Process.GetCurrentProcess().MainModule?.FileName ??
                                    Path.Combine(AppContext.BaseDirectory, "SmilezStrap.exe");
            
            int currentProcessId = Process.GetCurrentProcess().Id;
            
            string batchPath = Path.Combine(Path.GetTempPath(), "update_smilezstrap.bat");
            string batchContent = $@"@echo off
title SmilezStrap Updater v{newVersion}
color 0C
echo.
echo =======================================
echo    SmilezStrap Auto Updater
echo =======================================
echo.
echo Waiting for SmilezStrap to close...

timeout /t 2 /nobreak >nul

:waitloop
tasklist /FI ""PID eq {currentProcessId}"" 2>NUL | find ""{currentProcessId}"" >NUL
if NOT ERRORLEVEL 1 (
    timeout /t 1 /nobreak >nul
    goto waitloop
)

echo Application closed successfully!
echo.
echo Installing update...
timeout /t 1 /nobreak >nul

del /f /q ""{currentExePath}"" 2>nul
if exist ""{currentExePath}"" (
    echo Retrying file deletion...
    timeout /t 2 /nobreak >nul
    del /f /q ""{currentExePath}""
)

copy /y ""{tempExePath}"" ""{currentExePath}""
if ERRORLEVEL 1 (
    echo.
    echo ERROR: Update failed!
    echo Press any key to exit...
    pause >nul
    exit
)

echo Update installed successfully!
echo.
echo Starting SmilezStrap v{newVersion}...
start """" ""{currentExePath}""

echo.
echo Cleaning up temporary files...
timeout /t 2 /nobreak >nul
del /f /q ""{tempExePath}"" 2>nul
del /f /q ""{batchPath}"" 2>nul

echo.
echo Done! This window will close automatically.
timeout /t 2 /nobreak >nul
exit
";
            File.WriteAllText(batchPath, batchContent);
            
            UpdateStatus("Update script created", 65);
            DetailsText.Text = "Installation script is ready";
            await Task.Delay(600);
            
            // Step 4: Launch updater (65-75%)
            UpdateStatus("Starting updater...", 68);
            DetailsText.Text = "Launching installation process...";
            
            var processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batchPath}\"",
                UseShellExecute = false,
                CreateNoWindow = false, // Show the window so user can see progress
                WindowStyle = ProcessWindowStyle.Normal
            };
            
            var updaterProcess = Process.Start(processInfo);
            
            UpdateStatus("Updater launched", 75);
            DetailsText.Text = "Installation process started successfully";
            await Task.Delay(1000);
            
            // Step 5: Verify updater is running (75-85%)
            UpdateStatus("Verifying updater...", 78);
            DetailsText.Text = "Confirming installation process...";
            await Task.Delay(800);
            
            if (updaterProcess == null || updaterProcess.HasExited)
            {
                UpdateStatus("Warning: Updater closed", 80);
                DetailsText.Text = "Installation script may have failed";
                await Task.Delay(1500);
            }
            else
            {
                UpdateStatus("Updater confirmed", 85);
                DetailsText.Text = "Installation script is running";
                await Task.Delay(800);
            }
            
            // Step 6: Prepare to close (85-95%)
            UpdateStatus("Finalizing...", 88);
            DetailsText.Text = "Preparing to close SmilezStrap...";
            await Task.Delay(1000);
            
            UpdateStatus("Ready to restart", 93);
            DetailsText.Text = "SmilezStrap will close and restart automatically";
            await Task.Delay(1200);
            
            // Step 7: Countdown (95-100%)
            for (int i = 3; i > 0; i--)
            {
                int progress = 95 + ((4 - i) * 2);
                UpdateStatus($"Restarting in {i}...", progress);
                DetailsText.Text = $"Application will restart in {i} second{(i > 1 ? "s" : "")}...";
                await Task.Delay(1000);
            }
            
            UpdateStatus("Restarting now!", 100);
            DetailsText.Text = "Closing application... Please wait...";
            await Task.Delay(800);
            
            // Close the application
            Application.Current.Shutdown();
        }

        private void UpdateStatus(string message, int progress)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = message;
                ProgressText.Text = $"{progress}%";
                
                // Calculate width based on progress percentage
                var parentBorder = ProgressBarFill.Parent as Border;
                if (parentBorder != null)
                {
                    double maxWidth = parentBorder.ActualWidth > 0 ? parentBorder.ActualWidth : 440;
                    ProgressBarFill.Width = (maxWidth * progress) / 100;
                }
            });
        }
    }
}
