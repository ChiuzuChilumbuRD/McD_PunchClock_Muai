using System.Diagnostics;
using Serilog;
using System.Text;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Data;
using Microsoft.Practices.EnterpriseLibrary.Data;

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
        private static string pglocation = Path.Combine(desktopPath, "PGFinger.exe");
        private DBFactory _dbFactory;
        static string _gAPPath = AppContext.BaseDirectory;


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

        private void InitializeApp()
        {
            AppendTextToEditor("App initialization started.");

            // Step 1: Load appsettings.json content
            LoadAppSettings();

            // Step 2: Check if file exists and execute if found
            // if (!CheckAndExecuteFile(this))
            // {

            //     AppendTextToEditor("Required file not found. Closing application.");
            //     return;
            // }


            // Step 3: Check internet connection
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

        private void LoadAppSettings()
        {
            AppendTextToEditor("Loading appsettings.json...");
            // TODO: Add logic to load and parse appsettings.json
            string appSettingsPath = Path.Combine(Environment.CurrentDirectory, "appsettings.json");

            // 檢查檔案是否存在
            if (File.Exists(appSettingsPath))
            {
                try
                {
                    // 讀取檔案內容
                    string jsonContent = File.ReadAllText(appSettingsPath);
                    AppendTextToEditor(jsonContent); // 將 JSON 內容顯示在 TextEditor
                }
                catch (Exception ex)
                {
                    // 處理讀取檔案時的異常
                    AppendTextToEditor("Error reading appsettings.json: " + ex.Message);
                }
            }
            else
            {
                AppendTextToEditor("appsettings.json not found.");
            }
        }

        private bool CheckAndExecuteFile(MainPage page)
        {
            string date = "";
            bool blResult = true;
            var content = new List<string>();

            if (blResult)
            {
                blResult = page.ensureFilePathExists();
                if (!blResult)
                {
                    AppendTextToEditor("The folder for storing PGFinger data does not exist.");
                    return false;
                }
            }

            if (blResult)
            {
                blResult = page.validatePGFingerExe();
                if (!blResult)
                {
                    AppendTextToEditor("PGFinger.exe does not exist.");
                    return false;
                }
            }

            if (blResult)
            {
                if (File.Exists(_gOutFilePath + @"\FingerOut.txt"))
                {
                    try
                    {
                        File.Delete(_gOutFilePath + @"\FingerOut.txt");
                    }
                    catch
                    {
                        page.show_err("Cannot delete FingerOut.txt file.");
                        blResult = false;
                    }
                }
                // AppendTextToEditor("Pass validatePGFingerExe");
            }

            // Call the PGFinger.exe process
            if (blResult)
            {
                // AppendTextToEditor("Call the PGFinger.exe process");
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

        public bool validatePGFingerExe()
        {
            // AppendTextToEditor(File.Exists(pglocation + @"\PGFinger.exe").ToString());
            return File.Exists(desktopPath + @"\PGFinger.exe");
        }



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
            // Disable buttons while the task is running
            set_btns_state(false);

            try
            {
                // Step 1: Send Restaurant Code (PSNSync) - Chiuzu 
                // This sends the organization code to the HCM server to get employee data.
                 // Example orgCode and punch card file path
                string orgCode = "S000123";
                string punchCardFilePath = Path.Combine(FileSystem.AppDataDirectory, "punchcard.json");

                // Call the service method to process initial steps
                bool result = await EmployeeService.ProcessInitialStepsAsync(orgCode, punchCardFilePath);

                if (result)
                {
                    await DisplayAlert("Success", "Initial steps completed successfully.", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "Failed to complete the initial steps.", "OK");
                }
                
                // Step 2: Load HCM Employee Data (LoadEmpInfo) - Chiuzu 
                // Loads the employee data from the HCM server, saved in 'GuruData.json'.
                var hcmEmployeeData = await LoadHCMEmployeeDataAsync();

                // Step 3: Read Punch Card Data (LoadPunchCardInfo) - Chiuzu
                // Reads the punch card data from 'punchcard.json'.
                var punchCardData = await ReadPunchCardDataAsync();

                // Step 4: Compare Employee Data (Rule 1) -Reena
                // Applies rule 1 to compare employee data from HCM and PunchCard.
                var comparisonResult1 = ApplyRule1(hcmEmployeeData, punchCardData);

                // Step 5: Compare Fingerprint Data (Rule 2) - Reena
                // Applies rule 2 to compare fingerprint data.
                var comparisonResult2 = ApplyRule2(hcmEmployeeData, punchCardData);

                // Step 6: Update Status if Necessary (PSNModify) 
                // If comparison result shows differences, update employee records.
                if (comparisonResult2.NeedsUpdate)
                {
                    await UpdateEmployeeDataAsync(comparisonResult2);
                }

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

            // Re-enable buttons after the task is complete
            set_btns_state(true);
        }

    

        private void conduct_HCM_to_fingerprint_work()
        {
            // Disable buttons in the UI thread
            MainThread.BeginInvokeOnMainThread(() => set_btns_state(false));

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

        public async Task<List<Employee>> LoadHCMEmployeeDataAsync()
        {
            // Placeholder for loading employee data from HCM server, saved in 'GuruData.json'.
            // Replace with logic to load and parse HCM data.
            await Task.Delay(500); // Simulate async work
            return new List<Employee>(); // Return mock employee data for now
        }

        public async Task<List<PunchCard>> ReadPunchCardDataAsync()
        {
            // Placeholder for reading punch card data from 'punchcard.json'.
            // Replace with actual file reading and deserialization logic.
            await Task.Delay(500); // Simulate async work
            return new List<PunchCard>(); // Return mock punch card data for now
        }

        public ComparisonResult ApplyRule1(List<Employee> hcmEmployeeData, List<PunchCard> punchCardData)
        {
            // Placeholder for comparing employee data (Rule 1).
            // Implement logic to compare HCM employee data with punch card data.
            return new ComparisonResult { NeedsUpdate = false }; // Return a mock result
        }

        public ComparisonResult ApplyRule2(List<Employee> hcmEmployeeData, List<PunchCard> punchCardData)
        {
            // Placeholder for comparing fingerprint data (Rule 2).
            // Implement logic to compare fingerprint data.
            return new ComparisonResult { NeedsUpdate = true }; // Return a mock result
        }

        public async Task UpdateEmployeeDataAsync(ComparisonResult comparisonResult)
        {
            // Placeholder for updating employee records if differences are found.
            // Replace with actual update logic.
            await Task.Delay(500); // Simulate async work
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
                var fileHCMPath = Path.Combine(_gAPPath, @"\test_files\HCM_fingerprint.json");
                var filePunchClockPath = Path.Combine(_gAPPath, @"\test_files\PunchClock_fingerprint.json");

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

        private async void Punch_Data_Changing_rule1(object sender, EventArgs e)
        {
            try
            {
                // 使用相對路徑來讀取 JSON 檔案
                var fileHCMPath = Path.Combine(_gAPPath, @"\test_files\HCM.json");
                var filePunchClockPath = Path.Combine(_gAPPath, @"\test_files\PunchClock.json");
                // var fileHCMPath = @"C:\Users\reena.tsai\Documents\maui-guru\McD_PunchClock_Muai\carddatasync3\test_files\HCM.json"; 
                // var filePunchClockPath = @"C:\Users\reena.tsai\Documents\maui-guru\McD_PunchClock_Muai\carddatasync3\test_files\PunchClock.json"; 

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
            // Display information message (e.g., in UI)
            XmlEditor.Text += $"{message}\n";
        }

        private void show_err(string message)
        {
            // Display error message
            XmlEditor.Text += $"Error: {message}\n";
        }

        private void show_info_2(string message)
        {
            XmlEditor.Text += $"{message}\n";
        }

        private void show_err_2(string message)
        {
            XmlEditor.Text += $"Error: {message}\n";
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
