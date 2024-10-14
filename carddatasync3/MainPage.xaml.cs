﻿using System.Diagnostics;
using Serilog;
using System.Text;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Collections.Specialized;
using System.IO;

namespace carddatasync3
{
    public partial class MainPage : ContentPage
    {
        // Lock to prevent concurrent UI operations
        private static SpinLock ui_sp = new SpinLock();
        private static string str_current_task = string.Empty;
        private static string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        private static string _gOutFilePath = Path.Combine(desktopPath, "FingerData");
        private static string _gBackUpPath = Path.Combine(desktopPath, "HCMBackUp");
        

        private static string apiBaseUrl = "https://gurugaia.royal.club.tw/eHR/GuruOutbound/getTmpOrg"; // Base URL for the API

        public MainPage()
        {
            // Initialize Serilog (this can also be done in the program's entry point)
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            Log.Information("App starting...");
            
            InitializeComponent();

            // Call initialization sequence
            InitializeApp();
        }

        #region Initialisation

        private void InitializeApp()
        {
            AppendTextToEditor("App initialization started.");

            // Step 1: Load appsettings.json content
            // 讀取 appsettings.json 顯示在 TextEditor 上面
            LoadAppSettings();

            // Step 2: Check if file exists and execute if found
            // 讀取並執行桌面上的 PGFinger.exe 檔案 (後段會被卡住)
            if (!CheckAndExecuteFile(this))
            {

                AppendTextToEditor("Required file not found. Closing application.");
                return;
            }


            // Step 3: Check internet connection
            // 確認網路連線
            if (!IsInternetAvailable())
            {
                AppendTextToEditor("No internet connection. Closing application.");
                return;
            }

            // Step 4: Ping server IP address
            if (!PingServer("8.8.8.8")) // Example IP, replace with the actual one
            {
                AppendTextToEditor("Unable to reach the server. Closing application.");
                return;
            }

            AppendTextToEditor("App initialization completed.");
        }

        #endregion

        #region Placeholder Functions

        // 讀取 appsettings.json 並將內容顯示在 TextEditor 上面
        private NameValueCollection LoadAppSettings()
        {
            var appSettings = new NameValueCollection();
            AppendTextToEditor("Loading appsettings.json...");

            string appSettingsPath = Path.Combine(Environment.CurrentDirectory, "appsettings.json");

            // 檢查檔案是否存在
            if (File.Exists(appSettingsPath))
            {
                try
                {
                    // 讀取檔案內容
                    string jsonContent = File.ReadAllText(appSettingsPath);
                    AppendTextToEditor(jsonContent); // 將 JSON 內容顯示在 TextEditor

                    // 反序列化 JSON 到字典
                    var jsonDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(jsonContent);

                    // 提取 Settings 部分
                    if (jsonDict.TryGetValue("Settings", out var settings))
                    {
                        foreach (var kvp in settings)
                        {
                            appSettings.Add(kvp.Key, kvp.Value);
                        }
                    }

                    // 顯示讀取的設置 (可選)
                    AppendTextToEditor($"Download Location: {appSettings["downloadLocation"]}");
                    AppendTextToEditor($"PGFinger Location: {appSettings["pgfingerlocation"]}");
                    AppendTextToEditor($"File Out Path: {appSettings["fileOutPath"]}");
                    AppendTextToEditor($"Back Up Path: {appSettings["BackUpPath"]}");
                    AppendTextToEditor($"Auto Run Time: {appSettings["AutoRunTime"]}");
                }
                catch (Exception ex)
                {
                    AppendTextToEditor("Error reading appsettings.json: " + ex.Message);
                }
            }
            else
            {
                AppendTextToEditor("appsettings.json not found.");
            }

            return appSettings;
        }


        private bool CheckAndExecuteFile(MainPage page)
        {
            string date = "";
            bool blResult = true;
            var content = new List<string>();

            // 確認 PGFinger.exe 的資料夾路徑
            if (blResult)
            {
                blResult = page.ensureFilePathExists();
                if (!blResult)
                {
                    AppendTextToEditor("The folder for storing PGFinger data does not exist.");
                    return false;
                }
            }

            // 確認 PGFinger.exe 本身是否存在
            if (blResult)
            {
                blResult = page.validatePGFingerExe();
                if (!blResult)
                {
                    AppendTextToEditor("PGFinger.exe does not exist.");
                    return false;
                }
            }

            // 確認桌面的 FingerData 資料夾中是否存在 FingerOut.txt -> 若存在，則刪除 FingerOut.txt
            if (blResult)
            {
                AppendTextToEditor(_gOutFilePath);
                if (File.Exists(_gOutFilePath + @"\FingerOut.txt"))
                {
                    try
                    {
                        File.Delete(_gOutFilePath + @"\FingerOut.txt");
                    }
                    catch
                    {
                        AppendTextToEditor("Cannot delete FingerOut.txt file.");
                        blResult = false;
                    }
                }
            }

            // Call the PGFinger.exe process
            // 執行 PGFinger.exe 並在桌面產生 FingerData\FingerOut.txt 檔案
            if (blResult)
            {
                // 執行 PGFinger.exe
                date = DateTime.Now.ToString("yyyy-MM-dd");
                try
                {
                    AppendTextToEditor("Reading fingerprint data from the machine...");
                    Process.Start(desktopPath + @"\PGFinger.exe");

                    wait_for_devicecontrol_complete();
                    AppendTextToEditor("Fingerprint data read completed.");
                }
                catch (Exception ex)
                {
                    AppendTextToEditor("Error running PGFinger.exe: " + ex.Message);
                    return false;
                }

                // 當 PGFinger.exe 執行成功，確認桌面的 FingerData\FingerOut.txt 是否存在
                // FingerData\FingerOut.txt 存在才會繼續執行，20 秒後沒有該檔案的話就會 return false
                int counter = 0;
                while (!File.Exists(_gOutFilePath + @"\FingerOut.txt"))
                {
                    Thread.Sleep(1000);
                    counter++;
                    if (counter > 20)
                    {
                        AppendTextToEditor("FingerOut.txt not generated. Please check the system.");
                        return false;
                    }
                }
            }


            // Process the fingerprint data
            // 讀取桌面上的 FingerData\FingerOut.txt 檔案
            if (File.Exists(_gOutFilePath + @"\FingerOut.txt"))
            {
                try
                {
                    using (var sr = new StreamReader(_gOutFilePath + @"\FingerOut.txt"))
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
                        AppendTextToEditor("FingerOut.txt is empty.");
                        return false;
                    }

                    // Backup the file
                    page.getFilePath1(date);
                    File.Copy(_gOutFilePath + @"\FingerOut.txt", _gBackUpPath + @"\FingerprintData" + date.Replace("-", "") + ".txt", true);

                    // Simulate updating the card number and employee data
                    int successCount = content.Count;
                    AppendTextToEditor($"{successCount} fingerprint records uploaded to HCM.");

                }
                catch (Exception ex)
                {
                    AppendTextToEditor("Error processing FingerOut.txt: " + ex.Message);
                    return false;
                }
            }

            return true;
        }


        // Utility functions in MainPage class
        public bool ensureFilePathExists()
        {
            if (!Directory.Exists(_gOutFilePath))
            {
                Directory.CreateDirectory(_gOutFilePath);
            }
            return true;
        }

        // 確認桌面上 PGFinger.exe 是否存在
        public bool validatePGFingerExe()
        {
            // AppendTextToEditor(File.Exists(pglocation + @"\PGFinger.exe").ToString());
            return File.Exists(desktopPath + @"\PGFinger.exe");
        }



        // 確定網路連線
        private bool IsInternetAvailable()
        {
            AppendTextToEditor("Checking internet connection...");

            var current = Connectivity.Current.NetworkAccess;
            var profiles = Connectivity.Current.ConnectionProfiles;

            // Check if the device is connected to the internet via WiFi or other profiles
            bool isConnected = current == NetworkAccess.Internet;

            if (isConnected)
            {
                AppendTextToEditor("Internet connection is available.");
                return true;
            }
            else
            {
                AppendTextToEditor("No internet connection available.");
                return false;
            }
        }



        // 確定是否能夠 ping server
        private bool PingServer(string ipAddress)
        {
            AppendTextToEditor($"Pinging server at {ipAddress}...");

            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send(ipAddress);

                    if (reply.Status == IPStatus.Success)
                    {
                        AppendTextToEditor($"Server at {ipAddress} is reachable. Response time: {reply.RoundtripTime} ms");
                        return true;
                    }
                    else
                    {
                        AppendTextToEditor($"Failed to reach server at {ipAddress}. Status: {reply.Status}");
                        return false;
                    }
                }
            }
            catch (PingException ex)
            {
                AppendTextToEditor($"Ping failed: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                AppendTextToEditor($"Unexpected error: {ex.Message}");
                return false;
            }
        }


        private void DisplayErrorMessage(string message)
        {
            AppendTextToEditor(message);
            Log.Error(message);
            DisplayAlert("Error", message, "OK");
        }

        #endregion


        // Helper function to append text to XmlEditor
        // 寫 log 到 TextEditor 中
        private void AppendTextToEditor(string text)
        {
            if (XmlEditor != null)
            {
                XmlEditor.Text += $"{text}\n"; // Append the text line by line
            }
        }


		#region banner Org code
        private void OnOrgTextChanged(object sender, TextChangedEventArgs e)
        {
            // Update the Label's text with the new value from the Entry
            outputOrg.Text = e.NewTextValue;
        }


		#endregion
        
        #region delivery button
        public class ComparisonResult
        {
            public string Key { get; set; }
            public string Result { get; set; }
            public string Failure { get; set; }
        }

        // 讀取 test_files/ 中的兩個檔案，套用 rules2 並將結果顯示於 alert 中
        private async void Punch_Data_Changing_rule2(object sender, EventArgs e)
        {
            try
            {
                // 使用絕對路徑來讀取 JSON 檔案
                var fileHCMPath = Path.Combine(Environment.CurrentDirectory, @"\test_files\HCM_fingerprint.json");
                var filePunchClockPath = Path.Combine(Environment.CurrentDirectory, @"\test_files\PunchClock_fingerprint.json");

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

            await DisplayAlert("Upload Started", "Uploading fingerprint data to HCM...", "OK");

             set_btns_state(true);
        }


        #endregion

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

       private void set_btns_state(bool state)
        {
            // Enable or disable the buttons
            btnHCMToFingerprint.IsEnabled = state;  
            btnUploadToHCM.IsEnabled = state;  
            btnDeliveryUpload.IsEnabled = state; 

            // Add or remove GestureRecognizers based on the state
            if (state)
            {
                // Add TapGestureRecognizer if the button is enabled and no recognizers exist
                if (btnHCMToFingerprint.GestureRecognizers.Count == 0)
                {
                    TapGestureRecognizer tapGesture = new TapGestureRecognizer();
                    tapGesture.Tapped += btn_HCM_to_fingerprint;
                    btnHCMToFingerprint.GestureRecognizers.Add(tapGesture);
                }
                if (btnUploadToHCM.GestureRecognizers.Count == 0)
                {
                    TapGestureRecognizer tapGesture = new TapGestureRecognizer();
                    tapGesture.Tapped += btn_upload_to_HCM;
                    btnUploadToHCM.GestureRecognizers.Add(tapGesture);
                }
                if (btnDeliveryUpload.GestureRecognizers.Count == 0)
                {
                    TapGestureRecognizer tapGesture = new TapGestureRecognizer();
                    tapGesture.Tapped += btn_delivery_upload;
                    btnDeliveryUpload.GestureRecognizers.Add(tapGesture);
                }
            }
            else
            {
                // Clear the GestureRecognizers if the buttons are disabled
                btnHCMToFingerprint.GestureRecognizers.Clear();
                btnUploadToHCM.GestureRecognizers.Clear();
                btnDeliveryUpload.GestureRecognizers.Clear();
            }

            // Change the appearance of the buttons based on the state (optional)
            btnHCMToFingerprint.Opacity = state ? 1 : 0.5;
            btnUploadToHCM.Opacity = state ? 1 : 0.5;
            btnDeliveryUpload.Opacity = state ? 1 : 0.5;
        }


    }
}
