using Microsoft.Maui.Controls;
using Newtonsoft.Json;
using System.IO;
using System;
using System.ComponentModel;

namespace carddatasync3
{
    public partial class AppSettingPage : ContentPage
    {
        static string _gAPPath = AppContext.BaseDirectory;
        string appSettingsFilePath = Path.Combine(_gAPPath, "appsettings.json");
        private Dictionary<string, Entry> dynamicEntries = new Dictionary<string, Entry>();

        public AppSettingPage()
        {
            InitializeComponent();
            
            LoadSettings();
            
            // Save Button (Navigation bar)
            var toolbarItem = new ToolbarItem
            {
                IconImageSource = "save.png", // 替換為你的圖片檔案名稱
                Order = ToolbarItemOrder.Primary,
                Priority = 0
            };
            toolbarItem.Clicked += OnSaveSettingsClicked;
            this.ToolbarItems.Add(toolbarItem);
        }

        // 讀取設定並顯示
        private void LoadSettings()
        {
            if (File.Exists(appSettingsFilePath))
            {
                var json = File.ReadAllText(appSettingsFilePath);
                var settings = JsonConvert.DeserializeObject<dynamic>(json);

                var settingsValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(settings.Settings.ToString());
                var parameterDescript = JsonConvert.DeserializeObject<Dictionary<string, string>>(settings.Parameter_Descript.ToString());

                dynamicEntries.Clear();
                try
                {
                    foreach (var key in settingsValues.Keys)
                    {
                        string description = parameterDescript[key];
                        string fieldValue = settingsValues[key];

                        // 設計欄位的 Grid
                        var grid = new Grid
                        {
                            ColumnDefinitions =
                            {
                                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, // Label 比例
                                new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }  // Entry 比例
                            },
                            Margin = new Thickness(0, 2),
                            ColumnSpacing = 10
                        };

                        // Label
                        var label = new Label
                        {
                            Text = description,
                            FontSize = 15,
                            VerticalTextAlignment = TextAlignment.Center
                        };

                        // Entry
                        var entry = new Entry
                        {
                            Text = fieldValue,
                            Placeholder = "請輸入" + description,
                            FontSize = 15,
                            VerticalOptions = LayoutOptions.FillAndExpand
                        };

                        // Label 的 Frame
                        var labelFrame = new Frame
                        {
                            BackgroundColor = Colors.White,
                            Padding = 2, // 縮小內間距
                            Margin = 3,
                            CornerRadius = 5,
                            Content = label,
                            HeightRequest = 45 // 控制 Frame 的高度
                        };

                        // Entry 的 Frame
                        var entryFrame = new Frame
                        {
                            BackgroundColor = Colors.White,
                            Padding = 2, // 縮小內間距
                            Margin = 3,
                            CornerRadius = 5,
                            Content = entry,
                            HeightRequest = 45 // 控制 Frame 的高度
                        };

                        Grid.SetColumn(labelFrame, 0);
                        grid.Children.Add(labelFrame);

                        Grid.SetColumn(entryFrame, 1);
                        grid.Children.Add(entryFrame);

                        // 加入 DynamicFieldsLayout
                        dynamicEntries[key] = entry;
                        FlexLayout.Children.Add(grid);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        private async void OnSaveSettingsClicked(object sender, EventArgs e)
        {
            var updatedSettings = new Dictionary<string, string>();

            // 遍歷所有欄位並將其值保存到字典中
            foreach (var entry in dynamicEntries)
            {
                updatedSettings[entry.Key] = entry.Value.Text; 
            }

            // 將原本的 Parameter_Descript 保留下來
            var json = File.ReadAllText(appSettingsFilePath);
            var existingContent = JsonConvert.DeserializeObject<dynamic>(json);
            var parameterDescript = JsonConvert.DeserializeObject<Dictionary<string, string>>(existingContent.Parameter_Descript.ToString());

            // 組合新的 JSON 結構
            var combinedContent = new
            {
                Settings = updatedSettings,
                Parameter_Descript = parameterDescript
            };

            var updatedJson = JsonConvert.SerializeObject(combinedContent, Formatting.Indented);

            File.WriteAllText(appSettingsFilePath, updatedJson);

            await DisplayAlert("Success", "設定檔儲存成功", "OK");
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            this.Title = "設定檔";
        }
    }
}
