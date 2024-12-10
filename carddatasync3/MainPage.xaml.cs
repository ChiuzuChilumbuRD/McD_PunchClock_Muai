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
using CommunityToolkit.Maui;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

using System.Xml.Linq;

namespace carddatasync3
{
    //  public void update_card_reader_ip(string str_ip)
    //     {
    //         XmlDocument doc = new XmlDocument();
    //         doc.Load(str_config_path);
    //         string str_xpath = "/configuration/appSettings/add[@key='DeviceIP']";
    //         var node = doc.SelectSingleNode(str_xpath);
    //         XmlElement element = (XmlElement)node;
    //         element.SetAttribute("value", str_ip);
    //         Log.Debug($"Set branch:{str_branch_id} card reader IP:{str_ip}");
    //         doc.Save(str_config_path);
    //     } 獲取IP 目錄:C:\Program Files\timeset\DeviceControl\DeviceControl.exe.config
 


    public partial class MainPage : ContentPage
    {
        private string str_log_level = "debug"; //TODO: Need to change after program release.
        static string _gAPPath = AppContext.BaseDirectory;
        protected static string databaseKey = "GAIA.EHR";
        protected static string location = "";
        protected static string responseData    ="";

        // ======================================================================================
        //將備份目錄提出來變成Config
        protected static string _gBackUpPath    ="";

        // ======================================================================================
        //指紋匯出及下載的檔案產生目的地
        protected static string _gOutFilePath = "";
        protected static string test_org_code = ""; 
        protected static string machineIP = "";

        public static string eHrToFingerData = "";
        public static StringBuilder str_accumulated_log = new StringBuilder();
        private static int peopleCount = 0;

        // Lock to prevent concurrent UI operations
        private static SpinLock ui_sp = new SpinLock();
        private static string str_current_task = string.Empty;
        private bool is_init_completed = true;
        public static NameValueCollection AppSettings { get; private set; }
        private static string apiBaseUrl    ="";


        //測試的模式
        private static string mode  ="";

        // 卡鐘 Timeout 時間
        private static int second_punchMachine = 0;

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
        private static string deviceIP = "";

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
        // private async void OnToggleLogButtonClicked(object sender, EventArgs e)
        // {
        //     if (LogFrame.IsVisible)
        //     {
        //         await textBox2.FadeTo(0, 500); // Fade-out effect
        //         LogFrame.IsVisible = false;
        //     }
        //     else
        //     {
        //         LogFrame.IsVisible = true;
        //         await textBox2.FadeTo(1, 500); // Fade-in effect
        //     }
        // }

        private void StartClock()
        {
            Device.StartTimer(TimeSpan.FromSeconds(5), () =>
            {
                ClockLabel.Text = DateTime.Now.ToString("hh:mm tt");
                return true; // Repeat timer
            });
        }

        // private async void OnClockTapped(object sender, EventArgs e)
        // {

        //     double startClockWidth = isClockExpanded ? 8 : 0;
        //     double endClockWidth = isClockExpanded ? 0 : 8;

        //     double startLogWidth = isClockExpanded ? 2 : 10;
        //     double endLogWidth = isClockExpanded ? 10 : 2;

        //     // 定義動畫
        //     var animation = new Animation();

        //     // 動畫 ClockColumn 的寬度
        //     animation.Add(0, 1, new Animation(v => ClockColumn.Width = new GridLength(v, GridUnitType.Star), startClockWidth, endClockWidth));

        //     // 動畫 LogColumn 的寬度
        //     animation.Add(0, 1, new Animation(v => LogColumn.Width = new GridLength(v, GridUnitType.Star), startLogWidth, endLogWidth));

        //     // 運行動畫
        //     animation.Commit(this, "GridAnimation", 16, 500, Easing.Linear);
            
        //     isClockExpanded = !isClockExpanded;

        // }

        private async void OnActionButtonClicked(object sender, EventArgs e)
        {

            try
            {
                var rule1Txt = Path.Combine(Environment.CurrentDirectory, "rule1.txt");

                // 確認檔案是否存在
                if (!File.Exists(rule1Txt))
                {
                    Console.WriteLine("檔案不存在！");
                    return;
                }

                // 讀取檔案內容
                string fileContent = await File.ReadAllTextAsync(rule1Txt);

                // 將檔案內容轉換為 JArray
                JArray rule1Result = JArray.Parse(fileContent);

                bool writeToFingerprinterFlag = await write_to_fingerprinter(this,rule1Result); // FingerIn.txt
        
                if(!writeToFingerprinterFlag) {
                    await DisplayAlert("錯誤", "寫入打卡機數據時發生錯誤。", "OK");
                    // set_btns_state(true);
                    return;
                }
                else await DisplayAlert("成功", "打卡機員工數據寫入完成。", "OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        private async void OnSettingButtonClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new AppSettingPage());
        }

        private async void OnDownloadButtonClicked(object sender, EventArgs e)
        {
            string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), DateTime.Now.ToString("yyyyMMdd") + "_log.txt");

            if (string.IsNullOrEmpty(logFilePath) || !File.Exists(logFilePath))
            {
                throw new FileNotFoundException("來源檔案不存在或路徑為空！");
            }

            // 取得使用者下載目錄
            string downloadsFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

            // 確認下載目錄是否存在
            if (!Directory.Exists(downloadsFolderPath))
            {
                AppendTextToEditor("下載目錄不存在。");
                throw new DirectoryNotFoundException("下載目錄不存在！");
            }

            // 建立 log 檔案路徑
            string destinationFilePath = Path.Combine(downloadsFolderPath, Path.GetFileName(logFilePath));

            // 複製檔案
            File.Copy(logFilePath, destinationFilePath, overwrite: true);

            await DisplayAlert("成功", $"日誌已成功下載到：{destinationFilePath}", "OK");
        }

        #region Initialisation
        private async void OnRefreshButtonClicked(object sender, EventArgs e)
        {

            // string message = "這是一個 Toast 訊息!";
            // var toast = Toast.Make(message, CommunityToolkit.Maui.Core.ToastDuration.Short, 12);
            // await toast.Show();

  // 文件路徑
               

            InitializeApp();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            InitializeApp(); // 每次頁面出現時執行
        }
        private void buttonRecover()
        {
    // 设置按钮为可用状态
            refreshButton.IsEnabled = true;
            settingButton.IsEnabled = true;
        }


        private async Task InitializeApp()
        {

            
            bool is_init_completed = true;
            
            // ================================ Step.0 Lock Button ===============================================
            set_btns_state(false);
            refreshButton.IsEnabled = false;
            settingButton.IsEnabled = false;
            textBox1.IsEnabled = false;
            

            AppendTextToEditor("【初始化設定開始】"); // App initialization Start

            // ======================= Step.1 取得目前 APP 版本號並印出 Get Current APP Version =====================
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            AppendTextToEditor($"APP 版本: {version.ToString()}");

            // ======================= Step.2 匯入設定檔 Load Config (AppSettings.json) =======================
            // 讀取 appsettings.json 存入 AppSettings 中
            
            
            LoadAppSettings();
            LoadAppSettingsLayout.IsVisible = false;



            // ======================== Step.3 確認資料夾是否存在 Check Folder Exist or Not ====================
            // 確認 appsettings.json 中的所有資料夾是否存在 -> 不存在則回傳 false
            
            if (!CheckFilesExist(this))
            {
                AppendTextToEditor("資料夾不存在，將關閉程式。"); // Required file not found. 
                CheckFilesExistLayout.IsVisible = true;
                is_init_completed = false;
            }
            else {
                AppendTextToEditor("資料夾存在。");
                CheckFilesExistLayout.IsVisible = false;
            }

            string configFilePath = "C:\\Program Files\\Timeset\\DeviceControl\\DeviceControl.exe.config";

            // 確保文件存在
            if (!File.Exists(configFilePath))
            {
            
                AppendTextToEditor("配置文件不存在！");
                return;
            }

            try
            {
                    // 加載 XML 文件
                    XDocument configXml = XDocument.Load(configFilePath);

                    // 查詢 appSettings 節點下 key="DeviceIP" 的值
                    var deviceIPNode = configXml
                        .Descendants("appSettings")
                        .Elements("add")
                        .FirstOrDefault(x => (string)x.Attribute("key") == "DeviceIP");

                    if (deviceIPNode != null)
                    {
                        deviceIP = (string)deviceIPNode.Attribute("value");
                        AppendTextToEditor($"DeviceIP 的值是：{deviceIP}");
                    }
                    else
                    {
                        AppendTextToEditor("未找到 DeviceIP 節點！");
                    }
            }
            catch (Exception ex)
            {
                    AppendTextToEditor($"解析配置文件時發生錯誤：{ex.Message}");
                
            }

            hcmEmployeeJson = null;
          

            // =============== Step.4 確認網路連線 Check Internet Available ==================================
            
            if(CheckModeJSON("internetCheck").Equals("NO")){
                if (!IsInternetAvailable())
                {
                    AppendTextToEditor("網路連線失敗，將關閉程式。"); // No internet connection. Closing application.
                    IsInternetAvailableLayout.IsVisible = true;
                    is_init_completed = false;
                }
               else {
                    AppendTextToEditor("網路連線成功。");
                    IsInternetAvailableLayout.IsVisible = false;
               }
            }

            // ================== Step.5 取得目前電腦名稱 Get Current Computer Name(GetOrgCode) ===============

            if(CheckModeJSON("orgNameCheck").Equals("NO")){
                //將組織代碼代入
                getOrgCode();

                // 確認組織代碼名稱
                if (string.IsNullOrEmpty(textBox1.Text) ) //|| textBox1.Text.Length != 8
                {
                    AppendTextToEditor($"組織代碼 {textBox1.Text} 錯誤，請檢查機器名稱。");
                    GetOrgCodeLayout.IsVisible = true;
                    is_init_completed = false;
                }
                else {
                    //用POST取得
                    labStoreName.Text = await getORGName(textBox1.Text);  // old name getCradOrgName
                    // AppendTextToEditor($"result:{labStoreName.Text} test");
                    if (labStoreName.Text.Length == 0)
                    {
                        AppendTextToEditor("無法連線至 HCM Server，請確認 HCM 連線網址。");
                        IsHCMReadyLayout.IsVisible = true;
                        AppendTextToEditor($"找不到{textBox1.Text}組織名稱。");
                        GetOrgCodeLayout.IsVisible = true;
                        is_init_completed = false;
                    }
                    else if (labStoreName.Text.ToString().Equals("示範餐廳")) {
                        AppendTextToEditor($"找不到{textBox1.Text}組織名稱。");
                        IsHCMReadyLayout.IsVisible = false;
                        GetOrgCodeLayout.IsVisible = true;
                        is_init_completed = false;
                    }
                    else {
                        AppendTextToEditor($"組織名稱取得成功。");
                        IsHCMReadyLayout.IsVisible = false;
                        GetOrgCodeLayout.IsVisible = false;
                    }
                }
            }
            
            // ================= Step.6 確認打卡機就緒 Check Punch Machine Available ==================        
            if(CheckModeJSON("machineCheck").Equals("NO")){
                // Ping server IP address
                if (machineIP.Equals("")) {
                    machineIP = deviceIP;
                    AppendTextToEditor($"打卡機 IP 為空，使用裝置 IP {deviceIP}。");
                }
                if (!PingMachine(machineIP)) // Example IP, replace with the actual one
                {
                    AppendTextToEditor($"打卡機 {machineIP} 連線失敗，將關閉程式。"); // Unable to reach the Machine. 
                    PingMachineLayout.IsVisible = true;
                    is_init_completed = false;
                }
                else {
                    AppendTextToEditor($"打卡機 {machineIP} 已就緒。");
                    PingMachineLayout.IsVisible = false;
                }
            }

            // ====================== Step.7 傳送日誌 use POST Send the Log / => 準備就緒 Ready ========================            
            bool logSuccess = await setLog(textBox1.Text,"Inital Success\n");

            if (logSuccess)
            {
                AppendTextToEditor("日誌傳送成功。");
                SendLogLayout.IsVisible = false;
            } else{
                AppendTextToEditor("無法傳送日誌或儲存數據，將關閉程式。"); // Failed to send log or save data.
                SendLogLayout.IsVisible = true;
                is_init_completed = false;
            }

            // ====================== Step.8 Unlock Button​ + 初始化成功 App initialization completed =======================
            set_btns_state(true);
            refreshButton.IsEnabled = true;
            settingButton.IsEnabled = true;
            textBox1.IsEnabled = true;

            if(CheckModeJSON("iconCheck").Equals("YES")) {
                LoadAppSettingsLayout.IsVisible = true;
                CheckFilesExistLayout.IsVisible = true;
                IsInternetAvailableLayout.IsVisible = true;
                IsHCMReadyLayout.IsVisible = true;
                GetOrgCodeLayout.IsVisible = true;
                PingMachineLayout.IsVisible = true;
                SendLogLayout.IsVisible = true;
            }

            if(is_init_completed) AppendTextToEditor("\n【初始化設定結束】"); // App initialization completed.
            else {
                AppendTextToEditor("\n【初始化設定有誤，請重新修改設定項目】");
                set_btns_state(false);
            }
        }

        #endregion
        
        #region Placeholder Functions
        
        private void getOrgCode()
        {
            string m_name =  Environment.MachineName;

            AppendTextToEditor("主機名稱: " + m_name);
            AppendTextToEditor("測試用 ID: " + test_org_code);


            textBox1.Text = string.Empty; // 清空 Entry 控件的文本

            // if (m_name.Length >= 7 && CommonUtility.is_valid_org_code(m_name.Substring(2, 5))) {
            //     textBox1.Text = "S00" + m_name.Substring(2, 5); // 設置 Entry 的值
            //     AppendTextToEditor($"使用主機 {textBox1.Text} 名稱作為組織代碼。");
            // } 
            // else if(test_org_code.ToString().Length > 3) {
            //     AppendTextToEditor($"主機名稱 {m_name} 錯誤，使用測試用帳號 {test_org_code}。");
            //     textBox1.Text = "S00"+test_org_code.ToString();
            // } else {
            //     AppendTextToEditor($"抓取組織代碼 {textBox1.Text} 失敗。");
            //     textBox1.Text = string.Empty; // 清空 Entry
            // }
            if (test_org_code.ToString().Length > 3){

                AppendTextToEditor("使用測試用帳號。");
                
                textBox1.Text = "S00"+test_org_code.ToString();

            } else {
                if (m_name.Length < 7){
                    AppendTextToEditor($"主機名稱 {m_name} 錯誤。");
                    textBox1.Text = string.Empty; // 清空 Entry
                } else{
                    AppendTextToEditor($"使用主機 {textBox1.Text} 名稱作為組織代碼。");
                    string l_name = m_name.Substring(2, 5);
                    if (CommonUtility.is_valid_org_code(l_name))
                    {
                        textBox1.Text = "S00" + l_name; // 設置 Entry 的值
                    }
                }
            }
            // AppendTextToEditor($"tOrgCode: {textBox1.Text}"); // 使用 textBox1.Text 來取得值
        } // END getOrgCode


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
                    // 設定超時時間，例如 5 秒
                    client.Timeout = TimeSpan.FromSeconds(5);

                    // 建立要傳遞的 JSON 資料，使用字串插值來替換 org_No 和 logData 的值
                    var requestData = new
                    {
                        ctrler = "STD1FORME00501",
                        method = "PSNVersion",
                        jsonParam = $@"{{
                            ""org_No"": ""{_orgCode}""
                        }}",

                        token = "your-token-here"
                    };


                    // 將資料序列化為 JSON 字串
                    string json = JsonConvert.SerializeObject(requestData);


                    // 設定請求內容
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var _sync = Path.Combine(Environment.CurrentDirectory, "PSNVersionRequest.txt");
                    await File.WriteAllTextAsync(_sync, json.ToString());

                    // 發送 POST 請求
                    var response = await client.PostAsync(AppSettings["serverInfo"] + "/GuruOutbound/Trans", content);

                    // 確認回應成功
                    if (response.IsSuccessStatusCode)
                    {
                        // 讀取並返回回應內容
                        string _str = await response.Content.ReadAsStringAsync();

                         _sync = Path.Combine(Environment.CurrentDirectory, "PSNVersionResponse.txt");
                        await File.WriteAllTextAsync(_sync, _str.ToString());

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
                        buttonRecover();
                        return "";
                    }
                }
            } 
            catch (HttpRequestException ex)
            {
                AppendTextToEditor($"Failed to connect to the API: {ex.Message}");

               // throw new Exception($"Failed to connect to the API: {ex.Message}");;
                return "";
           
            }
            catch (TaskCanceledException ex)
            {
            // 超時或請求被取消的錯誤
                buttonRecover();
                AppendTextToEditor("Request timed out. Please try again.");         

                //throw new Exception($"Request timed out. Please try again. {ex.Message}");
                return "";
             
            }
    
 
        } // END getCradORGName



        private async Task<bool> setLog(string _orgCode,string _logData)
        {
            AppendTextToEditor($"正在寫入日誌...");
            _logData = $"組織代碼 {_orgCode}: Inital Success\n";
            try
            {
                

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    // 建立要傳遞的 JSON 資料，使用字串插值來替換 org_No 和 logData 的值
                    var requestData = new
                    {
                        ctrler = "STD1FORME00501",
                        method = "PSNLog",
                        jsonParam = "{'logData': '" + _logData + "'}",
                        token = "your-token-here"
                    };

                    // 方法2：使用 JsonSerializerSettings
                    string json = JsonConvert.SerializeObject(requestData, new JsonSerializerSettings 
                    { 
                        Formatting = Formatting.None 
                    });
                    
                    AppendTextToEditor($"PSNLog POST 請求資訊內容: {_logData}");

                    // 設定請求內容
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    // 發送 POST 請求
                    var response = await client.PostAsync(AppSettings["serverInfo"] + "/GuruOutbound/Trans", content);

                    // AppendTextToEditor(AppSettings["serverInfo"] + "/GuruOutbound/Trans");
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
                        // AppendTextToEditor($"rootElement: {rootElement}");
                        
                        // 因為是陣列，所以先獲取第一個元素
                        var firstElement = rootElement[0]; // rootElement[0]
                        
                        // 獲取 flag 值
                        var flag = firstElement.GetProperty("flag").GetBoolean();
                        
                        // 記錄返回的訊息
                        var _resultStr = firstElement.GetProperty("data").GetString();
                        // AppendTextToEditor($"PSNLog POST 回應資訊 (data): {_resultStr}");
                        
                        // 根據 flag 值返回結果
                        return flag;
                    }
                    else
                    {   
                        buttonRecover();
                        AppendTextToEditor($"無法從 API 獲取數據: {response.StatusCode}");
                        
                        return false;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                AppendTextToEditor($"無法連接到 API: {ex.Message}");
                return false;
            }
            catch (TaskCanceledException ex)
            {
            // 超時或請求被取消的錯誤
                buttonRecover();
                AppendTextToEditor("Request timed out. Please try again.");         
                return false;
                

                
            }



        } //setLog END


        private async Task<bool> setModify(JArray _chgList)
        {
            AppendTextToEditor("正在更新 HCM 資料...");
            const int batchSize = 200; // 每次上傳的批次大小
            
            try
            {
                
               
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    // 將資料分割成每 10 筆為一批
                    for (int i = 0; i < _chgList.Count; i += batchSize)
                    {
                        // 取得當前批次的資料
                        var batch = new JArray(_chgList.Skip(i).Take(batchSize));
                        string batchStr = batch.ToString().Replace("\n", "").Replace("\r", "").Replace("  ", "");
                        batchStr = " {0:"+batchStr+"}";
                        // 準備要傳遞的 JSON 資料
                        var requestData = new
                        {
                            ctrler = "STD1FORME00501",
                            method = "PSNModify",
                            jsonParam = batchStr,
                            token = "your-token-here"
                        };

                        // 將 requestData 轉為 JSON 字串
                        // 將資料序列化為 JSON 字串
                        string json = JsonConvert.SerializeObject(requestData);

                        // 設定請求內容
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        
                        // -display 印出即將發送的請求內容
        

                            var modify2RequestTxt = Path.Combine(Environment.CurrentDirectory, "PSNModify2Request.txt");
                                 // Write the response data to a JSON file
                            await File.WriteAllTextAsync(modify2RequestTxt, json.ToString());

                            //AppendTextToEditor($"PSNModify POST 請求資訊 (json): {json}");



                            // 發送 POST 請求
                            var response = await client.PostAsync(AppSettings["serverInfo"] + "/GuruOutbound/Trans", content);

                            // 確認回應成功
                            if (response.IsSuccessStatusCode)
                            {
                                // 讀取並返回回應內容
                                string _str = await response.Content.ReadAsStringAsync();
                                // 反序列化 JSON 並提取 orgName
                                var jsonDocument = JsonDocument.Parse(_str);
                                var rootElement  = jsonDocument.RootElement;
                                var firstElement = rootElement[0];

                                var flag = firstElement.GetProperty("flag").GetBoolean();
                                var _resultStr1 = firstElement.GetProperty("data")  // 進入 "data" 屬性
                                                    .GetString();  // 提取 orgName 的值

                                //AppendTextToEditor($"PSNModify POST 回應資訊 (json): {_resultStr}");

                                var modify2ResponseTxt = Path.Combine(Environment.CurrentDirectory, "PSNModify2Response.txt");
                                 // Write the response data to a JSON file
                                await File.WriteAllTextAsync(modify2ResponseTxt, _resultStr1.ToString());

                                return flag;
                            }
                            else
                            {   
                                buttonRecover();

                                AppendTextToEditor($"無法從 API 獲取數據: {response.StatusCode}");
                                        
                                return false;
                            }
                    }//10 records
                }
                
                return true; // 成功完成所有批次
            }
            catch (HttpRequestException ex)
            {
                AppendTextToEditor($"setModify Exception: {ex.Message}");

                return false;
           
            }
            catch (TaskCanceledException ex)
            {
            // 超時或請求被取消的錯誤
                buttonRecover();
                AppendTextToEditor($"setModify Exception: {ex.Message}");
                AppendTextToEditor("Request timed out. Please try again.");         

                return false;

                
            }

        }//setModify END

       
        private async Task<bool> getPSNSync(string _orgCode)
        {

            // 記錄開始時間
            Stopwatch stopwatch = Stopwatch.StartNew();

            AppendTextToEditor("正在取得 HCM 員工數據...組織代碼：" + _orgCode);

            try
            {
                
            
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
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

                    var _sync = Path.Combine(Environment.CurrentDirectory, "PSNSyncRequest.txt");
                    await File.WriteAllTextAsync(_sync, json.ToString());


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
                        var fileHCMPath = Path.Combine(fingerDataPath, "PSNSyncResponseFingerIn.txt");

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
                        AppendTextToEditor($"HCM資料已成功載入 FingerIn.json。花費時間：{timeSpent.TotalMilliseconds / 1000} 秒");
                        
                        return true;
                    }
                    else
                    {
                        buttonRecover();
                        AppendTextToEditor($"無法從 API 獲取數據: {response.StatusCode}");
                                
                        return false;
                    }
                }
            
            } 
            catch (HttpRequestException ex)
            {
                AppendTextToEditor($"無法連接到 API: {ex.Message}");
                return false;
                
            }
            catch (TaskCanceledException ex)
            {
            // 超時或請求被取消的錯誤
                
                buttonRecover();
                AppendTextToEditor("Request timed out. Please try again.");         
                return false;
            }
        } //getPSNSync END

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
                    AppendTextToEditor("讀取 appsettings.json 時發生錯誤: " + ex.Message);
                    LoadAppSettingsLayout.IsVisible = true;
                }
            }
            else
            {
                AppendTextToEditor("找不到 appsettings.json 文件。");
                LoadAppSettingsLayout.IsVisible = true;
            }
            

           
            location    = AppSettings["location"];
            _gBackUpPath    = AppSettings["BackUpPath"];
            _gOutFilePath   = AppSettings["fileOutPath"];
            test_org_code   = AppSettings["test_org_code"];

            machineIP       = AppSettings["machineIP"];
            apiBaseUrl      = AppSettings["serverInfo"] + "/GuruOutbound/Trans?ctrler=Std1forme00501&method=PSNSync&jsonParam=";
    
            mode            = AppSettings["mode"];
            second_punchMachine = Int32.Parse(AppSettings["second_punchMachine"]);
            

            // 將 JSON 字串解析為 Dictionary<string, object>
            modeJSON = JsonConvert.DeserializeObject<Dictionary<string, object>>(mode);

            // 存取 key 的值
            // AppendTextToEditor(CheckModeJSON("debugFileCheck")); // 輸出 "value"
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

            var Location        = AppSettings["location"];
            var fileOutPath     = AppSettings["fileOutPath"];
            var BackUpPath      = AppSettings["BackUpPath"];

            // Check if the directories exist
            if (!Directory.Exists(Location))
            {
                AppendTextToEditor($"上傳文件/指紋位置資料夾不存在: {Location}"); // The download folder does not exist
                return false;
            }

            if (!Directory.Exists(fileOutPath))
            {
                AppendTextToEditor($"輸出文件路徑不存在: {fileOutPath}"); // The BackupPath folder does not exist
                return false;
            }

            if (!Directory.Exists(BackUpPath))
            {
                AppendTextToEditor($"備份路徑不存在: {BackUpPath}"); // The Backup folder does not exist
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

        // 確定是否能夠 ping machine
        private bool PingMachine(string ipAddress)
        {
            AppendTextToEditor($"正在 Ping 指紋機 {ipAddress}...");

            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send(ipAddress);

                    if (reply.Status == IPStatus.Success)
                    {
                        AppendTextToEditor($"位於 {ipAddress} 的指紋機可連線。回應時間：{reply.RoundtripTime} 毫秒。");
                        return true;
                    }
                    else
                    {
                        AppendTextToEditor($"無法連接到位於 {ipAddress} 的指紋機。狀態：{reply.Status}。");
                        return false;
                    }
                }
            }
            catch (PingException ex)
            {
                AppendTextToEditor($"指紋機 ping 失敗: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                AppendTextToEditor($"發生了意外錯誤: {ex.Message}");
                return false;
            }
        } // END PingMachine

        #endregion


        // Helper function to append text to textBox2
        // 寫 log 到 TextEditor 中
        private void AppendTextToEditor(string text)
        {
                int maxTextLength = 300; // 設定 textBox2.Text 的最大長度

                if (textBox2 != null)
                {
                    // 添加新文本
                    textBox2.Text = textBox2.Text + $"{text}\n"  ;

                    textBox2.CursorPosition = textBox2.Text.Length;
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
            refreshButton.IsEnabled = false;
            settingButton.IsEnabled = false;
            textBox1.IsEnabled = false;

            // Step 1: getPSNSync
            AppendTextToEditor("【人事同步開始】");

            bool hcmResult = await getPSNSync(textBox1.Text);

            if (!hcmResult)
            {
                await DisplayAlert("錯誤", "PSNSync 執行失敗。", "OK");

                set_btns_state(true);
                refreshButton.IsEnabled = true;
                settingButton.IsEnabled = true;
                textBox1.IsEnabled = true;
                return;
            }
                    
            // Step 2: Load HCM Employee Data
                
            if (hcmEmployeeJson == null)
            {
                await DisplayAlert("錯誤", "無法加載 HCM 員工 JSON 數據 hcmEmployeeJson。", "OK");
                
                set_btns_state(true);
                refreshButton.IsEnabled = true;
                settingButton.IsEnabled = true;
                textBox1.IsEnabled = true;
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
                
                    await DisplayAlert("錯誤", "HCM 員工數據 dataArray 為空。", "OK");
                    
                    set_btns_state(true);
                    refreshButton.IsEnabled = true;
                    settingButton.IsEnabled = true;
                    textBox1.IsEnabled = true;
                    return;

                }
            }
            AppendTextToEditor("成功取得 HCM 員工數據。");

            // Step 3: Read Punch Card Data => punchMachineEmployeeJson
            bool getPunchMachineEmployeeJson = await upload_fingerprint_to_HCM(this); // FingerOut.txt

            if(!getPunchMachineEmployeeJson){

                await DisplayAlert("錯誤", "無法讀取打卡機員工 JSON 數據。", "OK");
                    
                set_btns_state(true);
                refreshButton.IsEnabled = true;
                settingButton.IsEnabled = true;
                textBox1.IsEnabled = true;
                return;
            }

            AppendTextToEditor("成功取得卡機員工數據。");

            // Step 4: Compare Employee Data (Rule 1)
            //--display
            // AppendTextToEditor($"Original punchMachineEmployeeJson: {punchMachineEmployeeJson.ToString()}");
            // AppendTextToEditor($"Original hcmEmployeeJson: {hcmEmployeeJson.ToString()}");
            
            JArray rule1Result = Punch_Data_Changing_rule1(hcmEmployeeJson, punchMachineEmployeeJson);
            // AppendTextToEditor("----------");
            // AppendTextToEditor($"Rule1 套用後結果: {rule1Result.ToString()}");

            if (rule1Result.Count == 0)
            {
                await DisplayAlert("錯誤", "Rule1 套用結果為空。", "OK");
                
                set_btns_state(true);
                refreshButton.IsEnabled = true;
                settingButton.IsEnabled = true;
                textBox1.IsEnabled = true;
                return;
            }
            AppendTextToEditor("員工資訊更新完成 (Rule1)。");

            var rule1Txt = Path.Combine(Environment.CurrentDirectory, "rule1.txt");

            await File.WriteAllTextAsync(rule1Txt, rule1Result.ToString());

            // Step 5: Compare Fingerprint Data (Rule 2)
            //JArray rule2Result = Punch_Data_Changing_rule2(hcmEmployeeJson, punchMachineEmployeeJson);

            JArray rule2Result = Punch_Data_Changing_rule21(rule1Result);

            // AppendTextToEditor($"After punchMachineEmployeeJson: {punchMachineEmployeeJson.ToString()}");
            // AppendTextToEditor($"After hcmEmployeeJson: {hcmEmployeeJson.ToString()}");
            //AppendTextToEditor("----------");
           // AppendTextToEditor($"Rule2 套用後結果: {rule2Result.ToString()}");

            var rule2Txt = Path.Combine(Environment.CurrentDirectory, "rule2.txt");
            // Write the response data to a JSON file
            await File.WriteAllTextAsync(rule2Txt, rule2Result.ToString());


            AppendTextToEditor("員工資訊更新完成 (Rule2)。");
            
            
            // Step 6: Write Data to PunchMachine
            bool writeToFingerprinterFlag = await write_to_fingerprinter(this,rule1Result); // FingerIn.txt

            if(!writeToFingerprinterFlag) {
                await DisplayAlert("錯誤", "寫入打卡機數據時發生錯誤。", "OK");
                set_btns_state(true);
                refreshButton.IsEnabled = true;
                settingButton.IsEnabled = true;
                textBox1.IsEnabled = true;
                return;
            }
            AppendTextToEditor("打卡機員工數據寫入完成。");

            // Step 7: Updated Employee be changed Data
            bool updateSuccess = await setModify(rule2Result);

            if (updateSuccess)
            {
                AppendTextToEditor("HCM 資料更新成功。");
            }
            else {
                AppendTextToEditor("無法更新 HCM 資料。");
                set_btns_state(true);
                refreshButton.IsEnabled = true;
                settingButton.IsEnabled = true;
                textBox1.IsEnabled = true;
                return;
            }

            // Step 8: Log the Sync Process
            bool logSuccess = await setLog(textBox1.Text,"人事同步完成");

            if (logSuccess)
            {
                //人事同步完成
                AppendTextToEditor("【日誌寫入成功，人事同步完成。】");
                set_btns_state(true);
                refreshButton.IsEnabled = true;
                settingButton.IsEnabled = true;
                textBox1.IsEnabled = true;
                return;

            } else {
                AppendTextToEditor("【無法傳送日誌或儲存數據，人事同步失敗。】");
                set_btns_state(true);
                refreshButton.IsEnabled = true;
                settingButton.IsEnabled = true;
                textBox1.IsEnabled = true;
                return;
            }

        } // btn_HCM_to_fingerprint END

        //xavier okay  back to machine
        private async Task<bool> write_to_fingerprinter(MainPage page,JArray _JSONArray)
        {
            bool ret_val = false;

            try
            {
                AppendTextToEditor("正在寫入打卡機員工數據...");

                // Step 2: Prepare the execution command and parameters for PGFinger.exe
                string str_exec_cmd = Path.Combine(location, "PGFinger.exe");

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

                //  Process.Start(str_exec_cmd, str_exec_parameter); //20241126
              // ret_val = await ExecuteProcessAsync(str_exec_cmd, str_exec_parameter, second_punchMachine);
              //20241126
              

                    // 建立外部程序處理
                    Process process = new Process();
                    process.StartInfo.FileName = str_exec_cmd;  // 外部程式路徑
                    process.StartInfo.Arguments = str_exec_parameter;  // 傳入的參數
                    process.StartInfo.CreateNoWindow = true;  // 不顯示命令窗口
                    process.StartInfo.UseShellExecute = false;  // 使用Shell啟動

                    // 啟動程序
                    process.Start();

                    // 等待程序結束
                    await Task.Run(() => process.WaitForExit());  // 非同步等待外部程式結束
                

/*
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
                            */

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

        private async Task<bool> ExecuteProcessAsync(string exePath, string arguments, int second)
        {
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException("Executable not found.", exePath);
            }

            try
            {
                string str_finger_out = Path.Combine(_gOutFilePath, "FingerOut.txt");

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

                    await process.WaitForExitAsync(); // 等待進程結束
                    int counter = 0;
                    while (!File.Exists(str_finger_out))
                    {
                        await Task.Delay(1000); // Wait 1 second
                        counter++;
                        if (counter > second)
                        {
                            break;
                        }
                    }

                    if (!File.Exists(str_finger_out)) // 如果是超時完成
                    {
                        // AppendTextToEditor("process.Kill()");
                        process.Kill(); // 強制中止進程

                        await MainThread.InvokeOnMainThreadAsync(() =>
                            DisplayAlert("Timeout", $"處理程序在 {second} 秒後超時。", "OK"));

                        return false;
                    }

                    // await process.WaitForExitAsync(); // Wait for the process to exit

                    // Asynchronously read the output and error streams
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();

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
            AppendTextToEditor("正在取得卡機員工數據...");
            string date = "";
            bool blResult = true;
            List<string> content = new List<string>();

            string str_exec_cmd = Path.Combine(location, "PGFinger.exe");
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

            if(CheckModeJSON("FingerOutFlag").Length>0){
                AppendTextToEditor("FingerOut檢查關閉");
            }else{
                AppendTextToEditor("FingerOut檢查開啟");
 /* 測試時打開 quicktest  */

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
                   
                    //  Process.Start(str_exec_cmd, str_exec_parameter);
                    // result = await ExecuteProcessAsync(str_exec_cmd, str_exec_parameter, second_punchMachine);
                   
                    // 启动外部进程并等待其完成
                    Process process = new Process();
                    process.StartInfo.FileName = str_exec_cmd;  // 要执行的程序路径
                    process.StartInfo.Arguments = str_exec_parameter;  // 参数
                    process.Start();  // 启动外部进程

                    // 等待外部进程执行完成
                    process.WaitForExit();

                    // 如果需要，您可以检查外部进程的退出代码
                    if (process.ExitCode == 0)
                    {

                        
                            int counter = 0;
                            while (!File.Exists(str_finger_out))
                            {
                                // 每 30 秒顯示一次過去的分鐘數
                                if (counter % 30 == 0 && counter > 0)  // counter > 0 確保不在第一次進入迴圈時顯示
                                {
                                    double minutesPassed = counter / 60.0;  // 計算已經過的分鐘，使用浮點數來保留小數
                                    AppendTextToEditor($"卡鐘尚在產生FingerOut.TXT資料...  {minutesPassed:F1} 分鐘");
                                }

                                await Task.Delay(1000);  // 每秒延遲 1 秒
                                counter++;
                            }

                            // 檔案找到後顯示成功訊息
                            if (File.Exists(str_finger_out))
                            {
                                AppendTextToEditor("成功  產生 FingerOut.txt 文件。");
                            }else{

                                AppendTextToEditor("失敗  產生 FingerOut.txt 文件。");
                            }

                    }
                }
                catch (Exception ex)
                {
                     AppendTextToEditor($"執行 PGFinger.exe 時發生錯誤：{ex.Message}");
                    blResult = false;
                    return false;
                }
            }
            #endregion 


            }//FingerOutFlag END

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

                        if (values.Length >= 5) // 確保行中有足夠的欄位
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
                    return true;


                }
                catch (Exception ex)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"讀取 FingerOut.txt 時發生錯誤：{ex.Message}"));
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
                        await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("錯誤：指紋數據導出格式不正確。請聯繫系統管理員。"));
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
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"已為 FingerOut.txt 創建備份，備份路徑：{backupPath}。"));
            }

            return blResult;
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
        #endregion


        #region Rules
        // private string GetFirst10Chars(string input)
        // {
        //     // 如果是null或"0"或空白,返回空字符串
        //     if (string.IsNullOrWhiteSpace(input) || input.Trim() == "0")
        //     {
        //         return "";
        //     }

        //     // 去除首尾空白
        //     input = input.Trim();
            
        //     // 如果长度小于等于10,直接返回
        //     if (input.Length <= 10)
        //     {
        //         return input;
        //     }
            
        //     // 如果长度大于10,截取前10位
        //     return input.Substring(0, 10);
        // }



        // private string CompareCardNo(string card1, string card2)
        // {
                
        //     // 比较前N位(N=compareLength)是否相同
        //     return GetFirst10Chars(card1) == GetFirst10Chars(card2)  ? "0" : "1";
        // }

        private JArray Punch_Data_Changing_rule21(JArray _source)
        {
            JArray results = new JArray();
            try{

                int countTotal = 0;

                foreach (var entry in _source)
                {
                    string hcmEmpID = entry["empNo"]?.ToString() ?? "";
                    // string _checkStr = entry["compare"].ToString()?? "";
                    string _checkStr = entry["compare"] != null ? entry["compare"].ToString() : "";
                   
                    if(_checkStr !="000" && _checkStr != "" ){
                        AppendTextToEditor(hcmEmpID +":"+ _checkStr);



                        results.Add(entry);
                    } else{
                        countTotal++;
                    }
                }
                
                int nonZeroCheckStrCount = results.Count; // 不是 "000" 的筆數（已經在results中過濾）

                // 顯示結果
                AppendTextToEditor($"🕒➡️💻  需更新筆數: {nonZeroCheckStrCount}, 相同筆數: {countTotal}");
                
            }
            
            catch (Exception ex)
            {
                // 例外處理：可以記錄錯誤訊息或處理其他的例外情況
                AppendTextToEditor($"Rule2 例外發生: {ex.Message}");
                
            }
            return results;
        }


        //rule2 should del
        // 讀取 test_files/ 中的兩個檔案，套用 rules2 並將結果顯示於 alert 中
        private JArray Punch_Data_Changing_rule2(JObject _hcmEmployeeData, JObject _punchCardData)
        {
            AppendTextToEditor("開始更新員工資訊 (Rule2) ...");

            var results = new JArray(); // 用來儲存比較結果
        

        // Helper function to handle the conversion logic
            string ConvertToValidString(object value)
            {
                string strValue = Convert.ToString(value) ?? "";  // Convert to string or empty if null
                strValue = strValue.Trim();  // Trim any leading/trailing spaces

                // If the string length is less than 3, treat it as empty
                if (strValue.Length < 3)
                {
                    return "";
                }

                // Otherwise, pad the string to 10 characters and return the first 10 characters
                return strValue.PadRight(10).Substring(0, 10);
            }

            
            int countTotal = 0;
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

                          
                           
      // Ensure both values are treated as strings, default to empty string if null
                            string hcmCardNo = ConvertToValidString(hcmEmployee["cardNo"]);
                            string punchCardNo = ConvertToValidString(punchCard["cardNo"]);

                            string _cardNoFlag = "B";

                            // Example to handle null or empty strings explicitly
                            if (string.IsNullOrEmpty(hcmCardNo) && string.IsNullOrEmpty(punchCardNo))
                            {
                                // Both are empty or null, consider them equal
                                _cardNoFlag = "B";  // No flag set, or some other logic
                            }
                            else if (string.IsNullOrWhiteSpace(hcmCardNo) && !string.IsNullOrWhiteSpace(punchCardNo) )
                            {
                                _cardNoFlag = "A"; // This will be executed, because one is empty and the other is non-empty.
                            }
                             else if (!string.IsNullOrWhiteSpace(hcmCardNo) && !string.IsNullOrWhiteSpace(punchCardNo) &&  !string.Equals(hcmCardNo, punchCardNo))
                            {
                                _cardNoFlag = "A"; // This will be executed, because one is empty and the other is non-empty.
                            }
                             
                            // Ensure both values are treated as strings, default to empty string if null
                            string hcmFinger2 = ConvertToValidString(hcmEmployee["finger2"]);
                            string punchFinger2 = ConvertToValidString(punchCard["finger2"]);

                            // Initialize _finger1Flag to "0" by default
                            string _finger2Flag = "B";

                            // Example to handle null or empty strings explicitly
                            if (string.IsNullOrEmpty(hcmFinger2) && string.IsNullOrEmpty(punchFinger2))
                            {
                                // Both are empty or null, consider them equal
                                _finger2Flag = "B";  // No flag set, or some other logic
                            }
                            else if (string.IsNullOrWhiteSpace(hcmFinger2) || string.IsNullOrWhiteSpace(punchFinger2) || !string.Equals(hcmFinger2, punchFinger2))
                            {
                                _finger2Flag = "A"; // This will be executed, because one is empty and the other is non-empty.
                            }

                               
                            // Ensure both values are treated as strings, default to empty string if null
                            string hcmFinger1 = ConvertToValidString(hcmEmployee["finger1"]);
                            string punchFinger1 = ConvertToValidString(punchCard["finger1"]);
                            // Initialize _finger1Flag to "0" by default
                            string _finger1Flag = "B";

                            // Compare the values, if they are different set _finger1Flag to "1"
                            // Example to handle null or empty strings explicitly
                            if (string.IsNullOrEmpty(hcmFinger1) && string.IsNullOrEmpty(punchFinger1))
                            {
                                // Both are empty or null, consider them equal
                                _finger1Flag = "B";  // No flag set, or some other logic
                            }
                            else if (string.IsNullOrWhiteSpace(hcmFinger1) || string.IsNullOrWhiteSpace(punchFinger1) || !string.Equals(hcmFinger1, punchFinger1))
                            {
                                _finger1Flag = "A"; // This will be executed, because one is empty and the other is non-empty.
                            }


                            string _checkStr = _finger1Flag +"" + _finger2Flag +""+ _cardNoFlag;

                        //punchCard["checkStr"] = _checkStr;
                            
                          if (_checkStr.Trim() !=  "BBB")
                            {
                                AppendTextToEditor(hcmEmpID +":"+ _checkStr);

                                results.Add(punchCard);
                                
                                
                            }else{
                                countTotal++;
                            }
                            // 計算總筆數和不是 "000" 的筆數
                        }
                            
                    }
                }


                    


            }
            catch (Exception ex)
            {
                // 例外處理：可以記錄錯誤訊息或處理其他的例外情況
                AppendTextToEditor($"Rule2 例外發生:: {ex.Message}");
                
            }


            int nonZeroCheckStrCount = results.Count; // 不是 "000" 的筆數（已經在results中過濾）

                        // 顯示結果
            AppendTextToEditor($"需更新筆數: {nonZeroCheckStrCount}, 相同筆數: {countTotal}");
            
            // 直接回傳 JArray
            return results;
        }


        private JArray Punch_Data_Changing_rule1(JObject _hcmEmployeeData, JObject _punchCardData)
        {
            AppendTextToEditor("開始更新員工資訊 (Rule1) ...");

            string ConvertToValidString(object value)
            {

                string strValue = Convert.ToString(value) ?? "";  // Convert to string or empty if null
                
                strValue = strValue.Trim();
                
                 // If the value is "0", return an empty string
                if (strValue == "0")
                {
                    return "";
                }
                
                  // Trim any leading/trailing spaces

                // If the string length is less than 3, treat it as empty
                if (strValue.Length < 3)
                {
                    return "";
                }

                strValue = strValue.PadRight(10).Substring(0, 10);

                strValue = strValue.Trim();

                // Otherwise, pad the string to 10 characters and return the first 10 characters
                return strValue;
            }



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

                    hcmEmployee["hcm_cardNoVal"]= ConvertToValidString(hcmEmployee["cardNo"].ToString());
                    hcmEmployee["hcm_finger1Val"]= ConvertToValidString(hcmEmployee["finger1"].ToString());
                    hcmEmployee["hcm_finger2Val"]= ConvertToValidString(hcmEmployee["finger2"].ToString());

                    if(!employeeMap.ContainsKey(hcmKey))
                    { 
                        employeeMap[hcmKey] = hcmEmployee;
                    }
                }


                int fromHcmDelCount = employeeMap.Values.Count(entry => entry["addFlag"]?.ToString() == "D");

                // 處理 punchCardData
                foreach (var punchcardEmployee in punchCardDataList) {
                    string cardKey = punchcardEmployee["empNo"].ToString();
                    punchcardEmployee["addFlag"] = "D";
                    punchcardEmployee.Remove("Status");

                    
                    //_yes["compare"] ="000";

                    if(!employeeMap.ContainsKey(cardKey)){ 
                        employeeMap[cardKey] = punchcardEmployee;
                    }else{

                        var _yes = employeeMap[cardKey];
                         _yes["pun_cardNoVal"]  = ConvertToValidString(punchcardEmployee["cardNo"].ToString());
                         _yes["pun_finger1Val"] = ConvertToValidString(punchcardEmployee["finger1"].ToString());
                         _yes["pun_finger2Val"] = ConvertToValidString(punchcardEmployee["finger2"].ToString());



                        string _cardV = string.Equals(_yes["pun_cardNoVal"], _yes["hcm_cardNoVal"]) ? "0" : "1";
                        string _finger1V = string.Equals(_yes["pun_finger1Val"], _yes["hcm_finger1Val"]) ? "0" : "1";
                        string _finger2V = string.Equals(_yes["pun_finger2Val"], _yes["hcm_finger2Val"]) ? "0" : "1";

                        // AppendTextToEditor($"Card Comparison: {_cardV}, Finger1 Comparison: {_finger1V}, Finger2 Comparison: {_finger2V}");
                        
                        _yes["compare"] = _finger2V+""+_finger1V+""+_cardV;
                        string  _checkStr = _finger2V+""+_finger1V+""+_cardV;

                        if(_checkStr !="000" && _checkStr != "" ){
                            _yes["cardNo"]  = punchcardEmployee["cardNo"];
                            _yes["finger1"] = punchcardEmployee["finger1"];
                            _yes["finger2"] = punchcardEmployee["finger2"];
                        }



                    }
                    
                } //END punchCardDataList


                //first time use hcm data
                if(CheckModeJSON("usehcm").Equals("yes")) {
                   
                    foreach (var key in employeeMap.Keys)
                    {
                        if (employeeMap[key]["empNo"] != null) // 確保 empNo 欄位存在
                        {
                            //employeeMap[key]["empNo"] = "A"; 
                          
                          
                            foreach (var hcmEmployee in hcmEmployeeDataList) {
                                
                                if( hcmEmployee["empNo"].ToString().Length>2 && hcmEmployee["empNo"].ToString() == key)
                                { 
                                    employeeMap[key]["cardNo"]  =  hcmEmployee["cardNo"].ToString() ;
                                    employeeMap[key]["finger1"] =  hcmEmployee["finger1"].ToString();
                                    employeeMap[key]["finger2"] =  hcmEmployee["finger2"].ToString() ;

                                    AppendTextToEditor(hcmEmployee["empNo"].ToString()+"<=hcm");
                                }
                            }



                        }
                    }
                }


                // 將結果放入
                foreach (var entry in employeeMap.Values)
                {
                    dataArray.Add(entry);
                }

                // 計算總筆數
                int totalCount = employeeMap.Values.Count;

                // 計算 addFlag 為 "D" 的筆數
                int addFlagDCount = employeeMap.Values.Count(entry => entry["addFlag"]?.ToString() == "D");

                AppendTextToEditor($"💻➡️🕒 總筆數:{employeeMap.Values.Count}|HCM標誌需刪除筆數:{fromHcmDelCount}|標誌被需刪除筆數: {addFlagDCount}");

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