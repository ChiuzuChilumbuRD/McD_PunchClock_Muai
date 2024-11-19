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

                // 假設settings.Settings是一個字典，包含所有的設定欄位
                var settingsValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(settings.Settings.ToString());
                var parameterDescript = JsonConvert.DeserializeObject<Dictionary<string, string>>(settings.Parameter_Descript.ToString());

                // 清空字典，確保每次加載新的設定時字典是空的
                dynamicEntries.Clear();
                try
                {
                    var commonKeys = new HashSet<string>(settingsValues.Keys);
                    foreach (var key in commonKeys)
                    {
                        string description = parameterDescript[key];
                        string fieldValue = settingsValues[key];

                        // 使用Frame包裝Label和Entry
                        var grid = new Grid
                        {
                            ColumnDefinitions =
                            {
                                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, // Label 比例
                                new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) } // Entry 比例
                            },
                            Margin = new Thickness(0, 2, 0, 2)
                        };

                        // 欄位名稱
                        var label = new Label
                        {
                            Text = description,
                            FontSize = 16,
                            VerticalTextAlignment = TextAlignment.Center
                        };

                        // 欄位值
                        var entry = new Entry
                        {
                            Text = fieldValue,
                            Placeholder = "請輸入" + description,
                        };

                        // 將Label和Entry包裝在Frame中
                        var labelFrame = new Frame
                        {
                            BackgroundColor = Colors.White,
                            Padding = 10,
                            Margin = new Thickness(5),
                            CornerRadius = 8,
                            Content = label
                        };

                        var entryFrame = new Frame
                        {
                            BackgroundColor = Colors.White,
                            Padding = 10,
                            Margin = new Thickness(5),
                            CornerRadius = 8,
                            Content = entry
                        };

                        Grid.SetColumn(labelFrame, 0);
                        grid.Children.Add(labelFrame);
                        Grid.SetColumn(entryFrame, 1);
                        grid.Children.Add(entryFrame);

                        dynamicEntries[key] = entry; // 儲存到 dynamicEntries 或其他處理邏輯

                        // 將Grid添加到DynamicFieldsLayout中
                        DynamicFieldsLayout.Children.Add(grid);
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
