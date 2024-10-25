using System.Diagnostics;
using Serilog;
using System.Text;
using System.Net.NetworkInformation;
using System.Data;
using System.Data.Common; 
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
        private static string pglocation = Path.Combine(desktopPath, "PGFinger.exe"); // Simulated path for executable
        private static int peopleCount = 0;
        public static StringBuilder str_accumulated_log = new StringBuilder();
        protected static string databaseKey = "GAIA.EHR";
        public static string eHrToFingerData;
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

            // // Call initialization sequence
            // InitializeApp();
            // //Database Factory 
            //  _dbFactory = new DBFactory();
        }

        #region Initialisation

        private void InitializeApp()
        {
            //AppendTextToEditor("App initialization started.");

            // Step 1: Load appsettings.json content
            //LoadAppSettings();

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

            //AppendTextToEditor("App initialization completed.");
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

         #region download from hcm functions


        private async void btn_HCM_to_fingerprint(object sender, EventArgs e)
        {
            //begin task 
            conduct_HCM_to_fingerprint_work();
        }



        private async void conduct_HCM_to_fingerprint_work()
        {
            set_btns_state(false);

            // Run the background work using Task.Run
            await Task.Run(() => HCM_to_fingerprint_thread(this));

            // Optionally, set buttons state back to true after the task completes
            set_btns_state(true);
        }


        private async void HCM_to_fingerprint_thread(object obj)
        {
            bool is_lock_taken = false;
            ui_sp.TryEnter(ref is_lock_taken);
            if (!is_lock_taken)
                return;

            string str_current_task = "員工資料匯入指紋機";
            MainPage this_page = (MainPage)obj; // Assuming MainPage is passed instead of Form1
            OperationRecorder op_recorder = null;
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
                        await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Starting fingerprint upload to HCM..."));

                        // Commented out upload_finger_print
                        // op_recorder = init_op_recorder(this_page.TextBox1.Text, "Upload FP to HCM");
                        // b_result = upload_fingerprint_to_HCM(this_page);
                        // upload_op_recorder(op_recorder, b_result);

                        // Proceed with downloading fingerprints from HCM
                        //op_recRecorder = init_op_recorder(this_page.TextBox1.Text, "Get FP from HCM");

                        await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Downloading fingerprint data from HCM..."));
                        b_result = download_fingerprint_from_HCM_imp(this_page);

                        //upload_op_recorder(op_recorder, b_result);

                        if (!b_result)
                            break;

                        // Proceed with writing fingerprints to the fingerprint device
                        //op_recorder = init_op_recorder(this_page.TextBox1.Text, "Upload FP to Fingerprint Device");

                        await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Writing fingerprint data to the fingerprint machine..."));
                        b_result = await Task.Run(() => write_to_fingerprinter(this_page));

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
        private bool download_fingerprint_from_HCM_imp(MainPage page)
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
                    MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("指紋機程序「PGFinger」儲存資料夾已創建。")); // PGFinger storage folder created successfully.
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

            #region 寫入FingerIn.txt (Write FingerIn.txt)
            if (blResult)
            {
                date = DateTime.Now.ToString("yyyy-MM-dd");

                MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("開始從 HCM 獲取員工指紋資料..."));
                DataTable dt = page.getPerson();  // Fetch the person data from HCM.

                if (dt != null && dt.Rows.Count > 0)  // Proceed only if there is data
                {
                    try
                    {
                        MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("獲取到的員工資料筆數：" + dt.Rows.Count)); // Log the number of records fetched from HCM.

                        // Create the FingerIn.txt file and write the data
                        using (FileStream fs = new FileStream(_gOutFilePath + @"\FingerIn.txt", FileMode.CreateNew))
                        // Replace 'big5' with 'UTF-8' if you don't need the big5 encoding
                        using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8)) 
                        {
                            foreach (DataRow dr in dt.Rows)
                            {
                                string data = $"{dr["EMPLOYEEID"]},{dr["TrueName"]},{dr["CARDNUM"]},{dr["finger1"]},{dr["finger2"]},{dr["DataType"]}";
                                MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"正在寫入數據到 FingerIn.txt: {data}")); // Log each data line being written to FingerIn.txt.
                                sw.WriteLine(data);
                            }
                        }


                        MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"HCM 匯出 {dt.Rows.Count} 筆員工指紋資料到 FingerIn.txt。")); // Successfully exported {count} records to FingerIn.txt.
                        blResult = true;
                    }
                    catch (Exception ex)
                    {
                        MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("寫入 FingerIn.txt 文件時出錯：" + ex.Message)); // Error writing to FingerIn.txt.
                        blResult = false;
                    }
                }
                else
                {
                    if (dt == null || dt.Rows.Count == 0)
                    {
                        MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("目前無相關指紋資料可以下載, 請確認")); // No relevant fingerprint data to download, please check.
                    }
                    else
                    {
                        MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("獲取門店員工資料失敗，請洽系統管理員")); // Failed to retrieve employee data, please contact system administrator.
                    }
                    blResult = false;
                }
            }
            #endregion 寫入FingerIn.txt

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




        private async Task<bool> write_to_fingerprinter(MainPage page)
        {
            bool ret_val = false;

            try
            {
                // Log the start of the process
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Write to fingerprinter initiated."));

                // Step 1: Show information about starting the fingerprint device program
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("啟動指紋機程式"));

                // Step 2: Prepare the execution command and parameters for PGFinger.exe
                string str_exec_cmd = Path.Combine(pglocation, "PGFinger.exe");
                string str_exec_parameter = @"2 " + Path.Combine(_gOutFilePath, "FingerIn.txt");

                // Log the execution command
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Executing: {str_exec_cmd} {str_exec_parameter}"));

                // Step 3: Start the PGFinger.exe process with the necessary parameters
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = str_exec_cmd,
                        Arguments = str_exec_parameter,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                // Start the process and wait for its completion
                process.Start();
                await process.WaitForExitAsync(); // Async wait for the process to finish

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
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Backing up FingerIn.txt to: {backupFile}"));
                File.Copy(Path.Combine(_gOutFilePath, "FingerIn.txt"), backupFile, true);
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Backup of FingerIn.txt created successfully."));

                ret_val = true;
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Error during fingerprint process: {ex.Message}"));
            }

            return ret_val;
        }


        
        private bool upload_fingerprint_to_HCM(MainPage page)
        {
            string date = "";
            bool blResult = true;
            List<string> content = new List<string>();

            // Step 1: Check if the required file paths and executables exist
            if (blResult)
            {
                blResult = page.checkFilePath();
                if (!blResult)
                {
                    AppendTextToEditor("指紋機程序「PGFinger」儲存資料夾" + _gOutFilePath + "不存在，請洽系統管理員「卡鐘廠商」");
                    return false;
                }
            }

            if (blResult)
            {
                blResult = page.checkIsExisitExe();
                if (!blResult)
                {
                    AppendTextToEditor("PGFinger.exe不存在，請洽系統管理員「卡鐘廠商」");
                    return false;
                }
            }

            // Step 2: Delete existing FingeOut.txt if it exists
            if (blResult)
            {
                if (File.Exists(_gOutFilePath + @"\FingeOut.txt"))
                {
                    try
                    {
                        File.Delete(_gOutFilePath + @"\FingeOut.txt");
                        AppendTextToEditor("Existing FingeOut.txt deleted.");
                    }
                    catch (Exception ex)
                    {
                        AppendTextToEditor($"Error deleting FingeOut.txt: {ex.Message}");
                        blResult = false;
                    }
                }
            }

            // Step 3: Simulate PGFinger.exe execution and generate FingeOut.txt
            if (blResult)
            {
                date = DateTime.Now.ToString("yyyy-MM-dd");
                try
                {
                    AppendTextToEditor("開始讀取指紋機指紋...");

                    // Simulate the process execution with asynchronous delay
                    SimulateFingeOutGeneration(); // Simulate creating the FingeOut.txt file

                    AppendTextToEditor("讀取指紋機指紋完成");
                }
                catch (Exception ex)
                {
                    AppendTextToEditor("執行PGFinger.exe出錯：" + ex.Message);
                    blResult = false;
                    return blResult;
                }
            }

            // Step 4: Check for FingeOut.txt
            if (!File.Exists(_gOutFilePath + @"\FingeOut.txt"))
            {
                blResult = false;
                AppendTextToEditor("執行PGFinger.exe出錯：未生成FingeOut.txt文件");
                return false;
            }

            // Step 5: Process the FingeOut.txt file and validate the content
            if (blResult)
            {
                try
                {
                    using (StreamReader sr = new StreamReader(_gOutFilePath + @"\FingeOut.txt", Encoding.Default))
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
                        AppendTextToEditor("FingeOut.txt文件為空，請洽系統管理員");
                        blResult = false;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    AppendTextToEditor("Error processing FingeOut.txt: " + ex.Message);
                    blResult = false;
                    return false;
                }
            }

            // Step 6: Backup FingeOut.txt
            if (blResult)
            {
                try
                {
                    page.getFilePath1(date); // Ensure backup directories are created
                    string backupPath = _gBackUpPath + @"\員工指紋匯出資料\" + date.Replace("-", "") + @"\PGFingeOut" + date.Replace("-", "") + DateTime.Now.ToString("HHmmss") + ".txt";
                    File.Copy(_gOutFilePath + @"\FingeOut.txt", backupPath, true);
                    AppendTextToEditor($"FingeOut.txt backed up successfully to: {backupPath}");
                }
                catch (Exception ex)
                {
                    AppendTextToEditor("Error backing up FingeOut.txt: " + ex.Message);
                    blResult = false;
                }
            }

            // Step 7: Simulate uploading the fingerprint data to HCM
            if (blResult)
            {
                int successCount = 0;
                blResult = SimulateUploadFingerprintData(content, ref successCount);
                if (!blResult)
                {
                    AppendTextToEditor("員工卡號匯入HCM系統失敗");
                }
                else
                {
                    AppendTextToEditor($"員工卡號資料匯入HCM共 {successCount} 筆");
                    try
                    {
                        // Delete the FingeOut.txt after successful upload
                        File.Delete(_gOutFilePath + @"\FingeOut.txt");
                        AppendTextToEditor("FingeOut.txt successfully deleted after upload.");
                    }
                    catch (Exception ex)
                    {
                        AppendTextToEditor("員工指紋資料匯入HCM成功，但FingerOut.txt檔案無法刪除");
                        blResult = false;
                    }
                }
            }

            return blResult;
        }

          private void SimulateFingeOutGeneration()
        {
            try
            {
                string outputFile = Path.Combine(_gOutFilePath, "FingeOut.txt");
                using (var fs = new FileStream(outputFile, FileMode.CreateNew))
                using (var sw = new StreamWriter(fs, Encoding.GetEncoding("big5")))
                {
                    // Simulate writing fingerprint data
                    for (int i = 1; i <= 10; i++)
                    {
                        string employeeId = "EMP" + i.ToString("D4");
                        string trueName = "Employee" + i;
                        string cardNum = "CARD" + i.ToString("D4");
                        string finger1 = "Finger1_" + i;
                        string finger2 = "Finger2_" + i;
                        string dataType = "Type" + i;

                        string data = $"{employeeId},{trueName},{cardNum},{finger1},{finger2},{dataType}";
                        sw.WriteLine(data);
                    }
                }
                AppendTextToEditor("Simulated FingeOut.txt file created successfully.");
            }
            catch (Exception ex)
            {
                AppendTextToEditor("Error simulating FingeOut.txt: " + ex.Message);
            }
        }

            
            
            

        private bool SimulateUploadFingerprintData(List<string> content, ref int successCount)
        {
            try
            {
                // Simulate processing each record and uploading it
                foreach (var record in content)
                {
                    // Simulate the validation of record
                    if (!string.IsNullOrWhiteSpace(record))
                    {
                        successCount++;
                    }
                }

                AppendTextToEditor($"Simulated upload of {successCount} fingerprint records to HCM.");
                return true;
            }
            catch (Exception ex)
            {
                AppendTextToEditor("Error uploading fingerprint data: " + ex.Message);
                return false;
            }
        }




        private async Task SimulateDeviceControlExecution()
        {
            int wait_count = 5;  // Maximum number of checks
            int wait_interval = 50;  // Wait 100ms between checks
            bool is_device_complete = false;

            AppendTextToEditor("Simulating DeviceControl process...");

            // Simulate waiting for the device to complete its operation
            for (int i = 0; i < wait_count; i++)
            {
                await Task.Delay(wait_interval);  // Wait asynchronously
                AppendTextToEditor($"Checking DeviceControl (attempt {i + 1})");

                // Simulate device being ready halfway through
                if (i == 25)
                {
                    is_device_complete = true;
                    break;
                }
            }

            if (is_device_complete)
            {
                AppendTextToEditor("DeviceControl process completed.");
            }
            else
            {
                AppendTextToEditor("DeviceControl process did not complete within the time limit.");
            }
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


       public async Task<bool> write_to_fingerprinter()
        {
            bool ret_val = false;  // Default to failure
            show_info_2("Write to fingerprinter.");
            try
            {
                // Notify that the fingerprint process is starting
                await Task.Run(() => show_info("啟動指紋機程式"));

                // Simulating running the `PGFinger.exe` process
                var str_exec_cmd = "Simulated_PGFinger";  // Simulated executable name
                var str_exec_parameter = $"2 {Path.Combine(_gOutFilePath, "FingerIn.txt")}";
                show_info_2($"Simulated Execute {str_exec_cmd} {str_exec_parameter}");

                // Simulate file creation instead of running an external process
                SimulateFingerprintProcess();

                // Simulate waiting for the fingerprint device to complete
                //await wait_for_devicecontrol_complete();

                // Notify the user that the process has completed
                await Task.Run(() => show_info("指紋機執行結束"));

                // Simulate further operations, e.g., getting file path (logic adjusted as needed)
                getFilePath1(eHrToFingerData);

                // Create backup of the file with a timestamp
                string backupDir = Path.Combine(_gBackUpPath, "員工指紋匯出資料", eHrToFingerData.Replace("-", ""));
                string backupFileName = $"PGFingerIn_{eHrToFingerData.Replace("-", "")}{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                Directory.CreateDirectory(backupDir);  // Ensure backup directory exists

                // Copy the generated file to the backup directory
                File.Copy(Path.Combine(_gOutFilePath, "FingerIn.txt"), Path.Combine(backupDir, backupFileName), true);
                show_info_2($"Copy FingerIn.txt to {backupDir}\\{backupFileName}");

                ret_val = true;  // Indicate success
            }
            catch (Exception ex)
            {
                // Handle errors and notify the user
                await Task.Run(() => show_err($"執行寫入指紋機出錯：{ex.Message}，請洽系統管理員"));
                show_info_2("Error writing to fingerprinter");
            }
            return ret_val;
        }

        // Simulate the behavior of running the PGFinger.exe process and creating FingerIn.txt
        private void SimulateFingerprintProcess()
        {
            string fingerInPath = Path.Combine(_gOutFilePath, "FingerIn.txt");
            Directory.CreateDirectory(_gOutFilePath);  // Ensure directory exists

            using (var writer = new StreamWriter(fingerInPath))
            {
                writer.WriteLine("Simulated Fingerprint Data");  // Write dummy data
                show_info_2("Simulated fingerprint data written to FingerIn.txt.");
            }
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
            p_device_control.WaitForExit(30*60*1000);

        }

        private static OperationRecorder init_op_recorder(string str_unitcode, string str_operation)
        {
            reset_log();
            OperationRecorder op_recorder = new OperationRecorder
            {
                unitcode = str_unitcode,
                operation = str_operation
            };
            return op_recorder;
        }

        private static async Task upload_op_recorder(OperationRecorder op_recorder, bool is_successful)
        {
            op_recorder.result = is_successful ? "Success" : "Failure";
            op_recorder.log = str_accumulated_log.ToString();
            op_recorder.end_time = DateTime.Now;
            await Task.Run(() => op_recorder.upload_log(databaseKey));  // Assuming upload_log is asynchronous or blocking
        }

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
        }

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

        private static void reset_log()
        {
            str_accumulated_log.Clear();
        }

        private static void LogDebug(string message)
        {
            Console.WriteLine($"DEBUG: {message}");
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

        // private void getFilePath1(string date)
        // {
        //     string path = _gBackUpPath + @"\FingerprintData\" + date.Replace("-", "");
        //     if (!Directory.Exists(path))
        //     {
        //         Directory.CreateDirectory(path);
        //     }
        // }

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
