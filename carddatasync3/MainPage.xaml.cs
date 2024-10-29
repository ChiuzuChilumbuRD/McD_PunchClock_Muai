using System.Diagnostics;
using Serilog;
using System.Text;
using System.Net.NetworkInformation;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Text.Json;
using Microsoft.Practices.EnterpriseLibrary.Data;
using Newtonsoft.Json;


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
        public static string eHrToFingerData;
        static string _gAPPath = AppContext.BaseDirectory;
        private static string apiBaseUrl = "https://gurugaia.royal.club.tw/eHR/GuruOutbound/Trans?ctrler=Std1forme00501&method=PSNSync&jsonParam=";
        



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


        private async void btn_HCM_to_fingerprint(object sender, EventArgs e)
        {
            conduct_HCM_to_fingerprint_work_1();
        }
    
        
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

                // Step 2: Load HCM Employee Data
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
            await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Downloading fingerprint data from HCM..."));
            bool result = await send_org_code_hcm("S000123"); // Assuming `send_org_code_hcm` returns bool
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
            var fileFingerprintPath = Path.Combine(desktopPath, "fingerprint_update.json");
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

           #region 寫入HCM json
            if (blResult)
            {
                date = DateTime.Now.ToString("yyyy-MM-dd");

                MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Sending organization code to HCM..."));

                // Call send_org_code_hcm and check for success
                bool isSuccessful = await send_org_code_hcm("S000123");

                if (isSuccessful) // Proceed only if HCM.json was created successfully
                {
                    MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("HCM.json created successfully on desktop."));
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


        
        private async Task<bool> upload_fingerprint_to_HCM(MainPage page)
        {
            string date = "";
            bool blResult = true;
            List<string> content = new List<string>();

            // Step 1: Check if the required file paths exist
            if (blResult)
            {
                blResult = page.checkFilePath();
                if (!blResult)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Directory {_gOutFilePath} does not exist. Please contact the system administrator."));
                    return false;
                }
            }

            // Step 2: Check if PGFinger.exe exists
            if (blResult)
            {
                blResult = page.checkIsExisitExe();
                if (!blResult)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("PGFinger.exe does not exist. Please contact the system administrator."));
                    return false;
                }
            }

            // Step 3: Delete existing FingeOut.txt if it exists
            if (blResult)
            {
                if (File.Exists(Path.Combine(_gOutFilePath, "FingeOut.txt")))
                {
                    try
                    {
                        File.Delete(Path.Combine(_gOutFilePath, "FingeOut.txt"));
                        await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Deleted existing FingeOut.txt file."));
                    }
                    catch
                    {
                        await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Failed to delete FingeOut.txt file. Please contact the system administrator."));
                        blResult = false;
                    }
                }
            }

            // Step 4: Generate fingerprint file using PGFinger.exe
            #region 使用廠商執行檔生成指紋檔案 (Execute PGFinger.exe to generate fingerprint file)
            if (blResult)
            {
                date = DateTime.Now.ToString("yyyy-MM-dd");
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Starting to read fingerprint from the fingerprint machine..."));
                    string str_exec_cmd = Path.Combine(pglocation, "PGFinger.exe");
                    string str_exec_parameter = @"1 " + Path.Combine(_gOutFilePath, "FingeOut.txt");
                    await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Executing: {str_exec_cmd} {str_exec_parameter}"));

                    // Start the process
                    Process.Start(str_exec_cmd, str_exec_parameter);
                    //await wait_for_devicecontrol_complete();

                    await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Completed reading fingerprint from the fingerprint machine."));
                }
                catch (Exception ex)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Error executing PGFinger.exe: {ex.Message}"));
                    blResult = false;
                    return false;
                }

                // Check if FingeOut.txt was created
                int counter = 0;
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Checking for the FingeOut.txt file..."));
                while (!File.Exists(Path.Combine(_gOutFilePath, "FingeOut.txt")))
                {
                    await Task.Delay(1000); // Wait 1 second
                    counter++;
                    if (counter > 20)
                    {
                        break;
                    }
                }
            }
            #endregion 使用廠商執行檔生成指紋檔案

            // Step 5: Verify that FingeOut.txt exists
            if (!File.Exists(Path.Combine(_gOutFilePath, "FingeOut.txt")))
            {
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Error: FingeOut.txt file was not generated by PGFinger.exe. Please contact the system administrator."));
                return false;
            }

            // Step 6: Read and validate FingeOut.txt content
            if (blResult)
            {
                try
                {
                    using (StreamReader sr = new StreamReader(Path.Combine(_gOutFilePath, "FingeOut.txt"), Encoding.Default))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                                content.Add(line);
                        }
                    }

                    if (content.Count <= 0)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("FingeOut.txt is empty. Please contact the system administrator."));
                        blResult = false;
                    }
                }
                catch (Exception ex)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Error reading FingeOut.txt: {ex.Message}"));
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
                File.Copy(Path.Combine(_gOutFilePath, "FingeOut.txt"), backupPath, true);
                await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Backup created for FingeOut.txt at: {backupPath}."));
            }

            // Step 9: Update card numbers and employee data in HCM
            if (blResult)
            {
                int success_card_count = 0;
                //blResult = page.update_card_number(content, ref success_card_count);

                if (!blResult)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Failed to upload employee card numbers to HCM."));
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Successfully uploaded {success_card_count} employee card numbers to HCM."));
                }

                int success_emp_count = 0;
                //blResult = page.updateDataByEmployeeId(content, ref success_emp_count);

                if (!blResult)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Failed to upload employee fingerprint data to HCM."));
                }
                else
                {
                    try
                    {
                        File.Delete(Path.Combine(_gOutFilePath, "FingeOut.txt"));
                        await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor("Successfully deleted FingeOut.txt after upload."));
                    }
                    catch (Exception ex)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Successfully uploaded data to HCM but failed to delete FingeOut.txt: {ex.Message}"));
                    }

                    await MainThread.InvokeOnMainThreadAsync(() => AppendTextToEditor($"Successfully uploaded {success_emp_count} employee fingerprint records to HCM."));
                }
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

                // Define the path for HCM.json within FingerData
                var fileHCMPath = Path.Combine(fingerDataPath, "HCM.json");

                // Call the API and get the JSON response
                string responseData = await GetRequestAsync(orgCode);

                // Write the response data to HCM.json, overwriting if it exists
                await File.WriteAllTextAsync(fileHCMPath, responseData);

                // Log success message
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
            }
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

        private async Task<dynamic> LoadHCMEmployeeDataAsync()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var fileHCMPath = Path.Combine(desktopPath, "FingerData/HCM.json");

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