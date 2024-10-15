namespace carddatasync3;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		MainPage = new MainPage();
	}

	protected override void OnStart()
	{
		base.OnStart();
		
		// 尝试在窗口启动时设置标题
		var mainWindow = Application.Current.Windows.FirstOrDefault();
		if (mainWindow != null)
		{
			Application.Current.Windows.First().Title = "HCM出勤資料同步工具";
		}
	}
}
