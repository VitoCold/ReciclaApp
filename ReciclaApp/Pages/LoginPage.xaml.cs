using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;

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

        try
        {
            var services = Handler?.MauiContext?.Services
                ?? throw new InvalidOperationException("No se pudo obtener el contenedor de servicios de MAUI.");

            var apiClient = services.GetRequiredService<IReciclaApiClient>();
            var logger = services.GetRequiredService<ILogger<LoginPage>>();

            var response = await apiClient.LoginAsync(new LoginRequest(usuario, clave));
            apiClient.SetAccessToken(response.AccessToken);

            try
            {
                await SecureStorage.Default.SetAsync("recicla_access_token", response.AccessToken);
                await SecureStorage.Default.SetAsync("recicla_usuario", response.Usuario.Usuario);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo persistir la sesión en SecureStorage.");
            }

            logger.LogInformation("Sesión iniciada para {UsuarioId}", response.Usuario.UsuarioId);
            ClaveEntry.Text = string.Empty;
            await AppNavigator.IrARegistrosAsync();
        }
        catch (HttpRequestException)
        {
            ShowError("No se pudo iniciar sesión. Verifica tus credenciales y que la API esté disponible.");
        }
        catch (Exception ex)
        {
            var services = Handler?.MauiContext?.Services;
            services?.GetService<ILogger<LoginPage>>()?.LogError(ex, "Error iniciando sesión.");
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
