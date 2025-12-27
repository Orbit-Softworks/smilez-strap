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
                Environment.Exit(1);
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
            
            // Step 2: Verify download (50-60%)
            UpdateStatus("Verifying download...", 55);
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
            
            UpdateStatus("Verification successful", 60);
            DetailsText.Text = $"File size: {fileInfo.Length / 1024 / 1024} MB";
            await Task.Delay(600);
            
            // Step 3: Prepare for installation (60-80%)
            UpdateStatus("Preparing installation...", 65);
            DetailsText.Text = "Getting ready to install update...";
            await Task.Delay(800);
            
            UpdateStatus("Ready to install", 75);
            DetailsText.Text = "Installation will begin shortly";
            await Task.Delay(1000);
            
            // Step 4: Countdown (80-95%)
            for (int i = 3; i > 0; i--)
            {
                int progress = 80 + ((3 - i) * 5);
                UpdateStatus($"Installing in {i}...", progress);
                DetailsText.Text = $"SmilezStrap will close in {i} second{(i > 1 ? "s" : "")}...";
                await Task.Delay(1000);
            }
            
            UpdateStatus("Starting installer...", 95);
            DetailsText.Text = "Launching installer...";
            await Task.Delay(300);
            
            // Step 5: Launch installer with proper flags
            UpdateStatus("Launching installer...", 98);
            
            // Start the installer
            var processInfo = new ProcessStartInfo
            {
                FileName = tempExePath,
                UseShellExecute = true,
                // Most installers will handle closing the app automatically
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS"
            };
            
            try
            {
                Process.Start(processInfo);
            }
            catch
            {
                // If flags don't work, try without them
                processInfo.Arguments = "";
                Process.Start(processInfo);
            }
            
            UpdateStatus("Closing SmilezStrap...", 100);
            DetailsText.Text = "Installer is running. SmilezStrap will now close.";
            await Task.Delay(200);
            
            // FORCE CLOSE IMMEDIATELY - no graceful shutdown
            Environment.Exit(0);
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
