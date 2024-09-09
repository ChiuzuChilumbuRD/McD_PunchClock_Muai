using CommunityToolkit.Maui.Alerts;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Maui.Controls;

namespace carddatasync3;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

		// 示例 XML 数据
		string xmlData = @"
		<metadata name=""timer1Min.TrayLocation"" type=""System.Drawing.Point, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"">
			<value>17, 17</value>
		</metadata>
		<metadata name=""timer1Min.TrayLocation"" type=""System.Drawing.Point, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"">
			<value>17, 17</value>
		</metadata>
		<metadata name=""timer1Min.TrayLocation"" type=""System.Drawing.Point, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"">
			<value>17, 17</value>
		</metadata>
		<metadata name=""timer1Min.TrayLocation"" type=""System.Drawing.Point, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"">
			<value>17, 17</value>
		</metadata>
		<metadata name=""timer1Min.TrayLocation"" type=""System.Drawing.Point, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"">
			<value>17, 17</value>
		</metadata>
		<metadata name=""timer1Min.TrayLocation"" type=""System.Drawing.Point, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"">
			<value>17, 17</value>
		</metadata>
		<metadata name=""timer1Min.TrayLocation"" type=""System.Drawing.Point, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"">
			<value>17, 17</value>
		</metadata>
		<metadata name=""timer1Min.TrayLocation"" type=""System.Drawing.Point, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"">
			<value>17, 17</value>
		</metadata>
		<metadata name=""timer1Min.TrayLocation"" type=""System.Drawing.Point, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"">
			<value>17, 17</value>
		</metadata>";

		// 将 XML 数据设置到 Editor 控件中
		XmlEditor.Text = xmlData;
    }

    private async void OnfingerprintDTapped(object sender, EventArgs e)
    {
        // 在主線程上顯示 Alert，確保跨平台一致性
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await DisplayAlert("Button Clicked", "You clicked the button fingerprintD!", "OK");
        });
    }

    private async void OnfingerprintUTapped(object sender, EventArgs e)
    {
        // 在主線程上顯示 Alert，確保跨平台一致性
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await DisplayAlert("Button Clicked", "You clicked the button fingerprintU!", "OK");
        });
    }

    private async void OnDeliveryUploadTapped(object sender, EventArgs e)
    {
        // 在主線程上顯示 Alert，確保跨平台一致性
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await DisplayAlert("Button Clicked", "You clicked the button delivery_upload!", "OK");
        });
    }

	private async void OnGetRequestButtonClicked(object sender, EventArgs e)
	{
		try
            {
                string url = "http://qcest-2/cvs1/bpm/forms/devformt05/getToken?formKind=1";
                string response = await GetRequestAsync(url);
                await DisplayAlert("GET Response", response, "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
	}

	private async void OnPostRequestButtonClicked(object sender, EventArgs e)
	{
		try
            {
                string url = "https://gurugaia.royal.club.tw/eHR/GuruOutbound/getTmpOrg?u=AxtimTmpOrg_List&code=BQ0000";
                var data = new { name = "John Doe", age = 30 };
                string response = await PostRequestAsync(url, data);
                await DisplayAlert("POST Response", response, "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
	}

	public async Task<string> GetRequestAsync(string url)
	{
		using (HttpClient client = new HttpClient())
		{
			HttpResponseMessage response = await client.GetAsync(url);
			response.EnsureSuccessStatusCode();
			string responseBody = await response.Content.ReadAsStringAsync();
			return responseBody;
		}
	}

	public async Task<string> PostRequestAsync(string url, object data)
	{
		using (HttpClient client = new HttpClient())
		{
			var json = JsonConvert.SerializeObject(data);
			var content = new StringContent(json, Encoding.UTF8, "application/json");
			HttpResponseMessage response = await client.PostAsync(url, content);
			response.EnsureSuccessStatusCode();
			string responseBody = await response.Content.ReadAsStringAsync();
			return responseBody;
		}
	}
}
