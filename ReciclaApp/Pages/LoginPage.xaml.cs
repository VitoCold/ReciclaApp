using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e) => await LoginAsync();

    private async void OnPasswordCompleted(object sender, EventArgs e) => await LoginAsync();

    private async Task LoginAsync()
    {
        var usuario = UsuarioEntry.Text?.Trim();
        var clave = ClaveEntry.Text;

        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(clave))
        {
            ShowError("Ingresa tu usuario y clave.");
            return;
        }

        SetBusy(true);
        ErrorLabel.IsVisible = false;

        var services = AppServices.Services;

        try
        {
            var session = services.GetRequiredService<IAuthSessionService>();
            var logger = services.GetRequiredService<ILogger<LoginPage>>();

            var response = await session.LoginAsync(usuario, clave);

            logger.LogInformation("Sesión iniciada para {UsuarioId}", response.Usuario.UsuarioId);
            ClaveEntry.Text = string.Empty;

            await AppNavigator.IrAControlesGeneracionAsync();
        }
        catch (HttpRequestException)
        {
            ShowError("No se pudo iniciar sesión. Verifica tus credenciales y que la API esté disponible.");
        }
        catch (Exception ex)
        {
            services.GetService<ILogger<LoginPage>>()?.LogError(ex, "Error iniciando sesión.");
            ShowError("Ocurrió un error al iniciar sesión.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        LoginButton.IsEnabled = !busy;
        UsuarioEntry.IsEnabled = !busy;
        ClaveEntry.IsEnabled = !busy;
        LoginActivity.IsVisible = busy;
        LoginActivity.IsRunning = busy;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
