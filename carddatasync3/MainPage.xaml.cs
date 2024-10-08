using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using System.Text.Json;

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

		#region banner Org code
		private void OnOrgTextChanged(object sender, TextChangedEventArgs e)
        {
            // 更新 Label 的文字為 Entry 中的文字
            outputOrg.Text = e.NewTextValue; // 使用新文字值更新 Label
        }
		#endregion
        
        #region delivery button
        public class ComparisonResult
        {
            public string Key { get; set; }
            public string Result { get; set; }
            public string Failure { get; set; }
        }

        private async void Punch_Data_Changing_rule2(object sender, EventArgs e)
        {
            try
            {
                // 使用絕對路徑來讀取 JSON 檔案
                var fileHCMPath = @"C:\Users\reena.tsai\Documents\maui-guru\McD_PunchClock_Muai\carddatasync3\test_files\HCM_fingerprint.json"; 
                var filePunchClockPath = @"C:\Users\reena.tsai\Documents\maui-guru\McD_PunchClock_Muai\carddatasync3\test_files\PunchClock_fingerprint.json"; 

                // 讀取檔案內容
                var fileHCMContent = await File.ReadAllTextAsync(fileHCMPath);
                var filePunchClockContent = await File.ReadAllTextAsync(filePunchClockPath);

                // 解析 JSON 檔案，轉換為 List<Dictionary<string, string>>
                var fileHCMData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(fileHCMContent);
                var filePunchClockData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(filePunchClockContent);

                // 比較兩個檔案的資料
                var comparisonResults = new List<List<bool>>();

                // 檢查 HCM 資料，並對應 PunchClock 資料
                for (int i = 0; i < fileHCMData.Count; i++)
                {
                    var empHCM = fileHCMData[i];
                    var empPunchClock = filePunchClockData.FirstOrDefault(e => e["empNo"] == empHCM["empNo"]); // PunchClock中找到對應員工資料

                    // 初始化比較結果陣列
                    List<bool> comparison = new List<bool>(); // 假設 HCM 中的所有欄位都需要比較

                    if (empPunchClock != null)
                    {
                        // 比較每個欄位
                        for (int j = 0; j < empHCM.Keys.Count; j++)
                        {
                            var key = empHCM.Keys.ElementAt(j); // 取得當前欄位的鍵
                            if (key != "empNo" && key != "displayName" && key != "addFlag") // 忽略 empNo 欄位的比較
                            {
                                // 比較值並將結果存入 comparison 陣列
                                comparison.Add(empPunchClock[key] == empHCM[key]);
                            }
                        }
                        comparisonResults.Add(comparison);
                    }
                }

                // 將比較結果轉換為 JSON 字串
                var comparisonResultJson = JsonSerializer.Serialize(comparisonResults, new JsonSerializerOptions { WriteIndented = true });

                // 將 comparisonResults 轉換為可讀字符串
                StringBuilder stringBuilder = new StringBuilder();
                for (int i = 0; i < comparisonResults.Count; i++)
                {
                    stringBuilder.AppendLine($"{string.Join("", comparisonResults[i].Select(result => result ? "0" : "1"))}");
                }

                await DisplayAlert("JSON Data Comparison Results", stringBuilder.ToString(), "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to read files: {ex.Message}", "OK");
            }
        }

        private async void btn_delivery_upload(object sender, EventArgs e)
        {
            try
            {
                // 使用絕對路徑來讀取 JSON 檔案
                var fileHCMPath = @"C:\Users\reena.tsai\Documents\maui-guru\McD_PunchClock_Muai\carddatasync3\test_files\HCM.json"; 
                var filePunchClockPath = @"C:\Users\reena.tsai\Documents\maui-guru\McD_PunchClock_Muai\carddatasync3\test_files\PunchClock.json"; 

                // 讀取檔案內容
                var fileHCMContent = await File.ReadAllTextAsync(fileHCMPath);
                var filePunchClockContent = await File.ReadAllTextAsync(filePunchClockPath);

                // 解析 JSON 檔案，轉換為 List<Dictionary<string, string>>
                var fileHCMData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(fileHCMContent);
                var filePunchClockData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(filePunchClockContent);

                // 比較兩個檔案的資料
                var comparisonResults = new List<ComparisonResult>();

                // 檢查 HCM 資料，並對應 PunchClock 資料
                for (int i = 0; i < fileHCMData.Count; i++)
                {
                    var empHCM = fileHCMData[i];
                    var empPunchClock = filePunchClockData.FirstOrDefault(e => e["empNo"] == empHCM["empNo"]); // PunchClock中找到對應員工資料

                    // 1. 當 HCM 找得到資料，且 addFlag == "D"，但 PunchClock 找不到該筆資料，result="不變"
                    if (empHCM["addFlag"] == "D" && empPunchClock == null)
                    {
                        comparisonResults.Add(new ComparisonResult
                        {
                            Key = empHCM["empNo"],
                            Result = "True",
                            Failure = "case1: no change"
                        });
                    }
                    // 2. 當 HCM 找得到資料，且 addFlag == "D"，且 PunchClock 找得到該筆資料，result="卡鐘刪除"
                    else if (empHCM["addFlag"] == "D" && empPunchClock != null)
                    {
                        comparisonResults.Add(new ComparisonResult
                        {
                            Key = empHCM["empNo"],
                            Result = "False",
                            Failure = "case2: PunchClock Deleted"
                        });

                        // 從 PunchClock 資料中刪除該筆資料
                        filePunchClockData.Remove(empPunchClock);
                    }
                    // 5. 當 HCM 找得到資料，且 addFlag == "A"，但 PunchClock 找不到該筆資料，result="卡鐘增加"
                    else if (empHCM["addFlag"] == "A" && empPunchClock == null)
                    {
                        comparisonResults.Add(new ComparisonResult
                        {
                            Key = empHCM["empNo"],
                            Result = "False",
                            Failure = "case5: PunchClock Added"
                        });
                    }
                }

                // 繼續檢查 PunchClock 資料中的項目，看看 HCM 是否有對應資料
                for (int i = 0; i < filePunchClockData.Count; i++)
                {
                    var empPunchClock = filePunchClockData[i];
                    var empHCM = fileHCMData.FirstOrDefault(e => e["empNo"] == empPunchClock["empNo"]); // HCM中找到對應員工資料

                    // 3. 當 HCM 找不到資料，但 PunchClock 找得到該筆資料，result="卡鐘刪除"
                    if (empHCM == null)
                    {
                        comparisonResults.Add(new ComparisonResult
                        {
                            Key = empPunchClock["empNo"],
                            Result = "False",
                            Failure = "case3: PunchClock Deleted"
                        });

                        // 從 PunchClock 資料中刪除該筆資料
                        filePunchClockData.Remove(empPunchClock);
                    }
                }

                // 將比較結果轉換為 JSON 字串
                var comparisonResultJson = JsonSerializer.Serialize(comparisonResults, new JsonSerializerOptions { WriteIndented = true });

                // 顯示比較結果在同一個 Alert
                await DisplayAlert("JSON Data Comparison Results", comparisonResultJson, "OK");

                // 將 PunchClock 資料寫回到原始檔案中（更新刪除後的資料）
                var updatedPunchClockContent = JsonSerializer.Serialize(filePunchClockData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePunchClockPath, updatedPunchClockContent);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to read files: {ex.Message}", "OK");
            }
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
			// 禁用 ContentView 的點擊
			contentViewHCMToFingerprint.IsEnabled = state;  
            contentViewUploadToHCM.IsEnabled = state;  
            contentViewDeliveryUpload.IsEnabled = state; 

			// 根據 state 添加或移除 GestureRecognizer
			if (state)
			{
				// 如果啟用，添加 TapGestureRecognizer
				if (contentViewHCMToFingerprint.GestureRecognizers.Count == 0)
				{
					TapGestureRecognizer tapGesture = new TapGestureRecognizer();
					tapGesture.Tapped += btn_HCM_to_fingerprint;
					contentViewHCMToFingerprint.GestureRecognizers.Add(tapGesture);
				}
				if (contentViewUploadToHCM.GestureRecognizers.Count == 0)
				{
					TapGestureRecognizer tapGesture = new TapGestureRecognizer();
					tapGesture.Tapped += btn_upload_to_HCM;
					contentViewUploadToHCM.GestureRecognizers.Add(tapGesture);
				}
				if (contentViewDeliveryUpload.GestureRecognizers.Count == 0)
				{
					TapGestureRecognizer tapGesture = new TapGestureRecognizer();
					tapGesture.Tapped += btn_delivery_upload;
					contentViewDeliveryUpload.GestureRecognizers.Add(tapGesture);
				}
			}
			else
			{
				// 如果禁用，移除 TapGestureRecognizer
				contentViewHCMToFingerprint.GestureRecognizers.Clear();
				contentViewUploadToHCM.GestureRecognizers.Clear();
				contentViewDeliveryUpload.GestureRecognizers.Clear();
			}

			// 根據狀態改變 ContentView 的外觀（可選）
			contentViewUploadToHCM.Opacity = state ? 1 : 0.5;
			contentViewUploadToHCM.Opacity = state ? 1 : 0.5;
			contentViewDeliveryUpload.Opacity = state ? 1 : 0.5;
        }

    }
}
