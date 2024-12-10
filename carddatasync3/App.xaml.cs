
namespace carddatasync3;

public partial class App : Application
{
	
	public App()
	{
		InitializeComponent();

		MainPage = new AppShell();
	}

	protected override void OnStart()
	{
		base.OnStart();
		
		// 尝试在窗口启动时设置标题
		var mainWindow = Application.Current.Windows.FirstOrDefault();
		if (mainWindow != null)
		{
			Application.Current.Windows.First().Title = "HCM出勤資料同步工具 1.2";
		}
	}

	protected override Window CreateWindow(IActivationState activationState)
    {
        var window = base.CreateWindow(activationState);

        const int newWidth = 1000;
        const int newHeight = 650;
        
        window.Width = newWidth;
        window.Height = newHeight;

		// 禁止縮放，將最小和最大寬度與高度設置為相同的值
		window.MinimumWidth = newWidth;
		window.MaximumWidth = newWidth;
		window.MinimumHeight = newHeight;
		window.MaximumHeight = newHeight;

        return window;
    }
}
