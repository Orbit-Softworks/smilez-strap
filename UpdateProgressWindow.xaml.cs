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
                MessageBox.Show($"Update failed: {ex.Message}\n\nPlease download the update manually from GitHub.", 
                    "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private async Task PerformUpdate()
        {
            // Step 1: Download update (0-60%)
            UpdateStatus("Downloading update...", 0);
            DetailsText.Text = $"Downloading SmilezStrap v{newVersion}";
            
            string tempFolder = Path.Combine(Path.GetTempPath(), "SmilezStrap_Update");
            Directory.CreateDirectory(tempFolder);
            
            string tempExePath = Path.Combine(tempFolder, "SmilezStrap_new.exe");
            
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "SmilezStrap");
                httpClient.Timeout = TimeSpan.FromMinutes(5);
                
                using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress = totalBytes != -1;
                    
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempExePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        long totalRead = 0L;
                        int bytesRead;
                        
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;
                            
                            if (canReportProgress && totalBytes > 0)
                            {
                                var progressPercentage = (int)((totalRead * 60) / totalBytes);
                                UpdateStatus("Downloading update...", progressPercentage);
                                double mbRead = totalRead / 1024.0 / 1024.0;
                                double mbTotal = totalBytes / 1024.0 / 1024.0;
                                DetailsText.Text = $"Downloaded {mbRead:F1} MB of {mbTotal:F1} MB";
                            }
                        }
                    }
                }
            }
            
            UpdateStatus("Download complete!", 60);
            DetailsText.Text = $"Successfully downloaded v{newVersion}";
            await Task.Delay(500);
            
            // Step 2: Verify download (60-70%)
            UpdateStatus("Verifying download...", 65);
            DetailsText.Text = "Checking file integrity...";
            
            if (!File.Exists(tempExePath))
            {
                throw new Exception("Downloaded file not found!");
            }
            
            var fileInfo = new FileInfo(tempExePath);
            if (fileInfo.Length < 100000) // Less than 100KB is definitely wrong
            {
                throw new Exception($"Downloaded file is too small ({fileInfo.Length} bytes)! Update may be corrupted.");
            }
            
            UpdateStatus("Verification successful", 70);
            DetailsText.Text = $"File verified ({fileInfo.Length / 1024 / 1024:F1} MB)";
            await Task.Delay(500);
            
            // Step 3: Create updater script (70-80%)
            UpdateStatus("Creating update script...", 75);
            DetailsText.Text = "Preparing to replace application...";
            
            string currentExePath = Process.GetCurrentProcess().MainModule?.FileName ?? 
                                   Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmilezStrap.exe");
            
            string updateBatchPath = Path.Combine(tempFolder, "update.bat");
            
            // Create PowerShell script for more reliable updating
            string updateScriptPath = Path.Combine(tempFolder, "update.ps1");
            string psScript = $@"
# SmilezStrap Auto-Updater
Write-Host 'SmilezStrap Auto-Updater v{newVersion}' -ForegroundColor Cyan
Write-Host '======================================' -ForegroundColor Cyan
Write-Host ''

# Wait for SmilezStrap to close
Write-Host 'Waiting for SmilezStrap to close...' -ForegroundColor Yellow
Start-Sleep -Seconds 2

$processName = 'SmilezStrap'
$maxWait = 30
$waited = 0

while ((Get-Process -Name $processName -ErrorAction SilentlyContinue) -and ($waited -lt $maxWait)) {{
    Write-Host '.' -NoNewline
    Start-Sleep -Seconds 1
    $waited++
}}

Write-Host ''

if (Get-Process -Name $processName -ErrorAction SilentlyContinue) {{
    Write-Host 'Force closing SmilezStrap...' -ForegroundColor Red
    Stop-Process -Name $processName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}}

Write-Host 'Application closed!' -ForegroundColor Green
Write-Host ''

# Backup old version
Write-Host 'Creating backup...' -ForegroundColor Yellow
$backupPath = '{currentExePath}.backup'
if (Test-Path $backupPath) {{
    Remove-Item $backupPath -Force
}}
Copy-Item '{currentExePath}' $backupPath -Force

# Replace with new version
Write-Host 'Installing update...' -ForegroundColor Yellow
Start-Sleep -Seconds 1

try {{
    Remove-Item '{currentExePath}' -Force
    Copy-Item '{tempExePath}' '{currentExePath}' -Force
    Write-Host 'Update installed successfully!' -ForegroundColor Green
}} catch {{
    Write-Host 'Update failed! Restoring backup...' -ForegroundColor Red
    Copy-Item $backupPath '{currentExePath}' -Force
    Write-Host 'Backup restored.' -ForegroundColor Yellow
    Read-Host 'Press Enter to exit'
    exit 1
}}

Write-Host ''
Write-Host 'Starting SmilezStrap v{newVersion}...' -ForegroundColor Cyan
Start-Sleep -Seconds 1

# Start the updated application
Start-Process '{currentExePath}'

# Cleanup
Write-Host 'Cleaning up...' -ForegroundColor Yellow
Start-Sleep -Seconds 2
Remove-Item '{tempFolder}' -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ''
Write-Host 'Update complete!' -ForegroundColor Green
Start-Sleep -Seconds 2
";

            File.WriteAllText(updateScriptPath, psScript);
            
            // Create batch file to run PowerShell script
            string batchContent = $@"@echo off
powershell -ExecutionPolicy Bypass -File ""{updateScriptPath}""
";
            File.WriteAllText(updateBatchPath, batchContent);
            
            UpdateStatus("Update script created", 80);
            DetailsText.Text = "Updater is ready";
            await Task.Delay(500);
            
            // Step 4: Countdown (80-95%)
            for (int i = 3; i > 0; i--)
            {
                int progress = 80 + ((3 - i) * 5);
                UpdateStatus($"Restarting in {i}...", progress);
                DetailsText.Text = $"SmilezStrap will close and update in {i} second{(i > 1 ? "s" : "")}";
                await Task.Delay(1000);
            }
            
            UpdateStatus("Starting updater...", 95);
            DetailsText.Text = "Launching update process...";
            
            // Start the updater batch file
            var processInfo = new ProcessStartInfo
            {
                FileName = updateBatchPath,
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal,
                WorkingDirectory = tempFolder
            };
            
            Process.Start(processInfo);
            
            UpdateStatus("Closing SmilezStrap...", 98);
            DetailsText.Text = "Update in progress...";
            await Task.Delay(500);
            
            UpdateStatus("Update in progress!", 100);
            DetailsText.Text = "SmilezStrap will restart automatically";
            await Task.Delay(500);
            
            // Force exit immediately
            Environment.Exit(0);
        }

        private void UpdateStatus(string message, int progress)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = message;
                ProgressText.Text = $"{progress}%";
                
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
