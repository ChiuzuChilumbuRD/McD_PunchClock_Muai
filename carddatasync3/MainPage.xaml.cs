using System.Diagnostics;
using Serilog;
using System.Text;
using System.Net.NetworkInformation;
using Newtonsoft.Json.Linq;
using System.Collections.Specialized;
using System.Data;
using System.Text.Json;
using Microsoft.Practices.EnterpriseLibrary.Data;
using Newtonsoft.Json;
using System.Reflection;
using System;
using System.IO;

namespace carddatasync3
{
    public partial class MainPage : ContentPage
    {
        private string str_log_level = "debug"; //TODO: Need to change after program release.
        static string _gAPPath = AppContext.BaseDirectory;
        protected static string databaseKey = "GAIA.EHR";
        protected static string downloaction    ="";
        protected static string pglocation      ="";
        protected static string responseData    ="";

        // ======================================================================================
        //將備份目錄提出來變成Config
        protected static string _gBackUpPath    ="";

        // ======================================================================================
        //指紋匯出及下載的檔案產生目的地
        protected static string _gOutFilePath   ="";
        //For Auto Run Time
        protected static string autoRunTime     ="";


        //
        protected static string test_org_code   =""; 
        protected static string machineIP   ="";

        protected static DateTime last_check_date = new DateTime();

        public static string eHrToFingerData    ="";
        public static StringBuilder str_accumulated_log = new StringBuilder();
        private static int peopleCount = 0;

        // Lock to prevent concurrent UI operations
        private static SpinLock ui_sp = new SpinLock();
        private static string str_current_task = string.Empty;
        private bool is_init_completed = true;
        public static NameValueCollection AppSettings { get; private set; }      

        // private static string apiBaseUrl = "https://gurugaia.royal.club.tw/eHR/GuruOutbound/getTmpOrg"; // Base URL for the API
        private static string apiBaseUrl    ="";


        //測試的模式
        private static string mode  ="";

        //將測試的模式轉為JSON
       private static Dictionary<string, object> modeJSON;


        //del
        private static string hcmEmployeeJsonData   ="";



        // Employee HCM =>
        private static JObject hcmEmployeeJson   = null;

        // Employee Machine =>
        private static JObject punchMachineEmployeeJson = new JObject
                                                            {
                                                                ["flag"] = true,
                                                                ["message"] = "",
                                                                ["data"] = new JArray(),
                                                                ["modelStatus"] = null
                                                            };
        
        private bool isClockExpanded = true;

        private static string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

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
            
            StartClock();
        }


        // Set hover effect for each button
        private async void OnToggleLogButtonClicked(object sender, EventArgs e)
        {
            if (LogFrame.IsVisible)
            {
                await textBox2.FadeTo(0, 500); // Fade-out effect
                LogFrame.IsVisible = false;
            }
            else
            {
                LogFrame.IsVisible = true;
                await textBox2.FadeTo(1, 500); // Fade-in effect
            }
        }

         private void StartClock()
        {
            Device.StartTimer(TimeSpan.FromSeconds(5), () =>
            {
                ClockLabel.Text = DateTime.Now.ToString("HH:mm");
                return true; // Repeat timer
            });
        }

        private async void OnClockTapped(object sender, EventArgs e)
        {

            double startClockWidth = isClockExpanded ? 8 : 0;
            double endClockWidth = isClockExpanded ? 0 : 8;

            double startLogWidth = isClockExpanded ? 2 : 10;
            double endLogWidth = isClockExpanded ? 10 : 2;

            // 定義動畫
            var animation = new Animation();

            // 動畫 ClockColumn 的寬度
            animation.Add(0, 1, new Animation(v => ClockColumn.Width = new GridLength(v, GridUnitType.Star), startClockWidth, endClockWidth));

            // 動畫 LogColumn 的寬度
            animation.Add(0, 1, new Animation(v => LogColumn.Width = new GridLength(v, GridUnitType.Star), startLogWidth, endLogWidth));

            // 運行動畫
            animation.Commit(this, "GridAnimation", 16, 500, Easing.Linear);
            
            isClockExpanded = !isClockExpanded;

        }

        private async void OnSettingButtonClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new AppSettingPage());
        }

        #region Initialisation
        private void OnRefreshButtonClicked(object sender, EventArgs e)
        {
            InitializeApp();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            InitializeApp(); // 每次頁面出現時執行
        }

         private async Task InitializeApp()
        {
            // ================================ Step.0 Lock Button ===============================================
            set_btns_state(false);

            // ======================= Step.1 取得目前 APP 版本號並印出 Get Current APP Version =====================
           // var version = Assembly.GetExecutingAssembly().GetName().Version;
           // AppendTextToEditor(version.ToString());
             
            AppendTextToEditor("App initialization Start"); // App initialization started.

            // ======================= Step.2 匯入設定檔 Load Config (AppSettings.json) =======================
            // 讀取 appsettings.json 存入 AppSettings 中
            LoadAppSettings();
            LoadAppSettingsLayout.IsVisible = false;

            // Step 2: Check if file exists and execute if found
            // ======================== Step.3 確認資料夾是否存在 Check Folder Exist or Not ====================
            // 確認 appsettings.json 中的所有資料夾是否存在 -> 不存在則回傳 false
            
            if (!CheckFilesExist(this))
            {
                AppendTextToEditor("Required file not found. ");
                CheckFilesExistLayout.IsVisible = true;

            }
            else CheckFilesExistLayout.IsVisible = false;


            hcmEmployeeJson = null;
          

            // =============== Step.4 確認網路連線 Check Internet Available ==================================
            
            if(CheckModeJSON("internetCheck").Length>0){
                if (!IsInternetAvailable())
                {
                    AppendTextToEditor("No internet connection. Closing application.");
                    IsInternetAvailableLayout.IsVisible = true;
                    
                }
               else IsInternetAvailableLayout.IsVisible = false;
            }
            // ================== Step.5 取得目前電腦名稱 Get Current Computer Name(GetOrgCode) ===============

            if(CheckModeJSON("orgNameCheck").Length>0){
                //將組織代碼代入
                
             
                if(string.IsNullOrEmpty(textBox1.Text) ){
                    AppendTextToEditor("將組織代碼代入");
                    getOrgCode();
                }
            

                // 確認組織代碼名稱
                if (string.IsNullOrEmpty(textBox1.Text) ) //|| textBox1.Text.Length != 8
                {
                    string str_msg = "組織代碼錯誤";
                    AppendTextToEditor(str_msg);
                    // await show_error(str_msg, "組織代碼錯誤");
                   // is_init_completed = false;
                    GetOrgCodeLayout.IsVisible = true;
                }
                else GetOrgCodeLayout.IsVisible = false;

                    
                //用POST取得  
                labStoreName.Text = await getORGName(textBox1.Text);  //old name getCradOrgName


                if (labStoreName.Text.Length == 0)
                {
                    string str_msg = $"找不到{textBox1.Text}組織名稱，將關閉程式";
                    // show_error(str_msg, "組織名稱錯誤");
                    AppendTextToEditor(str_msg);
                    //is_init_completed = false;
                    // GetOrgCodeLayout.IsEnabled = true;
                    GetOrgCodeLayout.IsVisible = true;
                    // IsHCMReadyLayout.IsEnabled = true;
                    IsHCMReadyLayout.IsVisible = true;
                 
                }
                else {
                    GetOrgCodeLayout.IsVisible = false;
                    IsHCMReadyLayout.IsVisible = false;
                }
            }
            
            // ================= Step.6 確認打卡機就緒 Check Punch Machine Available ==================
            // TODO: 無法測試，未完成
            /* if (!is_HCM_ready())
            {
                string str_msg = "HCM連接發生問題，將關閉程式";
                // await show_error(str_msg, "連線問題");
                AppendTextToEditor(str_msg);
                is_init_completed = false;
                IsHCMReadyLayout.IsEnabled = true;
                IsHCMReadyLabel.TextColor = Colors.Blue;
                // return;
            }
            else IsHCMReadyLayout.IsEnabled = false;*/
        
                if(CheckModeJSON("machineCheck").Length>0){
                    // Ping server IP address
                    if (!PingMachine(machineIP)) // Example IP, replace with the actual one
                    {
                     //   AppendTextToEditor("Unable to reach the Machine. "+machineIP);
                        // PingServerLayout.IsEnabled = true;
                        PingServerLayout.IsVisible = true;
                       
                    }
                    else PingServerLayout.IsVisible = false;
                }
            // ====================== Step.7 確認人資系統有沒有上線 (ping IP)​ Check if the HR Server is available ============
         
            // ====================== Step.8 以組織代碼APP=>回傳版本 Check GuruOutbound service =======================
            // TODO: 未完成 (沒有 code)

            // ====================== Step.9 傳送日誌 use POST Send the Log / => 準備就緒 Ready ========================
           /* string orgCode = ;  
            string jsonParam = $"{{\"org_no\":\"{orgCode}\"}}";
            string fullUrl = $"{apiBaseUrl}{Uri.EscapeDataString(jsonParam)}";
            bool postSuccess = await send_org_code_hcm(orgCode);
*/

            SendLogLayout.IsVisible = true;
              
            bool logSuccess = await setLog(textBox1.Text,"Inital Success\n");

            if (logSuccess)
            {
             //   AppendTextToEditor("Log sent successfully ");
                SendLogLayout.IsVisible = false;
            } else{
                AppendTextToEditor("Failed to send log or save data.");
               SendLogLayout.IsVisible = true;
            }

            //AppendTextToEditor($"url: {fullUrl}");
          
/*
            if (postSuccess)
            {
                AppendTextToEditor("Log sent successfully and data saved to FingerIn.json.");
                SendLogLayout.IsEnabled = false;
            }
            else
            {
                AppendTextToEditor("Failed to send log or save data.");
                SendLogLayout.IsEnabled = true;
                SendLogLabel.TextColor = Colors.Blue;
                return;
            }
*/
            // ====================== Step.10 Unlock Button​ + 初始化成功 App initialization completed =======================
            set_btns_state(true);

            AppendTextToEditor("App initialization completed.");
        }

        #endregion
        
        #region Placeholder Functions
        
        private void getOrgCode()
        {
            // TODO: 修改廠商代碼的取得方式
            string m_name =  Environment.MachineName;//"S000123"; // TODO:

            AppendTextToEditor("Machine: " + m_name);
            AppendTextToEditor("ID: " + test_org_code);


            textBox1.Text = string.Empty; // 清空 Entry 控件的文本

            if (test_org_code.ToString().Length > 3){

                 AppendTextToEditor("使用測試用帳號");
                
                 textBox1.Text = "S00"+test_org_code.ToString();

            }else {
                
                if (m_name.Length < 7){
            
                AppendTextToEditor("抓取組織代碼失敗");
                textBox1.Text = string.Empty; // 清空 Entry
            
                }else{
                

                    string l_name = m_name.Substring(2, 5);
                    if (CommonUtility.is_valid_org_code(l_name))
                    {
                        textBox1.Text = "S00" + l_name; // 設置 Entry 的值
                    }

                    
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
            AppendTextToEditor($"tOrgCode: {textBox1.Text}"); // 使用 textBox1.Text 來取得值
        } // END getOrgCode

        private bool is_HCM_ready()
        {
            // TODO: 確認 HCM 連線是否成功
          //  AppendTextToEditor("HCM連線中,請等待");
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


        //用組織代碼去要資料
        public static async Task<bool> send_org_code_hcm(string orgCode)
        {

            try
            {
                // Define the path for the FingerData directory on the Desktop
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string fingerDataPath = Path.Combine(desktopPath, "FingerData");

                // Ensure the FingerData directory exists; create if not
                if (!Directory.Exists(fingerDataPath))
                {
                    Directory.CreateDirectory(fingerDataPath);
                }

                // Define the path for FingerIn.json within FingerData
                var fileHCMPath = Path.Combine(fingerDataPath, "FingerIn.json");

                // Call the API and get the JSON response
                string responseData = await GetRequestAsync(orgCode);
               
                hcmEmployeeJsonData = responseData;

            

                // Write the response data to a JSON file
                await File.WriteAllTextAsync(fileHCMPath, responseData);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }
        
        //delete
        // Function to handle the API POST request just for PSNSync
        static async Task<string> GetRequestAsync(string orgCode)
        {
            // Construct the full URL with the org_code
            string jsonParam = $"{{\"org_no\":\"{orgCode}\"}}"; //fix param
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
            }
        }

        private async Task<string> getORGName(string _orgCode)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // 建立要傳遞的 JSON 資料，使用字串插值來替換 org_No 和 logData 的值
                    var requestData = new
                    {
                        ctrler = "STD1FORME00501",
                        method = "PSNVersion",
                        jsonParam = $@"{{
                            'org_No': '{_orgCode}',
                        }}",
                        token = "your-token-here"
                    };


                    // 將資料序列化為 JSON 字串
                    string json = JsonConvert.SerializeObject(requestData);

                    // 設定請求內容
                    var content = new StringContent(json, Encoding.UTF8, "application/json");


                    // 發送 POST 請求
                    var response = await client.PostAsync(AppSettings["serverInfo"] + "/GuruOutbound/Trans", content);

                    // 確認回應成功
                    if (response.IsSuccessStatusCode)
                    {
                        // 讀取並返回回應內容
                        string _str = await response.Content.ReadAsStringAsync();
                        AppendTextToEditor(_str);

                        // 反序列化 JSON 並提取 orgName
                        var jsonDocument = JsonDocument.Parse(_str);
                        var orgName = jsonDocument.RootElement
                                            .GetProperty("data")  // 進入 "data" 屬性
                                            .GetProperty("orgName")  // 進入 "orgName" 屬性
                                            .GetString();  // 提取 orgName 的值

                        return orgName;
                    }
                    else
                    {
                        return "";
                        // throw new Exception($"Failed to get data from API: {response.StatusCode}");
                    }
                }
            } 
            catch (HttpRequestException ex)
            {
                throw new Exception($"Failed to connect to the API: {ex.Message}");
            }
        } // END getCradORGName



        private async Task<bool> setLog(string _orgCode,string _logData)
        {

            try
            {
                
                using (var client = new HttpClient())
                {
                    // 建立要傳遞的 JSON 資料，使用字串插值來替換 org_No 和 logData 的值
                    var requestData = new
                    {
                        ctrler = "STD1FORME00501",
                        method = "PSNLog",
                        jsonParam = "{'logData': '" + _logData + "'}",
                        token = "your-token-here"
                    };
                    AppendTextToEditor($"jsonParam: {_logData}");

                    // 方法2：使用 JsonSerializerSettings
                    string json = JsonConvert.SerializeObject(requestData, new JsonSerializerSettings 
                    { 
                        Formatting = Formatting.None 
                    });

                    
                    AppendTextToEditor(json);

                    // 設定請求內容
                    var content = new StringContent(json, Encoding.UTF8, "application/json");


                    // 發送 POST 請求
                    var response = await client.PostAsync(AppSettings["serverInfo"] + "/GuruOutbound/Trans", content);

                    AppendTextToEditor(AppSettings["serverInfo"] + "/GuruOutbound/Trans");
/*
[
 {
"flag": true,
"message": "",
"data": "Total 1 records",
"modelStatus": "Success"
 }
]
*/

                    // 確認回應成功
                    if (response.IsSuccessStatusCode)
                    {
                        // 讀取並返回回應內容
                        string _str = await response.Content.ReadAsStringAsync();
                        // 反序列化 JSON 陣列
                        var jsonDocument = JsonDocument.Parse(_str);
                        var rootElement = jsonDocument.RootElement;
                        AppendTextToEditor($"rootElement: {rootElement}");
                        
                        // 因為是陣列，所以先獲取第一個元素
                        var firstElement = rootElement[0]; // rootElement[0]
                        
                        // 獲取 flag 值
                        var flag = firstElement.GetProperty("flag").GetBoolean();
                        
                        // 記錄返回的訊息
                        var _resultStr = firstElement.GetProperty("data").GetString();
                        AppendTextToEditor($"resultStr: {_resultStr}");
                        
                        // 根據 flag 值返回結果
                        return flag;
                    }
                    else
                    {
                        AppendTextToEditor($"Failed to get data from API: {response.StatusCode}");
                                
                        return false;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                AppendTextToEditor($"Failed to connect to the API: {ex.Message}");
                return false;
            }
        } //setLog END


private async Task<bool> setModify(JArray _chgList)
{
    const int batchSize = 100; // 每次上傳的批次大小
    
    
    try
    {
        using (var client = new HttpClient())
        {
            // 將資料分割成每 10 筆為一批
            for (int i = 0; i < _chgList.Count; i += batchSize)
            {
                // 取得當前批次的資料
                var batch = new JArray(_chgList.Skip(i).Take(batchSize));
                // string batchStr = batch.ToString().Replace("\n", "").Replace("\r", "").Replace("  ", "");
                // batchStr = " {0:"+batchStr+"}";
                // 準備要傳遞的 JSON 資料
                var requestData = new
                {
                    ctrler = "STD1FORME00501",
                    method = "PSNModify",
                    jsonParam = batch,
                    token = "your-token-here"
                };

                // 將 requestData 轉為 JSON 字串
                // 將資料序列化為 JSON 字串
                string json = JsonConvert.SerializeObject(requestData);

                // 設定請求內容
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // -display 印出即將發送的請求內容
              
                    AppendTextToEditor($"PSNModify json: {json}");

                    // 發送 POST 請求
                    var response = await client.PostAsync(AppSettings["serverInfo"] + "/GuruOutbound/Trans", content);

                    // 確認回應成功
                    if (response.IsSuccessStatusCode)
                    {
                        // 讀取並返回回應內容
                        string _str = await response.Content.ReadAsStringAsync();
                        // 反序列化 JSON 並提取 orgName
                        var jsonDocument = JsonDocument.Parse(_str);
                        var _resultStr = jsonDocument.RootElement
                                            .GetProperty("data")  // 進入 "data" 屬性
                                            .GetString();  // 提取 orgName 的值

                        AppendTextToEditor($"resultStr: {_resultStr}");

                        return true;
                    }
                    else
                    {
                        AppendTextToEditor($"Failed to get data from API: {response.StatusCode}");
                                
                        return false;
                    }
            }//10 records
        }
        
        return true; // 成功完成所有批次
    }
    catch (Exception ex)
    {
        AppendTextToEditor($"Exception: {ex.Message}");
        return false;
    }
}//setModify END

       
        private async Task<bool> getPSNSync(string _orgCode)
        {

            // 記錄開始時間
            Stopwatch stopwatch = Stopwatch.StartNew();


            AppendTextToEditor("人事同步開始");

            try
            {
                
            
                using (var client = new HttpClient())
                {
                    // 建立要傳遞的 JSON 資料，使用字串插值來替換 org_No 和 logData 的值
                    var requestData = new
                    {
                        ctrler = "STD1FORME00501",
                        method = "PSNSync",
                        jsonParam = $@"{{
                                'org_No': '{_orgCode}',
                        }}",
                        token = "your-token-here"
                    };


                    // 將資料序列化為 JSON 字串
                    string json = JsonConvert.SerializeObject(requestData);

                    // 設定請求內容
                    var content = new StringContent(json, Encoding.UTF8, "application/json");


                    // 發送 POST 請求
                    var response = await client.PostAsync(AppSettings["serverInfo"] + "/GuruOutbound/Trans", content);

                    // 確認回應成功
                    if (response.IsSuccessStatusCode)
                    {
                        // 讀取並返回回應內容
                        string _str = await response.Content.ReadAsStringAsync();

                        // 定義當前目錄下的 FingerData 資料夾路徑
                        string fingerDataPath = Path.Combine(Environment.CurrentDirectory, "");

                        // 確保 FingerData 資料夾存在；如果不存在則建立
                        if (!Directory.Exists(fingerDataPath))
                        {
                            Directory.CreateDirectory(fingerDataPath);
                        }

                        // 定義 FingerData 資料夾中 FingerIn.json 的路徑
                        var fileHCMPath = Path.Combine(fingerDataPath, "FingerIn.json");

                        // 呼叫 API 並取得 JSON 回應
                        string responseData = _str;
                        //PSNsync
                        // hcmEmployeeJsonData = responseData;

                        hcmEmployeeJson = JObject.Parse(responseData);

                        // 將回應資料寫入 JSON 檔案 (會覆寫)
                        await File.WriteAllTextAsync(fileHCMPath, responseData);

                     //   AppendTextToEditor("Data has been successfully written to FingerIn.json");
                        
                        stopwatch.Stop();
                        TimeSpan timeSpent = stopwatch.Elapsed;

                        // 顯示成功訊息及花費的時間
                        AppendTextToEditor($"Data has been successfully written to FingerIn.json. Time spent: {timeSpent.TotalMilliseconds} ms");
                        
                        return true;
                    }
                    else
                    {
                        AppendTextToEditor($"Failed to get data from API: {response.StatusCode}");
                                
                        return false;
                    }
                }
            
            } catch (HttpRequestException ex)
            {
                AppendTextToEditor($"Failed to connect to the API: {ex.Message}");
                return false;
                
            }
        } //getPSNSync END

        // private async Task<bool> sendPSNModify(string org_no)
        // {
        //     // 記錄開始時間
        //     Stopwatch stopwatch = Stopwatch.StartNew();
        //     AppendTextToEditor("Uploading fingerprint data to HCM...");

        //     try
        //     {
        //         using (var client = new HttpClient())
        //         {
        //             // 建立要傳遞的 JSON 資料，包含上傳的 dataToUpload
        //             var requestData = new
        //             {
        //                 ctrler = "STD1FORME00501",
        //                 method = "PSNModify",
        //                 jsonParam = $@"{{
        //                     'org_No': '{_orgCode}',
        //                     'data': {dataToUpload.ToString()}
        //                 }}",
        //                 token = "your-token-here"
        //             };

        //             // 序列化 JSON 資料
        //             string json = JsonConvert.SerializeObject(requestData);

        //             // 設定請求內容
        //             var content = new StringContent(json, Encoding.UTF8, "application/json");

        //             // 發送 POST 請求
        //             var response = await client.PostAsync(AppSettings["serverInfo"] + "/GuruOutbound/Trans", content);


        //         }

        //     }
        //     catch (HttpRequestException ex)
        //     {
        //         AppendTextToEditor($"Failed to connect to the API: {ex.Message}");
        //         return false;
        //     }

        //     // Prepare the response structure​
        //     var response = new
        //     {
        //         flag = true,               // Set to true or false based on your logic​
        //         message = "",              // Empty message or adjust as needed​
        //         data = "共幾筆",            // The data field contains the string "1.2.0"​
        //         modelStatus = (object)null // Can be set to null or any specific value​
        //     };

        //     // Return the JSON response​
        //     return Json(response, JsonRequestBehavior.AllowGet);
        // }

        // 讀取 appsettings.json 並將相關路徑傳入參數中
        private void LoadAppSettings()
        {
            AppSettings = new NameValueCollection();

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
                    LoadAppSettingsLayout.IsVisible = true;
                }
            }
            else
            {
                AppendTextToEditor("appsettings.json not found.");
                LoadAppSettingsLayout.IsVisible = true;
            }
            

           
            downloaction    = AppSettings["downloadlocation"];
            pglocation      = AppSettings["pgfingerlocation"];
            _gBackUpPath    = AppSettings["BackUpPath"];
            _gOutFilePath   = AppSettings["fileOutPath"];
            autoRunTime     = AppSettings["AutoRunTime"];
            test_org_code   = AppSettings["test_org_code"];
            last_check_date = DateTime.ParseExact(
                                    AppSettings["LastCheckDate"],
                                    "yyyy-MM-dd",
                                    System.Globalization.CultureInfo.InvariantCulture);

            machineIP       = AppSettings["machineIP"];
            apiBaseUrl      = AppSettings["serverInfo"] + "/GuruOutbound/Trans?ctrler=Std1forme00501&method=PSNSync&jsonParam=";
    
            mode            = AppSettings["mode"];
            

            // 將 JSON 字串解析為 Dictionary<string, object>
             modeJSON = JsonConvert.DeserializeObject<Dictionary<string, object>>(mode);

            // 存取 key 的值
           
            AppendTextToEditor(CheckModeJSON("debugFileCheck")); // 輸出 "value"
            
            


        } // END LoadAppSettings


        //看是否mode有這個key值
        private string CheckModeJSON(string _key){
            if (modeJSON != null && modeJSON.ContainsKey(_key))
            {
                //AppendTextToEditor(modeJSON[_key].ToString()); // 輸出 "value"
                return modeJSON[_key].ToString();
          
            }

            return "";
        }

        private bool CheckFilesExist(MainPage page)
        {
            // Load settings from appsettings.json

            var downloadLocation    = AppSettings["downloadlocation"];
            var pgFingerLocation    = AppSettings["pgfingerlocation"];
            var fileOutPath         = AppSettings["fileOutPath"];
            var BackUpPath          = AppSettings["BackUpPath"];

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
                AppendTextToEditor($"The BackupPath folder does not exist: {fileOutPath}");
                return false;
            }

            if (!Directory.Exists(BackUpPath))
            {
                AppendTextToEditor($"The Backup folder does not exist: {BackUpPath}");
                return false;
            }

            // AppendTextToEditor("The Folders are all ready.");

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




        // 確認 PGFinger.exe 是否存在
        public bool validatePGFingerExe()
        {
            // AppendTextToEditor(File.Exists(pglocation + @"\PGFinger.exe").ToString());
            return File.Exists(pglocation + @"\PGFinger.exe");
        } // END validatePGFingerExe

        // 確定網路連線
        private bool IsInternetAvailable()
        {
            //AppendTextToEditor("Checking Internet connection...");

            var current = Connectivity.Current.NetworkAccess;
            var profiles = Connectivity.Current.ConnectionProfiles;

            // Check if the device is connected to the internet via WiFi or other profiles
            bool isConnected = current == NetworkAccess.Internet;

            if (isConnected)
            {
                
                return true;
            }
            else
            {
                
                return false;
            }
        } // END IsInternetAvailable

        // 確定是否能夠 ping server
        private bool PingMachine(string ipAddress)
        {
            AppendTextToEditor($"Pinging Machine at {ipAddress}...");

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


        private async void DisplayErrorMessage(string message)
        {
            // 使用 MAUI 的 DisplayAlert 顯示錯誤訊息
            await Application.Current.MainPage.DisplayAlert("錯誤", message, "確定");

            // 如果你需要在控制台輸出錯誤訊息，適用於其他平台的處理方式
            Console.WriteLine(message);

            // 自定義方法，假設用於編輯器中追加文本
            AppendTextToEditor(message);

            // 日誌記錄錯誤
            Log.Error(message);
        }// END DisplayErrorMessage

        #endregion


        // Helper function to append text to textBox2
        // 寫 log 到 TextEditor 中
        private void AppendTextToEditor(string text)
        {
                int maxTextLength = 300; // 設定 textBox2.Text 的最大長度

                if (textBox2 != null)
                {
                    // 檢查 textBox2 的文本長度是否已達到最大限制
                   // if (textBox2.Text.Length >= maxTextLength)
                   // {
                    //   textBox2.Text=""; // 清除文本框
                   // }

                    // 添加新文本
                    textBox2.Text = $"{text}\n" + textBox2.Text;
                }


            //xavier
            // 如果成功，則在當前目錄寫入或附加到 log 檔案
            string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), DateTime.Now.ToString("yyyyMMdd") + "_log.txt");
           
            File.AppendAllText(logFilePath, DateTime.Now.ToString("HH:mm:ss") + " "+text + Environment.NewLine);


        } // END AppendTextToEditor

         #region download from hcm




        //xavier main button 1
        private async void btn_HCM_to_fingerprint(object sender, EventArgs e)
        {
            set_btns_state(false);

       
            // Step 1:
            bool hcmResult = await getPSNSync(textBox1.Text);

            if (!hcmResult)
            {
                await DisplayAlert("Error", "Failed to execute HCM to fingerprint thread.", "OK");

                set_btns_state(true);
                return;
            }

                    
            // Step 2: Load HCM Employee Data
                
            if (hcmEmployeeJson == null)
            {
                await DisplayAlert("Error", "Failed to load HCM employee JSON data.", "OK");
                
                set_btns_state(true);
                return;
            }
            else
            {
                
                JArray dataArray = (JArray)hcmEmployeeJson["data"]; // HCM data
                /*
                //example:
                [{
                empNo = "工號003",
                displayName = "員工姓名3",
                finger1 = "CABCDEFGHIJKLMNOPQRSTUVWXYYZABCDEFGHIJKLMNOPQRSTUVWX",
                finger2 = "XABCDEFGHIJKLMNOPQRSTUVWXYYZABCDEFGHIJKLMNOPQRSTUVWX",
                cardNo = "C123456789",
                addFlag = "A"
                },.....]
                */
                if (dataArray == null){
                
                    await DisplayAlert("Error", "Failed to load HCM employee JSON data.", "OK");
                    
                    set_btns_state(true);
                    return;

                }


            }
             
            /*  
            foreach (JObject item in (JArray)hcmEmployeeJson["data"])
            {
                string displayName = (string)item["finger2"];
                
                AppendTextToEditor(displayName);
            }
            */
            // AppendTextToEditor(hcmEmployeeJson.ToString()); 
            

            // Step 3: Read Punch Card Data => punchMachineEmployeeJson
            bool getPunchMachineEmployeeJson = await upload_fingerprint_to_HCM(this); // FingerOut.txt

            if(!getPunchMachineEmployeeJson){

                await DisplayAlert("Error", "Failed to load Punch Machine employee JSON data.", "OK");
                    
                set_btns_state(true);
                return;
            }
        

            // 驗證並訪問 "data" 屬性中的每個項目
            // foreach (JObject item in (JArray)punchMachineEmployeeJson["data"])
            // {
            //     string empNo = (string)item["EmpNo"];
            //     string displayName = (string)item["DisplayName"];
            //     // 這裡可以進行需要的操作，例如輸出或儲存
            //     Console.WriteLine($"EmpNo: {empNo}, DisplayName: {displayName}");
            // }


            // Step 4: Compare Employee Data (Rule 1)
            //--display
            // AppendTextToEditor($"Original punchMachineEmployeeJson: {punchMachineEmployeeJson.ToString()}");
            // AppendTextToEditor($"Original hcmEmployeeJson: {hcmEmployeeJson.ToString()}");
            
            JArray rule1Result = Punch_Data_Changing_rule1(hcmEmployeeJson, punchMachineEmployeeJson);
            // AppendTextToEditor("----------");
            AppendTextToEditor($"rule1Result: {rule1Result.ToString()}");

            if (rule1Result.Count == 0)
            {
                await DisplayAlert("Error", "Rule 1 No record", "OK");
                
                set_btns_state(true);
                return;
            }

            AppendTextToEditor($"Rule1 punchMachineEmployeeJson: {punchMachineEmployeeJson.ToString()}");
            AppendTextToEditor($"Rule1 hcmEmployeeJson: {hcmEmployeeJson.ToString()}");

             
            // Step 5: Compare Fingerprint Data (Rule 2)
            JArray rule2Result = Punch_Data_Changing_rule2(hcmEmployeeJson, punchMachineEmployeeJson);
            // AppendTextToEditor($"After punchMachineEmployeeJson: {punchMachineEmployeeJson.ToString()}");
            // AppendTextToEditor($"After hcmEmployeeJson: {hcmEmployeeJson.ToString()}");
            //AppendTextToEditor("----------");
            AppendTextToEditor($"rule2Result: {rule2Result.ToString()}");
            
            
            // Step 6: Write Data to PunchMachine
            bool writeToFingerprinterFlag = await write_to_fingerprinter(this,rule1Result);

            if(!writeToFingerprinterFlag) {
                await DisplayAlert("Error", "Write Data to PunchMachine Error", "OK");
                set_btns_state(true);
                return;
            }

            // Step 7: Updated Employee be changed Data
           bool updateSuccess = await setModify(rule2Result);

            if (updateSuccess)
            {
                //AppendTextToEditor("Log sent successfully ");
            } else {
                
                AppendTextToEditor("Failed to send log or save data.");
                
                set_btns_state(true);
                return;
            }

            // Step 8: Log the Sync Process
            bool logSuccess = await setLog(textBox1.Text,"人事同步完成\n");

            if (logSuccess)
            {
                //人事同步完成
               AppendTextToEditor("Log sent successfully ");

                set_btns_state(true);
                return;

            } else {
                AppendTextToEditor("Failed to send log or save data.");
                
                set_btns_state(true);
                return;

            }

            // AppendTextToEditor($"result: {result1.Item1.ToString()}");
            // AppendTextToEditor($"result HCM: {result1.Item2.ToString()}");
            // AppendTextToEditor($"result punchClock: {result1.Item3.ToString()}");

          

        }//btn_HCM_to
    
        //del
        private async Task conduct_HCM_to_fingerprint_work_1()
        {
            set_btns_state(false);

            try
            {
                // Step 1: Run HCM to fingerprint thread
                bool hcmResult = await Task.Run(() => HCM_to_fingerprint_thread(this));
                if (!hcmResult)
                {
                    await DisplayAlert("Error", "Failed to execute HCM to fingerprint thread.", "OK");
                    return;
                }

                  set_btns_state(true);
                return;

                // Step 2: Load HCM Employee DataD
                dynamic hcmEmployeeData = await LoadHCMEmployeeDataAsync();
                if (hcmEmployeeData == null)
                {
                    await DisplayAlert("Error", "Failed to load HCM employee JSON data.", "OK");
                    return;
                }
                else
                {
                    ParseEmployeeData(hcmEmployeeData);
                }

                // Step 3: Read Punch Card Data
                dynamic punchCardData = await updateDataByEmployeeId();
                if (punchCardData == null)
                {
                    await DisplayAlert("Error", "Failed to load punch card data.", "OK");
                    return;
                }

                // Step 4: Compare Employee Data (Rule 1)
                await ApplyRule1Comparison(hcmEmployeeData, punchCardData);

                // Step 5: Compare Fingerprint Data (Rule 2)
                await ApplyRule2Comparison(hcmEmployeeData, punchCardData);

                // Step 6: Write Data to JSON File
                WriteDataToFile(punchCardData);

                // Step 7: Save Updated Employee Data
                await SaveEmployeeDataAsync();

                // Step 8: Log the Sync Process
                await LogSyncProcessAsync();

                // Finalize the process
                ReturnSuccess();
            }
            catch (Exception ex)
            {
                // Handle any exceptions and log them
                HandleError(ex);
            }
            finally
            {
                // Restore the button state after the task completes
                set_btns_state(true);
            }
        }

        private async Task<bool> HCM_to_fingerprint_thread(MainPage page)
        {
           
            bool result = await getPSNSync(textBox1.Text); // Assuming `send_org_code_hcm` returns bool
            return result;
        }

        private void ParseEmployeeData(dynamic hcmEmployeeData)
        {
            AppendTextToEditor("Parsing HCM employee data...");
            var dataArray = (JArray)hcmEmployeeData.data;
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

        private async Task ApplyRule1Comparison(dynamic hcmEmployeeData, dynamic punchCardData)
        {
            AppendTextToEditor("Applying Rule 1...");
            var result = Punch_Data_Changing_rule1(hcmEmployeeData, punchCardData);
            hcmEmployeeData = result.Item2;
            punchCardData = result.Item3;
        }

        private async Task ApplyRule2Comparison(dynamic hcmEmployeeData, dynamic punchCardData)
        {
            AppendTextToEditor("Applying Rule 2...");
            var result = Punch_Data_Changing_rule2(hcmEmployeeData, punchCardData);
            hcmEmployeeData = result.Item2;
            punchCardData = result.Item3;
        }

        private void WriteDataToFile(dynamic punchCardData)
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var fileFingerprintPath = Path.Combine(desktopPath, "FingerData/fingerprint_update.json");
            File.WriteAllText(fileFingerprintPath, JsonConvert.SerializeObject(punchCardData));
            AppendTextToEditor("Data successfully written to fingerprint_update.json");
        }



        private async void HCM_to_fingerprint_thread(object obj)
        {
            bool is_lock_taken = false;



            ui_sp.TryEnter(ref is_lock_taken);
            if (!is_lock_taken)
                return;

            string str_current_task = "員工資料匯入指紋機";
            MainPage this_page = (MainPage)obj; // Assuming MainPage is passed instead of Form1
            //OperationRecorder op_recorder = null;
            bool b_result = false;

            // Refresh or update connection strings
            //update_conn_str_and_refresh();

            try
            {
                while (true)
                {
                    //using (var btn_ctl = new BtnController(this_page))

                    {
                        // Conduct fingerprint upload before fingerprint download
                        // await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Starting fingerprint upload to HCM..."));

                        // // Upload fingerprint to HCM
                        // b_result = await upload_fingerprint_to_HCM(this_page);

                        // if (!b_result)
                        // {
                        //     await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Upload to HCM failed."));
                        //     return;
                        // }

                        // Proceed with downloading fingerprints from HCM
                        await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Downloading fingerprint data from HCM..."));
                        b_result = await download_fingerprint_from_HCM_imp(this_page); // Add await here


                        //upload_op_recorder(op_recorder, b_result);

                        if (!b_result)
                            break;

                        // Proceed with writing fingerprints to the fingerprint device
                        //op_recorder = init_op_recorder(this_page.TextBox1.Text, "Upload FP to Fingerprint Device");

                        // await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Writing fingerprint data to the fingerprint machine..."));
                        // b_result = await Task.Run(() => write_to_fingerprinter(this_page));

                        //upload_op_recorder(op_recorder, b_result);
                    }
                    break;  // Exit after one cycle
                }
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Error during HCM to fingerprint processing: {ex.Message}"));
            }
            finally
            {
                // Clear the connection strings if needed
                //clear_conn_str();

                // Release the lock
                if (is_lock_taken)
                    ui_sp.Exit();
            }
        }



        // Main method to simulate downloading fingerprints from HCM and writing them to a file
        private async Task<bool> download_fingerprint_from_HCM_imp(MainPage page)
        {
            // Import fingerprint data from the database to the card reader.
            bool blResult = true;
            string date = "";
            eHrToFingerData = "";

            MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Calling download_fingerprint_from_HCM_imp"));

            #region 儲存資料夾不存在 (Check if the folder for storing PGFinger data exists)
            if (blResult)
            {
                MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("檢查相關資料夾..")); // Checking relevant folders...
              
              
                blResult = page.ensureFilePathExists();

                if (!blResult)
                {
                    blResult = page.create_out_folder();
                    if (!blResult)
                    {
                        MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("指紋機程序「PGFinger」儲存資料夾不存在，請洽系統管理員")); // PGFinger storage folder not found, please contact system administrator.
                        return false;
                    }
                  //  MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("指紋機程序「PGFinger」儲存資料夾已創建。")); // PGFinger storage folder created successfully.
                }
                else
                {
                    MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("指紋機程序「PGFinger」儲存資料夾存在。")); // PGFinger storage folder exists.
                }
            }
            #endregion 儲存資料夾不存在

            #region 刪除FingerIn.txt (Delete FingerIn.txt if it exists)
            if (blResult)
            {
                MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("檢查是否有現存的 FingerIn.txt..."));
                if (File.Exists(_gOutFilePath + @"\FingerIn.txt"))
                {
                    try
                    {
                        File.Delete(_gOutFilePath + @"\FingerIn.txt");
                        MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("已刪除現存的 FingerIn.txt 文件。")); // Existing FingerIn.txt file deleted.
                    }
                    catch (Exception ex)
                    {
                        MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("無法刪除 FingerIn.txt 文件：" + ex.Message)); // Failed to delete FingerIn.txt file.
                        blResult = false;
                    }
                }
                else
                {
                    MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("FingerIn.txt 文件不存在，無需刪除。")); // No existing FingerIn.txt file to delete.
                }
            }
            #endregion 刪除FingerIn.txt

           #region 寫入HCM json
            if (blResult)
            {
                date = DateTime.Now.ToString("yyyy-MM-dd");

                MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Sending organization code to HCM..."));

                // Call send_org_code_hcm and check for success
                bool isSuccessful = await send_org_code_hcm("S000123");

                if (isSuccessful) // Proceed only if FingerIn.json was created successfully
                {
                    MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("FingerIn.json created successfully on desktop."));
                    blResult = true; // Set blResult to true, indicating success
                }
                else
                {
                    MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Failed to retrieve data from HCM. Aborting operation."));
                    blResult = false; // Set blResult to false if operation fails
                }
            }
            #endregion 寫入HCM json


            if (blResult)
            {
                MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("FingerIn.txt 文件已成功生成並匯出。")); // FingerIn.txt file successfully created and exported.
            }
            else
            {
                MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("FingerIn.txt 文件生成失敗，請洽系統管理員。")); // Failed to create FingerIn.txt, please contact system administrator.
            }

            return blResult;
        }



        //xavier okay  back to machine
        private async Task<bool> write_to_fingerprinter(MainPage page,JArray _JSONArray)
        {
            bool ret_val = false;

            try
            {
                 AppendTextToEditor("啟動指紋機程式");

                // Step 2: Prepare the execution command and parameters for PGFinger.exe
                string str_exec_cmd = Path.Combine(pglocation, "PGFinger.exe");
              
              
              //  AppendTextToEditor(str_exec_cmd);


                 if (!File.Exists(str_exec_cmd))
                {
                    AppendTextToEditor("PGFinger.exe 路徑無效");
                    return false;
                }


                string str_finger_in = Path.Combine(_gOutFilePath, "FingerIn.txt");
                string str_exec_parameter = @"2 " + str_finger_in;

                // prepare - _JSONArray: Rule1 result
                if (_JSONArray is JArray dataArray)
                {
                      /*
                        // 建立 StringBuilder 來儲存格式化的內容
                        StringBuilder sb = new StringBuilder();
                       
                        // 迭代每個員工資料並格式化成每行
                        foreach (var item in dataArray)
                        {
                            string empNo = item["empNo"]?.ToString();
                            string displayName = item["displayName"]?.ToString();
                            string cardNo   = item["cardNo"]?.ToString();
                            string finger1  = item["finger1"]?.ToString();
                            string finger2  = item["finger2"]?.ToString();
                            string addFlag  = item["addFlag"]?.ToString();

                            // 將每個屬性值格式化為 CSV 格式
                            sb.AppendLine($"{empNo},{displayName},{cardNo},{finger1},{finger2},{addFlag}");
                        }

                        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                        
                         
*/

                    // Initialize a list to store each line of CSV data
                    List<string> lines = new List<string>();

                    // Iterate over each employee data and format it as a line
                    foreach (var item in dataArray)
                    {
                        string empNo = item["empNo"]?.ToString();
                        string displayName = item["displayName"]?.ToString();
                        string cardNo   = item["cardNo"]?.ToString();
                        string finger1  = item["finger1"]?.ToString();
                        string finger2  = item["finger2"]?.ToString();
                        string addFlag  = item["addFlag"]?.ToString();

                        // Format each attribute value as CSV and add to list
                        lines.Add($"{empNo},{displayName},{cardNo},{finger1},{finger2},{addFlag}");
                    }

                    // Write all lines to file.dat with ASCII encoding
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                    // Write all lines to file with CP950 encoding (ANSI)
                    File.WriteAllText(str_finger_in, string.Join(Environment.NewLine, lines), Encoding.GetEncoding(950));

                    
                }
                else
                {
                    throw new InvalidDataException("無效的資料格式，找不到 'data' 陣列");
                }



             
                // Log the execution command
             
               
                if (!File.Exists(Path.Combine(_gOutFilePath, "FingerIn.txt")))
                {
                    AppendTextToEditor("FingerIn.txt 路徑無效");
                    return false;
                }


                // Step 3: Start the PGFinger.exe process with the necessary parameters

                // process.Start();
                ret_val = await ExecuteProcessAsync(str_exec_cmd, str_exec_parameter);
                // await process.WaitForExitAsync();
                if (!ret_val)
                {
                    AppendTextToEditor("Error: FingeIn.txt file was not generated by PGFinger.exe. Please contact the system administrator.");
                    throw new Exception("FingeIn.exe execution failed.");
                }

                while (!File.Exists(str_finger_in))
                {
                    await Task.Delay(1000); // Wait 10 second
                }

                // ret_val = process.ExitCode == 0;
                            
                // Step 4: Log the completion of the fingerprint device execution
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("指紋機執行結束"));



                // Step 5: Ensure backup directories exist and back up the FingerIn.txt file
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("準備備份 FingerIn.txt..."));

                string date = DateTime.Now.ToString("yyyy-MM-dd");
                page.getFilePath1(eHrToFingerData);  // Ensure backup directories are created

                string backupDir = Path.Combine(_gBackUpPath, @"員工指紋匯出資料\", eHrToFingerData.Replace("-", ""));
                string backupFile = Path.Combine(backupDir, $"PGFingerIn_{eHrToFingerData.Replace("-", "")}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                if (!Directory.Exists(backupDir))
                {
                    await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"備份目錄不存在，正在創建：{backupDir}"));
                    Directory.CreateDirectory(backupDir);
                }

                // Backup the FingerIn.txt file
                //  await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Backing up FingerIn.txt to: {backupFile}"));
                
                
                File.Copy(Path.Combine(_gOutFilePath, "FingerIn.txt"), backupFile, true);
                
                
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("備份卡鐘人事資料"));

                ret_val = true;
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Error during fingerprint process: {ex.Message}"));
            }

            return ret_val;
        }

        private async Task<bool> ExecuteProcessAsync(string exePath, string arguments)
        {
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException("Executable not found.", exePath);
            }

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = new Process { StartInfo = processStartInfo })
                {
                    process.Start();

                    // Asynchronously read the output and error streams
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();

                    await process.WaitForExitAsync(); // Wait for the process to exit

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"Process exited with code {process.ExitCode}. Error: {error}");
                    }

                    // Optionally, log the output for debugging
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Process output: {output}"));
                    }
                    return process.ExitCode == 0;
                }
                
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to execute {exePath}: {ex.Message}", ex);
            }
        }
        

         //xavier okay  machine download
        private async Task<bool> upload_fingerprint_to_HCM(MainPage page)
        {
            string date = "";
            bool blResult = true;
            List<string> content = new List<string>();

            string str_exec_cmd = Path.Combine(pglocation, "PGFinger.exe");
            string str_finger_out = Path.Combine(_gOutFilePath, "FingerOut.txt");

            // Step 1: Check if the required file paths exist



            // Step 2: Check if PGFinger.exe exists
            if (blResult)
            {
              

                 if (!File.Exists(str_exec_cmd))
                {
                    AppendTextToEditor("PGFinger.exe 路徑無效");
                    return false;
                }

            }

            #region 輸入檔案

 /* 測試時打開 quicktest */
            //輸入檔案以FingerOut.txt這個檔案為準
            //如果不要使用卡鐘所產生的檔案的話  測試時，這段要關閉
            //FingerOut.txt

            // Step 3: Delete existing FingeOut.txt if it exists
            if (blResult)
            {
                if (File.Exists(str_finger_out))
                {
                    try
                    {
                        File.Delete(str_finger_out);
                           }
                    catch
                    {
                         blResult = false;
                    }
                }
            }



            // Step 4: Generate fingerprint file using PGFinger.exe
            #region 使用廠商執行檔生成指紋檔案 (Execute PGFinger.exe to generate fingerprint file)
            if (blResult)
            {
                date = DateTime.Now.ToString("yyyy-MM-dd");
                bool result = false;
                try
                {
                    string str_exec_parameter = @"1 " + str_finger_out;  //
                   
                  
                    // Process.Start(str_exec_cmd, str_exec_parameter);
                    result = await ExecuteProcessAsync(str_exec_cmd, str_exec_parameter);
                    //await wait_for_devicecontrol_complete();
                    if (!result)
                    {
                        AppendTextToEditor("Error: FingeOut.txt file was not generated by PGFinger.exe. Please contact the system administrator.");
                        throw new Exception("PGFinger.exe execution failed.");
                    }

                    await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Completed reading fingerprint from the fingerprint machine."));
                }
                catch (Exception ex)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Error executing PGFinger.exe: {ex.Message}"));
                    blResult = false;
                    return false;
                }

                // Check if FingeOut.txt was created
                // int counter = 0;
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Checking for the FingeOut.txt file..."));

                while (!File.Exists(str_finger_out))
                {
                    await Task.Delay(1000); // Wait 10 second
                    // counter++;
                    // if (counter > 20)
                    // {
                    //     break;
                    // }
                }
            }
            #endregion 


            // Step 5: Verify that FingeOut.txt exists
            if (!File.Exists(str_finger_out))
            {
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Error: FingeOut.txt file was not generated by PGFinger.exe. Please contact the system administrator."));
                return false;
            }


/* 測試時打開 quicktest*/
            
             #endregion

            // Step 6: Read and validate FingeOut.txt content
            if (blResult)
            {
                try
                {

                   // 清空 punchMachineEmployeeJson 的 data 陣列，避免重複載入
                    punchMachineEmployeeJson["data"] = new JArray();

                    //--display
                  //  AppendTextToEditor(str_finger_out);


                    // Wait for the asynchronous method to complete, and get the file's content as bytes
                      /*  byte[] fileBytes = await File.ReadAllBytesAsync(str_finger_out);

                        // Decode using ASCII encoding
                        Encoding asciiEncoding = Encoding.ASCII;
                        string contentInAscii = asciiEncoding.GetString(fileBytes);

                        // Convert to UTF-8 encoding
                        byte[] utf8Bytes = Encoding.UTF8.GetBytes(contentInAscii);

                        // Use MemoryStream and StreamReader to read the UTF-8 content line by line
                        using (var memoryStream = new MemoryStream(utf8Bytes))
                        using (var reader = new StreamReader(memoryStream, Encoding.UTF8))
                        {
                            string line;
                            while ((line = await reader.ReadLineAsync()) != null)
                            {(*/


                         // 使用 CP950 讀取檔案的內容
        // 註冊支援的編碼提供者
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        
        //dotnet add package System.Text.Encoding.CodePages --version 8.0.0

        // 使用 CP950 讀取檔案的內容
        Encoding cp950 = Encoding.GetEncoding(950);
        byte[] fileBytes = File.ReadAllBytes(str_finger_out);
        
        // 將檔案內容轉為 CP950 字串
        string cp950String = cp950.GetString(fileBytes);
 // 將每一行分割
        var lines = cp950String.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
      
        foreach (var line in lines)
        {
            // 依據逗號切割每一行
            var values = line.Split(',');

            if (values.Length >= 6) // 確保行中有足夠的欄位
            {
                var employeeData = new JObject
                {
                    ["empNo"] = values[0].Trim(),
                    ["displayName"] = values[1].Trim(),
                    ["cardNo"] = values[2].Trim(),
                    ["finger1"] = values[3].Trim(),
                    ["finger2"] = values[4].Trim(),
                    ["addFlag"] = values[5].Trim()
                };

                // 將每個員工資料加入到 data 陣列中
                ((JArray)punchMachineEmployeeJson["data"]).Add(employeeData);
            }
        }
       
/*
       // Wait for the asynchronous method to complete, and get the file's content as bytes
byte[] fileBytes = await File.ReadAllBytesAsync(str_finger_out);
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
// 使用 CP950 编码解码字节
Encoding cp950Encoding = Encoding.GetEncoding(950);
string contentInCp950 = cp950Encoding.GetString(fileBytes);

// 将 CP950 编码内容转换为 UTF-8 编码
byte[] utf8Bytes = Encoding.UTF8.GetBytes(contentInCp950);

// 使用 MemoryStream 和 StreamReader 逐行读取 UTF-8 内容
using (var memoryStream = new MemoryStream(utf8Bytes))
using (var reader = new StreamReader(memoryStream, Encoding.UTF8))
{
    string line;
    while ((line = await reader.ReadLineAsync()) != null)
    {
 

                            
                            var values = line.Split(',');

                            if (values.Length >= 6) // 確保行中有足夠的欄位
                            {
                                var employeeData = new JObject
                                {
                                    ["empNo"] = values[0].Trim(),
                                    ["displayName"] = values[1].Trim(),
                                    ["cardNo"] = values[2].Trim(),
                                    ["finger1"] = values[3].Trim(),
                                    ["finger2"] = values[4].Trim(),
                                    ["addFlag"] = values[5].Trim()
                                };

                                // 將每個員工資料加入到 data 陣列中
                                ((JArray)punchMachineEmployeeJson["data"]).Add(employeeData);
                            }
                        }
}*/
    return true;


                }
                catch (Exception ex)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Error reading FingerOut.txt: {ex.Message}"));
                    blResult = false;
                }
            }

            // Step 7: Validate and process content
            if (blResult)
            {
                foreach (var data in content)
                {
                    var purged_data = data.Trim();
                    if (string.IsNullOrWhiteSpace(purged_data))
                        continue;

                    string[] detail = data.Split(',');
                    if (detail.Length < 5)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Error: Fingerprint data export format is incorrect. Please contact the system administrator."));
                        blResult = false;
                        break;
                    }
                }
            }

            // Step 8: Backup FingeOut.txt
            if (blResult)
            {
                page.getFilePath1(date);
                string backupPath = Path.Combine(_gBackUpPath, @"員工指紋匯出資料", date.Replace("-", ""), $"PGFingeOut{date.Replace("-", "")}_{DateTime.Now:HHmmss}.txt");
                File.Copy(Path.Combine(_gOutFilePath, "FingerOut.txt"), backupPath, true);
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Backup created for FingerOut.txt at: {backupPath}."));
            }

            return blResult;
        }

        private static void wait_for_devicecontrol_complete()
        {
            int wait_count = 50;
            int wait_interval = 100;
            Process p_device_control = null;
            string str_device_control = "DeviceControl";
            while (wait_count > 0)
            {
                Thread.Sleep(wait_interval);
                p_device_control = Process.GetProcessesByName(str_device_control).FirstOrDefault();
                if (p_device_control != null)
                    break;
                wait_count--;
            }
            if (p_device_control != null)
            {
                p_device_control.WaitForExit(30 * 60 * 1000);  // Wait for 30 minutes for process to exit
            }
        }

/*// del
        // Check if the necessary folder exists, create it if not
        private bool checkFilePath()
        {
            bool b_ret = false;
            try
            {
                if (!Directory.Exists(_gOutFilePath))
                    Directory.CreateDirectory(_gOutFilePath);
                b_ret = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Can not create {_gOutFilePath}");
            }
            return b_ret;
        }*/

        private bool create_out_folder()
        {
            bool ret_val = false;
            try
            {
                Directory.CreateDirectory(_gOutFilePath);
                ret_val = true;
            }
            catch(Exception ex)
            {
                Log.Error(ex, $"Can not create the folder:{_gOutFilePath}");
            }
            return ret_val;
        }

        private bool checkDownloadFilePath()
        {
            bool b_ret = false;
            try
            {
                if (!Directory.Exists(desktopPath))
                    Directory.CreateDirectory(desktopPath);
                b_ret = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Can not create {desktopPath}");
            }
            return b_ret;
        }

        private bool checkIsExisitExe()
        {
            return File.Exists(pglocation + @"\PGFinger.exe");
        }

        private bool checkIsExisitDownExe()
        {
            //return File.Exists("C:/Program Files/Timeset/download.exe");
            return File.Exists(desktopPath+@"\download.exe");
        }

        private void getFilePath1(string date)
        {
            if (!Directory.Exists(_gBackUpPath))
            {
                Directory.CreateDirectory(_gBackUpPath);
            }
            if (!Directory.Exists(_gBackUpPath +@"\員工指紋匯出資料"))
            {
                Directory.CreateDirectory(_gBackUpPath +@"\員工指紋匯出資料");
            }
            if (!Directory.Exists(_gBackUpPath +@"\員工指紋匯出資料\" + date.Replace("-","")))
            {
                Directory.CreateDirectory(_gBackUpPath +@"\員工指紋匯出資料\" + date.Replace("-", ""));
            }
        }

        private void getFilePath2()
        {
            if (!Directory.Exists(_gBackUpPath))
            {
                Directory.CreateDirectory(_gBackUpPath);
            }
            if (!Directory.Exists(_gBackUpPath +@"\考勤資料"))
            {
                Directory.CreateDirectory(_gBackUpPath +@"\考勤資料");
            }            
        }

        private async Task<dynamic> LoadHCMEmployeeDataAsync()
        {
            try
            {
                // string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                // var fileHCMPath = Path.Combine(desktopPath, "FingerData/FingerIn.json");

                string jsonContent = hcmEmployeeJsonData; // await File.ReadAllTextAsync(fileHCMPath)
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
            string outputFilePath = Path.Combine(desktopPath, "FingerData/fingerprint.json");

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



                // 使用 CP950 將字串轉換為位元組陣列
        Encoding cp950 = Encoding.GetEncoding("CP950");
        byte[] cp950Bytes = cp950.GetBytes(json);

        // 使用 ANSI 編碼（系統預設）將 CP950 字串轉換為 ANSI 格式
        Encoding ansi = Encoding.Default;
        byte[] ansiBytes = Encoding.Convert(cp950, ansi, cp950Bytes);

        // 將結果寫入檔案
        File.WriteAllBytes(outputFilePath, ansiBytes);

                // 將 JSON 寫入文件
              //  File.WriteAllText(outputFilePath, json);


                AppendTextToEditor("JSON file created successfully at: " + outputFilePath);
            }
            catch (Exception ex)
            {
                AppendTextToEditor("An error occurred: " + ex.Message);
            }
            return result;
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


        #region Rules
        private string GetFirst10Chars(string input)
        {
            // 如果是null或"0"或空白,返回空字符串
            if (string.IsNullOrWhiteSpace(input) || input.Trim() == "0")
            {
                return "";
            }

            // 去除首尾空白
            input = input.Trim();
            
            // 如果长度小于等于10,直接返回
            if (input.Length <= 10)
            {
                return input;
            }
            
            // 如果长度大于10,截取前10位
            return input.Substring(0, 10);
        }



        private string CompareCardNo(string card1, string card2)
        {
                
            // 比较前N位(N=compareLength)是否相同
            return GetFirst10Chars(card1) == GetFirst10Chars(card2)  ? "0" : "1";
        }


        // 讀取 test_files/ 中的兩個檔案，套用 rules2 並將結果顯示於 alert 中
        private JArray Punch_Data_Changing_rule2(JObject _hcmEmployeeData, JObject _punchCardData)
        {
            var results = new JArray(); // 用來儲存比較結果

            try
            {
                // 將 hcmEmployeeData 和 punchCardData 轉為 List<JObject>
                var hcmEmployeeDataList = _hcmEmployeeData["data"]?.ToObject<List<JObject>>() ?? new List<JObject>();
                var punchCardDataList = _punchCardData["data"]?.ToObject<List<JObject>>() ?? new List<JObject>();

                // 開始比較
                foreach (var hcmEmployee in hcmEmployeeDataList)
                {
                    foreach (var punchCard in punchCardDataList)
                    {
                        // 比對 EmpNo 是否相同
                        if (hcmEmployee["empNo"]?.ToString() == punchCard["empNo"]?.ToString())
                        {
                            string hcmEmpID = hcmEmployee["empNo"]?.ToString() ?? "";

                            string hcmCardNo = Convert.ToString(hcmEmployee["cardNo"]) ?? "";
                            string punchCardNo  = Convert.ToString(punchCard["cardNo"]) ?? "";
                        
                            string _cardNoFlag = "0";

                            if (punchCardNo.Length == 10 && !string.Equals(hcmCardNo, punchCardNo))
                            {
                                _cardNoFlag = "1";
                            }
                             
                            string hcmFinger2 = (Convert.ToString(hcmEmployee["finger2"]) ?? "").PadRight(10).Substring(0, 10);
                            string punchFinger2 = (Convert.ToString(punchCard["finger2"]) ?? "").PadRight(10).Substring(0, 10);
                            string _finger2Flag = "0";

                            if (punchFinger2.Length != 0 || !string.Equals(hcmFinger2, punchFinger2))
                            {
                                _finger2Flag = "1";
                            }

                            // string _finger2Flag  = CompareCardNo(hcmEmployee["finger2"]?.ToString(),punchCard["finger2"]?.ToString()); 
                            //  string _finger1Flag  = CompareCardNo(hcmEmployee["finger1"]?.ToString(),punchCard["finger1"]?.ToString()); 


                               
                            string hcmFinger1 = (Convert.ToString(hcmEmployee["finger1"]) ?? "").PadRight(10).Substring(0, 10);
                            string punchFinger1 = (Convert.ToString(punchCard["finger1"]) ?? "").PadRight(10).Substring(0, 10);
                            string _finger1Flag = "0";

                            if (punchFinger1.Length != 0 || !string.Equals(hcmFinger1, punchFinger1))
                            {
                                _finger1Flag = "1";
                            }


                            string _checkStr = _finger1Flag + _finger2Flag + _cardNoFlag;
                            if(!string.Equals(_checkStr, "000")) 
                            {
                                results.Add(punchCard);

                                AppendTextToEditor(hcmEmpID +":"+ _checkStr);

                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 例外處理：可以記錄錯誤訊息或處理其他的例外情況
                AppendTextToEditor($"Rule2 例外發生:: {ex.Message}");
                
            }

            // 直接回傳 JArray
            return results;
        }


       private JArray Punch_Data_Changing_rule1(JObject _hcmEmployeeData, JObject _punchCardData)
        {

            JArray dataArray = new JArray();
           

            try
            {
                // 將 hcmEmployeeData 和 punchCardData 轉為 List<JObject>
                var hcmEmployeeDataList = _hcmEmployeeData["data"]?.ToObject<List<JObject>>() ?? new List<JObject>();
                var punchCardDataList = _punchCardData["data"]?.ToObject<List<JObject>>() ?? new List<JObject>();

                // 建立 employeeMap，鍵為 empNo
                Dictionary<string, JObject> employeeMap = new Dictionary<string, JObject>();

                // 處理 hcmEmployeeData
                foreach (var hcmEmployee in hcmEmployeeDataList) {
                    string hcmKey = hcmEmployee["empNo"].ToString();
                    if(!employeeMap.ContainsKey(hcmKey))
                    { 
                        employeeMap[hcmKey] = hcmEmployee;
                    }
                }


                // 處理 punchCardData
                foreach (var punchcardEmployee in punchCardDataList) {
                    string cardKey = punchcardEmployee["empNo"].ToString();
                    punchcardEmployee["addFlag"] = "D";
                    punchcardEmployee.Remove("Status");
                    if(!employeeMap.ContainsKey(cardKey)) employeeMap[cardKey] = punchcardEmployee;
                } 


                // 將結果放入
                foreach (var entry in employeeMap.Values)
                {
                    dataArray.Add(entry);
                }

                
            }
            catch (Exception ex)
            {
                
                AppendTextToEditor($"Rule1 例外發生: {ex.Message}");
            }

            return dataArray;
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

/*
         private void show_info(string message)
        {
            // Display information message (e.g., in UI)
            textBox2.Text += $"{message}\n";
        }

        private void show_err(string message)
        {
            // Display error message
            textBox2.Text += $"Error: {message}\n";
        }

        private void show_info_2(string message)
        {
            textBox2.Text += $"{message}\n";
        }

        private void show_err_2(string message)
        {
            textBox2.Text += $"Error: {message}\n";
        }
*/
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