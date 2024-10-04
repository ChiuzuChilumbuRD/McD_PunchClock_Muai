using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;

namespace carddatasync3
{
    public partial class MainPage : ContentPage
    {
        // Lock to prevent concurrent UI operations
        private static SpinLock ui_sp = new SpinLock();
        private static string str_current_task = string.Empty;
        private static string pglocation = "your_pgfingerlocation_path"; // Set these paths manually
        private static string _gOutFilePath = "your_fileOutPath";
        private static string _gBackUpPath = "your_BackUpPath";
        private static string apiBaseUrl = "https://gurugaia.royal.club.tw/eHR/GuruOutbound/getTmpOrg"; // Base URL for the API


        public MainPage()
        {
            InitializeComponent();
        }
        
        #region delivery button
        private async void btn_delivery_upload(object sender, EventArgs e)
        {
            // 在主線程上顯示 Alert，確保跨平台一致性
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert("Button Clicked", "You clicked the button delivery_upload!", "OK");
            });
        }
        #endregion

        #region download from hcm
        
         // Event handler for the button that downloads data from the external API
        private async void btn_HCM_to_fingerprint(object sender, EventArgs e)
        {   
            // Disable buttons while the task is running
            set_btns_state(false);

            // Make the API GET request instead of interacting with the database
            bool result = await DownloadAndProcessDataFromApiAsync();

            if (result)
            {
                await DisplayAlert("Success", "Data downloaded and processed successfully!", "OK");
            }
            else
            {
                await DisplayAlert("Error", "Failed to download and process data from the API.", "OK");
            }

            // Re-enable buttons after the task is complete
            set_btns_state(true);
        }

        // Replaces the database logic with API logic to get data from the external source
        private async Task<bool> DownloadAndProcessDataFromApiAsync()
        {
            bool result = true;

            // Make the GET request to the API
            string url = $"{apiBaseUrl}?u=AxtimTmpOrg_List&code=BQ0000"; // Modify the query params as needed
            string responseData;

            try
            {
                // Call the helper function to make the GET request
                responseData = await GetRequestAsync(url);
                
                // Update the UI with the API response
                ShowInfo("Data from API: ");
                UpdateEditor(responseData); // Assuming responseData will be displayed in the Editor
                
                // Process the data further (if necessary)
                // e.g., parse JSON or XML response to update UI or other elements
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error making API call: {ex.Message}");
                ShowError("Failed to get data from the API.");
                return false;
            }

            return result;
        }

        // Makes the HTTP GET request and returns the response
        public async Task<string> GetRequestAsync(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();
                return responseBody;
            }
        }

        // Updates the Editor control with the response data
        private void UpdateEditor(string data)
        {
            MainThread.InvokeOnMainThreadAsync(() =>
            {
                XmlEditor.Text = data; // Update the Editor control with the response data
            });
        }

        // Show error message in alert
        private void ShowError(string message)
        {
            MainThread.InvokeOnMainThreadAsync(() =>
            {
                DisplayAlert("Error", message, "OK");
            });
        }

        // Show information message in alert
        private void ShowInfo(string message)
        {
            MainThread.InvokeOnMainThreadAsync(() =>
            {
                DisplayAlert("Info", message, "OK");
            });
        }

        #endregion


        #region upload to HCM

        // Event handler for the "Upload to HCM" button click
        private async void btn_upload_to_HCM(object sender, EventArgs e)
        {
            // Disable buttons while the task is running
            set_btns_state(false);

            // Start a new thread to handle the upload process
            var work_thread = new Thread(upload_to_HCM_thread);
            work_thread.Start(this);

            await DisplayAlert("Upload Started", "Uploading fingerprint data to HCM...", "OK");
        }

        // Thread to handle fingerprint and data upload
        private static void upload_to_HCM_thread(object obj)
        {
            bool is_lock_taken = false;
            ui_sp.TryEnter(ref is_lock_taken);
            if (!is_lock_taken) return;

            str_current_task = "Uploading card and fingerprint data...";
            MainPage this_page = (MainPage)obj;
            bool b_result = false;

            try
            {
                // Simulate updating the connection string in MAUI
                update_conn_str_and_refresh();

                // Call the fingerprint upload process
                b_result = upload_fingerprint_to_HCM(this_page);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error during upload: " + ex.Message);
            }
            finally
            {
                clear_conn_str();
                if (is_lock_taken)
                    ui_sp.Exit();
            }
        }

        // Upload fingerprint data to HCM
        private static bool upload_fingerprint_to_HCM(MainPage page)
        {
            string date = "";
            bool blResult = true;
            var content = new List<string>();

            if (blResult)
            {
                blResult = page.checkFilePath();
                if (!blResult)
                {
                    page.show_err("The folder for storing PGFinger data does not exist.");
                    return false;
                }
            }

            if (blResult)
            {
                blResult = page.checkIsExisitExe();
                if (!blResult)
                {
                    page.show_err("PGFinger.exe does not exist.");
                    return false;
                }
            }

            if (blResult)
            {
                if (File.Exists(_gOutFilePath + @"\FingeOut.txt"))
                {
                    try
                    {
                        File.Delete(_gOutFilePath + @"\FingeOut.txt");
                    }
                    catch
                    {
                        page.show_err("Cannot delete FingeOut.txt file.");
                        blResult = false;
                    }
                }
            }

            // Call the PGFinger.exe process
            if (blResult)
            {
                date = DateTime.Now.ToString("yyyy-MM-dd");
                try
                {
                    page.show_info("Reading fingerprint data from the machine...");
                    Process.Start(pglocation + @"\PGFinger.exe", @"1 " + _gOutFilePath + @"\FingeOut.txt");

                    wait_for_devicecontrol_complete();
                    page.show_info("Fingerprint data read completed.");
                }
                catch (Exception ex)
                {
                    page.show_err("Error running PGFinger.exe: " + ex.Message);
                    return false;
                }

                int counter = 0;
                while (!File.Exists(_gOutFilePath + @"\FingeOut.txt"))
                {
                    Thread.Sleep(1000);
                    counter++;
                    if (counter > 20)
                    {
                        page.show_err("FingeOut.txt not generated. Please check the system.");
                        return false;
                    }
                }
            }

            // Process the fingerprint data
            if (File.Exists(_gOutFilePath + @"\FingeOut.txt"))
            {
                try
                {
                    using (var sr = new StreamReader(_gOutFilePath + @"\FingeOut.txt"))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (line.Trim().Length > 0)
                                content.Add(line);
                        }
                    }

                    if (content.Count <= 0)
                    {
                        page.show_err("FingeOut.txt is empty.");
                        return false;
                    }

                    // Backup the file
                    page.getFilePath1(date);
                    File.Copy(_gOutFilePath + @"\FingeOut.txt", _gBackUpPath + @"\FingerprintData" + date.Replace("-", "") + ".txt", true);

                    // Simulate updating the card number and employee data
                    int successCount = content.Count;
                    page.show_info($"{successCount} fingerprint records uploaded to HCM.");

                }
                catch (Exception ex)
                {
                    page.show_err("Error processing FingeOut.txt: " + ex.Message);
                    return false;
                }
            }

            return true;
        }

        #endregion

        // Utility functions adapted to MAUI
        private bool checkFilePath()
        {
            if (!Directory.Exists(_gOutFilePath))
            {
                Directory.CreateDirectory(_gOutFilePath);
            }
            return true;
        }

        private bool checkIsExisitExe()
        {
            return File.Exists(pglocation + @"\PGFinger.exe");
        }

        private void show_info(string message)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Info", message, "OK");
            });
        }

        private void show_err(string message)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Error", message, "OK");
            });
        }

        private static void wait_for_devicecontrol_complete()
        {
            Thread.Sleep(5000); // Simulate device wait time
        }

        private void getFilePath1(string date)
        {
            string path = _gBackUpPath + @"\FingerprintData\" + date.Replace("-", "");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static void update_conn_str_and_refresh()
        {
            // Simulated logic for updating connection string
            Debug.WriteLine("Connection string updated.");
        }

        private static void clear_conn_str()
        {
            // Simulated logic for clearing connection string
            Debug.WriteLine("Connection string cleared.");
        }

        private void set_btns_state(bool state)
        {
            btnHCMToFingerprint.IsEnabled = state;  
            btnUploadToHCM.IsEnabled = state;  
            btnDeliveryUpload.IsEnabled = state; 
        }

    }
}
