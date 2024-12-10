using System.Threading;
using Microsoft.Maui.Controls;

namespace carddatasync3.WinUI
{
    public partial class App : MauiWinUIApplication
    {
        private static Mutex _mutex = null;

        /// <summary>
        /// Initializes the singleton application object. This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            bool createdNew;
            string appName = "YourUniqueAppName"; // 唯一的應用名稱，用來識別這個應用

            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                // 如果應用已經在運行，顯示警告並退出
                ShowAlertAndExit();
            }
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        private async void ShowAlertAndExit()
        {
            // 取得主頁面 (MauiApp 啟動後設置的主頁面)
            var mainPage = MauiProgram.CreateMauiApp().Services.GetService(typeof(Page)) as Page;

            // 顯示警告並退出
            if (mainPage != null)
            {
                await mainPage.DisplayAlert("警告", "應用已在運行", "確定");
            }

            //Environment.Exit(0);
        }
    }
}