using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using carddatasync3;


namespace carddatasync3;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Load configuration from appsettings.json
        var configuration = LoadConfiguration();
        builder.Services.AddSingleton<IConfiguration>(configuration);

        // Bind the AppSettings section of the config to the AppSettings class
        var appSettings = configuration.GetSection("Settings").Get<AppSettings>();
        builder.Services.AddSingleton(appSettings); // Register AppSettings in the service container

        #if DEBUG
        builder.Logging.AddDebug();
        #endif

        return builder.Build();
    }

    private static IConfiguration LoadConfiguration()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceStream = assembly.GetManifestResourceStream("carddatasync3.appsettings.json");

        var configBuilder = new ConfigurationBuilder()
            .AddJsonStream(resourceStream);

        return configBuilder.Build();
    }
}
