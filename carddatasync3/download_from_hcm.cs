//TODO: Refactor Large functions

/*
private static string _downloadLocation = "your_downloadlocation"; // Set manually

 private async void btn_HCM_to_fingerprint(object sender, EventArgs e)
        {
            // Disable buttons while the task is running
            set_btns_state(false);

            // Execute the download and upload process asynchronously
            bool result = await DownloadAndUploadToHCMAsync();
            
            if (result)
            {
                await DisplayAlert("Success", "Data downloaded from HCM and processed successfully!", "OK");
            }
            else
            {
                await DisplayAlert("Error", "Failed to download and process data from HCM.", "OK");
            }

            // Re-enable buttons after the task is complete
            set_btns_state(true);
        }

        // Main function for download and upload to HCM
        private async Task<bool> DownloadAndUploadToHCMAsync()
        {
            bool result = true;
            List<string> content = new List<string>();
            string cradMachineId = ""; 
            string cradMachineIdOut = ""; 
            string orgid = "";

            // Simulate the download and processing logic here
            // For demonstration, assume `content` has been filled with data

            // Get machine IDs and org ID
            cradMachineId = GetCradMachineId(1);
            cradMachineIdOut = GetCradMachineId(2);
            orgid = GetCradOrgId();

            // Insert the card data into the database
            if (!InsertCardData(content, cradMachineId, cradMachineIdOut, orgid))
            {
                ShowError("Failed to sync attendance data, please try again.");
                return false;
            }

            ShowInfo("Attendance data sync with HCM completed successfully.");
            return true;
        }

        /// <summary>
        /// Retrieves the card machine ID based on type
        /// Type: 1 = 上班卡 (Check-in), 2 = 下班卡 (Check-out)
        /// </summary>
        private string GetCradMachineId(int type)
        {
            //place holder function - no database access 
            string id = string.Empty;
            return id;
        }

        /// <summary>
        /// Gets the organization ID
        /// </summary>
        private string GetCradOrgId()
        {
            // place holder function - no database access
            string id = string.Empty;
            return id;
        }

        /// <summary>
        /// Inserts card data into the database
        /// </summary>
        private bool InsertCardData(List<string> content, string machineIdIn, string machineIdOut, string orgId)
        {
            // place holder function - no database access
            bool result = true;
            return result;
        }

        // Define ShowError to display error messages
        private void ShowError(string message)
        {
            MainThread.InvokeOnMainThreadAsync(() =>
            {
                DisplayAlert("Error", message, "OK");
            });
        }

        // Define ShowInfo to display info messages
        private void ShowInfo(string message)
        {
            MainThread.InvokeOnMainThreadAsync(() =>
            {
                DisplayAlert("Info", message, "OK");
            });
        }
*/