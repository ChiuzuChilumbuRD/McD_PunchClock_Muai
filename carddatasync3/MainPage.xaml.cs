using System;
using Microsoft.Maui.Controls;
using carddatasync3;  // Ensure you include the namespace for AppSettings

namespace carddatasync3
{
    public partial class MainPage : ContentPage
    {
        private readonly AppSettings _settings;

        // Inject the AppSettings object into the constructor
        public MainPage(AppSettings settings)
        {
            _settings = settings;
            InitializeComponent();
            InitializeConfigData(); 
        }

        // Method to initialize and display configuration data
        private void InitializeConfigData()
        {
            try
            {
                // Retrieve values from the AppSettings class
                string downloadLocation = _settings.DownloadLocation;
                string pgFingerLocation = _settings.PGFingerLocation;
                string outFilePath = _settings.FileOutPath;
                string backupPath = _settings.BackUpPath;
                string autoRunTime = _settings.AutoRunTime;

                // Format and display the configuration in the XmlEditor
                XmlEditor.Text = $"Configuration loaded successfully!\n" +
                                 $"Download Location: {downloadLocation}\n" +
                                 $"PG Finger Location: {pgFingerLocation}\n" +
                                 $"File Out Path: {outFilePath}\n" +
                                 $"Backup Path: {backupPath}\n" +
                                 $"Auto Run Time: {autoRunTime}";
            }
            catch (Exception ex)
            {
                // Display any errors in the XmlEditor
                XmlEditor.Text = $"Error loading configuration: {ex.Message}";
            }
        }

        #region Button Event Handlers

        // The rest of the event handlers for buttons
        private async void btn_delivery_upload(object sender, EventArgs e)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert("Button Clicked", "You clicked the button delivery_upload!", "OK");
            });
        }

        private async void btn_HCM_to_fingerprint(object sender, EventArgs e)
        {   
            set_btns_state(false);
            bool result = true; // Placeholder logic

            if (result)
            {
                await DisplayAlert("Success", "Data downloaded and processed successfully!", "OK");
            }

            set_btns_state(true);
        }

        private async void btn_upload_to_HCM(object sender, EventArgs e)
        {
            set_btns_state(false);
            await DisplayAlert("Upload Started", "Uploading fingerprint data to HCM...", "OK");
            set_btns_state(true);
        }

        #endregion

        private void set_btns_state(bool state)
        {
            btnHCMToFingerprint.IsEnabled = state;
            btnUploadToHCM.IsEnabled = state;
            btnDeliveryUpload.IsEnabled = state;
        }
    }
}
