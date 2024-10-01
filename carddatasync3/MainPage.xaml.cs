using Microsoft.Maui.Storage;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace carddatasync3
{
    public partial class MainPage : ContentPage
    {
        private string _backupPath;
        private string _outFilePath;

        public MainPage()
        {
            InitializeComponent();
            
            // Get the path to the Desktop
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            
            // Define the backup and output paths on the Desktop
            _backupPath = Path.Combine(desktopPath, "Backup");
            _outFilePath = Path.Combine(desktopPath, "Output");
        }

        // Check and create files
        private bool CheckAndCreateFile(string filePath, string fileContent)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    File.WriteAllText(filePath, fileContent);
                }
                return true;
            }
            catch (Exception ex)
            {
                OutputEditor.Text += $"Error creating file: {ex.Message}\n";
                return false;
            }
        }

        // Create directories if they don't exist
        private void CheckAndCreateDirectories()
        {
            if (!Directory.Exists(_backupPath))
            {
                Directory.CreateDirectory(_backupPath);
            }

            if (!Directory.Exists(_outFilePath))
            {
                Directory.CreateDirectory(_outFilePath);
            }
        }

        // Simulate fingerprint upload
        private async Task SimulateFingerprintUploadAsync()
        {
            try
            {
                string fingerprintFilePath = Path.Combine(_outFilePath, "FingerOut.txt");
                string backupFilePath = Path.Combine(_backupPath, $"FingerOut_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                // Simulate file creation and backup
                CheckAndCreateFile(fingerprintFilePath, "Simulated Fingerprint Data");
                File.Copy(fingerprintFilePath, backupFilePath, true);

                OutputEditor.Text += $"Fingerprint data uploaded and backed up at {backupFilePath}\n";
                await DisplayAlert("Success", "Fingerprint uploaded and backed up!", "OK");
            }
            catch (Exception ex)
            {
                OutputEditor.Text += $"Error during fingerprint upload: {ex.Message}\n";
                await DisplayAlert("Error", $"Failed: {ex.Message}", "OK");
            }
        }

        // Retry card data sync
        private async Task RetryCardDataSync()
        {
            try
            {
                string errorDataPath = Path.Combine(_backupPath, "ErroData.txt");
                if (File.Exists(errorDataPath))
                {
                    string[] failedData = File.ReadAllLines(errorDataPath);
                    foreach (var line in failedData)
                    {
                        OutputEditor.Text += $"Retrying card sync for data: {line}\n";
                    }
                    File.Delete(errorDataPath);
                    OutputEditor.Text += $"Card data sync retry successful. Removed {errorDataPath}.\n";
                }
                else
                {
                    OutputEditor.Text += "No failed card data found for retry.\n";
                }

                await DisplayAlert("Info", "Card data sync retry completed.", "OK");
            }
            catch (Exception ex)
            {
                OutputEditor.Text += $"Error during card data sync retry: {ex.Message}\n";
                await DisplayAlert("Error", $"Failed: {ex.Message}", "OK");
            }
        }

        // Simulate the interaction with HCM service
        private async void OnUploadToHCMClicked(object sender, EventArgs e)
        {
            // Simulate threading with Task.Run for a long-running process
            await Task.Run(() =>
            {
                // Log that the upload has started
                Dispatcher.Dispatch(() => OutputEditor.Text += "Starting upload to HCM...\n");

                // Simulate long-running task (e.g., network request)
                SimulateHCMUpload();

                // Log that the upload has completed
                Dispatcher.Dispatch(() => OutputEditor.Text += "Upload to HCM completed.\n");
            });
        }

        // Simulate a long-running HCM upload process
        private void SimulateHCMUpload()
        {
            // Simulating some time-consuming task (e.g., network upload)
            Thread.Sleep(5000); // Simulates a 5-second task
        }

        // Button click event handlers
        private async void OnCheckAndCreateFileClicked(object sender, EventArgs e)
        {
            CheckAndCreateDirectories();

            var filePath = Path.Combine(_outFilePath, "sync_data.txt");
            bool success = CheckAndCreateFile(filePath, "Initial sync data.");

            if (success)
            {
                OutputEditor.Text += $"File created at {filePath}\n";
                await DisplayAlert("Success", "File created successfully!", "OK");
            }
        }

        private async void OnSimulateFingerprintUploadClicked(object sender, EventArgs e)
        {
            await SimulateFingerprintUploadAsync();
        }

        private async void OnRetryCardDataSyncClicked(object sender, EventArgs e)
        {
            await RetryCardDataSync();
        }

        // Simulate a long-running fingerprint upload process
        private void SimulateFingerprintUpload()
        {
            // Simulating file processing and fingerprint upload
            Thread.Sleep(3000); // Simulates a 3-second task
        }
    }
}
