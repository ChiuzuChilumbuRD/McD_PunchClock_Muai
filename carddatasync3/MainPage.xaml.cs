using System.Diagnostics;
using Serilog;
using System.Text;
using System.Net.NetworkInformation;
using Newtonsoft.Json.Linq;
using System.Data;
using Microsoft.Practices.EnterpriseLibrary.Data;
using Newtonsoft.Json;
using System.Collections.Specialized;
using System.IO;
using System;
using System.Reflection;

namespace carddatasync3
{
    public partial class MainPage : ContentPage
    {
        private string str_log_level = "debug"; //TODO: Need to change after program release.
        static string _gAPPath = AppContext.BaseDirectory;
        protected static string databaseKey = "GAIA.EHR";
        protected static string downloaction;
        protected static string pglocation;

        // ======================================================================================
        //將備份目錄提出來變成Config
        protected static string _gBackUpPath;

        // ======================================================================================
        //指紋匯出及下載的檔案產生目的地
        protected static string _gOutFilePath;
        //For Auto Run Time
        protected static string autoRunTime;

        protected static string test_org_code;

        protected static DateTime last_check_date = new DateTime();

        public static string eHrToFingerData;
        public static StringBuilder str_accumulated_log = new StringBuilder();
        private static int peopleCount = 0;
        
        // Lock to prevent concurrent UI operations
        private static SpinLock ui_sp = new SpinLock();
        private static string str_current_task = string.Empty;
        // private static string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        // private static string _gOutFilePath = Path.Combine(desktopPath, "FingerData");
        // private static string _gBackUpPath = Path.Combine(desktopPath, "HCMBackUp");
        // private static string pglocation = Path.Combine(desktopPath, "PGFinger.exe");
        private bool is_init_completed = true;
        public static NameValueCollection AppSettings { get; private set; }      

        // private static string apiBaseUrl = "https://gurugaia.royal.club.tw/eHR/GuruOutbound/getTmpOrg"; // Base URL for the API
        private static string apiBaseUrl;
        private DBFactory _dbFactory;


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
            //Database Factory 
             _dbFactory = new DBFactory();
        }

        #region Initialisation

        private async void InitializeApp()
        {
            // ======================= Step.1 取得目前 APP 版本號並印出 Get Current APP Version =====================
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            AppendTextToEditor(version.ToString());
            AppendTextToEditor("Form init"); // App initialization started.

            // ======================= Step.2 匯入設定檔 Load Config (AppSettings.json) =======================
            // 讀取 appsettings.json 存入 AppSettings 中
            LoadAppSettings();
            AppendTextToEditor("App Settings load successfully.");

            // ======================== Step.3 確認資料夾是否存在 Check Folder Exist or Not ====================
            // 確認 appsettings.json 中的所有資料夾是否存在 -> 不存在則回傳 false
            
            // if (!CheckFilesExist(this))
            // {
            //     AppendTextToEditor("Required file not found. Closing application.");
            //     return;
            // }


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
            if (!PingServer("8.8.8.8")) // Example IP, replace with the actual one
            {
                AppendTextToEditor("Unable to reach the server. Closing application.");
                return;
            }

            // ====================== Step.8 以組織代碼APP=>回傳版本 Check GuruOutbound service =======================
            // TODO: 未完成 (沒有 code)

            // ====================== Step.9 傳送日誌 use POST Send the Log / => 準備就緒 Ready ========================
            // TODO: 待確認
            string orgCode = "S000123"; // You can dynamically retrieve the org code if needed.
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
        }

        #endregion

        #region Placeholder Functions

        private void getOrgCode()
        {
            // TODO: 取得廠商代碼
            string m_name = Environment.MachineName;
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
            // TODO: Add logic to load and parse appsettings.json
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
                    // var jsonDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(jsonContent);

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
            // 正確獲取 LastCheckDate 並解析
            if (AppSettings["LastCheckDate"] != null)
            {
                last_check_date = DateTime.ParseExact(
                    AppSettings["LastCheckDate"],
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                AppendTextToEditor("LastCheckDate is not set.");
            }
            AppendTextToEditor("step1");
            apiBaseUrl = AppSettings["serverInfo"] + "/GuruOutbound/Trans?ctrler=Std1forme00501&method=PSNSync&jsonParam=";
            AppendTextToEditor("step1");
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

        // public bool validatePGFingerExe()
        // {
        //     // AppendTextToEditor(File.Exists(pglocation + @"\PGFinger.exe").ToString());
        //     return File.Exists(desktopPath + @"\PGFinger.exe");
        // } // END validatePGFingerExe



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

        #region download from hcm
        
         // Event handler for the button that downloads data from the external API
        // private async void btn_HCM_to_fingerprint(object sender, EventArgs e)
        // {
        //     // Disable buttons while the task is running
        //     set_btns_state(false);

        //     // Run the process in a background thread
        //     await Task.Run(() => conduct_HCM_to_fingerprint_work());

        //     // Re-enable buttons after the task is complete
        //     set_btns_state(true);
        // }


        private async void btn_HCM_to_fingerprint(object sender, EventArgs e)
        {
            conduct_HCM_to_fingerprint_work();
        }

    

        private async void conduct_HCM_to_fingerprint_work()
        {
            // Disable buttons in the UI thread
            MainThread.BeginInvokeOnMainThread(() => set_btns_state(false));

            try
            {
                // Step 1: Send Restaurant Code (PSNSync) - Chiuzu 
                // This sends the organization code to the HCM server to get employee data.
                // Example orgCode and punch card file path
                string orgCode = "S000123";
                string punchCardFilePath = Path.Combine(FileSystem.AppDataDirectory, "punchcard.json");

                // Call the service method to process initial steps
                bool result = true;

                if (result)
                {
                    // await DisplayAlert("Success", "Initial steps completed successfully.", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "Failed to complete the initial steps.", "OK");
                }
                
                // Step 2: Load HCM Employee Data (LoadEmpInfo) - Chiuzu 
                // Loads the employee data from the HCM server, saved in 'GuruData.json'.
                dynamic hcmEmployeeData = await LoadHCMEmployeeDataAsync();

                if (hcmEmployeeData == null)
                {
                    await DisplayAlert("Error", "Failed to load HCM employee JSON data.", "OK");
                    return;
                }
                else
                {
                    // 將 hcmEmployeeJson.data 轉換為 JArray
                    var dataArray = (JArray) hcmEmployeeData.data;

                    // 將每個員工資料轉換為匿名物件
                    var employeeDataList = dataArray.Select(employee => new
                    {
                        EmployeeId = (string)employee["EmpNo"],
                        Name = (string)employee["DisplayName"],
                        Finger1 = (string)employee["Finger1"],
                        Finger2 = (string)employee["Finger2"],
                        CardNo = (string)employee["CardNo"],
                        AddFlag = (string)employee["addFlag"]
                    }).ToList();
                }

                // Step 3: Read Punch Card Data (LoadPunchCardInfo) - Reena
                // Reads the punch card data from 'punchcard.json'.
                // TODO: updateDataByEmployeeId (讀取 fingerprint.dat 轉為 json 儲存)
                dynamic punchCardData = await updateDataByEmployeeId();

                // read json data
                // var punchCardJson = await ReadPunchCardJsonAsync();

                // Step 4: Compare Employee Data (Rule 1) -Reena
                // Applies rule 1 to compare employee data from HCM and PunchCard.
                AppendTextToEditor("Applying Rule 1...");
                var result1 = Punch_Data_Changing_rule1(hcmEmployeeData, punchCardData);

                object comparisonResult1 = result1.Item1;
                hcmEmployeeData = result1.Item2;
                punchCardData = result1.Item3;

                // Step 5: Compare Fingerprint Data (Rule 2) - Reena
                // Applies rule 2 to compare fingerprint data.
                AppendTextToEditor("Applying Rule 2...");
                var result2 = Punch_Data_Changing_rule2(hcmEmployeeData, punchCardData);
                object comparisonResult2 = result2.Item1;
                hcmEmployeeData = result2.Item2;
                punchCardData = result2.Item3;
                await DisplayAlert("Compare Result (Rules)", $"Rule1 Result:\n{JsonConvert.SerializeObject(comparisonResult1)}\nRule2 Result:\n{JsonConvert.SerializeObject(comparisonResult2)}", "OK");
                AppendTextToEditor(JsonConvert.SerializeObject(hcmEmployeeData));
                AppendTextToEditor(JsonConvert.SerializeObject(punchCardData));

                // Write to .json file
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var fileFingerprintPath = Path.Combine(desktopPath, "fingerprint_update.json");

                File.WriteAllText(fileFingerprintPath, JsonConvert.SerializeObject(punchCardData));

                // Step 6: Update Status if Necessary (PSNModify) 
                // If comparison result shows differences, update employee records.
                // if (comparisonResult2.NeedsUpdate)
                // {
                //     await UpdateEmployeeDataAsync(comparisonResult2);
                // }

                // Step 7: Save Updated Employee Data (LoadEmpSave)
                // Saves updated employee data after applying changes.
                await SaveEmployeeDataAsync();

                // Step 8: Log the Sync Process (PSNLog)
                // Logs the synchronization process for audit purposes.
                await LogSyncProcessAsync();

                // Step 9: Return Success
                // Finalizes the process and returns a success result.
                ReturnSuccess();
            }
            catch (Exception ex)
            {
                // Handles exceptions and returns error message/logs it.
                HandleError(ex);
            }

            // Run the fingerprint process in a separate thread
            Task.Run(() => HCM_to_fingerprint_thread());
        }

        private async Task HCM_to_fingerprint_thread()
        {
            bool is_lock_taken = false;
            ui_sp.TryEnter(ref is_lock_taken);
            if (!is_lock_taken)
                return;

            str_current_task = "員工資料匯入指紋機";

            bool b_result = await Task.Run(() => download_fingerprint_from_HCM_imp());

            if (b_result)
            {
                await MainThread.InvokeOnMainThreadAsync(() => show_info("Fingerprint download successful."));
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(() => show_err("Fingerprint download failed."));
            }

            // Ensure the UI lock is released
            if (is_lock_taken)
                ui_sp.Exit();
        }

                public async Task<bool> ProcessInitialStepsAsync(string orgCode, string punchCardFilePath)
        {
            // Placeholder for sending organization code to HCM server and processing initial steps.
            // Replace with actual logic for sending orgCode and handling the punchCardFilePath.
            await Task.Delay(500); // Simulate async work
            return true; // Assume success for now
        }

        private async Task<dynamic> LoadHCMEmployeeDataAsync()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var fileHCMPath = Path.Combine(desktopPath, "HCM.json");

                string jsonContent = await File.ReadAllTextAsync(fileHCMPath);
                dynamic employeeData = JsonConvert.DeserializeObject<dynamic>(jsonContent);

                return employeeData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading HCM JSON data: {ex.Message}");
                return null;
            }
        }

        public async Task<dynamic> updateDataByEmployeeId()
        {
            // Placeholder for reading punch card data from 'punchcard.json'.
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string inputFilePath = Path.Combine(desktopPath, "fingerprint.dat");
            string outputFilePath = Path.Combine(desktopPath, "fingerprint.json");

            // 讀取文件內容
            // 準備 JSON 結構
            var result = new
            {
                flag = true,
                message = "",
                data = new List<dynamic>(),
                modelStatus = (object)null
            };

            try
            {
                // 讀取每一行
                var lines = File.ReadAllLines(inputFilePath);

                foreach (string line in lines)
                {
                    // 分割每一行
                    string[] parts = line.Split(',');

                    // 確保每行至少有 6 個元素
                    if (parts.Length < 6)
                    {
                        Console.WriteLine($"Skipping invalid line (expected 6 fields but got {parts.Length}): {line}");
                        continue;
                    }

                    // 建立動態物件
                    var employee = new
                    {
                        EmpNo = parts[0].Trim(), // 工號
                        DisplayName = parts[1].Trim(), // 員工姓名
                        Finger1 = parts[3].Trim(), // Fingerprint1
                        Finger2 = parts[4].Trim(), // Fingerprint2
                        CardNo = parts[2].Trim(), // CardNo
                        Status = parts[5].Trim() // 狀態
                    };

                    // 添加到 data 列表中
                    result.data.Add(employee);
                }

                // 將結果轉換為 JSON
                string json = JsonConvert.SerializeObject(result, Formatting.Indented);

                // 將 JSON 寫入文件
                File.WriteAllText(outputFilePath, json);
                AppendTextToEditor("JSON file created successfully at: " + outputFilePath);
            }
            catch (Exception ex)
            {
                AppendTextToEditor("An error occurred: " + ex.Message);
            }
            return result;
        }

        public bool ApplyRule1(List<Employee> hcmEmployeeData, List<PunchCard> punchCardData)
        {
            // Placeholder for comparing employee data (Rule 1).
            // Implement logic to compare HCM employee data with punch card data.
            return true; // Return a mock result
        }

        public bool ApplyRule2(List<Employee> hcmEmployeeData, List<PunchCard> punchCardData)
        {
            // Placeholder for comparing fingerprint data (Rule 2).
            // Implement logic to compare fingerprint data.
            return true; // Return a mock result
        }

        public async Task SaveEmployeeDataAsync()
        {
            // Placeholder for saving updated employee data after changes.
            // Replace with actual save logic.
            await Task.Delay(500); // Simulate async work
        }

        public async Task LogSyncProcessAsync()
        {
            // Placeholder for logging the synchronization process for audit purposes.
            // Replace with actual logging logic.
            await Task.Delay(500); // Simulate async work
        }

        public void ReturnSuccess()
        {
            // Placeholder for finalizing the process and returning success.
            // Implement logic to return success result.
        }

        public void HandleError(Exception ex)
        {
            // Placeholder for handling exceptions and returning/logging error messages.
            // Replace with actual error handling and logging logic.
            Console.WriteLine($"Error: {ex.Message}");
        }

        // Mock data classes and comparison result for illustration purposes:
        public class Employee { /* Add relevant fields */ }
        public class PunchCard { /* Add relevant fields */ }

        #endregion

        #region File Handling and Fingerprint Download Logic

        private async Task<bool> download_fingerprint_from_HCM_imp()
        {
            bool blResult = true;
            string date = "";
            string eHrToFingerData = "";

            #region Check if Folder Exists
            if (blResult)
            {
                await MainThread.InvokeOnMainThreadAsync(() => show_info_2("Checking file paths..."));
                blResult = checkFilePath();
                if (!blResult)
                {
                    blResult = create_out_folder();
                    if (!blResult)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() => show_err_2("Fingerprinter folder missing."));
                    }
                }
            }
            #endregion

            #region Delete Old FingerIn.txt
            if (blResult && File.Exists(Path.Combine(_gOutFilePath, "FingerIn.txt")))
            {
                File.Delete(Path.Combine(_gOutFilePath, "FingerIn.txt"));
            }
            #endregion

            if (blResult)
            {
                date = DateTime.Now.ToString("yyyy-MM-dd");

                DataTable dt = getPerson();  // Simulate fetching data
                
                if (dt != null && dt.Rows.Count > 0)
                {
                    await writeToFingerInFile(dt);
                    blResult = true;
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(() => show_err_2("No data available to download."));
                    blResult = false;
                }
            }

            return blResult;
        }

        private async Task writeToFingerInFile(DataTable dt)
        {
            await MainThread.InvokeOnMainThreadAsync(() => show_info_2($"Exporting {dt.Rows.Count} rows from HCM."));

            using (FileStream fs = new FileStream(Path.Combine(_gOutFilePath, "FingerIn.txt"), FileMode.CreateNew))
            using (StreamWriter sw = new StreamWriter(fs, Encoding.GetEncoding("big5")))
            {
                foreach (DataRow dr in dt.Rows)
                {
                    string data = $"{dr["EMPLOYEEID"]},{dr["TrueName"]},{dr["CARDNUM"]},{dr["finger1"]},{dr["finger2"]},{dr["DataType"]}";
                    sw.WriteLine(data);
                    await MainThread.InvokeOnMainThreadAsync(() => show_info_2($"Exporting data: {data}"));
                }
            }

            await MainThread.InvokeOnMainThreadAsync(() => show_info_2("Data export completed."));
        }

        private DataTable getPerson()
        {
            // Simulated data fetching logic (this would come from the database in a real implementation)
            DataTable table = new DataTable();
            table.Columns.Add("EMPLOYEEID", typeof(string));
            table.Columns.Add("TrueName", typeof(string));
            table.Columns.Add("CARDNUM", typeof(string));
            table.Columns.Add("finger1", typeof(string));
            table.Columns.Add("finger2", typeof(string));
            table.Columns.Add("DataType", typeof(string));

            // Add example rows
            table.Rows.Add("E001", "John Doe", "123456", "F1", "F2", "TypeA");
            table.Rows.Add("E002", "Jane Smith", "789101", "F3", "F4", "TypeB");

            return table;
        }

        // private DataTable getPerson()
        // {
        //     DataTable dt = new DataTable();
        //     Database db = DatabaseFactory.CreateDatabase(databaseKey);
        //     DbConnection dbc = db.CreateConnection();
        //     dbc.Open();

        //     string storeProcName = "";
        //     storeProcName = "usp_McDFingerImport";
        //     DbCommand dc = db.GetStoredProcCommand(storeProcName);
        //     db.AddInParameter(dc, "@UnitCode", DbType.String,textBox1.Text);
        //     try
        //     {
        //         dt = db.ExecuteDataSet(dc).Tables[0];
        //     }
        //     catch(Exception ex)
        //     {
        //         MessageBox.Show("指紋資料取得失敗" + ex.Message);
        //     }
        //     finally
        //     {
        //         if (dbc.State != ConnectionState.Closed)
        //         {
        //             dbc.Close();
        //         }

        //     }
        //     return dt;
        // }



        #endregion

        #region Rules

        // 讀取 test_files/ 中的兩個檔案，套用 rules2 並將結果顯示於 alert 中
        private (object, object, object) Punch_Data_Changing_rule2(dynamic _hcmEmployeeData, dynamic _punchCardData)
        {
            // 將 dynamic 資料轉為 IEnumerable<dynamic>
            IEnumerable<dynamic> punchCardDataList = _punchCardData?.data ?? new List<dynamic>();
            IEnumerable<dynamic> hcmEmployeeDataList = _hcmEmployeeData?.data ?? new List<dynamic>();

            var results = new List<object>(); // 用來儲存比較結果

            // 開始比較
            foreach (var hcmEmployee in hcmEmployeeDataList)
            {
                foreach (var punchCard in punchCardDataList)
                {
                    // 比對 EmpNo 是否相同
                    if (hcmEmployee.EmpNo == punchCard.EmpNo)
                    {
                        // 比對除了 addFlag 和 Status 的其他欄位是否相同，忽略 Finger1
                        bool areFieldsEqualExceptFinger1 =
                            hcmEmployee.DisplayName == punchCard.DisplayName &&
                            hcmEmployee.Finger2.ToString().Substring(0, 10) == punchCard.Finger2.ToString().Substring(0, 10) &&
                            hcmEmployee.CardNo == punchCard.CardNo;

                        // 如果 Finger1 欄位不同且其他欄位相同，印出 case2
                        if (areFieldsEqualExceptFinger1 && hcmEmployee.Finger1.ToString().Substring(0, 10) != punchCard.Finger1.ToString().Substring(0, 10))
                        {
                            results.Add(new { result = false, Failure = $"100" });
                        }

                        // 比對除了 addFlag 和 Status 的其他欄位是否相同，忽略 Finger2
                        bool areFieldsEqualExceptFinger2 =
                            hcmEmployee.DisplayName == punchCard.DisplayName &&
                            hcmEmployee.Finger1.ToString().Substring(0, 10) == punchCard.Finger1.ToString().Substring(0, 10) &&
                            hcmEmployee.CardNo == punchCard.CardNo;

                        // 如果 Finger2 欄位不同且其他欄位相同，印出 case3
                        if (areFieldsEqualExceptFinger2 && hcmEmployee.Finger2.ToString().Substring(0, 10) != punchCard.Finger2.ToString().Substring(0, 10))
                        {
                            results.Add(new { result = false, Failure = $"010" });
                        }

                        // 比對除了 addFlag 和 Status 的其他欄位是否相同，忽略 CardNo
                        bool areFieldsEqualExceptCardNo =
                            hcmEmployee.DisplayName == punchCard.DisplayName &&
                            hcmEmployee.Finger1.ToString().Substring(0, 10) == punchCard.Finger1.ToString().Substring(0, 10) &&
                            hcmEmployee.Finger2.ToString().Substring(0, 10) == punchCard.Finger2.ToString().Substring(0, 10);

                        // 如果 CardNo 欄位不同且其他欄位相同，印出 case4
                        if (areFieldsEqualExceptCardNo && hcmEmployee.CardNo != punchCard.CardNo)
                        {
                            results.Add(new { result = false, Failure = $"001" });
                        }

                        // 如果所有欄位都相同，印出 case1
                        bool areFieldsEqual =
                            hcmEmployee.DisplayName == punchCard.DisplayName &&
                            hcmEmployee.Finger1.ToString().Substring(0, 10) == punchCard.Finger1.ToString().Substring(0, 10) &&
                            hcmEmployee.Finger2.ToString().Substring(0, 10) == punchCard.Finger2.ToString().Substring(0, 10) &&
                            hcmEmployee.CardNo == punchCard.CardNo;

                        if (areFieldsEqual)
                        {
                            results.Add(new { result = true, Failure = $"000" });
                        }
                    }
                }
            }

            
            return (results, _hcmEmployeeData, _punchCardData);
        }

        private (object, object, object) Punch_Data_Changing_rule1(dynamic _hcmEmployeeData, dynamic _punchCardData)
        {
            // 將 dynamic 資料轉為 IEnumerable<dynamic>
            IEnumerable<dynamic> punchCardDataList = _punchCardData?.data ?? new List<dynamic>();
            IEnumerable<dynamic> hcmEmployeeDataList = _hcmEmployeeData?.data ?? new List<dynamic>();

            // 初始化 EmpNo 列表
            HashSet<string> allEmpNos = new HashSet<string>();
            var results = new List<object>(); // 用來儲存比較結果

            // 檢查並添加 _hcmEmployeeData 中的 EmpNo
            if (hcmEmployeeDataList != null)
            {
                foreach (var hcmEmployee in hcmEmployeeDataList)
                {
                    if (hcmEmployee?.EmpNo != null) allEmpNos.Add((string)hcmEmployee.EmpNo);
                }
            }

            // 檢查並添加 _punchCardData 中的 EmpNo
            if (punchCardDataList != null)
            {
                foreach (var punchEmployee in punchCardDataList)
                {
                    if (punchEmployee?.EmpNo != null) allEmpNos.Add((string)punchEmployee.EmpNo);
                }
            }

            // 處理情況1 和 情況2
            foreach (var hcmEmployee in hcmEmployeeDataList)
            {
                var punchEmployee = punchCardDataList.FirstOrDefault(p => 
                    p.EmpNo != null && (string)p.EmpNo == (string)hcmEmployee.EmpNo);

                if (punchEmployee == null && (string) hcmEmployee.addFlag == "D")
                {
                    results.Add(new { result = true, Failure = $"case 1: Employee with EmpNo: {(string) hcmEmployee.EmpNo} has flag 'D' but is missing in punchCardData." });
                }
                else if (punchEmployee != null && (string) hcmEmployee.addFlag == "D")
                {
                    results.Add(new { result = true, Failure = $"case 2: Employee with EmpNo: {(string) hcmEmployee.EmpNo} has flag 'D' and exists in both hcmEmployeeData and punchCardData." });
                    
                    // 將 punchCardDataList 轉換為 List 以便刪除該筆資料
                    var punchCardList = punchCardDataList.ToList();
                    punchCardList.Remove(punchEmployee);
                    punchCardDataList = punchCardList; // 更新為修改過的 List
                }
            }

            // 處理情況3
            foreach (var punchEmployee in punchCardDataList)
            {
                var hcmEmployee = hcmEmployeeDataList.FirstOrDefault(h => 
                    h.EmpNo != null && (string)h.EmpNo == (string)punchEmployee.EmpNo);

                if (hcmEmployee == null)
                {
                    results.Add(new { result = true, Failure = $"case 3: Employee with EmpNo: {(string) punchEmployee.EmpNo} exists in punchCardData but not in hcmEmployeeData." });
                    
                    // 將 punchCardDataList 轉換為 List 以便刪除該筆資料
                    var punchCardList = punchCardDataList.ToList();
                    punchCardList.Remove(punchEmployee);
                    punchCardDataList = punchCardList; // 更新為修改過的 List
                }
            }

            // 處理情況5：_hcmEmployeeData有資料且addFlag為'A'，但_punchCardData沒有該筆資料
            foreach (var hcmEmployee in hcmEmployeeDataList)
            {
                // 確保 EmpNo 不為 null
                if (hcmEmployee.EmpNo != null)
                {
                    var punchEmployee = punchCardDataList.FirstOrDefault(p => 
                        p.EmpNo != null && (string)p.EmpNo == (string)hcmEmployee.EmpNo);

                    // 檢查 addFlag 是否為 null 再進行比較
                    if (punchEmployee == null && hcmEmployee.addFlag != null && (string)hcmEmployee.addFlag == "A")
                    {
                        results.Add(new { result = true, Failure = $"case 5: Employee with EmpNo: {(string)hcmEmployee.EmpNo} has flag 'A' but is missing in punchCardData." });
                    
                        // 新增該筆資料到 punchCardDataList
                        var newPunchEmployee = new
                        {
                            EmpNo = (string)hcmEmployee.EmpNo,
                            DisplayName = (string)hcmEmployee.DisplayName,
                            Finger1 = (string)hcmEmployee.Finger1,
                            Finger2 = (string)hcmEmployee.Finger2,
                            CardNo = (string)hcmEmployee.CardNo,
                            Status = "A"
                        };

                        // 將 punchCardDataList 轉換為 List 以便新增該筆資料
                        var punchCardList = punchCardDataList.ToList();
                        punchCardList.Add(newPunchEmployee);
                        punchCardDataList = punchCardList; // 更新為修改過的 List
                    }
                }
            }

            // 處理情況4：當 _hcmEmployeeData 和 _punchCardData 都沒有該筆資料
            foreach (var empNo in allEmpNos)
            {
                var hcmEmployee = hcmEmployeeDataList.FirstOrDefault(h => 
                    h.EmpNo != null && (string)h.EmpNo == (string)empNo);
                
                var punchEmployee = punchCardDataList.FirstOrDefault(p => 
                    p.EmpNo != null && (string)p.EmpNo == (string)empNo);

                if (hcmEmployee == null && punchEmployee == null)
                {
                    results.Add(new { result = false, Failure = $"case 4: EmpNo {(string) empNo} is missing in both hcmEmployeeData and punchCardData." });
                }
            }

            // 將更新後的資料重新分配給 _hcmEmployeeData 和 _punchCardData
            if (_hcmEmployeeData.data is IList<dynamic> hcmDataList)
            {
                hcmDataList.Clear();
                foreach (var employee in hcmEmployeeDataList)
                {
                    hcmDataList.Add(employee);
                }
            }

            if (_punchCardData.data is IList<dynamic> punchDataList)
            {
                punchDataList.Clear();
                foreach (var punch in punchCardDataList)
                {
                    punchDataList.Add(punch);
                }
            }
            return (results, _hcmEmployeeData, _punchCardData);
        }


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


        
        #region UI Updates and Helper Functions

        private bool checkFilePath()
        {
            return Directory.Exists(_gOutFilePath);
        }

        private bool create_out_folder()
        {
            try
            {
                Directory.CreateDirectory(_gOutFilePath);
                return true;
            }
            catch
            {
                return false;
            }
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

        private void show_info_2(string message)
        {
            textBox2.Text += $"{message}\n";
        }

        private void show_err_2(string message)
        {
            textBox2.Text += $"Error: {message}\n";
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

        #endregion


    }
}
