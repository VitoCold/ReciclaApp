using Microsoft.Extensions.Logging;
using Recicla.Shared.Services;
using ReciclaApp.Services;

namespace ReciclaApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if ANDROID
            var apiBaseUrl = "http://10.0.2.2:5298/";
#else
            var apiBaseUrl = "http://localhost:5298/";
#endif

            builder.Services.AddSingleton(new HttpClient
            {
                BaseAddress = new Uri(apiBaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            });
            builder.Services.AddSingleton<IReciclaApiClient, ReciclaApiClient>();
            builder.Services.AddSingleton<IAuthSessionService, AuthSessionService>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            var app = builder.Build();
            AppServices.Initialize(app.Services);
            return app;
        }
    }
}
