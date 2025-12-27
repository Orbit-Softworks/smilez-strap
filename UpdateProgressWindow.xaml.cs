using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

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
            // Step 1: Download update (0-60%)
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
                                var progressPercentage = (int)((totalRead * 60) / totalBytes);
                                UpdateStatus("Downloading update...", progressPercentage);
                                DetailsText.Text = $"Downloaded {totalRead / 1024 / 1024} MB of {totalBytes / 1024 / 1024} MB";
                            }
                        }
                    }
                }
            }
            
            UpdateStatus("Download complete!", 60);
            await Task.Delay(500);
            
            // Step 2: Prepare update (60-70%)
            UpdateStatus("Preparing update...", 65);
            DetailsText.Text = "Creating update script...";
            
            string currentExePath = Process.GetCurrentProcess().MainModule?.FileName ??
                                    Path.Combine(AppContext.BaseDirectory, "SmilezStrap.exe");
            
            int currentProcessId = Process.GetCurrentProcess().Id;
            
            // Create update batch script
            string batchPath = Path.Combine(Path.GetTempPath(), "update_smilezstrap.bat");
            string batchContent = $@"@echo off
title SmilezStrap Updater
echo Waiting for SmilezStrap to close...
timeout /t 1 /nobreak >nul

:waitloop
tasklist /FI ""PID eq {currentProcessId}"" 2>NUL | find ""{currentProcessId}"" >NUL
if NOT ERRORLEVEL 1 (
    timeout /t 1 /nobreak >nul
    goto waitloop
)

echo Installing update...
timeout /t 1 /nobreak >nul

del /f /q ""{currentExePath}"" 2>nul
if exist ""{currentExePath}"" (
    echo Retrying...
    timeout /t 2 /nobreak >nul
    del /f /q ""{currentExePath}""
)

copy /y ""{tempExePath}"" ""{currentExePath}""
if ERRORLEVEL 1 (
    echo Update failed! Press any key to exit.
    pause >nul
    exit
)

echo Starting SmilezStrap v{newVersion}...
start """" ""{currentExePath}""

timeout /t 2 /nobreak >nul
del /f /q ""{tempExePath}"" 2>nul
del /f /q ""{batchPath}"" 2>nul
";
            File.WriteAllText(batchPath, batchContent);
            
            UpdateStatus("Ready to install", 70);
            await Task.Delay(500);
            
            // Step 3: Launch updater (70-90%)
            UpdateStatus("Launching updater...", 75);
            DetailsText.Text = "Starting update process...";
            
            Process.Start(new ProcessStartInfo
            {
                FileName = batchPath,
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            
            UpdateStatus("Updater started", 85);
            await Task.Delay(500);
            
            // Step 4: Close application (90-100%)
            UpdateStatus("Closing application...", 90);
            DetailsText.Text = "SmilezStrap will restart with the new version";
            await Task.Delay(1000);
            
            UpdateStatus("Update complete!", 100);
            await Task.Delay(500);
            
            // Close the application
            Application.Current.Shutdown();
        }

        private void UpdateStatus(string message, int progress)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = message;
                UpdateProgressBar.Value = progress;
                ProgressText.Text = $"{progress}%";
            });
        }
    }
}
