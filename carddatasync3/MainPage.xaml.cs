using System.Diagnostics;
using Serilog;
using System.Text;
using System.Net.NetworkInformation;
using System.Collections.Specialized;
using System.IO;
using System;
using System.Reflection;
using Newtonsoft.Json;

namespace carddatasync3
{
    public partial class MainPage : ContentPage
    {
        private string str_log_level = "debug"; //TODO: Need to change after program release.
        static string _gAPPath = AppContext.BaseDirectory;
        protected static string databaseKey = "GAIA.EHR";
        protected static string downloaction;
        protected static string pglocation;
        protected static string responseData;

        // ======================================================================================
        //將備份目錄提出來變成Config
        protected static string _gBackUpPath;

        // ======================================================================================
        //指紋匯出及下載的檔案產生目的地
        protected static string _gOutFilePath;
        //For Auto Run Time
        protected static string autoRunTime;

        protected static string test_org_code;
        protected static string machineIP;

        protected static DateTime last_check_date = new DateTime();

        public static string eHrToFingerData;
        public static StringBuilder str_accumulated_log = new StringBuilder();
        private static int peopleCount = 0;

        // Lock to prevent concurrent UI operations
        private static SpinLock ui_sp = new SpinLock();
        private static string str_current_task = string.Empty;
        private bool is_init_completed = true;
        public static NameValueCollection AppSettings { get; private set; }      

        // private static string apiBaseUrl = "https://gurugaia.royal.club.tw/eHR/GuruOutbound/getTmpOrg"; // Base URL for the API
        private static string apiBaseUrl;
        private async Task show_error(string message, string caption)
        {
            await DisplayAlert(caption, message, "OK");
        }

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
        private async void InitializeApp()
        {
            // ================================ Step.0 Lock Button ===============================================
            set_btns_state(false);

            // ======================= Step.1 取得目前 APP 版本號並印出 Get Current APP Version =====================
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            AppendTextToEditor(version.ToString());
            AppendTextToEditor("Form init"); // App initialization started.

            // ======================= Step.2 匯入設定檔 Load Config (AppSettings.json) =======================
            // 讀取 appsettings.json 存入 AppSettings 中
            LoadAppSettings();

            // ======================== Step.3 確認資料夾是否存在 Check Folder Exist or Not ====================
            // 確認 appsettings.json 中的所有資料夾是否存在 -> 不存在則回傳 false
            
            if (!CheckFilesExist(this))
            {
                AppendTextToEditor("Required file not found. Closing application.");
                return;
            }

            // =============== Step.4 確認網路連線 Check Internet Available ==================================
            if (!IsInternetAvailable())
            {
                AppendTextToEditor("No internet connection. Closing application.");
                return;
            }

            // ================== Step.5 取得目前電腦名稱 Get Current Computer Name(GetOrgCode) ===============
            // TODO: 未完成 (getCradORGName)
            getOrgCode();

            // 確認組織代碼名稱
            if (string.IsNullOrEmpty(textBox1.Text) || textBox1.Text.Length != 8)
            {
                string str_msg = "組織代碼錯誤，將關閉程式";
                AppendTextToEditor(str_msg);
                // await show_error(str_msg, "組織代碼錯誤");
                is_init_completed = false;
                // return;
            }
            // ------------------------ 會卡在這裡 --------------------------------

            labStoreName.Text = getCradORGName();
            if (labStoreName.Text.Length == 0)
            {
                string str_msg = $"找不到{textBox1.Text}組織名稱，將關閉程式";
                // show_error(str_msg, "組織名稱錯誤");
                AppendTextToEditor(str_msg);
                is_init_completed = false;
                // return;
            }

            // ================= Step.6 確認打卡機就緒 Check Punch Machine Available ==================
            // TODO: 無法測試，未完成
            // if (!is_HCM_ready())
            // {
            //     string str_msg = "HCM連接發生問題，將關閉程式";
            //     // await show_error(str_msg, "連線問題");
            //     AppendTextToEditor(str_msg);
            //     is_init_completed = false;
            //     return;
            // }
            // ----------------------------------- 會卡在這裡 ------------------------------------------

            // ====================== Step.7 確認人資系統有沒有上線 (ping IP)​ Check if the HR Server is available ============
            // Ping server IP address
            if (!PingServer(machineIP)) // Example IP, replace with the actual one
            {
                AppendTextToEditor("Unable to reach the server. Closing application.");
                return;
            }

            // ====================== Step.8 以組織代碼APP=>回傳版本 Check GuruOutbound service =======================
            // TODO: 未完成 (沒有 code)

            // ====================== Step.9 傳送日誌 use POST Send the Log / => 準備就緒 Ready ========================
            string orgCode = "S000123"; // "S000123"
            bool postSuccess = await send_org_code_hcm(orgCode);

            if (postSuccess)
            {
                AppendTextToEditor("Log sent successfully and data saved to HCM.json.");
            }
            else
            {
                AppendTextToEditor("Failed to send log or save data.");
                return;
            }

            // ====================== Step.10 Unlock Button​ + 初始化成功 App initialization completed =======================
            set_btns_state(true);
            AppendTextToEditor("App initialization completed.");
        } // END InitializeApp

        #endregion

        #region Placeholder Functions

        private void getOrgCode()
        {
            // TODO: 修改廠商代碼的取得方式
            string m_name = "S000123"; // TODO: Environment.MachineName
            AppendTextToEditor("Machine name: " + m_name);
            textBox1.Text = string.Empty; // 清空 Entry 控件的文本

            if (m_name.Length < 7)
            {
                show_err("抓取組織代碼失敗");
                textBox1.Text = string.Empty; // 清空 Entry
            }
            else
            {
                string l_name = m_name.Substring(2, 5);
                if (CommonUtility.is_valid_org_code(l_name))
                {
                    textBox1.Text = "S00" + l_name; // 設置 Entry 的值
                }
            }

            // if (CommonUtility.is_valid_org_code(test_org_code))
            // {
            //     test_org_code = "S00" + test_org_code;
            //     string message = $"是否使用測試用組織代碼 {test_org_code}?";
            //     string caption = "測試用組織代碼";
            //     MessageBoxButtons buttons = MessageBoxButtons.YesNo;
            //     DialogResult result;

            //     // 顯示 MessageBox 讓用戶選擇
            //     result = MessageBox.Show(message, caption, buttons);
            //     if (result == System.Windows.Forms.DialogResult.Yes)
            //     {
            //         show_info($"使用測試組織代碼 {test_org_code}");
            //         textBox1.Text = test_org_code; // 設置 Entry 的值
            //     }
            // }
            AppendTextToEditor($"OrgCode: {textBox1.Text}"); // 使用 textBox1.Text 來取得值
        } // END getOrgCode

        private bool is_HCM_ready()
        {
            // TODO: 確認 HCM 連線是否成功
            AppendTextToEditor("HCM連線中,請等待");
            // if (ConfigurationManager.ConnectionStrings[databaseKey] != null)
            // {
            //     string conn = ConfigurationManager.ConnectionStrings[databaseKey].ConnectionString;
            //     SqlConnection conns = new SqlConnection(conn);
            //     try
            //     {
            //         conns.Open();
            //         conns.Close();
            //         AppendTextToEditor("HCM連線狀態正常");
            //         return true;
            //     }
            //     catch (Exception ex)
            //     {
            //         AppendTextToEditor("HCM連線失敗，請洽系統管理員", ex);
            //         return false;
            //     }
            // }
            // else
            // {
            //     AppendTextToEditor("考勤資料庫HCM未配置，請洽系統管理員");
            //     return false;
            // } 
            return false;
        } // END is_HCM_ready

        public static async Task<bool> send_org_code_hcm(string orgCode)
        {

            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var fileHCMPath = Path.Combine(desktopPath, @"HCM.json");

                // Call the API and get the JSON response
                string responseData = await GetRequestAsync(orgCode);

                // Write the response data to a JSON file
                await File.WriteAllTextAsync(fileHCMPath, responseData);

                // Success message (optional)
                Console.WriteLine("Data has been successfully written to HCM.json");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
            
        }

        // Function to handle the API POST request
        static async Task<string> GetRequestAsync(string orgCode)
        {
            // Construct the full URL with the org_code
            string jsonParam = $"{{\"org_no\":\"{orgCode}\"}}";
            string fullUrl = $"{apiBaseUrl}{Uri.EscapeDataString(jsonParam)}";

            using (var client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(fullUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync();
                    }
                    else   

                    {
                        throw new Exception($"Failed to get data from API: {response.StatusCode}");
                    }
                }
                catch (HttpRequestException ex)
                {
                    throw new Exception($"Failed to connect to the API: {ex.Message}");
                }
                catch (JsonException ex)
                {
                    throw new Exception($"Error parsing JSON response: {ex.Message}");
                }
            }
        }

        private string getCradORGName()
        {
            // TODO: 取得廠商名稱 (不使用 db)
            string name = string.Empty;
            // string sql = string.Format(@"SELECT UNITNAME FROM VW_MDS_ORGSTDSTRUCT WHERE UNITCODE='{0}'", this.textBox1.Text);
            // Database db = DatabaseFactory.CreateDatabase(databaseKey);
            // DbCommand dc = db.GetSqlStringCommand(sql);

            // DataSet ds = db.ExecuteDataSet(dc);

            // if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
            // {
            //     name = ds.Tables[0].Rows[0][0].ToString();
            // }
            // else
            // {
            //     name = "";
            // }
            return name;
        } // END getCradORGName

        // 讀取 appsettings.json 並將相關路徑傳入參數中
        private void LoadAppSettings()
        {
            AppSettings = new NameValueCollection();
            AppendTextToEditor("Loading appsettings.json...");

            string appSettingsPath = Path.Combine(_gAPPath, "appsettings.json");

            // 檢查檔案是否存在
            if (File.Exists(appSettingsPath))
            {
                try
                {
                    // 讀取檔案內容
                    string jsonContent = File.ReadAllText(appSettingsPath);
                    // AppendTextToEditor(jsonContent); // 將 JSON 內容顯示在 TextEditor

                    // 反序列化 JSON 到字典
                    var jsonDict = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(jsonContent);

                    // 提取 Settings 部分
                    if (jsonDict.TryGetValue("Settings", out var settings))
                    {
                        foreach (var kvp in settings)
                        {
                            AppSettings.Add(kvp.Key, kvp.Value);
                        }
                    }
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
            
            downloaction = AppSettings["downloadlocation"];
            pglocation = AppSettings["pgfingerlocation"];
            _gBackUpPath = AppSettings["BackUpPath"];
            _gOutFilePath = AppSettings["fileOutPath"];
            autoRunTime = AppSettings["AutoRunTime"];
            test_org_code = AppSettings["test_org_code"];
            last_check_date = DateTime.ParseExact(
                                    AppSettings["LastCheckDate"],
                                    "yyyy-MM-dd",
                                    System.Globalization.CultureInfo.InvariantCulture);
            machineIP = AppSettings["machineIP"];
            apiBaseUrl = AppSettings["serverInfo"] + "/GuruOutbound/Trans?ctrler=Std1forme00501&method=PSNSync&jsonParam=";
        } // END LoadAppSettings

         private bool CheckFilesExist(MainPage page)
        {
            // Load settings from appsettings.json

            var downloadLocation = AppSettings["downloadlocation"];
            var pgFingerLocation = AppSettings["pgfingerlocation"];
            var fileOutPath = AppSettings["fileOutPath"];
            var BackUpPath = AppSettings["BackUpPath"];

            // Check if the directories exist
            if (!Directory.Exists(downloadLocation))
            {
                AppendTextToEditor($"The download folder does not exist: {downloadLocation}");
                return false;
            }

            if (!Directory.Exists(pgFingerLocation))
            {
                AppendTextToEditor($"The PGFinger folder does not exist: {pgFingerLocation}");
                return false;
            }

            if (!Directory.Exists(fileOutPath))
            {
                AppendTextToEditor($"The BackUpPath folder does not exist: {fileOutPath}");
                return false;
            }

            if (!Directory.Exists(BackUpPath))
            {
                AppendTextToEditor($"The BackUp folder does not exist: {BackUpPath}");
                return false;
            }

            // If both directories exist, return true
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
        } // END ensureFilePathExists

        // 確認桌面上 PGFinger.exe 是否存在
        public bool validatePGFingerExe()
        {
            // AppendTextToEditor(File.Exists(pglocation + @"\PGFinger.exe").ToString());
            return File.Exists(pglocation + @"\PGFinger.exe");
        } // END validatePGFingerExe

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
        } // END IsInternetAvailable

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
        } // END PingServer

        private void DisplayErrorMessage(string message)
        {
            AppendTextToEditor(message);
            Log.Error(message);
            DisplayAlert("Error", message, "OK");
        } // END DisplayErrorMessage

        #endregion


        // Helper function to append text to textBox2
        // 寫 log 到 TextEditor 中
        private void AppendTextToEditor(string text)
        {
            if (textBox2 != null)
            {
                textBox2.Text += $"{text}\n"; // Append the text line by line
            }
        } // END AppendTextToEditor


		#region banner Org code
        private void OnOrgTextChanged(object sender, TextChangedEventArgs e)
        {
            // Update the Label's text with the new value from the Entry
            labStoreName.Text = e.NewTextValue;
        } // END OnOrgTextChanged
		#endregion

        #region delivery Button
        private async void btn_delivery_upload(object sender, EventArgs e)
        {
            // Disable buttons while the task is running
            set_btns_state(false);

            await DisplayAlert("Upload Started", "Uploading delivery data...", "OK");

            set_btns_state(true);
        }
        #endregion
        
        #region Rules
        public class ComparisonResult
        {
            public string Key { get; set; }
            public string Result { get; set; }
            public string Failure { get; set; }
        }

        // 讀取 test_files/ 中的兩個檔案，套用 rules2 並將結果顯示於 alert 中
        private async void Punch_Data_Changing_rule2(object sender, EventArgs e)
        {
            // try
            // {
            //     // 使用絕對路徑來讀取 JSON 檔案
            //     var fileHCMPath = Path.Combine(_gAPPath, @"\test_files\HCM_fingerprint.json");
            //     var filePunchClockPath = Path.Combine(_gAPPath, @"\test_files\PunchClock_fingerprint.json");

            //     // 讀取檔案內容
            //     var fileHCMContent = await File.ReadAllTextAsync(fileHCMPath);
            //     var filePunchClockContent = await File.ReadAllTextAsync(filePunchClockPath);

            //     // 解析 JSON 檔案，轉換為 List<Dictionary<string, string>>
            //     var fileHCMData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(fileHCMContent);
            //     var filePunchClockData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(filePunchClockContent);

            //     // 比較兩個檔案的資料
            //     var comparisonResults = new List<List<bool>>();

            //     // 檢查 HCM 資料，並對應 PunchClock 資料
            //     for (int i = 0; i < fileHCMData.Count; i++)
            //     {
            //         var empHCM = fileHCMData[i];
            //         var empPunchClock = filePunchClockData.FirstOrDefault(e => e["empNo"] == empHCM["empNo"]); // PunchClock中找到對應員工資料

            //         // 初始化比較結果陣列
            //         List<bool> comparison = new List<bool>(); // 假設 HCM 中的所有欄位都需要比較

            //         if (empPunchClock != null)
            //         {
            //             // 比較每個欄位
            //             for (int j = 0; j < empHCM.Keys.Count; j++)
            //             {
            //                 var key = empHCM.Keys.ElementAt(j); // 取得當前欄位的鍵
            //                 if (key != "empNo" && key != "displayName" && key != "addFlag") // 忽略 empNo 欄位的比較
            //                 {
            //                     // 比較值並將結果存入 comparison 陣列
            //                     comparison.Add(empPunchClock[key] == empHCM[key]);
            //                 }
            //             }
            //             comparisonResults.Add(comparison);
            //         }
            //     }

            //     // 將比較結果轉換為 JSON 字串
            //     var comparisonResultJson = JsonSerializer.Serialize(comparisonResults, new JsonSerializerOptions { WriteIndented = true });

            //     // 將 comparisonResults 轉換為可讀字符串
            //     StringBuilder stringBuilder = new StringBuilder();
            //     for (int i = 0; i < comparisonResults.Count; i++)
            //     {
            //         stringBuilder.AppendLine($"{string.Join("", comparisonResults[i].Select(result => result ? "0" : "1"))}");
            //     }

            //     await DisplayAlert("JSON Data Comparison Results", stringBuilder.ToString(), "OK");
            // }
            // catch (Exception ex)
            // {
            //     await DisplayAlert("Error", $"Failed to read files: {ex.Message}", "OK");
            // }
        }

        private async void Punch_Data_Changing_rule1(object sender, EventArgs e)
        {
            // try
            // {
            //     // 使用相對路徑來讀取 JSON 檔案
            //     var fileHCMPath = Path.Combine(_gAPPath, @"\test_files\HCM.json");
            //     var filePunchClockPath = Path.Combine(_gAPPath, @"\test_files\PunchClock.json");
            //     // var fileHCMPath = @"C:\Users\reena.tsai\Documents\maui-guru\McD_PunchClock_Muai\carddatasync3\test_files\HCM.json"; 
            //     // var filePunchClockPath = @"C:\Users\reena.tsai\Documents\maui-guru\McD_PunchClock_Muai\carddatasync3\test_files\PunchClock.json"; 

            //     // 讀取檔案內容
            //     var fileHCMContent = await File.ReadAllTextAsync(fileHCMPath);
            //     var filePunchClockContent = await File.ReadAllTextAsync(filePunchClockPath);

            //     // 解析 JSON 檔案，轉換為 List<Dictionary<string, string>>
            //     var fileHCMData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(fileHCMContent);
            //     var filePunchClockData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(filePunchClockContent);

            //     // 比較兩個檔案的資料
            //     var comparisonResults = new List<ComparisonResult>();

            //     // 檢查 HCM 資料，並對應 PunchClock 資料
            //     for (int i = 0; i < fileHCMData.Count; i++)
            //     {
            //         var empHCM = fileHCMData[i];
            //         var empPunchClock = filePunchClockData.FirstOrDefault(e => e["empNo"] == empHCM["empNo"]); // PunchClock中找到對應員工資料

            //         // 1. 當 HCM 找得到資料，且 addFlag == "D"，但 PunchClock 找不到該筆資料，result="不變"
            //         if (empHCM["addFlag"] == "D" && empPunchClock == null)
            //         {
            //             comparisonResults.Add(new ComparisonResult
            //             {
            //                 Key = empHCM["empNo"],
            //                 Result = "True",
            //                 Failure = "case1: no change"
            //             });
            //         }
            //         // 2. 當 HCM 找得到資料，且 addFlag == "D"，且 PunchClock 找得到該筆資料，result="卡鐘刪除"
            //         else if (empHCM["addFlag"] == "D" && empPunchClock != null)
            //         {
            //             comparisonResults.Add(new ComparisonResult
            //             {
            //                 Key = empHCM["empNo"],
            //                 Result = "False",
            //                 Failure = "case2: PunchClock Deleted"
            //             });

            //             // 從 PunchClock 資料中刪除該筆資料
            //             filePunchClockData.Remove(empPunchClock);
            //         }
            //         // 5. 當 HCM 找得到資料，且 addFlag == "A"，但 PunchClock 找不到該筆資料，result="卡鐘增加"
            //         else if (empHCM["addFlag"] == "A" && empPunchClock == null)
            //         {
            //             comparisonResults.Add(new ComparisonResult
            //             {
            //                 Key = empHCM["empNo"],
            //                 Result = "False",
            //                 Failure = "case5: PunchClock Added"
            //             });
            //         }
            //     }

            //     // 繼續檢查 PunchClock 資料中的項目，看看 HCM 是否有對應資料
            //     for (int i = 0; i < filePunchClockData.Count; i++)
            //     {
            //         var empPunchClock = filePunchClockData[i];
            //         var empHCM = fileHCMData.FirstOrDefault(e => e["empNo"] == empPunchClock["empNo"]); // HCM中找到對應員工資料

            //         // 3. 當 HCM 找不到資料，但 PunchClock 找得到該筆資料，result="卡鐘刪除"
            //         if (empHCM == null)
            //         {
            //             comparisonResults.Add(new ComparisonResult
            //             {
            //                 Key = empPunchClock["empNo"],
            //                 Result = "False",
            //                 Failure = "case3: PunchClock Deleted"
            //             });

            //             // 從 PunchClock 資料中刪除該筆資料
            //             filePunchClockData.Remove(empPunchClock);
            //         }
            //     }

            //     // 將比較結果轉換為 JSON 字串
            //     var comparisonResultJson = JsonSerializer.Serialize(comparisonResults, new JsonSerializerOptions { WriteIndented = true });

            //     // 顯示比較結果在同一個 Alert
            //     await DisplayAlert("JSON Data Comparison Results", comparisonResultJson, "OK");

            //     // 將 PunchClock 資料寫回到原始檔案中（更新刪除後的資料）
            //     var updatedPunchClockContent = JsonSerializer.Serialize(filePunchClockData, new JsonSerializerOptions { WriteIndented = true });
            //     await File.WriteAllTextAsync(filePunchClockPath, updatedPunchClockContent);
            // }
            // catch (Exception ex)
            // {
            //     await DisplayAlert("Error", $"Failed to read files: {ex.Message}", "OK");
            // }
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
            string url = "S000123"; // Modify the query params as needed

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

        // Updates the Editor control with the response data
        private void UpdateEditor(string data)
        {
            MainThread.InvokeOnMainThreadAsync(() =>
            {
                textBox2.Text += data; // Update the Editor control with the response data
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
